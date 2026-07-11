import { useState, useEffect, useCallback, useMemo } from 'react';
import { MonitorPlay, MonitorOff, IndianRupee, Clock } from 'lucide-react';
import { useBranch } from '../../contexts/BranchContext';
import { useSocket } from '../../contexts/SocketContext';
import api from '../../config/api';
import PageHeader from '../../components/layout/PageHeader';

function useElapsedTime(startTimeIso) {
  const [elapsed, setElapsed] = useState({ h: 0, m: 0 });

  useEffect(() => {
    if (!startTimeIso) return;
    const update = () => {
      const diffMs = Date.now() - new Date(startTimeIso).getTime();
      const totalSec = Math.max(0, Math.floor(diffMs / 1000));
      setElapsed({
        h: Math.floor(totalSec / 3600),
        m: Math.floor((totalSec % 3600) / 60)
      });
    };
    update();
    const id = setInterval(update, 10000); // update every 10s is enough for read-only
    return () => clearInterval(id);
  }, [startTimeIso]);

  if (!startTimeIso) return '0h 0m';
  if (elapsed.h > 0) return `${elapsed.h}h ${elapsed.m}m`;
  return `${elapsed.m}m`;
}

// Separate component to handle individual PC logic and ticking
const AdminPcCard = ({ pc }) => {
  const elapsedText = useElapsedTime(pc.sessionStartTime);
  
  const isActive = pc.state === 'Active';
  const isAwaiting = pc.state === 'AwaitingBilling';
  const isReserved = pc.state === 'Reserved';
  const isIdle = pc.state === 'Idle';
  const isMaintenance = pc.state === 'UnderMaintenance';

  let borderClass = 'border-pc-idle/30 hover:border-pc-idle/50';
  let bgClass = 'bg-pc-idle/5';
  
  if (isActive) {
    borderClass = 'border-pc-active/50';
    bgClass = 'bg-pc-active/10';
  } else if (isAwaiting) {
    borderClass = 'border-neon-orange/50';
    bgClass = 'bg-neon-orange/10';
  } else if (isReserved) {
    borderClass = 'border-pc-reserved/50';
    bgClass = 'bg-pc-reserved/10';
  } else if (isMaintenance) {
    borderClass = 'border-pc-offline/30';
    bgClass = 'bg-bg-2/60 opacity-75';
  }

  // Calculate live charge
  let liveCharge = 0;
  if (pc.sessionEndTime) {
    liveCharge = pc.totalAmount || 0;
  } else if (pc.sessionStartTime && pc.ratePerHour > 0) {
    const elapsedMin = Math.max(0, (Date.now() - new Date(pc.sessionStartTime).getTime()) / 60000);
    liveCharge = (pc.totalAmount || 0) + Math.ceil((elapsedMin / 60) * pc.ratePerHour);
  } else if (pc.totalAmount > 0) {
    liveCharge = pc.totalAmount;
  }

  const showCharge = (isActive || isAwaiting) && liveCharge > 0;
  
  return (
    <div className={`rounded-xl border p-4 flex flex-col gap-3 transition-colors ${borderClass} ${bgClass}`}>
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <span className="font-heading font-bold text-sm tracking-wide text-text">{pc.name}</span>
          {/* Agent Connectivity Indicator */}
          {pc.isAgentOnline ? (
            <span className={`w-2 h-2 rounded-full ${pc.connectionMode === 'Cloud' ? 'bg-neon-orange' : 'bg-pc-active'}`} title={`Agent Online (${pc.connectionMode})`} />
          ) : (
            <span className="w-2 h-2 rounded-full bg-pc-offline animate-pulse" title="Agent Offline" />
          )}
        </div>
        
        {isActive && <span className="text-[9px] font-mono font-bold px-1.5 py-0.5 border border-pc-active/50 text-pc-active rounded uppercase">ACTIVE</span>}
        {isAwaiting && <span className="text-[9px] font-mono font-bold px-1.5 py-0.5 border border-neon-orange/50 text-neon-orange rounded uppercase">AWAITING BILL</span>}
        {isReserved && <span className="text-[9px] font-mono font-bold px-1.5 py-0.5 border border-pc-reserved/50 text-pc-reserved rounded uppercase">RESERVED</span>}
        {isIdle && <span className="text-[9px] font-mono font-bold px-1.5 py-0.5 border border-border text-text-3 rounded uppercase">FREE</span>}
        {isMaintenance && <span className="text-[9px] font-mono font-bold px-1.5 py-0.5 border border-pc-offline/50 text-pc-offline rounded uppercase">MAINTENANCE</span>}
      </div>

      {/* Details Area */}
      <div className="flex flex-col gap-1.5 text-xs">
        {(isActive || isAwaiting) ? (
          <>
            <div className="flex justify-between items-center">
              <span className="text-text-3 font-mono">User:</span>
              <span className={`font-semibold ${pc.customerType === 'Member' ? 'text-neon-blue' : 'text-neon-orange'}`}>
                {pc.customerName || (pc.customerType === 'Member' ? 'Member' : 'Walk-in')}
              </span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-text-3 font-mono">Time:</span>
              <span className={`font-mono font-bold ${isActive ? 'text-pc-active' : 'text-neon-orange'}`}>
                {elapsedText}
              </span>
            </div>
            {showCharge && (
              <div className="flex justify-between items-center">
                <span className="text-text-3 font-mono">Accrued:</span>
                <span className="font-mono font-bold text-text">
                  ₹{liveCharge}
                </span>
              </div>
            )}
          </>
        ) : isReserved ? (
          <>
            <div className="flex justify-between items-center">
              <span className="text-text-3 font-mono">Reserved By:</span>
              <span className="font-semibold text-pc-reserved">
                {pc.customerName || '—'}
              </span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-text-3 font-mono">Time:</span>
              <span className="font-mono font-bold text-pc-reserved">
                {pc.nextReservationTime ? new Date(pc.nextReservationTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '—'}
              </span>
            </div>
          </>
        ) : (
          <div className="flex flex-col items-center justify-center py-2 text-text-3 opacity-50">
            <span className="font-mono">Ready for Session</span>
          </div>
        )}
      </div>
    </div>
  );
};

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

export default function PcStatusPage() {
  const { activeBranch } = useBranch();
  const { subscribe, connected, SIGNALR_HUBS } = useSocket();
  const [pcs, setPcs] = useState([]);
  const [isLoading, setIsLoading] = useState(true);

  const fetchPcs = useCallback(async () => {
    if (!activeBranch?.id) {
      setPcs([]);
      setIsLoading(false);
      return;
    }
    try {
      const { data } = await api.get('/pcs', { params: { branchId: activeBranch.id } });
      const sorted = (data?.data || []).sort((a, b) =>
        a.name.localeCompare(b.name, undefined, { numeric: true })
      );
      setPcs(sorted);
    } catch (err) {
      console.error('Failed to load PCs', err);
    } finally {
      setIsLoading(false);
    }
  }, [activeBranch?.id]);

  useEffect(() => {
    setIsLoading(true);
    fetchPcs();
  }, [fetchPcs]);

  // SignalR realtime PC state updates
  useEffect(() => {
    if (!connected || !activeBranch?.id) return;
    const unsub = subscribe(SIGNALR_HUBS.PC_STATUS, 'PcStatusChanged', () => {
      console.log('[PcStatusPage] PcStatusChanged received. Refetching PCs...');
      fetchPcs();
    });
    return () => unsub();
  }, [connected, subscribe, SIGNALR_HUBS.PC_STATUS, activeBranch?.id]);

  // Ticker to force live revenue update every 10 seconds
  const [ticker, setTicker] = useState(0);
  useEffect(() => {
    const interval = setInterval(() => setTicker(t => t + 1), 10000);
    return () => clearInterval(interval);
  }, []);

  const stats = useMemo(() => {
    const activeSessions = pcs.filter(p => p.state === 'Active').length;
    const idleStations = pcs.filter(p => p.state === 'Idle').length;
    const awaitingBilling = pcs.filter(p => p.state === 'AwaitingBilling').length;
    const reservedStations = pcs.filter(p => p.state === 'Reserved').length;

    // Live accrued revenue across all active sessions
    const liveRevenue = pcs
      .filter(p => (p.state === 'Active' || p.state === 'AwaitingBilling') && p.sessionStartTime && p.ratePerHour > 0)
      .reduce((sum, p) => {
        const elapsedMin = Math.max(0, (Date.now() - new Date(p.sessionStartTime).getTime()) / 60000);
        return sum + (p.totalAmount || 0) + Math.ceil((elapsedMin / 60) * p.ratePerHour);
      }, 0);

    return { activeSessions, idleStations, awaitingBilling, reservedStations, liveRevenue };
  }, [pcs, ticker]);

  return (
    <div className="space-y-4">
      <PageHeader
        title="PC Status"
        subtitle="Full PC fleet overview — all branches, all states"
        icon="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"
        badge="ADMIN ONLY"
      />

      <div className="card space-y-4">
        {/* Stats Row */}
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
          <StatCard
            icon={<MonitorPlay className="w-4 h-4" />}
            label="ACTIVE SESSIONS"
            value={stats.activeSessions + (stats.awaitingBilling ? ` (+${stats.awaitingBilling} bill)` : '')}
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
            label="LIVE REVENUE"
            value={`₹${stats.liveRevenue}`}
            color="text-neon-orange"
            borderColor="border-neon-orange/20"
          />
          <StatCard
            icon={<Clock className="w-4 h-4" />}
            label="RESERVED"
            value={stats.reservedStations}
            color="text-pc-reserved"
            borderColor="border-pc-reserved/20"
          />
        </div>

        {isLoading ? (
          <div className="flex items-center justify-center min-h-[30vh]">
            <div className="w-8 h-8 rounded-full border-2 border-accent border-t-transparent animate-spin" />
          </div>
        ) : (
          <>
            {/* Grid */}
            <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 xl:grid-cols-5 gap-3">
              {pcs.map(pc => (
                <AdminPcCard key={pc.id} pc={pc} />
              ))}
            </div>

            {/* Legend */}
            <div className="flex flex-wrap items-center gap-4 text-[11px] text-text-2 border-t border-border pt-4 mt-2">
              <div className="flex items-center gap-1.5">
                <span className="w-2 h-2 rounded bg-pc-active" /> Active
              </div>
              <div className="flex items-center gap-1.5">
                <span className="w-2 h-2 rounded bg-pc-idle" /> Idle
              </div>
              <div className="flex items-center gap-1.5">
                <span className="w-2 h-2 rounded bg-pc-reserved" /> Reserved
              </div>
              <div className="flex items-center gap-1.5">
                <span className="w-2 h-2 rounded bg-neon-orange" /> Awaiting Bill
              </div>
              <div className="flex items-center gap-1.5">
                <span className="w-2 h-2 rounded bg-pc-offline" /> Maintenance
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
