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
  deleteReservation,
  setReservationArrived
} from '../../api/reservations.api';
import { Calendar, User, Clock, IndianRupee, FileText, Trash2, CheckCircle, UserCheck, Search, Download, History, X } from 'lucide-react';
import { format } from 'date-fns';
import { createReport, addTable, save } from '../../utils/pdfReport';

const todayIso = () => format(new Date(), 'yyyy-MM-dd');

// Same per-browser remembered range as the other desks.
const readStoredDate = (key) => {
  try {
    return localStorage.getItem(key) || todayIso();
  } catch {
    return todayIso();
  }
};

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
  const getDefaultDateTime = () => {
    const now = new Date();
    // Use local date, not UTC (toISOString converts to UTC which gives wrong date in IST)
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');
    const date = `${year}-${month}-${day}`;
    const hours = String(now.getHours()).padStart(2, '0');
    const minutes = String(now.getMinutes()).padStart(2, '0');
    const time = `${hours}:${minutes}`;
    return { date, time };
  };

  const [form, setForm] = useState(() => {
    const { date, time } = getDefaultDateTime();
    return {
      customerName: '',
      pcId: '',
      date,
      time,
      durationMin: null,
      advanceDeposit: 0,
      depositMethod: 'cash',
      gracePeriodMin: 15,
      notes: '',
      selectedTier: ''
    };
  });
  const [submittingForm, setSubmittingForm] = useState(false);

  // Per-PC plans (fetched when PC is selected)
  const [pcPlans, setPcPlans] = useState([]);
  const [pcPlansLoading, setPcPlansLoading] = useState(false);

  useEffect(() => {
    if (!form.pcId) {
      setPcPlans([]);
      return;
    }
    setPcPlansLoading(true);
    api.get(`/public/pcs/${form.pcId}/plans`)
      .then(res => {
        if (res.data?.success !== false) setPcPlans(res.data?.data || []);
        else setPcPlans([]);
      })
      .catch(() => setPcPlans([]))
      .finally(() => setPcPlansLoading(false));
    // Reset plan selection when PC changes
    setForm(f => ({ ...f, durationMin: null, advanceDeposit: 0, selectedTier: '' }));
  }, [form.pcId]);

  const [removingId, setRemovingId] = useState(null);

  // Read-only history, separate from the live to-do list above - GetActiveReservations only
  // ever shows Pending, so a past-day lookup needs its own endpoint.
  const [isHistoryOpen, setIsHistoryOpen] = useState(false);
  const [historyFrom, setHistoryFrom] = useState(() => readStoredDate('reservations.historyFrom'));
  const [historyTo, setHistoryTo] = useState(() => readStoredDate('reservations.historyTo'));
  const [history, setHistory] = useState([]);
  const [historyLoading, setHistoryLoading] = useState(false);

  useEffect(() => {
    try { localStorage.setItem('reservations.historyFrom', historyFrom); } catch { /* ignore */ }
  }, [historyFrom]);

  useEffect(() => {
    try { localStorage.setItem('reservations.historyTo', historyTo); } catch { /* ignore */ }
  }, [historyTo]);

  const fetchHistory = useCallback(async () => {
    if (!targetBranchId) { setHistory([]); return; }
    setHistoryLoading(true);
    try {
      const { data } = await api.get('/reservations/history', {
        params: { branchId: targetBranchId, fromDate: historyFrom, toDate: historyTo },
      });
      setHistory(data?.data || []);
    } catch (err) {
      console.error('Failed to load reservation history:', err);
    } finally {
      setHistoryLoading(false);
    }
  }, [targetBranchId, historyFrom, historyTo]);

  useEffect(() => { if (isHistoryOpen) fetchHistory(); }, [isHistoryOpen, fetchHistory]);

  const resetHistoryToToday = () => {
    setHistoryFrom(todayIso());
    setHistoryTo(todayIso());
  };

  const handleDownloadHistoryPdf = () => {
    if (history.length === 0) return;
    const rangeLabel = historyFrom === historyTo ? historyFrom : `${historyFrom} to ${historyTo}`;
    const subtitle = `${activeBranch?.name || 'Branch'}  •  ${rangeLabel}`;
    const { doc } = createReport({ title: 'PC Reservations History', subtitle });

    addTable(doc, 90, {
      title: 'PC Reservations History', subtitle,
      head: ['Time', 'PC', 'Customer', 'Duration', 'Deposit', 'Status'],
      body: history.map(r => [
        r.reservationTime ? format(new Date(r.reservationTime), 'MMM d, hh:mm a') : '-',
        r.pcName || '-',
        r.customerName || '-',
        r.durationMin ? `${r.durationMin}m` : '-',
        `Rs ${(r.advanceDeposit || 0).toFixed(2)}`,
        r.arrived ? 'Arrived' : r.state,
      ]),
    });

    save(doc, `reservations-history-${historyFrom}${historyFrom !== historyTo ? `_to_${historyTo}` : ''}.pdf`);
  };

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

  // ── Reset form date/time to current on mount ──
  useEffect(() => {
    const { date, time } = getDefaultDateTime();
    setForm(prev => ({ ...prev, date, time }));
  }, []);

  // ── Fetch Reservations & PCs ──
  const fetchReservationsList = useCallback(async () => {
    // Clear the spinner before bailing out. This used to `return` while loadingList was
    // still true from its initial useState(true), so a Super Admin on "All Branches" -
    // which BranchContext defaults to on every login with no saved branch - got a spinner
    // that never stopped, with no request made and no error shown. It read as "reservations
    // are broken at Head Office"; the page had simply never asked for anything.
    if (!targetBranchId) {
      setReservations([]);
      setLoadingList(false);
      return;
    }
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

  // Eligible PCs for the selected time slot (no tier filter — PC is chosen first)
  const requestedStart = new Date(`${form.date}T${form.time}`);
  const requestedDurationMs = (form.durationMin || 60) * 60000;
  const requestedEnd = new Date(requestedStart.getTime() + requestedDurationMs);

  const eligiblePcs = pcs.filter(pc => {
    if (pc.state === 'UnderMaintenance' || pc.state === 'Offline') return false;

    // 1. Check against active sessions
    const activeSession = sessions.find(s => s.pcId === pc.id && s.status === 'Active');
    if (activeSession) {
      const sessionEnd = new Date(activeSession.endTime);
      if (sessionEnd > requestedStart) return false;
    }

    // 2. Check against pending reservations
    const pendingRes = reservations.find(r => r.pcId === pc.id && r.state === 'Pending');
    if (pendingRes) {
      const resStart = new Date(pendingRes.reservationTime);
      const resEnd = new Date(resStart.getTime() + (pendingRes.durationMin || 60) * 60000);
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
      // Convert local IST time to UTC (PostgreSQL only accepts UTC)
      // IST = UTC+5:30, so UTC = IST - 5:30 hours
      const parts = form.date.split('-'); // YYYY-MM-DD
      const year = parseInt(parts[0]);
      const month = parseInt(parts[1]) - 1; // 0-indexed
      const day = parseInt(parts[2]);

      const timeParts = form.time.split(':');
      const hours = parseInt(timeParts[0]);
      const minutes = parseInt(timeParts[1]);

      // Create UTC date by subtracting 5.5 hours from IST time
      const istDate = new Date(Date.UTC(year, month, day, hours, minutes, 0));
      const istMs = istDate.getTime() - (5.5 * 60 * 60 * 1000); // Subtract 5.5 hours
      const utcDate = new Date(istMs);
      const reservationTime = utcDate.toISOString();

      const depositAmount = isMemberBooking || form.durationMin === null ? 0 : Number(form.advanceDeposit);

      await createReservation({
        pcId: form.pcId,
        customerName: form.customerName.trim(),
        memberId: selectedMember?.id || null,
        reservationTime: reservationTime,
        durationMin: isMemberBooking ? null : (form.durationMin !== null ? Number(form.durationMin) : null),
        advanceDepositCash: form.depositMethod === 'online' ? 0 : depositAmount,
        advanceDepositOnline: form.depositMethod === 'online' ? depositAmount : 0,
        gracePeriodMin: Number(form.gracePeriodMin),
        notes: form.notes.trim()
      });

      toast.success('Reservation created successfully!');
      const { date, time } = getDefaultDateTime();
      setForm(prev => ({
        ...prev,
        customerName: '',
        pcId: '',
        date,
        time,
        notes: '',
        advanceDeposit: 0,
        depositMethod: 'cash',
        durationMin: null,
        selectedTier: ''
      }));
      setPcPlans([]);
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

  // ── Actions: Mark Arrived — one-way, like checking off a todo item. The reservation is still
  // Pending underneath (Arrived is a plain reminder flag, not a gate on anything - starting a
  // session for this customer still goes through the ordinary Sessions screen either way), it
  // just no longer needs the counter's attention, so it drops off this list once checked. ──
  const handleMarkArrived = async (res) => {
    setReservations(prev => prev.filter(r => r.id !== res.id));
    try {
      await setReservationArrived(res.id, true);
    } catch (err) {
      setReservations(prev => [...prev, res].sort((a, b) => new Date(a.reservationTime) - new Date(b.reservationTime)));
      toast.error('Failed to mark as arrived');
    }
  };

  // ── Actions: Remove — permanent, no reason needed. ──
  const handleRemove = async (res) => {
    if (!window.confirm(`Remove the booking for ${res.customerName}? This can't be undone.`)) return;
    setRemovingId(res.id);
    try {
      await deleteReservation(res.id);
      toast.success('Reservation removed');
      setReservations(prev => prev.filter(r => r.id !== res.id));
      fetchPcsAndSessions();
    } catch (err) {
      toast.error(err.response?.data?.error || err.response?.data?.message || 'Failed to remove reservation');
    } finally {
      setRemovingId(null);
    }
  };


  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-start justify-between gap-4">
        <PageHeader
          title="PC Reservations"
          subtitle="Manage future PC bookings with automated grace period and real-time state broadcasts"
          icon="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"
        />
        <button
          onClick={() => setIsHistoryOpen(true)}
          className="btn-secondary shrink-0 flex items-center gap-2 text-xs py-2 px-4 uppercase font-bold"
        >
          <History className="w-4 h-4" /> History
        </button>
      </div>

      <div className="flex h-[calc(100vh-11rem)] overflow-hidden gap-4 p-1">
        {/* Left Side: Creation Form */}
        <div className="flex flex-col w-[35%] min-w-[340px] bg-bg-2 border border-border rounded-xl shadow-lg overflow-hidden">
          <div className="px-4 py-3 border-b border-border bg-bg-3/50 flex items-center gap-2">
            <Calendar className="w-4 h-4 text-neon-purple" />
            <h3 className="font-heading font-bold text-text uppercase tracking-wider text-xs">New Reservation</h3>
          </div>

          <form onSubmit={handleFormSubmit} className="flex-1 flex flex-col overflow-hidden p-3 text-sm">
            <div className="flex-1 overflow-y-auto space-y-2 pr-1">
            {/* Member Booking Toggle */}
            <div className="flex items-center justify-between bg-bg-3/50 border border-border rounded px-2 py-1.5">
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
                    <span className="text-[9px] text-text-3 font-mono">• Member Amount: ₹{selectedMember.gamingBalance?.toFixed(0) || 0}</span>
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

            {/* PC selector — STEP 1 (moved to top) */}
            <div className="space-y-1">
              <label className="text-[10px] font-mono font-semibold text-text-2 uppercase tracking-wider block">
                ① Select PC *
              </label>
              <select
                value={form.pcId}
                onChange={e => setForm(f => ({ ...f, pcId: e.target.value }))}
                className="w-full bg-bg-3 border border-border rounded px-3 py-2 text-xs text-text focus:border-neon-purple focus:outline-none transition-colors"
                required
              >
                <option value="">-- Select Available Station --</option>
                {eligiblePcs.map(p => {
                  const isConfigured = p.monitorHz && p.monitorHz.trim() !== '';
                  return (
                    <option key={p.id} value={p.id}>
                      {p.name}{p.zone ? ` · ${p.zone}` : ''} | {isConfigured ? `${p.monitorHz}` : '⚠ Not Configured'}
                    </option>
                  );
                })}
              </select>
              <p className="text-[9px] text-text-3 font-mono mt-1">
                {eligiblePcs.length} stations available for selected time slot
              </p>
            </div>

            {/* STEP 2: Plan Selection — shown only when PC selected and NOT member booking */}
            {!isMemberBooking && (
              <div className="space-y-1">
                <label className="text-[10px] font-mono font-semibold text-text-2 uppercase tracking-wider block">
                  ② Select Plan
                </label>
                {!form.pcId ? (
                  <div className="text-[9px] text-text-3 font-mono">← Select PC first</div>
                ) : pcPlansLoading ? (
                  <div className="text-[9px] text-text-3">Loading...</div>
                ) : (
                  <select
                    value={form.durationMin !== null ? form.durationMin : 'postpaid'}
                    onChange={(e) => {
                      const val = e.target.value;
                      const plan = pcPlans.find(p => val === 'postpaid' ? p.isPostpaid : p.duration === parseInt(val));
                      if (plan) setForm(f => ({ ...f, durationMin: plan.isPostpaid ? null : plan.duration, advanceDeposit: 0 }));
                    }}
                    className="w-full bg-bg-3 border border-neon-purple/40 rounded px-2 py-1.5 text-xs text-text focus:border-neon-purple focus:outline-none"
                  >
                    <option value="">-- Select a plan --</option>
                    {pcPlans.map(plan => (
                      <option key={plan.id} value={plan.isPostpaid ? 'postpaid' : plan.duration}>
                        {plan.name} {plan.price > 0 ? `₹${plan.price}` : '(Pay as you go)'}
                      </option>
                    ))}
                  </select>
                )}
              </div>
            )}

            {/* Member booking note */}
            {isMemberBooking && form.pcId && (
              <div className="flex items-center gap-2 rounded border border-neon-purple/30 bg-neon-purple/10 px-3 py-2">
                <CheckCircle className="w-3.5 h-3.5 text-neon-purple flex-shrink-0" />
                <span className="text-[10px] text-neon-purple font-mono">Member session — plan is selected when member logs in on the PC</span>
              </div>
            )}

            {/* Advance Deposit — only for non-member, fixed-duration bookings (not postpaid) */}
            {!isMemberBooking && form.durationMin !== null && (
              <div className="space-y-1">
                <label className="text-[10px] font-mono font-semibold text-text-2 uppercase tracking-wider flex items-center gap-1">
                  <IndianRupee className="w-3 h-3 text-text-3" /> Deposit (₹)
                </label>
                <div className="flex gap-2">
                  <input
                    type="number"
                    placeholder="0.00"
                    min="0"
                    value={form.advanceDeposit || ''}
                    onChange={e => setForm(f => ({ ...f, advanceDeposit: parseFloat(e.target.value) || 0 }))}
                    className="flex-1 bg-bg-3 border border-border rounded px-3 py-2 text-xs text-text focus:border-neon-purple focus:outline-none"
                  />
                  <div className="flex rounded border border-border overflow-hidden shrink-0">
                    {['cash', 'online'].map(method => (
                      <button
                        key={method}
                        type="button"
                        onClick={() => setForm(f => ({ ...f, depositMethod: method }))}
                        className={`px-3 py-2 text-[10px] font-mono font-semibold uppercase tracking-wider transition-colors ${
                          form.depositMethod === method
                            ? 'bg-neon-purple/20 text-neon-purple'
                            : 'bg-bg-3 text-text-3 hover:text-text-2'
                        }`}
                      >
                        {method}
                      </button>
                    ))}
                  </div>
                </div>
                {/* Only the cash portion of a deposit ever sits in the physical drawer - an
                    online deposit is still credited off the customer's final bill, it just
                    never touches the till, so it must never count toward what the drawer is
                    expected to hold. */}
                <p className="text-[10px] text-text-3">
                  {form.depositMethod === 'online'
                    ? 'Paid online — not counted in the cash drawer.'
                    : 'Paid as cash — added to the cash drawer.'}
                </p>
              </div>
            )}

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
            </div>

            <button
              type="submit"
              disabled={submittingForm}
              className="w-full py-2 mt-2 rounded border border-neon-purple/50 bg-neon-purple/10 text-neon-purple font-heading font-bold uppercase tracking-widest text-xs hover:bg-neon-purple/20 transition-colors flex items-center justify-center gap-2"
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
                          <span>Time: {(() => {
                            const date = new Date(res.reservationTime);
                            const hours = String(date.getHours()).padStart(2, '0');
                            const minutes = String(date.getMinutes()).padStart(2, '0');
                            return `${hours}:${minutes}`;
                          })()}</span>
                          <span>Duration: {res.durationMin} Min</span>
                        </div>

                        {res.notes && (
                          <p className="text-[10px] text-text-3 italic font-body bg-bg-3/50 p-1.5 border border-border/40 rounded mt-1">
                            Notes: {res.notes}
                          </p>
                        )}
                      </div>

                      {/* Mark Arrived (one-way — checking it off drops the card from this list,
                          same as ticking a todo item) + Remove (only while still Pending) */}
                      <div className="flex gap-2 shrink-0">
                        <button
                          onClick={() => handleMarkArrived(res)}
                          title="Mark arrived"
                          className="p-2 border border-border bg-bg-3 text-text-3 hover:text-pc-active hover:border-pc-active/40 hover:bg-pc-active/10 rounded flex items-center gap-1.5 text-xs font-semibold transition-colors"
                        >
                          <CheckCircle className="w-3.5 h-3.5" /> Arrived
                        </button>
                        {isPendingState && (
                          <button
                            onClick={() => handleRemove(res)}
                            disabled={removingId === res.id}
                            title="Remove Reservation"
                            className="p-2 border border-neon-red/40 bg-neon-red/10 text-neon-red rounded hover:bg-neon-red/20 transition-colors flex items-center gap-1.5 text-xs font-semibold disabled:opacity-50"
                          >
                            <Trash2 className="w-3.5 h-3.5" /> Remove
                          </button>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        </div>
      </div>

      {isHistoryOpen && (
        <div className="fixed inset-0 z-[9999] flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
          <div className="w-full max-w-4xl max-h-[85vh] bg-bg-2 border border-border rounded-xl shadow-2xl flex flex-col overflow-hidden">
            <div className="px-5 py-4 border-b border-border bg-bg-3 flex items-center justify-between shrink-0">
              <h2 className="font-heading font-bold text-text uppercase tracking-wider text-base flex items-center gap-2">
                <History className="w-4 h-4 text-accent" /> Reservations History
              </h2>
              <button onClick={() => setIsHistoryOpen(false)} className="p-1 text-text-3 hover:text-text rounded transition-colors">
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-5 flex flex-wrap items-center gap-3 border-b border-border shrink-0">
              <Calendar className="w-4 h-4 text-text-3 shrink-0" />
              <div className="flex items-center gap-2">
                <label className="text-xs text-text-3 uppercase tracking-wider">From</label>
                <input
                  type="date"
                  value={historyFrom}
                  max={historyTo}
                  onChange={(e) => setHistoryFrom(e.target.value)}
                  className="bg-bg-3 border border-border rounded-lg px-3 py-1.5 text-sm text-text"
                />
              </div>
              <div className="flex items-center gap-2">
                <label className="text-xs text-text-3 uppercase tracking-wider">To</label>
                <input
                  type="date"
                  value={historyTo}
                  min={historyFrom}
                  max={todayIso()}
                  onChange={(e) => setHistoryTo(e.target.value)}
                  className="bg-bg-3 border border-border rounded-lg px-3 py-1.5 text-sm text-text"
                />
              </div>
              <button onClick={resetHistoryToToday} className="text-xs font-medium text-accent hover:text-accent/80 transition-colors ml-auto">
                Today
              </button>
              <button
                onClick={handleDownloadHistoryPdf}
                disabled={history.length === 0}
                className="btn-secondary py-1.5 px-3 flex items-center gap-1.5 text-xs font-bold disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <Download className="w-3.5 h-3.5" /> Download PDF
              </button>
            </div>

            <div className="flex-1 overflow-y-auto p-5">
              {historyLoading ? (
                <div className="flex justify-center py-12">
                  <div className="w-6 h-6 border-2 border-accent border-t-transparent rounded-full animate-spin" />
                </div>
              ) : history.length === 0 ? (
                <div className="text-center text-text-3 text-xs italic py-8 border border-dashed border-border rounded-lg">
                  No reservations found in this range.
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-left border-collapse text-xs whitespace-nowrap">
                    <thead>
                      <tr className="border-b border-border text-text-3 uppercase tracking-wider font-bold text-[10px]">
                        <th className="py-2 px-3">Time</th>
                        <th className="py-2 px-3">PC</th>
                        <th className="py-2 px-3">Customer</th>
                        <th className="py-2 px-3">Duration</th>
                        <th className="py-2 px-3 text-right">Deposit</th>
                        <th className="py-2 px-3 text-center">Status</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-border/40 font-mono">
                      {history.map(r => (
                        <tr key={r.id}>
                          <td className="py-2 px-3 text-text-2">{r.reservationTime ? format(new Date(r.reservationTime), 'MMM d, hh:mm a') : '-'}</td>
                          <td className="py-2 px-3 text-text font-bold">{r.pcName || '-'}</td>
                          <td className="py-2 px-3 text-text-2 font-sans">{r.customerName || '-'}</td>
                          <td className="py-2 px-3 text-text-2">{r.durationMin ? `${r.durationMin}m` : '-'}</td>
                          <td className="py-2 px-3 text-right text-text">₹{(r.advanceDeposit || 0).toFixed(2)}</td>
                          <td className="py-2 px-3 text-center">
                            {r.arrived ? (
                              <span className="text-[10px] px-1.5 py-0.5 rounded border uppercase tracking-wider font-bold text-neon-green bg-neon-green/10 border-neon-green/20">Arrived</span>
                            ) : (
                              <StatusBadge state={r.state} />
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
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
