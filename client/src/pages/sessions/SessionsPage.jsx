import { useState, useEffect, useCallback, useMemo } from 'react';
import { MonitorPlay, MonitorOff, IndianRupee, Clock, ShieldAlert } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import { useBranch } from '../../contexts/BranchContext';
import { useSocket } from '../../contexts/SocketContext';
import api from '../../config/api';

import PcGrid from '../../components/sessions/PcGrid';
import SessionActionModal from '../../components/sessions/SessionActionModal';
import { useToast } from '../../components/ui/Toast';
import { startReservedSession, overrideReservation } from '../../api/reservations.api';
import { getRangeReport } from '../../api/food.api';

export default function SessionsPage() {
  const { isSuperAdmin, user } = useAuth();
  const { activeBranch } = useBranch();
  const { subscribe, connected, SIGNALR_HUBS } = useSocket();
  const toast = useToast();

  const [pcs, setPcs] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [startModalPc, setStartModalPc] = useState(null); // PC to start session on

  // Reservation Override modal states
  const [overrideData, setOverrideData] = useState(null); // { id, pcName }
  const [overrideReason, setOverrideReason] = useState('');
  const [overrideLoading, setOverrideLoading] = useState(false);

  // Billing Audit Logs states
  const [auditDate, setAuditDate] = useState(new Date().toISOString().split('T')[0]);
  const [auditLogs, setAuditLogs] = useState([]);
  const [auditLoading, setAuditLoading] = useState(false);

  // Walk-in requests state
  const [walkinRequests, setWalkinRequests] = useState([]);

  const targetBranchId = isSuperAdmin ? activeBranch?.id : user?.branchId;

  const fetchPcs = useCallback(async () => {
    if (!targetBranchId) {
      setPcs([]);
      setIsLoading(false);
      return;
    }
    try {
      const { data } = await api.get('/pcs', { params: { branchId: targetBranchId } });
      const sorted = (data?.data || []).sort((a, b) =>
        a.name.localeCompare(b.name, undefined, { numeric: true })
      );
      setPcs(sorted);
    } catch (err) {
      console.error('Failed to load PCs', err);
    } finally {
      setIsLoading(false);
    }
  }, [targetBranchId]);

  const handleStartReservedSession = async (reservationId) => {
    try {
      await startReservedSession(reservationId);
      toast.success('Reserved session started successfully!');
      fetchPcs();
    } catch (err) {
      toast.error(err.response?.data?.error || err.response?.data?.message || 'Failed to start reserved session');
    }
  };

  const handleOverrideClick = (reservationId, pc) => {
    setOverrideData({ id: reservationId, pcName: pc.name });
    setOverrideReason('');
  };

  const handleFlagMaintenance = async (pc, enable = true) => {
    try {
      await api.post(`/pc-management/${pc.id}/maintenance?enable=${enable}`);
      toast.success(enable ? `${pc.name} flagged for maintenance.` : `${pc.name} restored from maintenance.`);
      fetchPcs();
    } catch (err) {
      toast.error(err.response?.data?.error || err.response?.data?.message || 'Failed to update maintenance status');
    }
  };

  const handleOverrideSubmit = async (e) => {
    e.preventDefault();
    if (!overrideReason.trim()) {
      toast.error('Override reason is required');
      return;
    }
    setOverrideLoading(true);
    try {
      await overrideReservation(overrideData.id, { reason: overrideReason.trim() });
      toast.success('Reservation overridden successfully');
      setOverrideData(null);
      setOverrideReason('');
      fetchPcs();
    } catch (err) {
      toast.error(err.response?.data?.error || err.response?.data?.message || 'Failed to override reservation');
    } finally {
      setOverrideLoading(false);
    }
  };

  const fetchAuditLogs = useCallback(async () => {
    if (!targetBranchId) return;
    setAuditLoading(true);
    try {
      const response = await getRangeReport({
        startDate: `${auditDate}T00:00:00Z`,
        endDate: `${auditDate}T23:59:59Z`,
        branchId: targetBranchId
      });
      setAuditLogs(response?.data?.allBills || []);
    } catch (err) {
      console.error('Failed to load audit logs', err);
    } finally {
      setAuditLoading(false);
    }
  }, [targetBranchId, auditDate]);

  useEffect(() => {
    setIsLoading(true);
    fetchPcs();
  }, [fetchPcs]);

  useEffect(() => {
    fetchAuditLogs();
  }, [fetchAuditLogs]);

  useEffect(() => {
    const handleRefresh = () => {
      fetchPcs();
      fetchAuditLogs(); // Refreshing audit logs too for consistency
    };
    window.addEventListener('refresh-pcs', handleRefresh);
    return () => window.removeEventListener('refresh-pcs', handleRefresh);
  }, [fetchPcs, fetchAuditLogs]);

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

  // SignalR realtime PC state updates & immediate walk-in notification
  useEffect(() => {
    if (!connected || !targetBranchId) return;
    const unsubPcStatus = subscribe(SIGNALR_HUBS.PC_STATUS, 'PcStatusChanged', (payload) => {
      const updatedPc = payload.data || payload.Data || payload;
      setPcs(current => {
        const idx = current.findIndex(p => p.id === updatedPc.id);
        if (idx === -1) return current;
        const next = [...current];
        next[idx] = { ...next[idx], ...updatedPc };
        return next;
      });
    });

    // Immediate delivery via SignalR (polling above provides the fallback)
    const unsubNotification = subscribe(SIGNALR_HUBS.NOTIFICATIONS, 'Alert', (alert) => {
      console.log('[SessionsPage] Received Alert:', alert);
      const type = alert.type || alert.Type;
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
      unsubPcStatus();
      unsubNotification();
    };
  }, [connected, subscribe, SIGNALR_HUBS.PC_STATUS, SIGNALR_HUBS.NOTIFICATIONS, targetBranchId]);

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
        setWalkinRequests(prev => prev.filter(r => r.pcId !== req.pcId));
        fetchPcs();
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
    } catch (err) {
      toast.error('Failed to decline request');
    }
  };

  // Ticker to force live revenue update every 10 seconds
  const [ticker, setTicker] = useState(0);
  useEffect(() => {
    const interval = setInterval(() => setTicker(t => t + 1), 10000);
    return () => clearInterval(interval);
  }, []);

  // ── Stats computed from PC list ──
  const stats = useMemo(() => {
    const activeSessions = pcs.filter(p => p.state === 'Active').length;
    const idleStations = pcs.filter(p => p.state === 'Idle').length;
    const awaitingBilling = pcs.filter(p => p.state === 'AwaitingBilling').length;

    // Live accrued revenue across all active sessions
    const liveRevenue = pcs
      .filter(p => p.state === 'Active' && p.sessionStartTime && p.ratePerHour > 0)
      .reduce((sum, p) => {
        const elapsedMin = (Date.now() - new Date(p.sessionStartTime).getTime()) / 60000;
        return sum + Math.ceil((elapsedMin / 60) * p.ratePerHour);
      }, 0);

    return { activeSessions, idleStations, awaitingBilling, liveRevenue };
  }, [pcs, ticker]);

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

      {/* ── Instruction strip ── */}
      <p className="text-text-3 text-xs font-mono">
        Click <span className="text-pc-active font-semibold">START SESSION</span> on a free station to begin. Red borders indicate active connections.
      </p>

      {/* ── Legend ── */}
      <div className="flex flex-wrap items-center gap-3 text-[10px] font-bold uppercase tracking-wider">
        <div className="flex items-center gap-1.5"><span className="w-2 h-2 rounded-full bg-pc-idle" /> Idle</div>
        <div className="flex items-center gap-1.5"><span className="w-2 h-2 rounded-full bg-pc-active" /> Active</div>
        <div className="flex items-center gap-1.5"><span className="w-2 h-2 rounded-full bg-pc-reserved" /> Reserved</div>
        <div className="flex items-center gap-1.5"><span className="w-2 h-2 rounded-full bg-neon-orange" /> Awaiting Bill</div>
        <div className="flex items-center gap-1.5"><span className="w-2 h-2 rounded-full bg-pc-offline" /> Maintenance</div>
      </div>

      {/* ── PC Grid ── */}
      <PcGrid
        pcs={pcs}
        walkinRequests={walkinRequests}
        onStartSession={setStartModalPc}
        onRefresh={fetchPcs}
        onStartReservedSession={handleStartReservedSession}
        onOverrideReservation={handleOverrideClick}
        onApproveWalkin={handleApproveWalkin}
        onDeclineWalkin={handleDeclineWalkin}
        onFlagMaintenance={handleFlagMaintenance}
      />

      {/* ── Complete Billing Audit Logs ── */}
      <div className="card bg-bg-2 border border-border p-6 rounded-xl mt-8">
        <div className="flex justify-between items-center mb-6">
          <h2 className="font-heading font-extrabold text-sm uppercase tracking-wider text-text flex items-center gap-2">
            <Clock className="w-4.5 h-4.5 text-accent" />
            Complete Billing Audit Logs
          </h2>
          <div className="flex items-center gap-3">
            <div className="flex items-center gap-2">
              <input
                type="date"
                value={auditDate}
                onChange={e => setAuditDate(e.target.value)}
                className="bg-bg-3 border border-border text-text text-xs rounded-md p-1.5 focus:border-accent outline-none cursor-pointer"
                style={{ colorScheme: 'dark' }}
              />
            </div>
            <button
              onClick={fetchAuditLogs}
              className="btn-secondary py-1 px-3 text-[11px] font-bold uppercase tracking-wider"
            >
              Refresh
            </button>
          </div>
        </div>

        <div className="overflow-x-auto">
          {auditLoading ? (
            <div className="flex justify-center items-center py-12">
              <div className="w-6 h-6 border-2 border-accent border-t-transparent rounded-full animate-spin" />
            </div>
          ) : auditLogs.length === 0 ? (
            <div className="text-center text-text-3 text-xs italic py-8 border border-dashed border-border rounded-lg">
              No bills found for the selected date.
            </div>
          ) : (
            <table className="w-full text-left border-collapse text-xs">
              <thead>
                <tr className="border-b border-border text-text-3 uppercase tracking-wider font-bold text-[10px]">
                  <th className="py-3 px-4">Date/Time</th>
                  <th className="py-3 px-4">Bill Number</th>
                  <th className="py-3 px-4">Operator</th>
                  <th className="py-3 px-4">Customer</th>
                  <th className="py-3 px-4 text-center">Payment</th>
                  <th className="py-3 px-4 text-right">Gaming</th>
                  <th className="py-3 px-4 text-right">Food</th>
                  <th className="py-3 px-4 text-right">Discount</th>
                  <th className="py-3 px-4 text-right">Total</th>
                  <th className="py-3 px-4">Notes</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border/40 font-mono">
                {auditLogs.map(bill => (
                  <tr key={bill.billId} className="hover:bg-bg-3/40 transition-colors">
                    <td className="py-3 px-4 text-text-2 flex items-center gap-1">
                      {new Date(bill.date).toLocaleString()}
                    </td>
                    <td className="py-3 px-4 text-text font-bold">{bill.billId}</td>
                    <td className="py-3 px-4 text-neon-blue font-bold">{bill.operator}</td>
                    <td className="py-3 px-4 text-text-2 font-sans">{bill.customer}</td>
                    <td className="py-3 px-4 text-center text-text-3 uppercase">{bill.paymentType}</td>
                    <td className="py-3 px-4 text-right text-text">₹{bill.gamingRevenue.toFixed(2)}</td>
                    <td className="py-3 px-4 text-right text-text">₹{bill.foodRevenue.toFixed(2)}</td>
                    <td className="py-3 px-4 text-right text-neon-red">{bill.discount > 0 ? `-₹${bill.discount.toFixed(2)}` : '-'}</td>
                    <td className="py-3 px-4 text-right text-neon-green font-bold">₹{bill.totalRevenue.toFixed(2)}</td>
                    <td className="py-3 px-4 text-text-3 text-[10px] whitespace-pre-wrap">{bill.sessionNotes || '-'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>

      {/* ── Start Session Modal (only for new session initiation) ── */}
      <SessionActionModal
        pc={startModalPc}
        onClose={() => setStartModalPc(null)}
        onActionSuccess={() => {
          setStartModalPc(null);
          fetchPcs();
        }}
      />

      {/* ── Override Reservation Modal ── */}
      {overrideData && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm animate-[fadeIn_0.15s_ease-out]">
          <div className="w-full max-w-sm bg-bg-2 border border-border rounded-xl shadow-2xl overflow-hidden">
            <div className="px-5 py-4 border-b border-border bg-bg-3 flex items-center justify-between">
              <div>
                <h2 className="font-heading font-bold text-text uppercase tracking-wider text-sm flex items-center gap-2">
                  <ShieldAlert className="w-4 h-4 text-neon-orange animate-bounce" />
                  Override Reservation — {overrideData.pcName}
                </h2>
                <p className="text-text-3 text-[10px] font-mono mt-0.5">
                  An audit log entry will document this override.
                </p>
              </div>
              <button onClick={() => setOverrideData(null)} className="text-text-3 hover:text-text text-xl">&times;</button>
            </div>
            <form onSubmit={handleOverrideSubmit} className="p-5 space-y-4">
              <div className="space-y-1.5">
                <label className="text-[10px] font-mono font-semibold text-text-2 uppercase tracking-wider block">
                  Mandatory Reason for Override *
                </label>
                <textarea
                  value={overrideReason}
                  onChange={(e) => setOverrideReason(e.target.value)}
                  placeholder="Provide detailed explanation..."
                  rows={3}
                  className="w-full bg-bg-3 border border-border rounded px-3 py-2 text-xs text-text placeholder-text-3 focus:border-neon-orange focus:outline-none transition-colors resize-none"
                  required
                  autoFocus
                />
              </div>
              <div className="flex justify-end gap-2.5">
                <button
                  type="button"
                  onClick={() => setOverrideData(null)}
                  className="px-4 py-2 border border-border bg-transparent text-text-2 rounded text-xs font-semibold hover:bg-bg-3 transition-colors"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={overrideLoading || !overrideReason.trim()}
                  className="px-4 py-2 bg-neon-orange/10 border border-neon-orange/50 text-neon-orange rounded text-xs font-semibold hover:bg-neon-orange/20 transition-colors flex items-center justify-center gap-1.5 disabled:opacity-50"
                >
                  {overrideLoading ? (
                    <span className="w-3.5 h-3.5 border border-current border-t-transparent rounded-full animate-spin" />
                  ) : (
                    'Override PC'
                  )}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
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
