import { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Bell, MonitorPlay, X, Check, Loader2 } from 'lucide-react';
import { useSocket } from '../../contexts/SocketContext';
import { useToast } from '../ui/Toast';
import { useBranch } from '../../contexts/BranchContext';
import { useAuth } from '../../contexts/AuthContext';
import api from '../../config/api';

export default function GlobalNotificationListener() {
  const { subscribe, SIGNALR_HUBS, connected } = useSocket();
  const { activeBranch } = useBranch();
  const { user } = useAuth();
  const toast = useToast();
  
  // Array of active walk-in requests
  const [requests, setRequests] = useState([]);
  const [isProcessing, setIsProcessing] = useState(false);

  useEffect(() => {
    if (!connected) return;

    const unsubscribe = subscribe(SIGNALR_HUBS.NOTIFICATIONS, 'Alert', async (data) => {
      const type = data.type || data.Type;
      
      let pcName = data.pcId || data.PcId;
      try {
        const res = await api.get(`/public/pcs/${pcName}`);
        if (res.data.success) pcName = res.data.data.name;
      } catch (e) {
        // silent fallback to ID
      }

      // Filter notifications for Admins and SuperAdmins so they only see them for the actively selected branch
      const isAdminOrSuperAdmin = user?.role === 'admin' || user?.role === 'super_admin';
      const alertBranchId = data.branchId || data.BranchId;
      if (isAdminOrSuperAdmin && alertBranchId && activeBranch?.id && alertBranchId !== activeBranch.id) {
        return; // Ignore this notification as it's not for the currently viewed branch
      }

      if (type === 'WalkinSessionRequest') {
        // Add to our list of active requests
        setRequests(prev => {
          const pcId = data.pcId || data.PcId;
          // avoid duplicates if same PC sends multiple times
          const filtered = prev.filter(r => (r.pcId || r.PcId) !== pcId);
          return [...filtered, { ...data, resolvedPcName: pcName }];
        });
        toast.info(`Walk-in request from ${pcName}`);
      } else if (type === 'OperatorCall') {
        const pcId = data.pcId || data.PcId;
        const msg = data.message || data.Message || `Assistance required at ${pcId}`;
        
        setRequests(prev => {
          const filtered = prev.filter(r => (r.pcId || r.PcId) !== pcId || r.type !== 'OperatorCall');
          return [...filtered, { ...data, type: 'OperatorCall', resolvedPcName: pcName }];
        });
        
        // Speak the notification
        if ('speechSynthesis' in window) {
           const utterance = new SpeechSynthesisUtterance(`Attention Operator. ${pcName} is calling you.`);
           utterance.rate = 0.9;
           utterance.pitch = 1.1;
           window.speechSynthesis.speak(utterance);
        }
      }
    });

    const unsubscribeStatus = subscribe(SIGNALR_HUBS.PC_STATUS, 'PcStatusChanged', (payload) => {
      const data = payload.data || payload.Data || payload;
      // If PC becomes active, remove any pending walk-in requests for it
      // Wait, our backend sends 'PcStatusChanged' on PC_STATUS hub
      const status = data.status || data.State || data.state;
      if (status === 'active' || status === 'Active') {
        setRequests(prev => prev.filter(r => (r.pcId || r.PcId) !== (data.pcId || data.id)));
      }
    });

    const unsubscribeExtension = subscribe(SIGNALR_HUBS.SESSIONS, 'ExtensionRequested', async (data) => {
      let pcName = data.pcId || data.PcId;
      try {
        const res = await api.get(`/public/pcs/${pcName}`);
        if (res.data.success) pcName = res.data.data.name;
      } catch (e) { }

      const isAdminOrSuperAdmin = user?.role === 'admin' || user?.role === 'super_admin';
      const alertBranchId = data.branchId || data.BranchId;
      if (isAdminOrSuperAdmin && alertBranchId && activeBranch?.id && alertBranchId !== activeBranch.id) {
        return; 
      }

      setRequests(prev => {
        const pcId = data.pcId || data.PcId;
        const reqData = { ...data, type: 'ExtensionRequested', resolvedPcName: pcName };
        const filtered = prev.filter(r => (r.pcId || r.PcId) !== pcId);
        return [...filtered, reqData];
      });
      toast.info(`Time extension request from ${pcName}`);
    });

    return () => {
      unsubscribe();
      if (unsubscribeStatus) unsubscribeStatus();
      if (unsubscribeExtension) unsubscribeExtension();
    };
  }, [connected, subscribe, SIGNALR_HUBS.NOTIFICATIONS, SIGNALR_HUBS.PC_STATUS, SIGNALR_HUBS.SESSIONS, toast, user, activeBranch]);

  const handleApprove = async (req) => {
    setIsProcessing(true);
    try {
      const pcId = req.pcId || req.PcId;
      const customerName = req.customerName || req.CustomerName;
      const duration = req.duration || req.Duration || 0;

      if (req.type === 'ExtensionRequested') {
        const sessionId = req.sessionId || req.SessionId;
        
        // Fetch session to get actual ratePerHour
        let ratePerHour = 100;
        try {
            const sessionRes = await api.get(`/public/session/pc/${pcId}`);
            if (sessionRes.data.success && sessionRes.data.data) {
                ratePerHour = sessionRes.data.data.ratePerHour || 100;
            }
        } catch (e) {
            console.warn('Failed to fetch session rate, using default 100');
        }
        
        const expectedAmount = duration ? (duration / 60) * ratePerHour : 0;
        
        const extendRes = await api.post(`/sessions/${sessionId}/extend`, { 
            AdditionalMinutes: duration,
            AdditionalAmount: expectedAmount,
            PackageName: req.packageName || req.PackageName || 'Extension'
        });
        if (extendRes.data.success) {
          toast.success(`Time extended for ${pcId}`);
          setRequests(prev => prev.filter(r => (r.pcId || r.PcId) !== pcId));
          window.dispatchEvent(new CustomEvent('refresh-pcs', { detail: { pcId } }));
        }
        setIsProcessing(false);
        return;
      }

      // 1. Resolve PC Guid (since the name might have been sent)
      const pcRes = await api.get(`/public/pcs/${pcId}`);
      if (!pcRes.data.success) {
        toast.error('PC not found in database');
        setIsProcessing(false);
        return;
      }
      
      const actualPcId = pcRes.data.data.id;
      const ratePerHour = pcRes.data.data.ratePerHour || 100;
      const expectedAmount = duration ? (duration / 60) * ratePerHour : 0;

      // 2. Start Session 
      const startRes = await api.post('/sessions/start', {
        pcId: actualPcId,
        memberId: null,
        customerName: customerName,
        durationMinutes: duration,
        packageName: req.packageName || req.PackageName || 'Walk-in',
        expectedAmount: expectedAmount
      });

      if (startRes.data.success) {
        toast.success(`Session started for ${pcId}`);
        setRequests(prev => prev.filter(r => (r.pcId || r.PcId) !== pcId));
        window.dispatchEvent(new CustomEvent('refresh-pcs', { detail: { pcId: actualPcId } }));
      }
    } catch (err) {
      toast.error(err.response?.data?.error || 'Failed to approve request');
    } finally {
      setIsProcessing(false);
    }
  };

  const handleDecline = async (req) => {
    try {
      const pcId = req.pcId || req.PcId;
      if (req.type === 'ExtensionRequested') {
        setRequests(prev => prev.filter(r => (r.pcId || r.PcId) !== pcId));
        toast.info(`Declined extension for ${pcId}`);
      } else {
        await api.post(`/public/pcs/${pcId}/decline-walkin`);
        setRequests(prev => prev.filter(r => (r.pcId || r.PcId) !== pcId));
        toast.info(`Declined walk-in for ${pcId}`);
      }
    } catch (err) {
      toast.error('Failed to decline request');
    }
  };

  const handleAcknowledgeCall = (req) => {
    const pcId = req.pcId || req.PcId;
    setRequests(prev => prev.filter(r => (r.pcId || r.PcId) !== pcId || r.type !== 'OperatorCall'));
    toast.success(`Acknowledged call from ${pcId}`);
  };

  if (requests.length === 0) return null;

  return (
    <div className="fixed bottom-6 left-1/2 -translate-x-1/2 z-[100] flex flex-col gap-4">
      <AnimatePresence>
        {requests.map((req) => {
          const isExtension = req.type === 'ExtensionRequested';
          const isOperatorCall = req.type === 'OperatorCall';
          const pcId = req.pcId || req.PcId;
          const pcName = req.resolvedPcName || pcId;
          const customerName = req.customerName || req.CustomerName || 'Current User';
          const duration = req.duration || req.Duration || 0;
          return (
            <motion.div
              key={pcId + req.type}
              initial={{ opacity: 0, y: 50, scale: 0.9 }}
              animate={{ opacity: 1, y: 0, scale: 1 }}
              exit={{ opacity: 0, scale: 0.9 }}
              className="bg-bg-2 border border-accent shadow-[0_0_30px_rgba(220,38,38,0.3)] rounded-lg p-5 w-[400px]"
            >
              <div className="flex items-center gap-3 mb-4">
                <div className="w-10 h-10 bg-accent/20 rounded-full flex items-center justify-center shrink-0">
                  <Bell className="w-5 h-5 text-accent animate-pulse" />
                </div>
                <div>
                  <h3 className="font-heading font-bold text-text uppercase tracking-widest text-sm">
                    {isOperatorCall ? 'Operator Call' : isExtension ? 'Time Extension' : 'Walk-in Request'}
                  </h3>
                  <p className="text-text-2 font-body text-xs mt-0.5">Station <span className="text-accent font-semibold">{pcName}</span></p>
                </div>
              </div>

              {!isOperatorCall && (
              <div className="bg-bg-3 border border-border p-3 rounded-md mb-5">
                <div className="flex justify-between items-center mb-2">
                  <span className="text-text-3 font-body text-xs">Customer</span>
                  <span className="text-text font-bold font-heading">{customerName}</span>
                </div>
                <div className="flex justify-between items-center mb-2">
                  <span className="text-text-3 font-body text-xs">Duration</span>
                  <span className="text-text font-mono font-bold text-sm">
                    {duration === 0 ? 'Pay As You Go' : `${duration >= 60 ? duration / 60 : duration} ${duration === 30 ? 'Min' : 'Hr'}`}
                  </span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-text-3 font-body text-xs">Expected Bill</span>
                  <span className="text-text font-mono font-bold text-sm text-accent">
                    {duration === 0 ? 'Variable' : `₹${(duration / 60) * 100}`}
                  </span>
                </div>
              </div>
              )}

              <div className="flex gap-3">
                {isOperatorCall ? (
                  <button
                    onClick={() => handleAcknowledgeCall(req)}
                    disabled={isProcessing}
                    className="flex-1 bg-accent hover:bg-accent-dark text-white rounded-sm py-2 text-sm font-semibold flex items-center justify-center gap-2 disabled:opacity-50 shadow-[0_0_15px_rgba(220,38,38,0.4)]"
                  >
                    <Check className="w-4 h-4" /> Acknowledge
                  </button>
                ) : (
                  <>
                    <button
                      onClick={() => handleDecline(req)}
                      disabled={isProcessing}
                      className="flex-1 bg-bg-3 hover:bg-bg-3/80 text-text-2 border border-border hover:border-text-3 transition-colors rounded-sm py-2 text-sm font-semibold flex items-center justify-center gap-2 disabled:opacity-50"
                    >
                      <X className="w-4 h-4" /> Decline
                    </button>
                    <button
                      onClick={() => handleApprove(req)}
                      disabled={isProcessing}
                      className="flex-1 bg-accent hover:bg-accent-dark text-white rounded-sm py-2 text-sm font-semibold shadow-[0_0_15px_rgba(220,38,38,0.4)] flex items-center justify-center gap-2 disabled:opacity-50"
                    >
                      {isProcessing ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
                      Approve
                    </button>
                  </>
                )}
              </div>
            </motion.div>
          );
        })}
      </AnimatePresence>
    </div>
  );
}
