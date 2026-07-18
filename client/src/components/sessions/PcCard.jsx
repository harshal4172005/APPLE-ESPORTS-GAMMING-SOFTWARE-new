import { memo, useState, useEffect, useCallback } from 'react';
import { User, Clock, Wrench, AlertTriangle, Square, RefreshCw, Receipt, Coffee, Gift, Banknote } from 'lucide-react';
import { motion } from 'framer-motion';
import api from '../../config/api';
import { useAuth } from '../../contexts/AuthContext';
import { useNavigate } from 'react-router-dom';
import { useToast } from '../../components/ui/Toast';
import ExtendSessionModal from './ExtendSessionModal';
import SessionDiscountModal from './SessionDiscountModal';

// ── Elapsed time from a start ISO string (counting UP) ──
function useElapsedTime(startTimeIso) {
  const [elapsed, setElapsed] = useState({ h: 0, m: 0, s: 0, totalMin: 0 });

  useEffect(() => {
    if (!startTimeIso) return;

    const update = () => {
      const diffMs = Date.now() - new Date(startTimeIso).getTime();
      const totalSec = Math.max(0, Math.floor(diffMs / 1000));
      const h = Math.floor(totalSec / 3600);
      const m = Math.floor((totalSec % 3600) / 60);
      const s = totalSec % 60;
      setElapsed({ h, m, s, totalMin: totalSec / 60 });
    };

    update();
    const id = setInterval(update, 1000);
    return () => clearInterval(id);
  }, [startTimeIso]);

  return elapsed;
}

// ── Format elapsed as "0h 12m" ──
function fmtElapsed(h, m) {
  if (h > 0) return `${h}h ${m}m`;
  return `${m}m`;
}

// ── Format time as HH:MM ──
function fmtTime(isoString) {
  if (!isoString) return '';
  const d = new Date(isoString);
  return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}

const PcCard = memo(({ pc, walkinReq, onStartSession, onRefresh, onStartReservedSession, onOverrideReservation, onApproveWalkin, onDeclineWalkin, onFlagMaintenance, onCreditClick }) => {
  const { isSuperAdmin, user } = useAuth();
  const navigate = useNavigate();
  const toast = useToast();
  const elapsed = useElapsedTime(pc.sessionStartTime);
  const [actionLoading, setActionLoading] = useState(null); // 'stop' | 'extend' | etc.
  const [showExtendModal, setShowExtendModal] = useState(false);
  const [showDiscountModal, setShowDiscountModal] = useState(false);

  // Drag and Drop state for transferring sessions
  const [isDragOver, setIsDragOver] = useState(false);
  const [isTransferring, setIsTransferring] = useState(false);

  // Live charge: if fixed session, just totalAmount. if open-ended, totalAmount + elapsed rate.
  let liveCharge = 0;
  if (pc.sessionEndTime) {
    liveCharge = pc.totalAmount || 0;
  } else {
    const elapsedGamingCharge = pc.ratePerHour > 0 ? Number((Math.max(elapsed.totalMin / 60, 1 / 60) * pc.ratePerHour).toFixed(2)) : 0;
    liveCharge = (pc.totalAmount || 0) + (elapsed.totalMin <= 10 ? 0 : elapsedGamingCharge);
  }

  const isActive = pc.state === 'Active';
  const isIdle = pc.state === 'Idle';
  const isReserved = pc.state === 'Reserved';
  const isAwaiting = pc.state === 'AwaitingBilling';
  const isMaintenance = pc.state === 'UnderMaintenance';

  const doAction = useCallback(async (action, payload = {}, loadingKey = null) => {
    const key = loadingKey || action;
    setActionLoading(key);
    try {
      await api.post(`/sessions/${pc.activeSessionId}/${action}`, payload);
      if (payload.deferPayment) {
        toast.success('Session stopped. Bill moved to Review Billing — PC is now free.');
      } else {
        toast.success(`Session successfully ${action}ed!`);
      }
      onRefresh?.();
    } catch (err) {
      console.error(`Action ${action} failed:`, err);
      toast.error(`Failed to ${action} session.`);
    } finally {
      setActionLoading(null);
    }
  }, [pc.activeSessionId, onRefresh, toast]);

  // ── PENDING WALKIN Card ──
  if (walkinReq) {
    return (
      <motion.div
        initial={{ opacity: 0, scale: 0.97 }}
        animate={{ opacity: 1, scale: 1 }}
        className="relative rounded-lg border border-accent bg-bg-2 p-4 flex flex-col gap-3 shadow-[0_0_15px_rgba(220,38,38,0.15)]"
      >
        <div className="flex items-center justify-between">
          <span className="font-heading font-bold text-text text-sm tracking-wider">{pc.name}</span>
          <span className="text-[9px] font-mono font-bold px-1.5 py-0.5 border border-accent/50 text-accent rounded animate-pulse">WALK-IN PENDING</span>
        </div>
        <div className="flex items-center gap-1.5 text-accent text-xs">
          <User className="w-3.5 h-3.5" />
          <span className="font-semibold">{walkinReq.customerName}</span>
        </div>
        <div className="flex justify-between items-center text-[10px] font-mono mt-1">
          <span className="text-text-3">Duration: {walkinReq.duration / 60} Hr</span>
          <span className="text-text">Amt: ₹{(walkinReq.duration / 60) * 100}</span>
        </div>
        <div className="grid grid-cols-2 gap-1.5 mt-1">
          <button
            onClick={() => onDeclineWalkin?.(walkinReq)}
            className="py-1.5 rounded border border-border bg-bg-3 text-text-2 text-[10px] font-bold uppercase tracking-wider hover:bg-bg-3/80 transition-colors flex items-center justify-center gap-1"
          >
            Decline
          </button>
          <button
            onClick={() => onApproveWalkin?.(walkinReq)}
            className="py-1.5 rounded border border-accent/40 bg-accent text-white text-[10px] font-bold uppercase tracking-wider hover:bg-accent-dark transition-colors flex items-center justify-center gap-1 shadow-[0_0_10px_rgba(220,38,38,0.3)]"
          >
            Approve
          </button>
        </div>
      </motion.div>
    );
  }

  // ── FREE (Idle) Card ──
  if (isIdle) {
    const handleDragOver = (e) => {
      e.preventDefault();
      e.dataTransfer.dropEffect = 'move';
    };

    const handleDragEnter = (e) => {
      e.preventDefault();
      setIsDragOver(true);
    };

    const handleDragLeave = (e) => {
      e.preventDefault();
      setIsDragOver(false);
    };

    const handleDrop = async (e) => {
      e.preventDefault();
      setIsDragOver(false);
      try {
        const data = JSON.parse(e.dataTransfer.getData('text/plain'));
        if (!data.sessionId || data.sourcePcId === pc.id) return;
        
        setIsTransferring(true);
        await api.post(`/sessions/${data.sessionId}/transfer`, { targetPcId: pc.id });
        toast.success(`Session transferred to ${pc.name}!`);
        onRefresh?.();
      } catch (err) {
        console.error('Transfer failed:', err);
        toast.error(err.response?.data?.error || err.response?.data?.message || 'Failed to transfer session');
      } finally {
        setIsTransferring(false);
      }
    };

    return (
      <motion.div
        initial={{ opacity: 0, scale: 0.97 }}
        animate={{ opacity: 1, scale: 1 }}
        onDragOver={handleDragOver}
        onDragEnter={handleDragEnter}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        className={`relative rounded-lg border bg-bg-2 p-4 flex flex-col gap-3 select-none transition-colors ${
          isDragOver ? 'border-pc-active bg-pc-active/5 shadow-[0_0_15px_rgba(34,211,166,0.2)]' : 'border-border'
        }`}
      >
        {isTransferring && (
          <div className="absolute inset-0 z-10 flex items-center justify-center bg-bg/80 backdrop-blur-sm rounded-lg">
            <span className="w-6 h-6 border-2 border-pc-active border-t-transparent rounded-full animate-spin" />
          </div>
        )}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="font-heading font-bold text-text text-sm tracking-wider">{pc.name}</span>
            {/* Agent Connectivity Indicator */}
            {pc.isAgentOnline ? (
              <span className={`w-2 h-2 rounded-full ${pc.connectionMode === 'Cloud' ? 'bg-neon-orange' : 'bg-pc-active'}`} title={`Agent Online (${pc.connectionMode})`} />
            ) : (
              <span className="w-2 h-2 rounded-full bg-pc-offline" title="Agent Offline" />
            )}
          </div>
          <span className="text-[9px] font-mono font-bold px-1.5 py-0.5 border border-border text-text-3 rounded">FREE</span>
        </div>
        <div className="flex items-center gap-1.5 text-text-3 text-xs">
          <User className="w-3.5 h-3.5" />
          <span>Available</span>
        </div>
        <div className="flex flex-col gap-1.5">
          <button
            onClick={() => onStartSession?.(pc)}
            className="w-full py-1.5 rounded border border-pc-idle/40 bg-pc-idle/10 text-pc-idle text-[11px] font-bold uppercase tracking-widest hover:bg-pc-idle/20 transition-colors"
          >
            START
          </button>
          
          <button
            onClick={() => onFlagMaintenance?.(pc, true)}
            title="Flag for Maintenance"
            className="w-full py-1.5 rounded border border-pc-offline/40 bg-pc-offline/10 text-pc-offline hover:bg-pc-offline/20 transition-colors flex items-center justify-center"
          >
            <Wrench className="w-3.5 h-3.5" />
          </button>
        </div>
      </motion.div>
    );
  }

  // ── OCCUPIED (Active) Card ──
  if (isActive) {
    return (
      <motion.div
        initial={{ opacity: 0, scale: 0.97 }}
        animate={{ opacity: 1, scale: 1 }}
        draggable={true}
        onDragStart={(e) => {
          e.dataTransfer.setData('text/plain', JSON.stringify({
            sessionId: pc.activeSessionId,
            sourcePcId: pc.id
          }));
          e.dataTransfer.effectAllowed = 'move';
        }}
        className="relative rounded-lg border border-pc-active/50 bg-bg-2 p-4 flex flex-col gap-2.5 shadow-[0_0_12px_rgba(34,211,166,0.08)] cursor-grab active:cursor-grabbing"
      >
        {/* Header */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="font-heading font-bold text-text text-sm tracking-wider">{pc.name}</span>
            {/* Agent Connectivity Indicator */}
            {pc.isAgentOnline ? (
              <span className={`w-2 h-2 rounded-full ${pc.connectionMode === 'Cloud' ? 'bg-neon-orange' : 'bg-pc-active'}`} title={`Agent Online (${pc.connectionMode})`} />
            ) : (
              <span className="w-2 h-2 rounded-full bg-pc-offline animate-pulse" title="Agent Offline" />
            )}
          </div>
          <span className="text-[9px] font-mono font-bold px-1.5 py-0.5 border border-pc-active/50 text-pc-active rounded">OCCUPIED</span>
        </div>

        {pc.hasOverrunWarning && (
          <div className="flex items-center gap-1.5 bg-neon-orange/15 border border-neon-orange/30 rounded p-1.5 text-[10px] text-neon-orange animate-pulse">
            <AlertTriangle className="w-3.5 h-3.5 flex-shrink-0" />
            <span className="leading-tight">{pc.overrunWarningMessage || 'Session overrun warning'}</span>
          </div>
        )}

        {/* Customer type */}
        <div className="flex items-center gap-1.5 text-text-2 text-xs">
          <User className="w-3.5 h-3.5 text-text-3" />
          <span>{pc.customerName || pc.customerType || 'Walk-in'}</span>
        </div>

        {/* Time + Charge row */}
        <div className="grid grid-cols-2 gap-2 bg-bg-3 rounded p-2.5 border border-border">
          <div>
            <div className="text-[9px] text-text-3 font-mono uppercase tracking-widest mb-0.5">
              {pc.sessionEndTime ? 'Ends At' : 'Elapsed'}
            </div>
            <div className="font-mono font-bold text-pc-active text-sm">
              {pc.sessionEndTime ? fmtTime(pc.sessionEndTime) : fmtElapsed(elapsed.h, elapsed.m)}
            </div>
          </div>
          <div>
            <div className="text-[9px] text-text-3 font-mono uppercase tracking-widest mb-0.5">Live Charge</div>
            <div className="font-mono font-bold text-neon-orange text-sm">
              ₹{liveCharge}
            </div>
          </div>
        </div>

        {/* Action Buttons Row 1: Stop + Extend */}
        <div className="grid grid-cols-3 gap-1.5">
          <ActionBtn
            color="red"
            icon={<Square className="w-3 h-3" />}
            label="Stop"
            loading={actionLoading === 'stop'}
            onClick={() => doAction('stop', { deferPayment: false })}
          />
          <ActionBtn
            color="yellow"
            icon={<Banknote className="w-3 h-3" />}
            label="Credit"
            loading={actionLoading === 'credit'}
            onClick={async () => {
              setActionLoading('credit');
              try {
                await onCreditClick?.(pc);
              } finally {
                setActionLoading(null);
              }
            }}
          />
          <ActionBtn
            color="blue"
            icon={<RefreshCw className="w-3 h-3" />}
            label="Extend"
            onClick={() => setShowExtendModal(true)}
          />
        </div>

        {/* Action Buttons Row 2: Bill + Food + Promo */}
        <div className={`grid ${isSuperAdmin ? 'grid-cols-3' : 'grid-cols-2'} gap-1.5`}>
          <ActionBtn
            color="orange"
            icon={<Receipt className="w-3 h-3" />}
            label="Bill"
            onClick={() => navigate('/app/billing', { state: { autoSelectPcId: pc.id } })}
            small
          />
          <ActionBtn
            color="green"
            icon={<Coffee className="w-3 h-3" />}
            label="Food"
            onClick={() => navigate('/app/food-orders', { state: { autoSelectPcId: pc.id } })}
            small
          />
          {user?.role === 'super_admin' || (user?.role === 'admin' && user?.dashboardPermissions?.discount === true) ? (
            <ActionBtn
              color="purple"
              icon={<Gift className="w-3 h-3" />}
              label="Discount"
              onClick={() => setShowDiscountModal(true)}
              small
            />
          ) : null}
        </div>

        {showExtendModal && (
          <ExtendSessionModal
            pc={pc}
            onClose={() => setShowExtendModal(false)}
            onActionSuccess={() => {
              setShowExtendModal(false);
              onRefresh?.();
            }}
          />
        )}
        <SessionDiscountModal
          isOpen={showDiscountModal}
          onClose={() => setShowDiscountModal(false)}
          pc={pc}
          onRefresh={onRefresh}
        />
      </motion.div>
    );
  }

  // ── AWAITING BILLING Card ──
  if (isAwaiting) {
    return (
      <motion.div
        initial={{ opacity: 0, scale: 0.97 }}
        animate={{ opacity: 1, scale: 1 }}
        className="relative rounded-lg border border-neon-orange/50 bg-bg-2 p-4 flex flex-col gap-3"
      >
        <div className="flex items-center justify-between">
          <span className="font-heading font-bold text-text text-sm tracking-wider">{pc.name}</span>
          <span className="text-[9px] font-mono font-bold px-1.5 py-0.5 border border-neon-orange/50 text-neon-orange rounded animate-pulse">BILLING</span>
        </div>
        <div className="flex items-center gap-1.5 text-neon-orange text-xs">
          <AlertTriangle className="w-3.5 h-3.5" />
          <span>{pc.customerName || 'Awaiting checkout'}</span>
        </div>
        <div className="text-[10px] text-text-3 font-mono text-center py-1">Pending at billing counter</div>
        <ActionBtn
          color="orange"
          icon={<Receipt className="w-3.5 h-3.5" />}
          label="Go to Billing"
          onClick={() => navigate('/app/billing', { state: { autoSelectPcId: pc.id } })}
        />
      </motion.div>
    );
  }

  // ── RESERVED Card ──
  if (isReserved) {
    return (
      <motion.div
        initial={{ opacity: 0, scale: 0.97 }}
        animate={{ opacity: 1, scale: 1 }}
        className="relative rounded-lg border border-pc-reserved/40 bg-bg-2 p-4 flex flex-col gap-3 shadow-[0_0_12px_rgba(234,179,8,0.08)]"
      >
        <div className="flex items-center justify-between">
          <span className="font-heading font-bold text-text text-sm tracking-wider">{pc.name}</span>
          <span className="text-[9px] font-mono font-bold px-1.5 py-0.5 border border-pc-reserved/50 text-pc-reserved rounded">RESERVED</span>
        </div>
        <div className="flex items-center gap-1.5 text-pc-reserved text-xs">
          <User className="w-3.5 h-3.5" />
          <span>{pc.customerName || 'Reserved slot'}</span>
        </div>
        {pc.nextReservationTime && (
          <div className="flex items-center gap-1 text-[10px] text-text-3 font-mono">
            <Clock className="w-3 h-3 text-pc-reserved" />
            <span>Starts: {new Date(pc.nextReservationTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: true })}</span>
          </div>
        )}
        <div className="grid grid-cols-2 gap-1.5 mt-1">
          <button
            onClick={() => onStartReservedSession?.(pc.nextReservationId)}
            className="py-1.5 rounded border border-pc-active/40 bg-pc-active/10 text-pc-active text-[10px] font-bold uppercase tracking-wider hover:bg-pc-active/20 transition-colors flex items-center justify-center gap-1"
          >
            Start Session
          </button>
          <button
            onClick={() => onOverrideReservation?.(pc.nextReservationId, pc)}
            className="py-1.5 rounded border border-neon-orange/40 bg-neon-orange/10 text-neon-orange text-[10px] font-bold uppercase tracking-wider hover:bg-neon-orange/20 transition-colors flex items-center justify-center gap-1"
          >
            Override
          </button>
        </div>
      </motion.div>
    );
  }

  // ── EXPIRED Card ──
  const isExpired = pc.state === 'Expired';
  if (isExpired) {
    return (
      <motion.div
        initial={{ opacity: 0, scale: 0.97 }}
        animate={{ opacity: 1, scale: 1 }}
        className="relative rounded-lg border border-border/30 bg-bg-2/40 p-4 flex flex-col gap-3 opacity-60"
      >
        <div className="flex items-center justify-between">
          <span className="font-heading font-bold text-text-3 text-sm tracking-wider">{pc.name}</span>
          <span className="text-[9px] font-mono font-bold px-1.5 py-0.5 border border-border text-text-3 rounded">EXPIRED</span>
        </div>
        <div className="flex items-center gap-1.5 text-text-3 text-xs">
          <Clock className="w-3.5 h-3.5" />
          <span>Expired Reservation</span>
        </div>
      </motion.div>
    );
  }

  // ── MAINTENANCE Card ──
  if (isMaintenance) {
    return (
      <motion.div
        initial={{ opacity: 0, scale: 0.97 }}
        animate={{ opacity: 1, scale: 1 }}
        className="relative rounded-lg border border-pc-offline/30 bg-bg-2/60 p-4 flex flex-col gap-3 opacity-75"
      >
        <div className="flex items-center justify-between">
          <span className="font-heading font-bold text-text-2 text-sm tracking-wider">{pc.name}</span>
          <span className="text-[9px] font-mono font-bold px-1.5 py-0.5 border border-pc-offline/30 text-pc-offline rounded">MAINT</span>
        </div>
        <div className="flex items-center gap-1.5 text-pc-offline text-xs">
          <Wrench className="w-3.5 h-3.5" />
          <span>Under maintenance</span>
        </div>
        <button
          onClick={() => onFlagMaintenance?.(pc, false)}
          className="mt-1 w-full py-1.5 rounded border border-pc-active/40 bg-pc-active/10 text-pc-active text-[11px] font-bold uppercase tracking-widest hover:bg-pc-active/20 transition-colors flex items-center justify-center gap-1"
        >
          <RefreshCw className="w-3 h-3" /> RESTORE PC
        </button>
      </motion.div>
    );
  }

  // ── OFFLINE / fallback ──
  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.97 }}
      animate={{ opacity: 1, scale: 1 }}
      className="relative rounded-lg border border-border/30 bg-bg-2/40 p-4 flex flex-col gap-3 opacity-40"
    >
      <div className="flex items-center justify-between">
        <span className="font-heading font-bold text-text-3 text-sm tracking-wider">{pc.name}</span>
        <span className="text-[9px] font-mono font-bold px-1.5 py-0.5 border border-border text-text-3 rounded">OFFLINE</span>
      </div>
    </motion.div>
  );
});

// ── Small reusable action button ──
function ActionBtn({ color, icon, label, onClick, loading, small = false }) {
  const colorMap = {
    red:    'border-neon-red/40    bg-neon-red/10    text-neon-red    hover:bg-neon-red/20',
    blue:   'border-neon-blue/40   bg-neon-blue/10   text-neon-blue   hover:bg-neon-blue/20',
    orange: 'border-neon-orange/40 bg-neon-orange/10 text-neon-orange hover:bg-neon-orange/20',
    yellow: 'border-pc-reserved/40 bg-pc-reserved/10 text-pc-reserved hover:bg-pc-reserved/20',
    green:  'border-pc-active/40   bg-pc-active/10   text-pc-active   hover:bg-pc-active/20',
    purple: 'border-neon-purple/40 bg-neon-purple/10 text-neon-purple hover:bg-neon-purple/20',
  };

  return (
    <button
      onClick={onClick}
      disabled={!!loading}
      className={`flex items-center justify-center gap-1 rounded border transition-colors
        ${small ? 'py-1 text-[10px]' : 'py-1.5 text-[11px]'}
        font-bold uppercase tracking-wider
        ${colorMap[color]}
        ${loading ? 'opacity-50 cursor-not-allowed' : ''}
      `}
    >
      {loading ? <span className="w-3 h-3 border border-current border-t-transparent rounded-full animate-spin" /> : icon}
      {label}
    </button>
  );
}

PcCard.displayName = 'PcCard';
export default PcCard;
