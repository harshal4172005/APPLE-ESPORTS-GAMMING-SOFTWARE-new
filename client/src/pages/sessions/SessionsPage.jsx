import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { MonitorPlay, MonitorOff, IndianRupee, Clock, Banknote, Minus, Plus, Power } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import { useBranch } from '../../contexts/BranchContext';
import { useSocket } from '../../contexts/SocketContext';
import api from '../../config/api';

import PcGrid from '../../components/sessions/PcGrid';
import { TILE_SIZES } from '../../components/sessions/PcTile';
import PcDetailPanel from '../../components/sessions/PcDetailPanel';
import QuickStartModal from '../../components/sessions/QuickStartModal';
import SessionActivityLog from '../../components/sessions/SessionActivityLog';
import { MaintenanceReasonModal } from '../../components/modals/MaintenanceReasonModal';
import { useToast } from '../../components/ui/Toast';
import { getRangeReport } from '../../api/food.api';
import { getActiveBills, getBill, processPayment } from '../../api/billing.api';
import { markMaintenanceAsync, resolveMaintenance } from '../../api/maintenanceLogs.api';
import { logActivity } from '../../utils/sessionLog';
import { roundBillTotal } from '../../utils/billRounding';
import { useNavigate } from 'react-router-dom';
import InterruptedSessionsBanner from '../../components/sessions/InterruptedSessionsBanner';

export default function SessionsPage() {
  const { isSuperAdmin, user } = useAuth();
  const { activeBranch } = useBranch();
  const { subscribe, isHubUp, emit, SIGNALR_HUBS } = useSocket();
  const toast = useToast();
  const navigate = useNavigate();

  const [pcs, setPcs] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [selectedPcId, setSelectedPcId] = useState(null); // PC shown in the detail panel
  const [quickStartPc, setQuickStartPc] = useState(null); // PC being quick-started via double-click
  const [tileSizeIndex, setTileSizeIndex] = useState(1); // index into TILE_SIZES — controls PC tile size

  // Maintenance modal states
  const [maintenanceModalOpen, setMaintenanceModalOpen] = useState(false);
  const [maintenancePc, setMaintenancePc] = useState(null);
  const [maintenanceIsMarking, setMaintenanceIsMarking] = useState(true); // true to mark, false to resolve
  const [maintenanceLoading, setMaintenanceLoading] = useState(false);

  // Walk-in requests state
  const [walkinRequests, setWalkinRequests] = useState([]);

  // Activity log height — fixed to the bottom of the viewport, adjustable via drag handle
  const [logHeight, setLogHeight] = useState(140);

  const targetBranchId = isSuperAdmin ? activeBranch?.id : user?.branchId;

  const fetchPcs = useCallback(async (silent = false) => {
    if (!targetBranchId) {
      if (!silent) setPcs([]);
      if (!silent) setIsLoading(false);
      return;
    }
    if (!silent) setIsLoading(true);
    try {
      const { data } = await api.get('/pcs', { params: { branchId: targetBranchId } });
      // PCs first, consoles (PS5/Xbox...) last - a name like "PS5-01" otherwise sorts ahead of
      // "TEST-PC-01" purely alphabetically, scattering consoles in among the PCs instead of
      // grouping them at the end of the grid where an operator expects to find them.
      const sorted = (data?.data || []).sort((a, b) => {
        const aIsConsole = a.zone === 'Console' ? 1 : 0;
        const bIsConsole = b.zone === 'Console' ? 1 : 0;
        if (aIsConsole !== bIsConsole) return aIsConsole - bIsConsole;
        return a.name.localeCompare(b.name, undefined, { numeric: true });
      });
      // Only update if data actually changed - but on the WHOLE row, not a hand-picked
      // subset of fields. This used to compare only id/state/totalAmount, so a change to
      // anything else (poweredOff, isAgentOnline, customerName, sessionEndTime...) looked
      // identical to no change at all and the freshly-fetched, correct data was thrown away
      // in favour of stale state - a PC shut down or powered back on with no session change
      // could sit showing the wrong colour forever, since every future poll hit the exact
      // same blind spot. Comparing the full snapshot means nothing on this DTO can be added
      // later and silently fall into that same gap again.
      setPcs(prev => {
        const prevJson = JSON.stringify(prev);
        const newJson = JSON.stringify(sorted);
        return prevJson !== newJson ? sorted : prev;
      });
    } catch (err) {
      console.error('Failed to load PCs', err);
    } finally {
      if (!silent) setIsLoading(false);
    }
  }, [targetBranchId]);

  /**
   * Coalesces a burst of "something changed" pushes into one refresh instead of one per push.
   *
   * PcStatusChanged fires once per PC, independently, straight from the server the instant that
   * PC's own state changes - correct for one PC changing on its own, and exactly the fault on a
   * busy evening when several do within the same couple of seconds (a rush of walk-ins starting
   * sessions close together). Each push used to call fetchPcs() directly: a loading-state flip,
   * a full round trip, and a full-array JSON.stringify comparison against the whole PC list -
   * once per PC, all landing on top of each other. Ten PCs changing inside two seconds meant ten
   * of all that stacked at once, on the one thread also responsible for drawing the screen and
   * answering a click - confirmed live as the app going fully unresponsive specifically during
   * those bursts, not a steady-state slowdown from simply having many PCs active.
   *
   * A short quiet window fixes it without losing anything: whichever PC's push arrives last
   * within it restarts the timer, and when things finally go quiet for a moment, one fetchPcs()
   * covers every PC that changed in the meantime - the same one call this page already relies on
   * to reconcile the whole list, just asked once instead of once per PC.
   */
  const fetchPcsDebounceRef = useRef(null);
  const scheduleFetchPcs = useCallback(() => {
    if (fetchPcsDebounceRef.current) clearTimeout(fetchPcsDebounceRef.current);
    fetchPcsDebounceRef.current = setTimeout(() => {
      fetchPcsDebounceRef.current = null;
      fetchPcs(true); // silent - a burst of live pushes should never flash the loading state
    }, 400);
  }, [fetchPcs]);
  useEffect(() => () => {
    if (fetchPcsDebounceRef.current) clearTimeout(fetchPcsDebounceRef.current);
  }, []);

  const handleFlagMaintenance = async (pc, enable = true) => {
    if (enable) {
      setMaintenancePc(pc);
      setMaintenanceIsMarking(true);
      setMaintenanceModalOpen(true);
    } else {
      setMaintenancePc(pc);
      setMaintenanceIsMarking(false);
      setMaintenanceModalOpen(true);
    }
  };

  const handleMaintenanceConfirm = async (reason) => {
    if (!maintenancePc) return;

    setMaintenanceLoading(true);
    try {
      if (maintenanceIsMarking) {
        await markMaintenanceAsync(maintenancePc.id, reason, targetBranchId);
        toast.success(`${maintenancePc.name} flagged for maintenance.`);
        logActivity(`${maintenancePc.name}: Flagged for maintenance - ${reason}`, 'warn');
      } else {
        await resolveMaintenance(maintenancePc.id, reason);
        toast.success(`${maintenancePc.name} resolved from maintenance.`);
        logActivity(`${maintenancePc.name}: Resolved from maintenance.`, 'success');
      }
      setMaintenanceModalOpen(false);
      setMaintenancePc(null);
      // Refresh PCs immediately to show updated state
      setTimeout(() => fetchPcs(), 500);
    } catch (err) {
      console.error('Maintenance error:', err);
      toast.error(err?.error || err?.message || 'Failed to update maintenance status');
    } finally {
      setMaintenanceLoading(false);
    }
  };

  useEffect(() => {
    if (!targetBranchId) return;
    setIsLoading(true);
    fetchPcs();
  }, [fetchPcs, targetBranchId]);

  // Safety net: refetch periodically (silently, without showing loading) so rates/buffer minutes never sit stale
  // Increased to 20 seconds to minimize visible refresh, only updates if data actually changed
  useEffect(() => {
    const interval = setInterval(() => fetchPcs(true), 20000);
    return () => clearInterval(interval);
  }, [fetchPcs]);

  useEffect(() => {
    const handleRefresh = (e) => {
      const pcId = e.detail?.pcId;
      if (pcId) {
        // Instantly mark active
        setPcs(current => {
          const idx = current.findIndex(p => p.id === pcId || p.name === pcId);
          if (idx === -1) return current;
          const next = [...current];
          next[idx] = { ...next[idx], state: 'Active' };
          return next;
        });
        
        // Instantly remove walkin pending status
        setWalkinRequests(prev => prev.filter(r => r.pcId !== pcId && r.PcId !== pcId));
      }
    };
    window.addEventListener('refresh-pcs', handleRefresh);
    return () => window.removeEventListener('refresh-pcs', handleRefresh);
  }, []);

  // Poll for pending walk-in requests every 5 seconds — reliable fallback regardless of SignalR state
  useEffect(() => {
    if (!targetBranchId) return;

    const fetchPending = async () => {
      try {
        const { data } = await api.get('/public/walkin-pending');
        if (data?.success && Array.isArray(data.data)) {
          setWalkinRequests(data.data);
        }
      } catch {
        // silent — SignalR may still deliver the event
      }
    };

    fetchPending();
    const interval = setInterval(fetchPending, 5000);
    return () => clearInterval(interval);
  }, [targetBranchId]);

  // SignalR realtime PC state updates. Gated on the pc-status hub's own health, not the
  // all-four `connected` badge - a blip on notifications, sessions or billing used to tear
  // this down along with everything else, and it stayed torn down until every one of those
  // recovered together, even though the pc-status hub itself never went anywhere. A PC shut
  // down or started during that window got no live push at all, only whatever the 20s poll
  // fallback could still catch (see fetchPcs).
  useEffect(() => {
    if (!isHubUp(SIGNALR_HUBS.PC_STATUS) || !targetBranchId) return;
    const unsubPcStatus = subscribe(SIGNALR_HUBS.PC_STATUS, 'PcStatusChanged', (payload) => {
      console.log('[SessionsPage] PcStatusChanged received. Refetching PCs...');
      const data = payload.payload || payload.Payload || payload.data || payload.Data || payload;
      const status = data.status || data.State || data.state;
      if (status === 'active' || status === 'Active' || status === 1 || status === '1') {
        setWalkinRequests(prev => prev.filter(r => {
          const reqPcId = r.pcId || r.PcId;
          return reqPcId !== (data.pcId || data.id) && reqPcId !== (data.name || data.Name);
        }));
      }
      // Coalesced, not called directly - see scheduleFetchPcs. A burst of these (several PCs
      // changing within the same couple of seconds) must land as one refresh, not one each.
      scheduleFetchPcs();
    });

    // Super Admin changed a Pricing Profile (rate or buffer) — refetch instantly so
    // every open PC card reflects it immediately, not just newly started sessions.
    const unsubPricing = subscribe(SIGNALR_HUBS.PC_STATUS, 'PricingProfileUpdated', () => {
      scheduleFetchPcs();
    });

    // A PC was flagged/unflagged for maintenance (or added/removed/transferred) —
    // PcManagementService broadcasts this on the same hub as PcStatusChanged, but under a
    // different event name, so without this the screen kept showing "Maintenance" (no Walk-in/
    // Member options) after an operator restored a PC until the app was reopened.
    const unsubPcManagement = subscribe(SIGNALR_HUBS.PC_STATUS, 'PcManagementUpdated', () => {
      scheduleFetchPcs();
    });

    return () => {
      unsubPcStatus();
      unsubPricing();
      unsubPcManagement();
    };
  }, [isHubUp, subscribe, SIGNALR_HUBS.PC_STATUS, targetBranchId, scheduleFetchPcs]);

  // Immediate walk-in notification. Its own effect, gated on the notifications hub's own
  // health rather than bundled with pc-status above - the two have nothing to do with each
  // other, and neither should be able to take the other one down.
  useEffect(() => {
    if (!isHubUp(SIGNALR_HUBS.NOTIFICATIONS) || !targetBranchId) return;
    // Immediate delivery via SignalR (polling above provides the fallback)
    const unsubNotification = subscribe(SIGNALR_HUBS.NOTIFICATIONS, 'Alert', (alert) => {
      console.log('[SessionsPage] Received Alert:', alert);
      const type = alert.type || alert.Type;

      // A member's gaming wallet is running low — surface it so the operator can walk over
      // and offer a top-up before the session auto-stops.
      if (type === 'MemberLowBalance') {
        const pcName = alert.pcName || alert.PcName || 'a PC';
        const memberName = alert.memberName || alert.MemberName || 'Member';
        const remaining = alert.remainingBalance ?? alert.RemainingBalance ?? 0;
        const mins = alert.minutesRemaining ?? alert.MinutesRemaining ?? 0;
        toast.warning(`${memberName} on ${pcName}: ₹${Number(remaining).toFixed(2)} gaming balance left (~${mins} min)`);
        logActivity(`${pcName}: ${memberName} low gaming balance — ₹${Number(remaining).toFixed(2)} left (~${mins} min). Offer a top-up.`, 'warn');
        return;
      }

      if (type === 'WalkinSessionRequest') {
        console.log('[SessionsPage] Setting Walkin Request state for pcId:', alert.pcId);
        setWalkinRequests(prev => {
          const exists = prev.find(p => p.pcId === (alert.pcId || alert.PcId));
          if (exists) return prev;
          const newReqs = [...prev, { ...alert, pcId: alert.pcId || alert.PcId }];
          console.log('[SessionsPage] New Walkin Requests state:', newReqs);
          return newReqs;
        });
      }
    });

    return () => {
      unsubNotification();
    };
  }, [isHubUp, subscribe, SIGNALR_HUBS.NOTIFICATIONS, targetBranchId]);

  const handleApproveWalkin = async (req) => {
    try {
      const expectedAmount = req.duration ? (req.duration / 60) * 100 : 0;
      const pc = pcs.find(p => p.name === req.pcId || p.id === req.pcId);
      const actualPcId = pc ? pc.id : req.pcId;

      const res = await api.post('/sessions/start', {
        pcId: actualPcId,
        memberId: null,
        customerName: req.customerName,
        durationMinutes: req.duration,
        packageName: req.packageName || 'Walk-in',
        expectedAmount: expectedAmount
      });
      if (res.data.success) {
        toast.success(`Walk-in session started for ${req.pcId}`);
        logActivity(`${req.pcId}: Walk-in session approved for ${req.customerName}.`, 'success');
        setWalkinRequests(prev => prev.filter(r => r.pcId !== req.pcId));
        setPcs(current => {
          const idx = current.findIndex(p => p.id === actualPcId);
          if (idx === -1) return current;
          const next = [...current];
          next[idx] = { ...next[idx], state: 'Active' };
          return next;
        });
      }
    } catch (err) {
      toast.error(err.response?.data?.error || 'Failed to approve walk-in');
    }
  };

  const handleDeclineWalkin = async (req) => {
    try {
      await api.post(`/public/pcs/${req.pcId}/decline-walkin`);
      setWalkinRequests(prev => prev.filter(r => r.pcId !== req.pcId));
      toast.info(`Declined walk-in for ${req.pcId}`);
      logActivity(`${req.pcId}: Walk-in request declined.`, 'error');
    } catch (err) {
      toast.error('Failed to decline request');
    }
  };

  const handleCreditClick = async (pc) => {
    try {
      if (pc.activeSessionId) {
        // Stop session and generate bill
        await api.post(`/sessions/${pc.activeSessionId}/stop`, { deferPayment: false });
      }
      navigate('/app/billing', { state: { autoSelectPcId: pc.id, autoSelectPaymentMethod: 'credit' } });
    } catch (err) {
      const errCode = err.response?.data?.code || err.response?.data?.errorCode;
      const errMsg = err.response?.data?.error || err.response?.data?.message || '';
      
      if (errCode === 'SESSION_ALREADY_ENDED' || errMsg?.toLowerCase().includes('already ended')) {
        navigate('/app/billing', { state: { autoSelectPcId: pc.id, autoSelectPaymentMethod: 'credit' } });
      } else {
        toast.error(`Error: ${errMsg || err.message || 'Failed to stop session for credit'}`);
      }
    }
  };

  // Ticker to force live revenue update every 10 seconds
  const [ticker, setTicker] = useState(0);
  useEffect(() => {
    const interval = setInterval(() => setTicker(t => t + 1), 10000);
    return () => clearInterval(interval);
  }, []);

  // ── Stats computed from PC list ──
  /**
   * Whether this person may switch machines off.
   *
   * Operators, and Admins standing at the branch having used Quick-Switch. Head Office is
   * excluded on purpose: switching off a PC is a physical act, and somebody in Surat cannot see
   * whether a customer is sitting at that machine. The server enforces exactly the same rule in
   * PcStatusHub.RequireShutdownPermission - this only decides whether to draw the button, and
   * hiding a control is not security on its own.
   */
  const canShutDownPcs = !isSuperAdmin && (user?.role === 'operator' || user?.role === 'admin');

  const shutDownPc = useCallback(async (pc) => {
    if (!pc?.id) return;
    if (!window.confirm(`Shut down ${pc.name}? It locks the screen and switches off after 10 seconds.`)) return;

    try {
      await emit(SIGNALR_HUBS.PC_STATUS, 'SendShutdownCommand', String(pc.id));
      toast.success(`${pc.name} is shutting down.`);
      logActivity(`${pc.name}: Shutdown sent from the counter.`, 'warn');
    } catch (err) {
      toast.error(err?.message || `Could not shut down ${pc.name}.`);
    }
  }, [emit, SIGNALR_HUBS.PC_STATUS, toast]);

  const shutDownAllPcs = useCallback(async () => {
    if (!window.confirm(
      'Shut down every free PC at this branch?\n\n' +
      'Any PC with someone still playing is left alone.'
    )) return;

    try {
      const result = await emit(SIGNALR_HUBS.PC_STATUS, 'SendShutdownAllCommand');
      const sent = result?.sent ?? 0;
      const skipped = result?.skippedBusy ?? 0;

      // The skipped count is the whole point of saying anything at all: "12 shutting down" alone
      // would read as "the room is empty now", and walking away on that would leave customers
      // sitting at machines nobody has closed.
      toast.success(
        skipped > 0
          ? `${sent} PCs shutting down. ${skipped} left on - still in use.`
          : `${sent} PCs shutting down.`
      );
      logActivity(`Shutdown sent to ${sent} PCs; ${skipped} still in use and left on.`, 'warn');
    } catch (err) {
      toast.error(err?.message || 'Could not shut the PCs down.');
    }
  }, [emit, SIGNALR_HUBS.PC_STATUS, toast]);

  const stats = useMemo(() => {
    const activeSessions = pcs.filter(p => p.state === 'Active').length;
    const idleStations = pcs.filter(p => p.state === 'Idle').length;
    const awaitingBilling = pcs.filter(p => p.state === 'AwaitingBilling').length;

    // Live accrued revenue across all active sessions — use the backend's own live
    // totalAmount (buffer-aware, same formula as the final bill) instead of re-deriving
    // it here, so this stat can never drift from what the PC cards / billing show.
    const rawRevenue = pcs
      .filter(p => p.state === 'Active')
      .reduce((sum, p) => sum + (p.totalAmount || 0), 0);
    const liveRevenue = roundBillTotal(rawRevenue);

    return { activeSessions, idleStations, awaitingBilling, liveRevenue };
  }, [pcs, ticker]);

  // Resolve the selected PC / its pending walk-in fresh from the live lists on every render,
  // instead of caching a snapshot — so the detail panel always reflects the latest state.
  const selectedPc = pcs.find(p => p.id === selectedPcId) || null;
  const selectedWalkinReq = selectedPc
    ? walkinRequests?.find(r => r.pcId === selectedPc.name || r.pcId === selectedPc.id)
    : null;

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[60vh]">
        <div className="w-8 h-8 rounded-full border-2 border-accent border-t-transparent animate-spin" />
      </div>
    );
  }

  return (
    <div className="space-y-4">

      {/* ── Stats Bar ── */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <StatCard
          icon={<MonitorPlay className="w-4 h-4" />}
          label="ACTIVE SESSIONS"
          value={stats.activeSessions}
          color="text-pc-active"
          borderColor="border-pc-active/20"
        />
        <StatCard
          icon={<MonitorOff className="w-4 h-4" />}
          label="IDLE STATIONS"
          value={stats.idleStations}
          color="text-text-2"
          borderColor="border-border"
        />
        <StatCard
          icon={<IndianRupee className="w-4 h-4" />}
          label="LIVE ACCRUED REVENUE"
          value={`₹${stats.liveRevenue}`}
          color="text-neon-orange"
          borderColor="border-neon-orange/20"
        />
        <StatCard
          icon={<Clock className="w-4 h-4" />}
          label="AWAITING BILLING"
          value={stats.awaitingBilling}
          color="text-neon-orange"
          borderColor="border-neon-orange/20"
        />
      </div>

      {/* Closing time. Only shown to whoever can actually see the room - see canShutDownPcs. */}
      {canShutDownPcs && (
        <div className="flex justify-end">
          <button
            onClick={shutDownAllPcs}
            title="Shut down every free PC at this branch"
            className="flex items-center gap-1.5 px-3 py-1.5 rounded border border-neon-red/40 bg-neon-red/10 text-neon-red hover:bg-neon-red/20 transition-colors text-[10px] font-bold uppercase tracking-wider"
          >
            <Power className="w-3.5 h-3.5" /> Shut Down All PCs
          </button>
        </div>
      )}

      {/* ── Sessions held after a power cut ──
          Above everything else: these have stopped clocks and unused paid time, and only
          someone at the counter can say whether the customer is still in the building. */}
      <InterruptedSessionsBanner onChanged={fetchPcs} />

      {/* ── Instruction strip ── */}
      <p className="text-text-3 text-xs font-mono">
        Click a PC to view details or start a session. <span className="text-pc-active font-semibold">Double-click</span> an idle PC to quick-start.
      </p>

      {/* ── Legend + tile size zoom control ── */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-3 text-[10px] font-bold uppercase tracking-wider">
          <div className="flex items-center gap-1.5"><span className="w-2 h-2 rounded-full bg-pc-idle" /> Idle</div>
          <div className="flex items-center gap-1.5"><span className="w-2 h-2 rounded-full bg-pc-active" /> Active</div>
          <div className="flex items-center gap-1.5"><span className="w-2.5 h-2.5 rounded-full bg-pc-awaiting" /> Awaiting Bill</div>
          <div className="flex items-center gap-1.5"><span className="w-2.5 h-2.5 rounded-full bg-pc-maintenance" /> Maintenance</div>
          <div className="flex items-center gap-1.5"><span className="w-2.5 h-2.5 rounded-full bg-pc-offline" /> Shut Down</div>
        </div>
        <div className="flex items-center gap-1 border border-border rounded-lg p-0.5">
          <button
            type="button"
            onClick={() => setTileSizeIndex((i) => Math.max(0, i - 1))}
            disabled={tileSizeIndex === 0}
            className="p-1.5 rounded-md text-text-2 hover:text-text hover:bg-bg-3 disabled:opacity-30 disabled:hover:bg-transparent transition-colors"
            title="Shrink PC tiles"
          >
            <Minus className="w-3.5 h-3.5" />
          </button>
          <span className="text-[10px] font-mono font-bold uppercase text-text-2 w-6 text-center select-none">
            {TILE_SIZES[tileSizeIndex]}
          </span>
          <button
            type="button"
            onClick={() => setTileSizeIndex((i) => Math.min(TILE_SIZES.length - 1, i + 1))}
            disabled={tileSizeIndex === TILE_SIZES.length - 1}
            className="p-1.5 rounded-md text-text-2 hover:text-text hover:bg-bg-3 disabled:opacity-30 disabled:hover:bg-transparent transition-colors"
            title="Enlarge PC tiles"
          >
            <Plus className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>

      {/* ── Detail panel + PC Grid ── */}
      <div className="flex flex-col lg:flex-row gap-4 items-start" style={{ paddingBottom: logHeight + 16 }}>
        <div className="w-full lg:w-[320px] flex-shrink-0 lg:sticky lg:top-4">
          <PcDetailPanel
            pc={selectedPc}
            walkinReq={selectedWalkinReq}
            onClose={() => setSelectedPcId(null)}
            onRefresh={fetchPcs}
            onApproveWalkin={handleApproveWalkin}
            onDeclineWalkin={handleDeclineWalkin}
            onFlagMaintenance={handleFlagMaintenance}
            onCreditClick={handleCreditClick}
            onShutdown={canShutDownPcs ? shutDownPc : undefined}
          />
        </div>
        <div className="flex-1 min-w-0 w-full">
          <PcGrid
            pcs={pcs}
            walkinRequests={walkinRequests}
            selectedPcId={selectedPcId}
            onSelectPc={(pc) => setSelectedPcId(pc.id)}
            onQuickStart={(pc) => setQuickStartPc(pc)}
            onRefresh={fetchPcs}
            size={TILE_SIZES[tileSizeIndex]}
          />
        </div>
      </div>

      {/* ── Activity Log strip (fixed to viewport bottom, resizable) ── */}
      <SessionActivityLog height={logHeight} onHeightChange={setLogHeight} />

      {/* ── Quick Start Modal (double-click on an idle PC) ── */}
      <QuickStartModal
        pc={quickStartPc}
        onClose={() => setQuickStartPc(null)}
        onActionSuccess={() => {
          setSelectedPcId(quickStartPc?.id ?? null);
          setQuickStartPc(null);
          fetchPcs();
        }}
      />


      {/* ── Maintenance Reason Modal ── */}
      <MaintenanceReasonModal
        isOpen={maintenanceModalOpen}
        pcNumber={maintenancePc?.name}
        onConfirm={handleMaintenanceConfirm}
        onCancel={() => {
          setMaintenanceModalOpen(false);
          setMaintenancePc(null);
        }}
        isLoading={maintenanceLoading}
      />

    </div>
  );
}

// ── Stats card component ──
function StatCard({ icon, label, value, color, borderColor }) {
  return (
    <div className={`bg-bg-2 border ${borderColor} rounded-lg p-4 flex flex-col gap-1.5`}>
      <div className={`flex items-center gap-1.5 text-[9px] font-mono font-semibold uppercase tracking-widest ${color}`}>
        {icon}
        {label}
      </div>
      <div className={`font-heading font-bold text-2xl ${color}`}>{value}</div>
    </div>
  );
}
