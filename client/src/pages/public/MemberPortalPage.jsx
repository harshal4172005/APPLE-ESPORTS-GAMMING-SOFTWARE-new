import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { Wallet, LogOut, MonitorPlay, History, ChevronRight, User, AlertTriangle, MapPin, Loader2 } from 'lucide-react';
import api from '../../config/api';
import { useToast } from '../../components/ui/Toast';

export default function MemberPortalPage() {
  const navigate = useNavigate();
  const toast = useToast();

  // Step: 'branch' | 'pc'
  const [step, setStep] = useState('branch');

  const [profile, setProfile] = useState(null);
  const [branches, setBranches] = useState([]);
  const [selectedBranch, setSelectedBranch] = useState(null);
  const [idlePcs, setIdlePcs] = useState([]);
  const [selectedPc, setSelectedPc] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isPcsLoading, setIsPcsLoading] = useState(false);
  const [isStarting, setIsStarting] = useState(false);

  // Read member profile from localStorage (set during login)
  const storedProfile = JSON.parse(localStorage.getItem('memberProfile') || 'null');
  const memberToken = localStorage.getItem('memberToken');

  useEffect(() => {
    if (!storedProfile || !memberToken) {
      navigate('/user/member-login');
      return;
    }
    setProfile(storedProfile);
    fetchBranches();
  }, []);

  const fetchBranches = async () => {
    try {
      const res = await api.get('/auth/branches');
      if (res.data.success) setBranches(res.data.data);
    } catch {
      toast.error('Failed to load branches');
    } finally {
      setIsLoading(false);
    }
  };

  const handleSelectBranch = async (branch) => {
    setSelectedBranch(branch);
    setSelectedPc(null);
    setStep('pc');
    setIsPcsLoading(true);
    try {
      const res = await api.get(`/auth/branches/${branch.id}/pcs/idle`); // TODO: update backend route if necessary
      if (res.data.success) setIdlePcs(res.data.data);
    } catch {
      toast.error('Failed to load PCs');
    } finally {
      setIsPcsLoading(false);
    }
  };

  const handleStartSession = async () => {
    if (!selectedPc) return;
    setIsStarting(true);

    try {
      const res = await api.post(
        '/sessions/start',
        {
          pcId: selectedPc.id,
          memberId: storedProfile.memberId,
          customerName: storedProfile.fullName,
          durationMinutes: 60,
          packageName: 'Member Session',
          expectedAmount: 0,
        },
        {
          headers: {
            Authorization: `Bearer ${memberToken}`,
            'X-Branch-Id': selectedBranch.id,
          },
        }
      );

      if (res.data.success) {
        toast.success(`Session started on ${selectedPc.name}!`);
        navigate(`/pc-overlay/${selectedPc.id}`);
      } else {
        toast.error(res.data.error || 'Failed to start session');
      }
    } catch (err) {
      toast.error(err.response?.data?.error || 'Failed to start session');
    } finally {
      setIsStarting(false);
    }
  };

  const handleLogout = () => {
    localStorage.removeItem('memberToken');
    localStorage.removeItem('memberProfile');
    navigate('/');
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-bg flex items-center justify-center text-accent animate-pulse font-heading text-2xl tracking-wider uppercase">
        Loading Portal...
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-bg relative overflow-x-hidden">
      <div className="absolute inset-0 z-0 bg-[radial-gradient(ellipse_at_top,_var(--tw-gradient-stops))] from-accent/10 via-bg to-bg pointer-events-none" />

      {/* Header */}
      <header className="relative z-10 bg-bg-2/80 backdrop-blur-xl border-b border-border/60 px-8 py-4 flex items-center justify-between shadow-lg">
        <div className="flex items-center gap-4">
          <div className="w-12 h-12 rounded-full bg-accent/20 flex items-center justify-center border border-accent/50 shadow-[0_0_10px_rgba(220,38,38,0.3)]">
            <User className="w-6 h-6 text-accent" />
          </div>
          <div>
            <h1 className="font-heading text-2xl font-bold text-text tracking-wide uppercase">
              {profile?.fullName || 'Member'}
            </h1>
            <p className="text-text-2 font-mono text-sm">{profile?.memberNumber || 'MEM-0000'}</p>
          </div>
        </div>
        <button onClick={handleLogout} className="btn-danger flex items-center gap-2">
          <LogOut className="w-4 h-4" />
          <span className="font-heading tracking-wide uppercase font-semibold">Logout</span>
        </button>
      </header>

      <main className="relative z-10 max-w-7xl mx-auto px-8 py-8 grid grid-cols-1 lg:grid-cols-3 gap-8">

        {/* Left: Wallet & Plan */}
        <div className="space-y-6">
          <div className="card bg-bg-2/80 backdrop-blur-xl border-border/60 shadow-xl shadow-black/50 p-6 relative overflow-hidden group hover:border-accent transition-all duration-300">
            <div className="absolute -right-4 -top-4 text-white/5">
              <Wallet className="w-32 h-32" />
            </div>
            <h2 className="text-text-2 font-heading text-sm tracking-widest uppercase mb-2">Gaming Balance</h2>
            <div className="font-mono text-5xl font-bold text-text mb-2">
              ₹{(profile?.gamingBalance || 0).toFixed(2)}
            </div>
            <p className="text-text-3 font-body text-xs mb-6">Food Balance: ₹{(profile?.foodBalance || 0).toFixed(2)}</p>

            {(profile?.gamingBalance || 0) < 100 && (
              <div className="bg-neon-orange/10 border border-neon-orange/30 rounded-md p-3 flex items-start gap-2 mb-4">
                <AlertTriangle className="w-4 h-4 text-neon-orange shrink-0 mt-0.5" />
                <p className="text-xs text-neon-orange font-body leading-relaxed">
                  Low balance. Please recharge at the counter.
                </p>
              </div>
            )}

            <button className="w-full btn-secondary flex items-center justify-between hover:border-accent hover:text-accent transition-colors">
              <span className="font-heading tracking-wider uppercase font-semibold">Transaction History</span>
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>

          <div className="card bg-bg-2/80 backdrop-blur-xl border-border/60 shadow-xl shadow-black/50 p-6">
            <h3 className="font-heading text-xl font-bold text-text mb-4 flex items-center gap-2 tracking-wider uppercase">
              <History className="w-5 h-5 text-accent" />
              Membership
            </h3>
            <div className="space-y-3 font-body">
              <div className="flex justify-between items-center pb-3 border-b border-border">
                <span className="text-text-2">Member ID</span>
                <span className="text-text font-mono text-sm">{profile?.memberNumber}</span>
              </div>
            </div>
          </div>
        </div>

        {/* Right: Branch → PC selection */}
        <div className="lg:col-span-2 card bg-bg-2/80 backdrop-blur-xl border-border/60 shadow-xl shadow-black/50 p-6 flex flex-col">

          {/* Step indicator */}
          <div className="flex items-center gap-3 mb-6">
            <StepDot active={step === 'branch'} done={step === 'pc'} label="Select Branch" n={1} />
            <div className="flex-1 h-px bg-border" />
            <StepDot active={step === 'pc'} done={false} label="Select PC" n={2} />
          </div>

          {/* STEP 1: Branch selection */}
          {step === 'branch' && (
            <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="flex-1">
              <h2 className="font-heading text-2xl font-bold text-text tracking-wide uppercase mb-2">Choose Branch</h2>
              <p className="text-text-2 font-body mb-6">Select the branch you're visiting today.</p>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                {branches.map(branch => (
                  <motion.div
                    key={branch.id}
                    whileHover={{ scale: 1.02 }}
                    whileTap={{ scale: 0.97 }}
                    onClick={() => handleSelectBranch(branch)}
                    className="p-5 rounded-xl border border-border bg-bg-3 hover:border-accent hover:bg-accent/5 cursor-pointer transition-all flex items-center gap-4"
                  >
                    <div className="w-10 h-10 bg-accent/10 rounded-lg flex items-center justify-center border border-accent/30">
                      <MapPin className="w-5 h-5 text-accent" />
                    </div>
                    <div>
                      <p className="font-heading font-bold text-text tracking-wide">{branch.name}</p>
                      {branch.address && <p className="text-text-3 text-xs font-body mt-0.5">{branch.address}</p>}
                    </div>
                  </motion.div>
                ))}
              </div>
            </motion.div>
          )}

          {/* STEP 2: PC selection */}
          {step === 'pc' && (
            <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="flex-1 flex flex-col">
              <div className="flex items-center justify-between mb-4">
                <div>
                  <h2 className="font-heading text-2xl font-bold text-text tracking-wide uppercase">Select PC</h2>
                  <p className="text-text-2 font-body text-sm mt-1">
                    Branch: <span className="text-accent font-semibold">{selectedBranch?.name}</span>
                  </p>
                </div>
                <button
                  onClick={() => { setStep('branch'); setSelectedPc(null); }}
                  className="text-text-3 hover:text-text text-sm font-body underline"
                >
                  Change branch
                </button>
              </div>

              {isPcsLoading ? (
                <div className="flex-1 flex items-center justify-center">
                  <Loader2 className="w-8 h-8 text-accent animate-spin" />
                </div>
              ) : (
                <>
                  <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-4 mb-6 overflow-y-auto pr-1 flex-1 max-h-[400px] scrollbar-thin">
                    {idlePcs.map(pc => (
                      <motion.div
                        key={pc.id}
                        whileHover={{ scale: 1.05 }}
                        whileTap={{ scale: 0.95 }}
                        onClick={() => setSelectedPc(pc)}
                        className={`p-4 rounded-xl border cursor-pointer transition-all duration-200 flex flex-col items-center justify-center gap-2
                          ${selectedPc?.id === pc.id
                            ? 'bg-accent/20 border-accent shadow-[0_0_15px_rgba(220,38,38,0.2)]'
                            : 'bg-bg-3 border-border hover:border-text-2'
                          }`}
                      >
                        <MonitorPlay className={`w-8 h-8 ${selectedPc?.id === pc.id ? 'text-accent' : 'text-text-3'}`} />
                        <span className={`font-mono font-bold text-lg ${selectedPc?.id === pc.id ? 'text-text' : 'text-text-2'}`}>
                          {pc.name}
                        </span>
                      </motion.div>
                    ))}
                    {idlePcs.length === 0 && (
                      <div className="col-span-full py-12 text-center text-text-3 flex flex-col items-center">
                        <MonitorPlay className="w-12 h-12 mb-4 opacity-50" />
                        <p className="font-heading font-bold text-2xl tracking-wide uppercase text-text-2">No PCs available</p>
                        <p className="text-sm mt-2 font-body">Please wait or check with the counter.</p>
                      </div>
                    )}
                  </div>

                  <AnimatePresence>
                    {selectedPc && (
                      <motion.div
                        initial={{ opacity: 0, y: 20 }}
                        animate={{ opacity: 1, y: 0 }}
                        exit={{ opacity: 0, y: 20 }}
                        className="p-4 bg-accent/10 border border-accent/30 rounded-xl flex items-center justify-between"
                      >
                        <div className="flex items-center gap-4">
                          <div className="w-12 h-12 bg-accent rounded-lg flex items-center justify-center text-bg font-bold font-mono shadow-[0_0_10px_rgba(220,38,38,0.4)] text-sm">
                            {selectedPc.name}
                          </div>
                          <div>
                            <p className="text-sm text-text-2 font-body tracking-wide">Selected Station</p>
                            <p className="text-text font-heading font-bold text-lg tracking-wider uppercase">Ready to start</p>
                          </div>
                        </div>
                        <button
                          onClick={handleStartSession}
                          disabled={isStarting}
                          className="bg-accent hover:bg-accent-dark text-bg px-8 py-3 font-heading font-bold tracking-wider uppercase text-lg rounded-md flex items-center gap-2 disabled:opacity-50 transition-colors shadow-[0_0_15px_rgba(220,38,38,0.3)]"
                        >
                          {isStarting ? <Loader2 className="w-5 h-5 animate-spin" /> : 'Start Session'}
                        </button>
                      </motion.div>
                    )}
                  </AnimatePresence>
                </>
              )}
            </motion.div>
          )}
        </div>
      </main>
    </div>
  );
}

function StepDot({ active, done, label, n }) {
  return (
    <div className="flex items-center gap-2">
      <div className={`w-7 h-7 rounded-full flex items-center justify-center text-sm font-bold font-mono border transition-colors
        ${done ? 'bg-neon-green border-neon-green text-bg' : active ? 'bg-accent border-accent text-bg' : 'bg-bg-3 border-border text-text-3'}`}>
        {done ? '✓' : n}
      </div>
      <span className={`font-body text-sm hidden sm:block ${active ? 'text-text' : 'text-text-3'}`}>{label}</span>
    </div>
  );
}
