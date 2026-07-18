import { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Play, X, User, Clock, Users, Search, Loader2 } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import { getMembers } from '../../api/members.api';
import api from '../../config/api';

/**
 * Modal for starting a NEW session on an idle PC.
 * Active session actions (Stop, Extend, Bill, Food, Promo)
 * are now handled inline on the PcCard itself.
 */
export default function SessionActionModal({ pc, onClose, onActionSuccess }) {
  const { isSuperAdmin, user } = useAuth();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const [form, setForm] = useState({
    customerName: pc?.isRestart && pc?.lastCustomerName ? pc.lastCustomerName : '',
    customerType: pc?.isRestart && pc?.lastMemberId ? 'Member' : 'Walk-in',
    memberId: pc?.isRestart && pc?.lastMemberId ? pc.lastMemberId : null,
  });

  const [searchQuery, setSearchQuery] = useState('');
  const [searchResults, setSearchResults] = useState([]);
  const [searching, setSearching] = useState(false);
  const [selectedMember, setSelectedMember] = useState(null);

  const [plans, setPlans] = useState([]);
  const [selectedPlan, setSelectedPlan] = useState(null);
  const [loadingPlans, setLoadingPlans] = useState(true);

  useEffect(() => {
    if (!pc) return;
    const fetchPlans = async () => {
      try {
        setLoadingPlans(true);
        const res = await api.get(`/public/pcs/${pc.id}/plans`);
        if (res.data.success) {
          setPlans(res.data.data);
          if (res.data.data.length > 0) {
            setSelectedPlan(res.data.data[0]);
          }
        }
      } catch (err) {
        console.error('Failed to fetch PC plans', err);
      } finally {
          setLoadingPlans(false);
      }
    };
    fetchPlans();
  }, [pc]);

  if (!pc) return null;

  // Only show for idle PCs — active sessions are handled inline on card
  if (pc.state !== 'Idle' && pc.state !== 'Offline') return null;

  const handleSearchMember = async (query) => {
    setSearchQuery(query);
    if (query.trim().length < 2) {
      setSearchResults([]);
      return;
    }
    setSearching(true);
    try {
      const activeBranchId = isSuperAdmin ? localStorage.getItem('activeBranchId') : user?.branchId;
      const res = await getMembers(activeBranchId, query, 1, 10);
      setSearchResults(res?.items ?? (Array.isArray(res) ? res : []));
    } catch {
      setSearchResults([]);
    } finally {
      setSearching(false);
    }
  };

  const selectMember = (m) => {
    setSelectedMember(m);
    setForm(f => ({
      ...f,
      customerName: m.fullName,
      memberId: m.id
    }));
    setSearchResults([]);
    setSearchQuery('');
  };

  const clearMember = () => {
    setSelectedMember(null);
    setForm(f => ({
      ...f,
      customerName: '',
      memberId: null
    }));
  };

  const handleStart = async (e) => {
    e.preventDefault();
    if (!form.customerName.trim()) {
      setError('Customer name is required.');
      return;
    }
    if (!selectedPlan) {
      setError('Please wait for plans to load or select a plan.');
      return;
    }

    setLoading(true);
    setError(null);
    try {
      await api.post('/sessions/start', {
        pcId: pc.id,
        customerName: form.customerName.trim(),
        customerType: form.customerType,
        durationMinutes: selectedPlan.duration > 0 ? selectedPlan.duration : 0,
        packageName: selectedPlan.name,
        expectedAmount: selectedPlan.price,
        isOverride: isSuperAdmin,
        operatorId: user?.id,
        memberId: form.memberId,
      });
      onActionSuccess?.();
    } catch (err) {
      setError(err.response?.data?.error || err.response?.data?.message || 'Failed to start session');
    } finally {
      setLoading(false);
    }
  };

  return (
    <AnimatePresence>
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
        <motion.div
          initial={{ opacity: 0, scale: 0.96, y: 10 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          exit={{ opacity: 0, scale: 0.96, y: 10 }}
          className="w-full max-w-sm bg-bg-2 border border-border rounded-xl shadow-2xl overflow-hidden"
        >
          {/* Header */}
          <div className="px-5 py-4 border-b border-border bg-bg-3 flex items-center justify-between">
            <div>
              <h2 className="font-heading font-bold text-text uppercase tracking-wider text-base flex items-center gap-2">
                <Play className="w-4 h-4 text-pc-active" />
                START SESSION — {pc.name}
              </h2>
              <p className="text-text-3 text-[10px] font-mono mt-0.5">
                {pc.zone && <span className="mr-2 text-neon-purple uppercase">{pc.zone}</span>}
              </p>
            </div>
            <button onClick={onClose} className="p-1 text-text-3 hover:text-text rounded transition-colors">
              <X className="w-5 h-5" />
            </button>
          </div>

          <form onSubmit={handleStart} className="p-5 space-y-4">
            {error && (
              <div className="p-3 bg-neon-red/10 border border-neon-red/20 rounded text-neon-red text-xs">
                {error}
              </div>
            )}

            {/* Customer Type */}
            <div className="space-y-1.5">
              <label className="text-[10px] font-mono font-semibold text-text-2 uppercase tracking-wider flex items-center gap-1">
                <Users className="w-3 h-3" /> Session Type
              </label>
              <div className="grid grid-cols-2 gap-2">
                {['Walk-in', 'Member'].map(type => (
                  <button
                    key={type}
                    type="button"
                    onClick={() => {
                      setForm(f => ({ ...f, customerType: type }));
                      if (type === 'Walk-in') clearMember();
                    }}
                    className={`py-2 rounded border text-xs font-semibold uppercase tracking-wide transition-colors ${
                      form.customerType === type
                        ? 'border-pc-active bg-pc-active/10 text-pc-active'
                        : 'border-border bg-bg-3 text-text-2 hover:border-border-2'
                    }`}
                  >
                    {type}
                  </button>
                ))}
              </div>
            </div>

            {/* Member Search / Selector (only if Member selected) */}
            {form.customerType === 'Member' && (
              <div className="space-y-2 border border-border/60 bg-bg-3/40 p-3 rounded-lg">
                <label className="text-[10px] font-mono font-semibold text-text-3 uppercase tracking-wider block">
                  Link Registered Member
                </label>
                
                {selectedMember ? (
                  <div className="flex items-center justify-between bg-accent/10 border border-accent/30 rounded px-2.5 py-1.5 text-xs text-accent">
                    <div className="truncate">
                      <span className="font-bold">{selectedMember.fullName}</span> 
                      <span className="font-mono text-[10px] ml-1.5 text-text-3">({selectedMember.mobileNumber})</span>
                    </div>
                    <button
                      type="button"
                      onClick={clearMember}
                      className="p-0.5 ml-2 text-text-3 hover:text-accent transition-colors"
                    >
                      <X className="w-3.5 h-3.5" />
                    </button>
                  </div>
                ) : (
                  <div className="relative">
                    <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-text-3" />
                    <input
                      type="text"
                      placeholder="Search by name, phone number..."
                      value={searchQuery}
                      onChange={e => handleSearchMember(e.target.value)}
                      className="w-full bg-bg-3 border border-border rounded pl-8 pr-3 py-1.5 text-xs text-text placeholder-text-3 focus:border-accent focus:outline-none transition-colors"
                    />
                    
                    {searching && (
                      <div className="absolute right-2.5 top-1/2 -translate-y-1/2">
                        <div className="w-3 h-3 rounded-full border border-accent border-t-transparent animate-spin" />
                      </div>
                    )}

                    {/* Autocomplete dropdown */}
                    {searchResults.length > 0 && (
                      <div className="absolute left-0 right-0 mt-1 max-h-40 overflow-y-auto bg-bg-2 border border-border rounded-lg shadow-xl z-20 divide-y divide-border">
                        {searchResults.map(m => (
                          <button
                            key={m.id}
                            type="button"
                            onClick={() => selectMember(m)}
                            className="w-full text-left px-3 py-2 text-xs text-text-2 hover:bg-bg-3 hover:text-text transition-colors flex justify-between items-center"
                          >
                            <span className="font-bold">{m.fullName}</span>
                            <span className="font-mono text-[10px] text-text-3">{m.mobileNumber}</span>
                          </button>
                        ))}
                      </div>
                    )}
                  </div>
                )}
              </div>
            )}

            {/* Customer Name */}
            <div className="space-y-1.5">
              <label className="text-[10px] font-mono font-semibold text-text-2 uppercase tracking-wider flex items-center gap-1">
                <User className="w-3 h-3" /> Customer Name *
              </label>
              <input
                type="text"
                disabled={form.customerType === 'Member' && !!selectedMember}
                value={form.customerName}
                onChange={(e) => setForm(f => ({ ...f, customerName: e.target.value }))}
                placeholder="Enter name or token..."
                className="w-full bg-bg-3 border border-border rounded px-3 py-2 text-sm text-text placeholder-text-3 focus:border-pc-active focus:outline-none transition-colors disabled:opacity-60 disabled:cursor-not-allowed"
                autoFocus
              />
            </div>

            {/* Branch-Wise Plan Selection */}
            <div className="space-y-1.5">
              <label className="text-[10px] font-mono font-semibold text-text-2 uppercase tracking-wider flex items-center justify-between">
                <span className="flex items-center gap-1"><Clock className="w-3 h-3" /> Select Plan</span>
                {loadingPlans && <Loader2 className="w-3 h-3 animate-spin text-accent" />}
              </label>
              
              {!loadingPlans && plans.length === 0 ? (
                <div className="text-xs text-text-3 border border-border/50 bg-bg-3 p-3 rounded text-center">
                  No plans available for this PC.
                </div>
              ) : (
                <div className="grid grid-cols-2 gap-2">
                  {plans.map(plan => (
                    <button
                      key={plan.id}
                      type="button"
                      onClick={() => setSelectedPlan(plan)}
                      className={`p-2 rounded border text-left transition-colors flex flex-col gap-0.5 ${
                        selectedPlan?.id === plan.id
                          ? 'border-pc-active bg-pc-active/10 text-pc-active'
                          : 'border-border bg-bg-3 text-text-2 hover:border-border-2 hover:text-text'
                      }`}
                    >
                      <span className="text-[11px] font-bold uppercase tracking-wide truncate w-full">{plan.name}</span>
                      <span className="text-[10px] font-mono opacity-80">
                        {plan.price > 0 ? `₹${plan.price}` : 'Pay Later'}
                      </span>
                    </button>
                  ))}
                </div>
              )}
            </div>

            {/* Submit */}
            <button
              type="submit"
              disabled={loading}
              className="w-full py-2.5 rounded border border-pc-active/50 bg-pc-active/10 text-pc-active font-heading font-bold uppercase tracking-widest text-sm hover:bg-pc-active/20 transition-colors flex items-center justify-center gap-2 disabled:opacity-50"
            >
              {loading
                ? <span className="w-4 h-4 border-2 border-pc-active border-t-transparent rounded-full animate-spin" />
                : <Play className="w-4 h-4" />
              }
              {loading ? 'Starting...' : 'Start Session'}
            </button>
          </form>
        </motion.div>
      </div>
    </AnimatePresence>
  );
}
