import { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../../contexts/AuthContext';
import { useBranch } from '../../contexts/BranchContext';
import { useSocket } from '../../contexts/SocketContext';
import PageHeader from '../../components/layout/PageHeader';
import { EmptyState } from '../../components/ui/LoadingStates';
import { useToast } from '../../components/ui/Toast';
import api from '../../config/api';
import { getMembers } from '../../api/members.api';
import {
  getActiveReservations,
  createReservation,
  cancelReservation,
  startReservedSession,
  overrideReservation
} from '../../api/reservations.api';
import { Calendar, User, Clock, IndianRupee, FileText, Ban, Play, ShieldAlert, CheckCircle, UserCheck, Search } from 'lucide-react';

export default function ReservationsPage() {
  const { isSuperAdmin, user } = useAuth();
  const { activeBranch } = useBranch();
  const { subscribe, connected, SIGNALR_HUBS } = useSocket();
  const toast = useToast();

  const targetBranchId = isSuperAdmin ? activeBranch?.id : user?.branchId;

  // List states
  const [reservations, setReservations] = useState([]);
  const [sessions, setSessions] = useState([]);
  const [pcs, setPcs] = useState([]);
  const [loadingList, setLoadingList] = useState(true);

  // Member search states
  const [isMemberBooking, setIsMemberBooking] = useState(false);
  const [memberSearch, setMemberSearch] = useState('');
  const [memberResults, setMemberResults] = useState([]);
  const [selectedMember, setSelectedMember] = useState(null);
  const [memberSearchLoading, setMemberSearchLoading] = useState(false);
  const [showMemberDropdown, setShowMemberDropdown] = useState(false);

  // Form states
  const [form, setForm] = useState({
    customerName: '',
    pcId: '',
    date: new Date().toISOString().split('T')[0],
    time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false }),
    durationMin: 60,
    advanceDeposit: 0,
    gracePeriodMin: 15,
    notes: '',
    selectedTier: ''
  });
  const [submittingForm, setSubmittingForm] = useState(false);
  const [branchPlans, setBranchPlans] = useState([]);

  useEffect(() => {
    if (targetBranchId) {
      api.get(`/public/branches/${targetBranchId}/plans`).then(res => {
        if (res.data?.success !== false) {
          setBranchPlans(res.data?.data || []);
        }
      }).catch(err => console.error('Failed to load branch plans', err));
    }
  }, [targetBranchId]);

  // Modal/Reason states
  const [cancelData, setCancelData] = useState(null); // { id, customerName }
  const [cancelReason, setCancelReason] = useState('');
  const [cancelLoading, setCancelLoading] = useState(false);

  const [overrideData, setOverrideData] = useState(null); // { id, pcName }
  const [overrideReason, setOverrideReason] = useState('');
  const [overrideLoading, setOverrideLoading] = useState(false);

  // ── Member search with debounce ──
  useEffect(() => {
    if (!isMemberBooking || memberSearch.length < 2 || !targetBranchId) {
      setMemberResults([]);
      return;
    }
    const timer = setTimeout(async () => {
      setMemberSearchLoading(true);
      try {
        const res = await getMembers(targetBranchId, memberSearch, 1, 10);
        setMemberResults(res?.items || []);
        setShowMemberDropdown(true);
      } catch {
        setMemberResults([]);
      } finally {
        setMemberSearchLoading(false);
      }
    }, 300);
    return () => clearTimeout(timer);
  }, [memberSearch, isMemberBooking, targetBranchId]);

  const handleMemberSelect = (member) => {
    setSelectedMember(member);
    setForm(f => ({ ...f, customerName: member.fullName }));
    setMemberSearch(member.fullName);
    setShowMemberDropdown(false);
  };

  const handleToggleMemberBooking = (val) => {
    setIsMemberBooking(val);
    if (!val) {
      setSelectedMember(null);
      setMemberSearch('');
      setMemberResults([]);
      setForm(f => ({ ...f, customerName: '' }));
    }
  };

  // ── Fetch Reservations & PCs ──
  const fetchReservationsList = useCallback(async () => {
    if (!targetBranchId) return;
    setLoadingList(true);
    try {
      const list = await getActiveReservations(1, 100);
      setReservations(list);
    } catch (err) {
      console.error('Failed to fetch reservations', err);
      toast.error('Failed to load reservations');
    } finally {
      setLoadingList(false);
    }
  }, [targetBranchId, toast]);

  const fetchPcsAndSessions = useCallback(async () => {
    if (!targetBranchId) return;
    try {
      const [pcsRes, sessionsRes] = await Promise.all([
        api.get('/pcs', { params: { branchId: targetBranchId } }),
        api.get('/sessions', { params: { branchId: targetBranchId, page: 1, pageSize: 100 } })
      ]);
      const sortedPcs = (pcsRes.data?.data || []).sort((a, b) =>
        a.name.localeCompare(b.name, undefined, { numeric: true })
      );
      setPcs(sortedPcs);
      setSessions(sessionsRes.data?.data?.items || []);
    } catch (err) {
      console.error('Failed to load PCs and sessions', err);
    }
  }, [targetBranchId]);

  useEffect(() => {
    fetchReservationsList();
    fetchPcsAndSessions();
  }, [fetchReservationsList, fetchPcsAndSessions]);

  // ── SignalR updates ──
  useEffect(() => {
    if (!connected || !targetBranchId) return;
    
    // Listen to reservation updates
    const unsubRes = subscribe(SIGNALR_HUBS.RESERVATIONS, 'ReservationUpdated', (payload) => {
      const data = payload?.data || payload?.Data;
      fetchReservationsList();
    });

    // Listen to PC changes (which affect eligibility/availability)
    const unsubPc = subscribe(SIGNALR_HUBS.PC_STATUS, 'PcStatusChanged', (payload) => {
      const data = payload?.data || payload?.Data;
      fetchReservationsList();
      fetchPcsAndSessions();
    });

    const unsubSessions = subscribe(SIGNALR_HUBS.SESSIONS, 'SessionUpdated', (payload) => {
      const data = payload?.data || payload?.Data;
      fetchPcsAndSessions();
    });

    return () => {
      unsubRes();
      unsubPc();
      unsubSessions();
    };
  }, [connected, subscribe, SIGNALR_HUBS, targetBranchId, fetchReservationsList, fetchPcsAndSessions]);

  // Filter dropdown PCs dynamically based on overlaps
  const requestedStart = new Date(`${form.date}T${form.time}`);
  const requestedDurationMs = form.durationMin * 60000;
  const requestedEnd = new Date(requestedStart.getTime() + requestedDurationMs);

  const eligiblePcs = pcs.filter(pc => {
    if (pc.state === 'Maintenance' || pc.state === 'Offline') return false;

    // Filter by tier if a plan with a specific tier is selected
    if (form.selectedTier !== undefined && form.selectedTier !== '') {
      const pcTier = pc.monitorHz || '';
      if (pcTier !== form.selectedTier) return false;
    }

    // 1. Check against active sessions
    const activeSession = sessions.find(s => s.pcId === pc.id && s.status === 'Active');
    if (activeSession) {
      // If session exists, we assume its endTime must be before our requested start time.
      const sessionEnd = new Date(activeSession.endTime);
      if (sessionEnd > requestedStart) return false; // Overlaps!
    }

    // 2. Check against pending reservations
    const pendingRes = reservations.find(r => r.pcId === pc.id && r.state === 'Pending');
    if (pendingRes) {
      const resStart = new Date(pendingRes.reservationTime);
      const resEnd = new Date(resStart.getTime() + (pendingRes.durationMin || 60) * 60000);
      
      // If strictly overlapping
      if (requestedStart < resEnd && requestedEnd > resStart) return false;
    }

    return true;
  });

  // ── Form Submission ──
  const handleFormSubmit = async (e) => {
    e.preventDefault();
    if (!form.customerName.trim()) {
      toast.error('Customer Name is required');
      return;
    }
    if (isMemberBooking && !selectedMember) {
      toast.error('Please select a member from the search results');
      return;
    }
    if (!form.pcId) {
      toast.error('Please select a PC');
      return;
    }

    setSubmittingForm(true);
    try {
      // Build ISO offset timestamp from date and time strings
      const localDateTimeString = `${form.date}T${form.time}`;
      const reservationTime = new Date(localDateTimeString).toISOString();

      await createReservation({
        pcId: form.pcId,
        customerName: form.customerName.trim(),
        memberId: selectedMember?.id || null,
        reservationTime: reservationTime,
        durationMin: Number(form.durationMin),
        advanceDeposit: Number(form.advanceDeposit),
        gracePeriodMin: Number(form.gracePeriodMin),
        notes: form.notes.trim()
      });

      toast.success('Reservation created successfully!');
      // Reset form (keep date/time default)
      setForm(prev => ({
        ...prev,
        customerName: '',
        pcId: '',
        notes: '',
        advanceDeposit: 0
      }));
      setSelectedMember(null);
      setMemberSearch('');
      setIsMemberBooking(false);
      fetchReservationsList();
      fetchPcsAndSessions();
    } catch (err) {
      toast.error(err.response?.data?.error || err.response?.data?.message || 'Failed to create reservation');
    } finally {
      setSubmittingForm(false);
    }
  };

  // ── Actions: Start Session ──
  const handleStartSession = async (id) => {
    try {
      await startReservedSession(id);
      toast.success('Reserved session started successfully!');
      fetchReservationsList();
      fetchPcsAndSessions();
    } catch (err) {
      toast.error(err.response?.data?.error || err.response?.data?.message || 'Failed to start session');
    }
  };

  // ── Actions: Cancel ──
  const handleCancelClick = (res) => {
    setCancelData(res);
    setCancelReason('');
  };

  const handleCancelSubmit = async (e) => {
    e.preventDefault();
    if (!cancelReason.trim()) {
      toast.error('Cancellation reason is required');
      return;
    }
    setCancelLoading(true);
    try {
      await cancelReservation(cancelData.id, { reason: cancelReason.trim() });
      toast.success('Reservation cancelled successfully');
      setCancelData(null);
      fetchReservationsList();
      fetchPcsAndSessions();
    } catch (err) {
      toast.error(err.response?.data?.error || err.response?.data?.message || 'Failed to cancel reservation');
    } finally {
      setCancelLoading(false);
    }
  };

  // ── Actions: Override ──
  const handleOverrideClick = (res) => {
    const matchedPc = pcs.find(p => p.id === res.pcId);
    setOverrideData({ id: res.id, pcName: matchedPc?.name || 'PC' });
    setOverrideReason('');
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
      fetchReservationsList();
      fetchPcsAndSessions();
    } catch (err) {
      toast.error(err.response?.data?.error || err.response?.data?.message || 'Failed to override reservation');
    } finally {
      setOverrideLoading(false);
    }
  };

  return (
    <div className="flex flex-col gap-4">
      <PageHeader
        title="PC Reservations"
        subtitle="Manage future PC bookings with automated grace period and real-time state broadcasts"
        icon="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"
      />

      <div className="flex h-[calc(100vh-11rem)] overflow-hidden gap-4 p-1">
        {/* Left Side: Creation Form */}
        <div className="flex flex-col w-[35%] min-w-[340px] bg-bg-2 border border-border rounded-xl shadow-lg overflow-hidden">
          <div className="px-4 py-3 border-b border-border bg-bg-3/50 flex items-center gap-2">
            <Calendar className="w-4 h-4 text-neon-purple" />
            <h3 className="font-heading font-bold text-text uppercase tracking-wider text-xs">New Reservation</h3>
          </div>

          <form onSubmit={handleFormSubmit} className="flex-1 overflow-y-auto p-4 space-y-3.5">
            {/* Member Booking Toggle */}
            <div className="flex items-center justify-between bg-bg-3/50 border border-border rounded-lg px-3 py-2">
              <div className="flex items-center gap-2">
                <UserCheck className={`w-4 h-4 ${isMemberBooking ? 'text-neon-purple' : 'text-text-3'}`} />
                <span className="text-[10px] font-mono font-semibold text-text-2 uppercase tracking-wider">
                  Member Booking
                </span>
              </div>
              <button
                type="button"
                onClick={() => handleToggleMemberBooking(!isMemberBooking)}
                className={`relative w-9 h-5 rounded-full transition-colors ${isMemberBooking ? 'bg-neon-purple' : 'bg-bg-3 border border-border'}`}
              >
                <span className={`absolute top-0.5 w-4 h-4 rounded-full transition-all ${isMemberBooking ? 'left-[18px] bg-white' : 'left-0.5 bg-text-3'}`} />
              </button>
            </div>

            {/* Customer / Member Name */}
            {isMemberBooking ? (
              <div className="space-y-1 relative">
                <label className="text-[10px] font-mono font-semibold text-text-2 uppercase tracking-wider flex items-center gap-1">
                  <Search className="w-3 h-3 text-neon-purple" /> Search Member *
                </label>
                <input
                  type="text"
                  placeholder="Search by name or phone..."
                  value={memberSearch}
                  onChange={e => {
                    setMemberSearch(e.target.value);
                    setSelectedMember(null);
                    setForm(f => ({ ...f, customerName: '' }));
                  }}
                  onFocus={() => memberResults.length > 0 && setShowMemberDropdown(true)}
                  className="w-full bg-bg-3 border border-neon-purple/40 rounded px-3 py-2 text-xs text-text placeholder-text-3 focus:border-neon-purple focus:outline-none transition-colors"
                />
                {memberSearchLoading && (
                  <div className="absolute right-3 top-7">
                    <div className="w-3.5 h-3.5 border border-neon-purple border-t-transparent rounded-full animate-spin" />
                  </div>
                )}
                {/* Dropdown results */}
                {showMemberDropdown && memberResults.length > 0 && (
                  <div className="absolute z-20 w-full mt-1 bg-bg-2 border border-border rounded-lg shadow-xl max-h-40 overflow-y-auto">
                    {memberResults.map(m => (
                      <button
                        key={m.id}
                        type="button"
                        onClick={() => handleMemberSelect(m)}
                        className="w-full px-3 py-2 text-left hover:bg-neon-purple/10 transition-colors flex items-center justify-between border-b border-border/40 last:border-0"
                      >
                        <div>
                          <span className="text-xs font-semibold text-text">{m.fullName}</span>
                          <span className="text-[10px] text-text-3 font-mono ml-2">{m.phone}</span>
                        </div>
                        <span className="text-[9px] font-mono text-neon-purple/70">₹{m.gamingBalance?.toFixed(0) || 0}</span>
                      </button>
                    ))}
                  </div>
                )}
                {showMemberDropdown && memberSearch.length >= 2 && memberResults.length === 0 && !memberSearchLoading && (
                  <div className="absolute z-20 w-full mt-1 bg-bg-2 border border-border rounded-lg shadow-xl p-3 text-center text-[10px] text-text-3 font-mono">
                    No members found
                  </div>
                )}
                {/* Selected member indicator */}
                {selectedMember && (
                  <div className="flex items-center gap-2 mt-1.5 bg-neon-purple/10 border border-neon-purple/30 rounded px-2.5 py-1.5">
                    <CheckCircle className="w-3.5 h-3.5 text-neon-purple" />
                    <span className="text-[10px] font-semibold text-neon-purple">{selectedMember.fullName}</span>
                    <span className="text-[9px] text-text-3 font-mono">• Wallet: ₹{selectedMember.gamingBalance?.toFixed(0) || 0}</span>
                  </div>
                )}
              </div>
            ) : (
              <div className="space-y-1">
                <label className="text-[10px] font-mono font-semibold text-text-2 uppercase tracking-wider flex items-center gap-1">
                  <User className="w-3 h-3 text-text-3" /> Customer Name *
                </label>
                <input
                  type="text"
                  placeholder="Enter walk-in client name..."
                  value={form.customerName}
                  onChange={e => setForm(f => ({ ...f, customerName: e.target.value }))}
                  className="w-full bg-bg-3 border border-border rounded px-3 py-2 text-xs text-text placeholder-text-3 focus:border-neon-purple focus:outline-none transition-colors"
                  required
                />
              </div>
            )}

            {/* Date and Time row */}
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <label className="text-[10px] font-mono font-semibold text-text-2 uppercase tracking-wider block">
                  Date *
                </label>
                <input
                  type="date"
                  value={form.date}
                  onChange={e => setForm(f => ({ ...f, date: e.target.value }))}
                  className="w-full bg-bg-3 border border-border rounded px-3 py-2 text-xs text-text focus:border-neon-purple focus:outline-none"
                  required
                />
              </div>
              <div className="space-y-1">
                <label className="text-[10px] font-mono font-semibold text-text-2 uppercase tracking-wider block">
                  Start Time *
                </label>
                <input
                  type="time"
                  value={form.time}
                  onChange={e => setForm(f => ({ ...f, time: e.target.value }))}
                  className="w-full bg-bg-3 border border-border rounded px-3 py-2 text-xs text-text focus:border-neon-purple focus:outline-none"
                  required
                />
              </div>
            </div>

            {/* Duration and Advance Deposit */}
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <label className="text-[10px] font-mono font-semibold text-text-2 uppercase tracking-wider flex items-center gap-1">
                  <Clock className="w-3 h-3 text-text-3" /> Select Plan
                </label>
                <select
                  value={form.durationMin + '|' + form.selectedTier}
                  onChange={e => {
                    const [dur, tier] = e.target.value.split('|');
                    const plan = branchPlans.find(p => p.duration === Number(dur) && p.tier === tier);
                    setForm(f => ({ 
                      ...f, 
                      durationMin: Number(dur), 
                      selectedTier: tier,
                      advanceDeposit: plan ? plan.price : f.advanceDeposit
                    }));
                  }}
                  className="w-full bg-bg-3 border border-border rounded px-3 py-2 text-xs text-text focus:border-neon-purple focus:outline-none"
                >
                  {branchPlans.length > 0 ? (
                    Array.from(new Set(branchPlans.map(p => p.tierLabel))).map(tierLabel => (
                      <optgroup key={tierLabel} label={tierLabel}>
                        {branchPlans.filter(p => p.tierLabel === tierLabel).map(plan => (
                          <option key={plan.id} value={`${plan.duration}|${plan.tier}`}>
                            {plan.name} - ₹{plan.price}
                          </option>
                        ))}
                      </optgroup>
                    ))
                  ) : (
                    // Fallback to configured durations if plans haven't loaded
                    configuredDurations.map(d => (
                      <option key={d} value={`${d}|`}>
                        {d < 60 ? `${d} Mins` : d % 60 === 0 ? `${d / 60} Hour${d / 60 > 1 ? 's' : ''}` : `${Math.floor(d / 60)}h ${d % 60}m`}
                      </option>
                    ))
                  )}
                </select>
              </div>
              <div className="space-y-1">
                <label className="text-[10px] font-mono font-semibold text-text-2 uppercase tracking-wider flex items-center gap-1">
                  <IndianRupee className="w-3 h-3 text-text-3" /> Deposit (₹)
                </label>
                <input
                  type="number"
                  placeholder="0.00"
                  min="0"
                  value={form.advanceDeposit || ''}
                  onChange={e => setForm(f => ({ ...f, advanceDeposit: parseFloat(e.target.value) || 0 }))}
                  className="w-full bg-bg-3 border border-border rounded px-3 py-2 text-xs text-text focus:border-neon-purple focus:outline-none"
                />
              </div>
            </div>

            {/* Grace Period */}
            <div className="space-y-1">
              <label className="text-[10px] font-mono font-semibold text-text-2 uppercase tracking-wider block">
                Grace Period (Minutes)
              </label>
              <input
                type="number"
                min="5"
                max="60"
                value={form.gracePeriodMin}
                onChange={e => setForm(f => ({ ...f, gracePeriodMin: parseInt(e.target.value) || 15 }))}
                className="w-full bg-bg-3 border border-border rounded px-3 py-2 text-xs text-text focus:border-neon-purple focus:outline-none"
              />
            </div>

            {/* Notes */}
            <div className="space-y-1">
              <label className="text-[10px] font-mono font-semibold text-text-2 uppercase tracking-wider flex items-center gap-1">
                <FileText className="w-3 h-3 text-text-3" /> Booking Notes
              </label>
              <textarea
                placeholder="Special hardware or VIP requests..."
                rows={2}
                value={form.notes}
                onChange={e => setForm(f => ({ ...f, notes: e.target.value }))}
                className="w-full bg-bg-3 border border-border rounded px-3 py-2 text-xs text-text focus:border-neon-purple focus:outline-none resize-none"
              />
            </div>

            {/* PC Selector Moved to Bottom */}
            <div className="space-y-1 pt-2 border-t border-border/50">
              <label className="text-[10px] font-mono font-semibold text-text-2 uppercase tracking-wider block">
                PC Number *
              </label>
              <select
                value={form.pcId}
                onChange={e => setForm(f => ({ ...f, pcId: e.target.value }))}
                className="w-full bg-bg-3 border border-border rounded px-3 py-2 text-xs text-text focus:border-neon-purple focus:outline-none transition-colors"
                required
              >
                <option value="">-- Select Available Station --</option>
                {eligiblePcs.map(p => (
                  <option key={p.id} value={p.id}>
                    {p.name} {p.zone ? `- ${p.zone}` : ''}
                  </option>
                ))}
              </select>
              <p className="text-[9px] text-text-3 font-mono mt-1">
                Showing {eligiblePcs.length} vacant PCs for the selected time block.
              </p>
            </div>

            <button
              type="submit"
              disabled={submittingForm}
              className="w-full py-2.5 rounded border border-neon-purple/50 bg-neon-purple/10 text-neon-purple font-heading font-bold uppercase tracking-widest text-xs hover:bg-neon-purple/20 transition-colors flex items-center justify-center gap-2"
            >
              {submittingForm ? (
                <div className="w-4 h-4 border border-current border-t-transparent rounded-full animate-spin" />
              ) : (
                'Create Reservation'
              )}
            </button>
          </form>
        </div>

        {/* Right Side: List of Reservations */}
        <div className="flex-1 min-w-0 bg-bg-2 border border-border rounded-xl shadow-lg overflow-hidden flex flex-col">
          <div className="px-4 py-3 border-b border-border bg-bg-3/50 flex items-center justify-between">
            <span className="font-heading font-bold text-text uppercase tracking-wider text-xs">Active Reservations List</span>
            <span className="text-[9px] font-mono text-text-3 font-semibold">
              {reservations.length} Active Slots
            </span>
          </div>

          <div className="flex-1 overflow-y-auto p-4 space-y-2">
            {loadingList && reservations.length === 0 ? (
              <div className="flex items-center justify-center h-48">
                <div className="w-6 h-6 border-2 border-neon-purple border-t-transparent rounded-full animate-spin" />
              </div>
            ) : reservations.length === 0 ? (
              <EmptyState
                icon="📅"
                title="No Reservations Found"
                message="There are no active or pending reservations logged for this branch."
              />
            ) : (
              <div className="divide-y divide-border/60">
                {reservations.map(res => {
                  const matchedPc = pcs.find(p => p.id === res.pcId);
                  const isPendingState = res.state === 'Pending';
                  
                  return (
                    <div key={res.id} className="py-3.5 flex items-center justify-between gap-3 first:pt-0 last:pb-0">
                      <div className="min-w-0 flex-1 space-y-1">
                        <div className="flex items-center gap-2.5 flex-wrap">
                          <span className="font-bold text-text-2 text-sm">{res.customerName}</span>
                          <span className="px-1.5 py-0.5 rounded bg-bg-3 border border-border text-[9px] font-mono font-bold text-neon-blue">
                            {matchedPc?.name || 'PC Station'}
                          </span>
                          
                          {res.advanceDeposit > 0 && (
                            <span className="px-1.5 py-0.5 rounded border border-accent/30 bg-accent/10 text-accent font-bold font-mono text-[9px] flex items-center gap-0.5">
                              Deposit: ₹{res.advanceDeposit}
                            </span>
                          )}

                          <StatusBadge state={res.state} />
                        </div>

                        <div className="flex items-center gap-4 text-[10px] text-text-3 font-mono">
                          <span>Date: {new Date(res.reservationTime).toLocaleDateString()}</span>
                          <span>Time: {new Date(res.reservationTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: true })}</span>
                          <span>Duration: {res.durationMin} Min</span>
                        </div>

                        {res.notes && (
                          <p className="text-[10px] text-text-3 italic font-body bg-bg-3/50 p-1.5 border border-border/40 rounded mt-1">
                            Notes: {res.notes}
                          </p>
                        )}
                      </div>

                      {/* Action buttons (only if Pending) */}
                      {isPendingState && (
                        <div className="flex gap-2">
                          <button
                            onClick={() => handleStartSession(res.id)}
                            title="Start Reserved Session"
                            className="p-2 border border-pc-active/40 bg-pc-active/10 text-pc-active rounded hover:bg-pc-active/20 transition-colors flex items-center gap-1.5 text-xs font-semibold"
                          >
                            <Play className="w-3.5 h-3.5" /> Start
                          </button>
                          <button
                            onClick={() => handleOverrideClick(res)}
                            title="Admin Override"
                            className="p-2 border border-neon-orange/40 bg-neon-orange/10 text-neon-orange rounded hover:bg-neon-orange/20 transition-colors flex items-center gap-1.5 text-xs font-semibold"
                          >
                            <ShieldAlert className="w-3.5 h-3.5" /> Override
                          </button>
                          <button
                            onClick={() => handleCancelClick(res)}
                            title="Cancel Reservation"
                            className="p-2 border border-neon-red/40 bg-neon-red/10 text-neon-red rounded hover:bg-neon-red/20 transition-colors flex items-center gap-1.5 text-xs font-semibold"
                          >
                            <Ban className="w-3.5 h-3.5" /> Cancel
                          </button>
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* ── Cancel Reservation Modal ── */}
      {cancelData && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm animate-[fadeIn_0.15s_ease-out]">
          <div className="w-full max-w-sm bg-bg-2 border border-border rounded-xl shadow-2xl overflow-hidden">
            <div className="px-5 py-4 border-b border-border bg-bg-3 flex items-center justify-between">
              <div>
                <h2 className="font-heading font-bold text-text uppercase tracking-wider text-sm flex items-center gap-2">
                  <Ban className="w-4 h-4 text-neon-red" />
                  Cancel Booking — {cancelData.customerName}
                </h2>
                <p className="text-text-3 text-[10px] font-mono mt-0.5">
                  Please provide a reason to cancel this reservation slot.
                </p>
              </div>
              <button onClick={() => setCancelData(null)} className="text-text-3 hover:text-text text-xl">&times;</button>
            </div>
            <form onSubmit={handleCancelSubmit} className="p-5 space-y-4">
              <div className="space-y-1.5">
                <label className="text-[10px] font-mono font-semibold text-text-2 uppercase tracking-wider block">
                  Cancellation Reason *
                </label>
                <textarea
                  value={cancelReason}
                  onChange={(e) => setCancelReason(e.target.value)}
                  placeholder="Provide reason for deletion..."
                  rows={3}
                  className="w-full bg-bg-3 border border-border rounded px-3 py-2 text-xs text-text placeholder-text-3 focus:border-neon-red focus:outline-none transition-colors resize-none"
                  required
                  autoFocus
                />
              </div>
              <div className="flex justify-end gap-2.5">
                <button
                  type="button"
                  onClick={() => setCancelData(null)}
                  className="px-4 py-2 border border-border bg-transparent text-text-2 rounded text-xs font-semibold hover:bg-bg-3 transition-colors"
                >
                  Close
                </button>
                <button
                  type="submit"
                  disabled={cancelLoading || !cancelReason.trim()}
                  className="px-4 py-2 bg-neon-red/10 border border-neon-red/50 text-neon-red rounded text-xs font-semibold hover:bg-neon-red/20 transition-colors flex items-center justify-center gap-1.5 disabled:opacity-50"
                >
                  {cancelLoading ? (
                    <span className="w-3.5 h-3.5 border border-current border-t-transparent rounded-full animate-spin" />
                  ) : (
                    'Cancel Booking'
                  )}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

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
                  An audit log entry will document this override action.
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

// ── Reusable Status Badge ──
function StatusBadge({ state }) {
  const configs = {
    Pending: 'border-pc-reserved/50 bg-pc-reserved/10 text-pc-reserved',
    Active: 'border-pc-active/50 bg-pc-active/10 text-pc-active',
    Expired: 'border-border bg-bg-3 text-text-3',
    Cancelled: 'border-neon-red/50 bg-neon-red/10 text-neon-red',
    Overridden: 'border-neon-orange/50 bg-neon-orange/10 text-neon-orange'
  };
  const cls = configs[state] || 'border-border bg-bg-3 text-text-3';
  return (
    <span className={`px-1.5 py-0.5 rounded border text-[9px] font-mono font-bold uppercase tracking-wider ${cls}`}>
      {state}
    </span>
  );
}
