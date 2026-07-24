import React, { createContext, useContext, useEffect, useState, useRef, useCallback } from 'react';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import api from '../config/api';

const OverlaySocketContext = createContext(null);

export function useOverlaySocket() {
  const context = useContext(OverlaySocketContext);
  if (!context) throw new Error('useOverlaySocket must be used within an OverlaySocketProvider');
  return context;
}

export function OverlaySocketProvider({ children, pcId, isMinimized: initialMinimized }) {
  const [connection, setConnection] = useState(null);
  const [sessionData, setSessionData] = useState(null);
  const [walkinDeclineEvent, setWalkinDeclineEvent] = useState(null);
  const [foodOrders, setFoodOrders] = useState([]);
  const [connectionStatus, setConnectionStatus] = useState('connecting');
  const [sessionLoading, setSessionLoading] = useState(true);
  const [walletApprovalRequest, setWalletApprovalRequest] = useState(null);
  const [branchId, setBranchId] = useState(null);

  const idleTimeoutRef = useRef(null);
  const heartbeatIntervalRef = useRef(null);

  const isMockMode = import.meta.env.VITE_MOCK_MODE === 'true';
  const [isMinimized, setIsMinimized] = useState(initialMinimized);

  // ── MOCK MODE ──
  useEffect(() => {
    if (!isMockMode) return;

    console.log('[MOCK MODE] Initializing PC Overlay Mock Data for', pcId);
    const mockSession = {
      sessionId: `sess_mock_${pcId}`,
      pcId,
      pcName: pcId,
      customerName: 'Guest (Mock)',
      sessionStart: new Date(Date.now() - 30 * 60000).toISOString(),
      remainingTime: 90 * 60,
      gamingCharges: 50.00,
      foodCharges: 0,
      foodItems: [],
      totalBill: 50.00,
      sessionStatus: 'active',
      memberId: null,
    };

    setSessionData(mockSession);
    setConnectionStatus('connected');
    setSessionLoading(false);

    const timer = setInterval(() => {
      setSessionData(prev => {
        if (!prev || prev.remainingTime <= 0) return prev;
        return {
          ...prev,
          remainingTime: prev.remainingTime - 1,
          gamingCharges: prev.gamingCharges + (100 / 3600),
          totalBill: prev.totalBill + (100 / 3600),
        };
      });
    }, 1000);

    return () => clearInterval(timer);
  }, [pcId, isMockMode]);

  const fetchSession = async ({ silent = false } = {}) => {
    if (!pcId || isMockMode) return;
    try {
      // Background refreshes (the periodic safety-net poll) must NOT toggle sessionLoading —
      // doing so briefly flips the top-level screen gate in UserOverlayApp away from whatever
      // pre-session screen (e.g. mid Member Login) the user is on and back, remounting it and
      // wiping its local state. Only the very first, real fetch should show a loading state.
      if (!silent) setSessionLoading(true);

      let pcRatePerHour = 0;
      try {
        const pcRes = await api.get(`/public/pcs/${pcId}`);
        if (pcRes.data.success && pcRes.data.data) {
          if (pcRes.data.data.ratePerHour) {
            pcRatePerHour = pcRes.data.data.ratePerHour;
          }
          if (pcRes.data.data.branchId) {
            setBranchId(pcRes.data.data.branchId);
          }
        }
      } catch (e) {
        console.warn('[Overlay] Failed to fetch PC rate, using default 100:', e);
      }

      const res = await api.get(`/public/session/pc/${pcId}`);
      if (res.data.success && res.data.data) {
        const d = res.data.data;
        setSessionData({
          sessionId: d.sessionId,
          pcId: d.pcId,
          pcName: d.pcName,
          customerName: d.customerName,
          sessionStart: d.sessionStart,
          remainingTime: d.remainingTime,
          plannedDurationMin: d.plannedDurationMin,
          ratePerHour: d.ratePerHour ?? pcRatePerHour,
          bufferMinutes: d.bufferMinutes ?? 10,
          gamingCharges: d.gamingCharges,
          foodCharges: d.foodCharges,
          foodItems: d.foodItems || [],
          totalBill: d.totalBill,
          sessionStatus: d.sessionStatus,
          memberId: d.memberId,
          memberLinked: !!d.memberId,
          walletBalance: d.walletBalance,
          gamingBalance: d.gamingBalance,
          foodBalance: d.foodBalance,
        });
      } else {
        // No active session found
        setSessionData(null);
      }
    } catch (err) {
      console.error('[Overlay] Failed to fetch session:', err);
    } finally {
      if (!silent) setSessionLoading(false);
    }
  };

  // ── REAL MODE: Fetch initial session from REST API ──
  useEffect(() => {
    fetchSession();
  }, [pcId, isMockMode]);

  // Safety net: re-pull the rate/buffer (and session state) every 10s so a Super Admin's
  // pricing change reaches an already-open customer overlay without needing a manual refresh.
  // Runs silently (no loading-state toggle) so it never disturbs a pre-session screen the
  // customer is actively using, like Member Login.
  useEffect(() => {
    if (isMockMode) return;
    const interval = setInterval(() => fetchSession({ silent: true }), 10000);
    return () => clearInterval(interval);
  }, [pcId, isMockMode]);

  // ── Helper to play beep & voice alert ──
  const playTimeAlert = (minutes) => {
    try {
      // 1. Play a gentle buzz/beep using AudioContext
      const audioCtx = new (window.AudioContext || window.webkitAudioContext)();
      const oscillator = audioCtx.createOscillator();
      const gainNode = audioCtx.createGain();
      oscillator.connect(gainNode);
      gainNode.connect(audioCtx.destination);
      oscillator.type = 'triangle'; // buzzy sound
      oscillator.frequency.setValueAtTime(400, audioCtx.currentTime);
      gainNode.gain.setValueAtTime(0.1, audioCtx.currentTime);
      oscillator.start();
      oscillator.stop(audioCtx.currentTime + 0.3);

      // 2. Speak the notification
      if ('speechSynthesis' in window) {
        setTimeout(() => {
          const utterance = new SpeechSynthesisUtterance(`Attention. ${minutes} minute${minutes > 1 ? 's' : ''} remaining.`);
          utterance.rate = 0.9;
          utterance.pitch = 1.1;
          window.speechSynthesis.speak(utterance);
        }, 400); // Speak after beep finishes
      }
    } catch (e) {
      console.warn('Audio/Speech alert failed', e);
    }
  };

  // Tracks which time-remaining alerts have already fired for the current session, so a
  // resync (the periodic silent refresh, a SignalR update, an Extend) that nudges
  // remainingTime back up past a threshold can never re-trigger the same alert.
  const firedAlertsRef = useRef({ sessionId: null, fired: new Set() });

  // Tracks whether the wallet-exhausted auto-checkout has already fired for the current
  // session, so it can only ever trigger once (and can't double-fire from a fast tick).
  const autoCheckoutRef = useRef({ sessionId: null, triggered: false });

  // ── REAL MODE: Live countdown from remainingTime ──
  // Runs at the provider level (not inside a screen component) so it keeps ticking — and a
  // member's wallet-exhausted auto-checkout keeps working — no matter which overlay screen
  // (Food, Extend, Call, Bill) the customer is currently looking at.
  useEffect(() => {
    if (isMockMode || !sessionData?.sessionId || sessionData.sessionStatus !== 'active') return;

    if (autoCheckoutRef.current.sessionId !== sessionData.sessionId) {
      autoCheckoutRef.current = { sessionId: sessionData.sessionId, triggered: false };
    }

    const timer = setInterval(() => {
      let shouldAutoCheckout = false;
      let checkoutSessionId = null;

      setSessionData(prev => {
        if (!prev) return prev;

        if (firedAlertsRef.current.sessionId !== prev.sessionId) {
          firedAlertsRef.current = { sessionId: prev.sessionId, fired: new Set() };
        }
        const fired = firedAlertsRef.current.fired;

        const isPrepaid = prev.plannedDurationMin != null;

        let nextTime = prev.remainingTime;
        if (isPrepaid && prev.remainingTime > 0) {
          nextTime = prev.remainingTime - 1;
          if (nextTime <= 600 && !fired.has(600)) { fired.add(600); playTimeAlert(10); }
          else if (nextTime <= 300 && !fired.has(300)) { fired.add(300); playTimeAlert(5); }
          else if (nextTime <= 60 && !fired.has(60)) { fired.add(60); playTimeAlert(1); }
        }

        // Live-tick the gaming charge for every session type — free during the branch's
        // buffer window, then billed for exact elapsed time. Same formula the backend uses.
        let nextGamingCharges = prev.gamingCharges;
        if (prev.sessionStart) {
          const elapsedMs = Date.now() - new Date(prev.sessionStart).getTime();
          const elapsedMin = elapsedMs / (1000 * 60);
          const bufferMinutes = prev.bufferMinutes ?? 10;
          nextGamingCharges = elapsedMin <= bufferMinutes ? 0 : (elapsedMin / 60) * (prev.ratePerHour || 0);
        }

        // A member's session must stop the instant their gaming wallet balance is used up —
        // this has to live here (not in a screen component) so it can't be skipped just
        // because the customer navigated to Food/Extend/Call/Bill.
        if (
          prev.memberLinked &&
          prev.gamingBalance != null &&
          nextGamingCharges >= prev.gamingBalance &&
          !autoCheckoutRef.current.triggered
        ) {
          autoCheckoutRef.current.triggered = true;
          shouldAutoCheckout = true;
          checkoutSessionId = prev.sessionId;
        }

        return {
          ...prev,
          remainingTime: nextTime,
          gamingCharges: nextGamingCharges,
          totalBill: nextGamingCharges + (prev.foodCharges || 0)
        };
      });

      if (shouldAutoCheckout && checkoutSessionId) {
        localStorage.setItem('walletEmptyAlert', 'true');
        memberCheckout(checkoutSessionId).then(res => {
          if (res?.success) {
            fetchSession();
          } else {
            // Let it retry on the next tick if the checkout call itself failed.
            autoCheckoutRef.current.triggered = false;
          }
        });
      }
    }, 1000);

    return () => clearInterval(timer);
  }, [isMockMode, sessionData?.sessionId, sessionData?.sessionStatus]);

  // ── REAL MODE: SignalR ──
  useEffect(() => {
    if (isMockMode) return;

    const hubUrl = '/hubs/pc-overlay';

    const newConnection = new HubConnectionBuilder()
      .withUrl(hubUrl)
      .configureLogging(LogLevel.Warning)
      .withAutomaticReconnect()
      .build();

    // Incoming from server
    newConnection.on('SessionStarted', () => {
      fetchSession();
    });

    newConnection.on('SessionUpdated', (updates) => {
      // Fetch fresh session data to get the new remainingTime after extensions
      fetchSession();
    });

    newConnection.on('SessionStopped', (finalBill) => {
      setSessionData(prev => ({ ...prev, ...finalBill, sessionStatus: 'awaiting_billing' }));
    });

    newConnection.on('SessionEnded', () => {
      setSessionData(null);
    });

    newConnection.on('BillUpdated', (update) => {
      setSessionData(prev => prev ? {
        ...prev,
        foodCharges: update.foodCharges ?? prev.foodCharges,
        totalBill: update.totalBill ?? prev.totalBill,
      } : prev);
    });

    newConnection.on('ExtensionApproved', ({ newRemainingTime }) => {
      setSessionData(prev => prev ? { ...prev, remainingTime: newRemainingTime } : prev);
    });

    newConnection.on('ExtensionDenied', ({ reason }) => {
      console.warn('[Overlay] Extension denied:', reason);
    });

    newConnection.on('OrderStatusUpdate', ({ orderId, status }) => {
      setFoodOrders(prev => prev.map(o => o.id === orderId ? { ...o, status } : o));
    });

    newConnection.on('OperatorAcknowledged', () => {
      console.log('[Overlay] Operator acknowledged call');
    });

    newConnection.on('ForceClose', ({ reason }) => {
      setSessionData(prev => ({ ...prev, sessionStatus: 'completed', forceCloseReason: reason }));
    });

    newConnection.on('ReceiveWalletApprovalRequest', (data) => {
      setWalletApprovalRequest(data);
    });

    // When PC status changes to Idle (e.g. after wallet payment completes), clear the overlay
    newConnection.on('PcStatusChanged', (pcStatus) => {
      const state = pcStatus?.state || pcStatus?.State;
      if (state === 'Idle' || state === 0) {
        console.log('[Overlay] PC is now Idle, clearing session');
        setSessionData(null);
        setWalletApprovalRequest(null);
      }
    });

    let isSubscribed = true;
    let reconnectTimeout = null;

    const startConnection = async () => {
      if (!isSubscribed) return;
      try {
        if (newConnection.state === 'Disconnected') {
          await newConnection.start();
          setConnectionStatus('connected');
          newConnection.invoke('ConnectPc', pcId).catch(console.error);
        }
      } catch (err) {
        console.error('[Overlay] SignalR connection error:', err);
        setConnectionStatus('disconnected');
        reconnectTimeout = setTimeout(startConnection, 5000);
      }
    };

    startConnection();

    newConnection.on('WalkinRequestDeclined', (data) => {
      console.log('Walkin request declined by operator', data);
      setWalkinDeclineEvent({ timestamp: Date.now(), reason: data.reason });
    });

    newConnection.onreconnecting(() => setConnectionStatus('reconnecting'));
    newConnection.onreconnected(() => {
      setConnectionStatus('connected');
      newConnection.invoke('ConnectPc', pcId).catch(console.error);
    });
    newConnection.onclose(() => {
      setConnectionStatus('disconnected');
      reconnectTimeout = setTimeout(startConnection, 5000);
    });

    setConnection(newConnection);
    return () => { 
      isSubscribed = false;
      clearTimeout(reconnectTimeout);
      newConnection.stop(); 
    };
  }, [pcId, isMockMode]);

  // ── HEARTBEAT ──
  useEffect(() => {
    const emitHeartbeat = () => {
      if (!isMockMode && connection?.state === 'Connected') {
        connection.invoke('Heartbeat', {
          pcId,
          sessionId: sessionData?.sessionId,
          timestamp: new Date().toISOString(),
          status: 'active',
        }).catch(console.error);
      }
    };

    emitHeartbeat();
    heartbeatIntervalRef.current = setInterval(emitHeartbeat, 30000);
    return () => clearInterval(heartbeatIntervalRef.current);
  }, [pcId, sessionData?.sessionId, connection, isMockMode]);

  // ── IDLE DETECTION ──
  useEffect(() => {
    const resetIdle = () => {
      clearTimeout(idleTimeoutRef.current);
      idleTimeoutRef.current = setTimeout(() => {
        if (!isMockMode && connection?.state === 'Connected') {
          connection.invoke('IdleDetected', {
            pcId,
            sessionId: sessionData?.sessionId,
            idleSince: new Date().toISOString(),
          }).catch(console.error);
        }
      }, 30 * 60 * 1000);
    };

    window.addEventListener('mousemove', resetIdle);
    window.addEventListener('keydown', resetIdle);
    window.addEventListener('click', resetIdle);
    resetIdle();

    return () => {
      window.removeEventListener('mousemove', resetIdle);
      window.removeEventListener('keydown', resetIdle);
      window.removeEventListener('click', resetIdle);
      clearTimeout(idleTimeoutRef.current);
    };
  }, [pcId, sessionData?.sessionId, connection, isMockMode]);

  // ── INVOKERS ──
  const emitActivity = useCallback((eventName) => {
    if (!isMockMode && connection?.state === 'Connected') {
      connection.invoke('ActivityEvent', {
        pcId,
        sessionId: sessionData?.sessionId,
        event: eventName,
        timestamp: new Date().toISOString(),
      }).catch(console.error);
    }
  }, [connection, isMockMode, pcId, sessionData?.sessionId]);

  const placeFoodOrder = async (items, totalAmount) => {
    const orderId = `ORD-${Date.now()}`;
    const payload = {
      pcId,
      sessionId: sessionData?.sessionId,
      items,
      totalAmount,
      orderId,
    };

    if (isMockMode) {
      const newOrder = { id: orderId, items, totalAmount, status: 'Pending', timestamp: new Date() };
      setFoodOrders(prev => [newOrder, ...prev]);
      setSessionData(prev => ({
        ...prev,
        foodCharges: (prev.foodCharges || 0) + totalAmount,
        totalBill: (prev.totalBill || 0) + totalAmount,
        foodItems: [...(prev.foodItems || []), ...items],
      }));
      await new Promise(r => setTimeout(r, 500));
      return { success: true, orderId };
    }

    if (connection?.state === 'Connected') {
      try {
        const result = await connection.invoke('PlaceFoodOrder', payload);
        if (result?.success) {
          const newOrder = { id: result.orderId || orderId, orderNumber: result.orderNumber, items, totalAmount, status: 'Pending', timestamp: new Date() };
          setFoodOrders(prev => [newOrder, ...prev]);
        }
        return result;
      } catch (err) {
        console.error('[Overlay] PlaceFoodOrder error:', err);
        return { success: false, error: err.message };
      }
    }
    return { success: false, error: 'Not connected' };
  };

  const requestExtension = async (durationMinutes) => {
    const payload = { pcId, sessionId: sessionData?.sessionId, duration: durationMinutes };

    if (isMockMode) {
      await new Promise(r => setTimeout(r, 1000));
      setSessionData(prev => ({ ...prev, remainingTime: (prev.remainingTime || 0) + durationMinutes * 60 }));
      return { success: true, status: 'approved' };
    }

    if (connection?.state === 'Connected') {
      return await connection.invoke('RequestExtension', payload);
    }
    return { success: false, error: 'Not connected' };
  };

  const callOperator = async () => {
    const payload = { pcId, sessionId: sessionData?.sessionId, timestamp: new Date().toISOString() };

    if (isMockMode) {
      await new Promise(r => setTimeout(r, 500));
      return { success: true };
    }

    if (connection?.state === 'Connected') {
      return await connection.invoke('CallOperator', payload);
    }
    return { success: false, error: 'Not connected' };
  };

  const requestWalkinSession = async (customerName, durationMinutes, packageName) => {
    setWalkinDeclineEvent(null); // Clear any stale decline events
    const payload = { pcId, customerName, duration: durationMinutes, packageName };

    if (isMockMode) {
      await new Promise(r => setTimeout(r, 1000));
      return { success: true, status: 'pending_operator_approval' };
    }

    if (connection?.state === 'Connected') {
      return await connection.invoke('RequestWalkinSession', payload);
    }
    return { success: false, error: 'Not connected' };
  };

  const respondToWalletApproval = async (billId, approved) => {
    if (isMockMode) {
      setWalletApprovalRequest(null);
      if (approved) setSessionData(null);
      return { success: true };
    }
    try {
      const endpoint = approved ? 'approve-wallet' : 'decline-wallet';
      const res = await api.post(`/public/bills/${billId}/${endpoint}`, {
        approvalToken: walletApprovalRequest.approvalToken
      });
      // If approved successfully, delay clearing the session to show a success message
      if (approved && res.data?.success !== false) {
        setTimeout(() => {
          setWalletApprovalRequest(null);
          setSessionData(null);
        }, 2500);
      } else {
        setWalletApprovalRequest(null);
      }
      return { success: true, data: res.data };
    } catch (err) {
      console.error('[Overlay] respondToWalletApproval error:', err);
      return { success: false, error: err.response?.data?.error || err.message };
    }
  };

  const memberCheckout = async (sessionId) => {
    const token = localStorage.getItem('memberToken');
    if (!token) return { success: false, error: 'Not logged in' };

    try {
      const response = await api.post(`/public/sessions/${sessionId}/member-checkout`, {}, {
        headers: {
          'Authorization': `Bearer ${token}`
        }
      });
      return response.data;
    } catch (error) {
      console.error('Member checkout error:', error);
      return { success: false, error: error.response?.data?.error || error.message };
    }
  };

  return (
    <OverlaySocketContext.Provider value={{
      pcId,
      sessionData,
      foodOrders,
      connectionStatus,
      sessionLoading,
      isMockMode,
      isMinimized,
      emitActivity,
      placeFoodOrder,
      requestExtension,
      callOperator,
      requestWalkinSession,
      walkinDeclineEvent,
      fetchSession,
      walletApprovalRequest,
      respondToWalletApproval,
      branchId,
      memberCheckout
    }}>
      {children}
    </OverlaySocketContext.Provider>
  );
}
