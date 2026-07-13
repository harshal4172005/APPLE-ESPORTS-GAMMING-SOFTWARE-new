import React, { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { UserPlus, UserCheck, ArrowLeft, MonitorPlay, Lock, Loader2, Clock, Wallet, CheckCircle, AlertTriangle } from 'lucide-react';
import axios from 'axios';
import api from '../../../config/api';
import { useToast } from '../../../components/ui/Toast';
import { useOverlaySocket } from '../../../contexts/OverlaySocketContext';

export default function PcLockScreen() {
  // step: 'selection' | 'walkin' | 'member_login' | 'time_selection'
  const [step, setStep] = useState('selection');
  const { pcId, requestWalkinSession, walkinDeclineEvent, fetchSession, sessionData } = useOverlaySocket();
  const toast = useToast();

  const [walkinName, setWalkinName] = useState('');
  const [isRequestingWalkin, setIsRequestingWalkin] = useState(false);
  const [walkinRequested, setWalkinRequested] = useState(false);

  const [identifier, setIdentifier] = useState('');
  const [password, setPassword] = useState('');
  const [isLoggingIn, setIsLoggingIn] = useState(false);
  const [loginError, setLoginError] = useState('');
  const [profile, setProfile] = useState(null);
  
  const [walletEmpty, setWalletEmpty] = useState(localStorage.getItem('walletEmptyAlert') === 'true');

  // Time Selection State
  const [isStarting, setIsStarting] = useState(false);

  const [selectedPlan, setSelectedPlan] = useState(null);
  const [plans, setPlans] = useState([]);
  const [plansError, setPlansError] = useState(false);



  // Fetch plans when step changes to walkin
  useEffect(() => {
    if ((step === 'walkin' || step === 'time_selection') && pcId) {
      const fetchPlans = async () => {
        try {
          setPlansError(false);
          const res = await api.get(`/public/pcs/${pcId}/plans`);
          if (res.data.success) {
            setPlans(res.data.data);
            if (res.data.data.length > 0) {
              setSelectedPlan(res.data.data[0]);
            }
          } else {
            setPlansError(true);
          }
        } catch (err) {
          console.error("Failed to fetch plans", err);
          setPlansError(true);
        }
      };
      fetchPlans();
    }
  }, [step, pcId]);

  // If a profile was previously stored, auto-login could be done, 
  // but since this is a public PC, we should clear the profile on idle/startup
  // so the next member can login.
  useEffect(() => {
    localStorage.removeItem('memberToken');
    localStorage.removeItem('memberProfile');
  }, []);

  // Listen for Walk-in Request Decline
  useEffect(() => {
    if (walkinDeclineEvent && walkinRequested) {
      toast.error(walkinDeclineEvent.reason || 'Operator declined the request');
      setWalkinRequested(false);
      setIsRequestingWalkin(false);
      setStep('selection');
    }
  }, [walkinDeclineEvent, walkinRequested]);

  const handleLogin = async (e) => {
    e.preventDefault();
    if (!identifier || !password) return;

    setIsLoggingIn(true);
    setLoginError('');
    try {
      const res = await api.post('/members/login', {
        identifier: identifier,
        password: password,
      });

      if (res.data.success) {
        const data = res.data.data;
        localStorage.setItem('memberToken', data.token);
        const userProfile = {
          memberId: data.memberId,
          memberNumber: data.memberNumber,
          fullName: data.fullName,
          gamingBalance: data.gamingBalance,
          foodBalance: data.foodBalance,
        };
        localStorage.setItem('memberProfile', JSON.stringify(userProfile));
        setProfile(userProfile);
        
        // Auto-start postpaid session
        try {
          const pcRes = await api.get(`/public/pcs/${pcId}`);
          if (pcRes.data.success) {
            const actualPcId = pcRes.data.data.id;
            const startRes = await api.post(
              '/public/sessions/member-start',
              {
                pcId: actualPcId,
                memberId: data.memberId,
                customerName: data.fullName,
                durationMinutes: 0,
                packageName: 'Postpaid',
                expectedAmount: 0,
              },
              {
                headers: {
                  Authorization: `Bearer ${data.token}`,
                  'X-Branch-Id': pcRes.data.data.branchId,
                },
              }
            );

            if (startRes.data.success) {
              toast.success(`Welcome back, ${data.fullName}! Postpaid session started.`);
              await fetchSession();
            } else {
              setLoginError(startRes.data.error || 'Failed to start session automatically.');
              toast.error(startRes.data.error || 'Failed to start session automatically.');
            }
          } else {
            setLoginError('PC not found in database');
            toast.error('PC not found in database');
          }
        } catch (err) {
          const msg = err.response?.data?.error || err.response?.data?.message || 'Failed to start session automatically.';
          setLoginError(msg);
          toast.error(msg);
        }
      } else {
        const errorText = res.data.error || 'Login failed';
        setLoginError(errorText);
        toast.error(errorText);
      }
    } catch (err) {
      const msg = err.response?.data?.error || err.response?.data?.message || 'Invalid credentials';
      setLoginError(msg);
      toast.error(msg);
    } finally {
      setIsLoggingIn(false);
    }
  };

  const handleStartSession = async () => {
    if (!profile || !selectedPlan) return;
    
    if (!selectedPlan.isPostpaid && (profile.gamingBalance || 0) < selectedPlan.price) {
      toast.error('Insufficient gaming balance');
      return;
    }

    setIsStarting(true);
    const memberToken = localStorage.getItem('memberToken');

    try {
      const pcRes = await api.get(`/public/pcs/${pcId}`);
      if (!pcRes.data.success) {
        toast.error('PC not found in database');
        setIsStarting(false);
        return;
      }
      const actualPcId = pcRes.data.data.id;

      const res = await api.post(
        '/public/sessions/member-start',
        {
          pcId: actualPcId,
          memberId: profile.memberId,
          customerName: profile.fullName,
          durationMinutes: selectedPlan.duration,
          packageName: selectedPlan.name,
          expectedAmount: selectedPlan.price,
        },
        {
          headers: {
            Authorization: `Bearer ${memberToken}`,
            'X-Branch-Id': pcRes.data.data.branchId,
          },
        }
      );

      if (res.data.success) {
        toast.success('Session started successfully!');
        await fetchSession();
      } else {
        toast.error(res.data.error || 'Failed to start session');
      }
    } catch (err) {
      toast.error(err.response?.data?.error || 'Failed to start session');
    } finally {
      setIsStarting(false);
    }
  };

  const renderSelection = () => (
    <motion.div
      key="selection"
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -20 }}
      className="max-w-4xl w-full mx-auto"
    >
      <div className="text-center mb-12">
        <h1 className="font-heading text-4xl md:text-5xl font-bold tracking-wide text-text mb-4 uppercase">
          CHOOSE USER TYPE
        </h1>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
        <motion.div
          whileHover={{ scale: 1.03, translateY: -5 }}
          whileTap={{ scale: 0.95 }}
          onClick={() => setStep('walkin')}
          className="card group relative flex flex-col items-center text-center p-8 bg-bg-2/80 backdrop-blur-xl border-border/60 shadow-xl shadow-black/50 hover:border-accent hover:shadow-[0_0_20px_rgba(220,38,38,0.15)] cursor-pointer transition-all duration-300"
        >
          <UserPlus className="w-16 h-16 text-accent mb-6 group-hover:scale-110 transition-transform" />
          <h2 className="font-heading text-3xl font-bold text-text mb-3 tracking-wider uppercase">WALK-IN USER</h2>
          <p className="text-text-2 font-body text-sm leading-relaxed">Play as a guest. Proceed to the counter for PC assignment and billing.</p>
        </motion.div>

        <motion.div
          whileHover={{ scale: 1.03, translateY: -5 }}
          whileTap={{ scale: 0.95 }}
          onClick={() => setStep('member_login')}
          className="card group relative flex flex-col items-center text-center p-8 bg-bg-2/80 backdrop-blur-xl border-border/60 shadow-xl shadow-black/50 hover:border-accent hover:shadow-[0_0_20px_rgba(220,38,38,0.15)] cursor-pointer transition-all duration-300"
        >
          <UserCheck className="w-16 h-16 text-accent mb-6 group-hover:scale-110 transition-transform" />
          <h2 className="font-heading text-3xl font-bold text-text mb-3 tracking-wider uppercase">MEMBER</h2>
          <p className="text-text-2 font-body text-sm leading-relaxed">Log in to your account, manage wallet balance, and start sessions directly.</p>
        </motion.div>
      </div>
    </motion.div>
  );

  const handleWalkinRequest = async () => {
    if (!walkinName.trim()) {
      toast.error('Please enter your name');
      return;
    }
    if (!selectedPlan) {
      toast.error('Please select a plan');
      return;
    }

    setIsRequestingWalkin(true);
    try {
      const res = await requestWalkinSession(walkinName.trim(), selectedPlan.duration, selectedPlan.name);
      if (res?.success) {
        setWalkinRequested(true);
        toast.success('Request sent to operator!');
      } else {
        toast.error(res?.error || 'Failed to send request');
      }
    } catch (err) {
      toast.error('Error communicating with operator');
    } finally {
      setIsRequestingWalkin(false);
    }
  };

  const renderWalkin = () => (
    <motion.div
      key="walkin"
      initial={{ opacity: 0, scale: 0.95 }}
      animate={{ opacity: 1, scale: 1 }}
      exit={{ opacity: 0 }}
      className="max-w-xl w-full mx-auto card bg-bg-2/80 backdrop-blur-xl border-accent/20 shadow-xl shadow-black/50 p-8 text-center relative"
    >
      {!walkinRequested && (
        <button 
          onClick={() => setStep('selection')}
          className="absolute top-6 left-6 flex items-center gap-2 text-text-2 hover:text-text transition-colors"
        >
          <ArrowLeft className="w-5 h-5" />
        </button>
      )}

      {walkinRequested ? (
        <div className="py-8 flex flex-col items-center">
          <div className="w-20 h-20 bg-accent/10 border border-accent/30 rounded-full flex items-center justify-center mb-6 shadow-[0_0_20px_rgba(220,38,38,0.2)]">
            <Clock className="w-10 h-10 text-accent animate-pulse" />
          </div>
          <h2 className="font-heading text-2xl font-bold text-text mb-4 uppercase tracking-widest">Request Sent!</h2>
          <p className="text-text-2 font-body mb-8 leading-relaxed max-w-sm">
            Please wait while the operator reviews your request for {selectedPlan?.name}. Your session will start automatically once approved.
          </p>
          <div className="flex gap-2 items-center justify-center">
            <span className="w-2 h-2 bg-accent rounded-full animate-ping" style={{ animationDelay: '0ms' }} />
            <span className="w-2 h-2 bg-accent rounded-full animate-ping" style={{ animationDelay: '200ms' }} />
            <span className="w-2 h-2 bg-accent rounded-full animate-ping" style={{ animationDelay: '400ms' }} />
          </div>
        </div>
      ) : (
        <>
          <div className="w-16 h-16 bg-accent/10 border border-accent/30 rounded-full flex items-center justify-center mx-auto mb-4 shadow-[0_0_15px_rgba(220,38,38,0.2)]">
            <UserPlus className="w-8 h-8 text-accent" />
          </div>
          <h1 className="font-heading text-2xl font-bold text-text mb-6 tracking-wide uppercase">Walk-in Session</h1>
          
          <div className="text-left space-y-6 mb-6">
            <div>
              <label className="block text-sm font-medium text-text-2 mb-2 font-body tracking-wide">YOUR NAME</label>
              <input
                type="text"
                value={walkinName}
                onChange={(e) => setWalkinName(e.target.value)}
                className="input w-full focus:border-accent focus:ring-accent/30"
                placeholder="e.g. John Doe"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-text-2 mb-2 font-body tracking-wide uppercase">Select Plan</label>
              {plansError ? (
                <div className="flex justify-center p-4">
                  <span className="text-accent font-bold">Failed to connect to backend. Is the server running?</span>
                </div>
              ) : plans.length === 0 ? (
                <div className="flex justify-center p-4">
                  <Loader2 className="w-6 h-6 animate-spin text-accent" />
                </div>
              ) : (
                <div className="grid grid-cols-2 gap-3">
                  {plans.map((plan) => (
                    <button
                      key={plan.id}
                      onClick={() => setSelectedPlan(plan)}
                      className={`p-3 rounded-lg border transition-all flex flex-col items-center justify-center gap-1 ${
                        selectedPlan?.id === plan.id 
                          ? 'bg-accent/20 border-accent shadow-[0_0_10px_rgba(220,38,38,0.2)] text-text' 
                          : 'bg-bg-3 border-border hover:border-text-3 text-text-2'
                      }`}
                    >
                      <span className="font-mono font-bold text-sm text-center">{plan.name}</span>
                      {plan.price > 0 && <span className="text-xs">₹{plan.price}</span>}
                      {plan.isPostpaid && <span className="text-xs text-accent">Pay as you go</span>}
                    </button>
                  ))}
                </div>
              )}
            </div>
          </div>

          <button
            onClick={handleWalkinRequest}
            disabled={isRequestingWalkin || !walkinName.trim()}
            className="w-full bg-accent hover:bg-accent-dark text-white font-semibold py-3 px-4 rounded-sm transition-all duration-200 flex items-center justify-center shadow-[0_0_15px_rgba(220,38,38,0.3)] disabled:opacity-50 disabled:cursor-not-allowed border border-accent/50 gap-2"
          >
            {isRequestingWalkin ? <Loader2 className="w-5 h-5 animate-spin" /> : (
              <>
                <MonitorPlay className="w-5 h-5" />
                <span className="font-heading text-base tracking-wider uppercase font-bold">Request Session</span>
              </>
            )}
          </button>
        </>
      )}
    </motion.div>
  );

  const renderMemberLogin = () => (
    <motion.div
      key="member_login"
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: -20 }}
      className="max-w-md w-full mx-auto card bg-bg-2/80 backdrop-blur-xl border-border/60 shadow-xl shadow-black/50 p-8 relative hover:border-accent transition-colors duration-300"
    >
      <button 
        onClick={() => setStep('selection')}
        className="absolute top-6 left-6 flex items-center gap-2 text-text-2 hover:text-text transition-colors"
      >
        <ArrowLeft className="w-5 h-5" />
      </button>

      <div className="text-center mb-8 mt-4">
        <div className="w-16 h-16 bg-accent/10 border border-accent/30 rounded-full flex items-center justify-center mx-auto mb-4 shadow-[0_0_15px_rgba(220,38,38,0.2)]">
          <UserCheck className="w-8 h-8 text-accent" />
        </div>
        <h1 className="font-heading text-3xl font-bold text-text tracking-wide uppercase">Member Login</h1>
        <p className="text-text-2 font-body mt-2 text-sm">Start a session directly from your wallet.</p>
      </div>

      <form onSubmit={handleLogin} className="space-y-6">
        <div>
          <label className="block text-sm font-medium text-text-2 mb-2 font-body tracking-wide">MOBILE NO. OR ID</label>
          <div className="relative">
            <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
              <UserCheck className="h-5 w-5 text-text-3" />
            </div>
            <input
              type="text"
              required
              value={identifier}
              onChange={(e) => setIdentifier(e.target.value)}
              className="input w-full pl-10 focus:border-accent focus:ring-accent/30"
              placeholder="e.g. 9876543210"
            />
          </div>
        </div>

        <div>
          <label className="block text-sm font-medium text-text-2 mb-2 font-body tracking-wide">PASSWORD</label>
          <div className="relative">
            <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
              <Lock className="h-5 w-5 text-text-3" />
            </div>
            <input
              type="password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="input w-full pl-10 focus:border-accent focus:ring-accent/30"
              placeholder="••••••••"
            />
          </div>
        </div>

        {loginError && (
          <motion.div 
            initial={{ opacity: 0, y: -5 }} animate={{ opacity: 1, y: 0 }}
            className="p-3 bg-neon-red/10 border border-neon-red/30 rounded flex items-start gap-2"
          >
            <AlertTriangle className="w-4 h-4 text-neon-red shrink-0 mt-0.5" />
            <p className="text-neon-red font-body text-xs font-semibold leading-relaxed">{loginError}</p>
          </motion.div>
        )}

        <button
          type="submit"
          disabled={isLoggingIn || !identifier || !password}
          className="w-full bg-accent hover:bg-accent-dark text-white font-semibold py-3 px-4 rounded-sm transition-all duration-200 flex items-center justify-center shadow-[0_0_15px_rgba(220,38,38,0.3)] disabled:opacity-50 disabled:cursor-not-allowed border border-accent/50 mt-4"
        >
          {isLoggingIn ? <Loader2 className="w-5 h-5 animate-spin" /> : <span className="font-heading text-lg tracking-wider uppercase font-bold">Login</span>}
        </button>
      </form>
    </motion.div>
  );

  const renderTimeSelection = () => (
    <motion.div
      key="time_selection"
      initial={{ opacity: 0, scale: 0.95 }}
      animate={{ opacity: 1, scale: 1 }}
      exit={{ opacity: 0 }}
      className="max-w-xl w-full mx-auto card bg-bg-2/80 backdrop-blur-xl border-border/60 shadow-xl shadow-black/50 p-6 relative"
    >
      <button 
        onClick={() => {
          setProfile(null);
          localStorage.removeItem('memberToken');
          localStorage.removeItem('memberProfile');
          setStep('member_login');
        }}
        className="absolute top-6 left-6 flex items-center gap-2 text-text-2 hover:text-text transition-colors"
      >
        <ArrowLeft className="w-5 h-5" />
      </button>

      <div className="text-center mb-8 mt-4">
        <h1 className="font-heading text-3xl font-bold text-text tracking-wide uppercase">Start Session</h1>
      </div>

      <div className="bg-bg-3 border border-border p-4 rounded-xl mb-4">
        <div className="flex items-center justify-between mb-2">
          <span className="text-text-3 font-heading uppercase tracking-widest text-xs font-bold">Member</span>
          <Wallet className="w-5 h-5 text-accent" />
        </div>
        <p className="font-bold text-text text-xl">{profile?.fullName}</p>
        <p className="text-text-2 font-mono text-sm mb-4">{profile?.memberNumber}</p>
        
        <div className="flex justify-between items-center bg-bg/50 p-4 rounded-lg border border-border/50">
          <span className="text-text-2 font-body">Gaming Balance</span>
          <span className="font-mono font-bold text-text text-2xl">₹{(profile?.gamingBalance || 0).toFixed(2)}</span>
        </div>
      </div>

      <h3 className="font-heading text-sm font-bold text-text-2 uppercase tracking-widest mb-4">Select Duration</h3>
      
      <div className="grid grid-cols-2 gap-4 mb-8">
        {plansError ? (
          <div className="col-span-2 text-center text-accent font-bold">Failed to connect to backend.</div>
        ) : plans.length === 0 ? (
          <div className="col-span-2 flex justify-center p-4">
            <Loader2 className="w-6 h-6 animate-spin text-accent" />
          </div>
        ) : (
          plans.map((plan) => (
            <button
              key={plan.id}
              onClick={() => setSelectedPlan(plan)}
              className={`p-4 rounded-lg border transition-all flex flex-col items-center justify-center gap-2 ${
                selectedPlan?.id === plan.id 
                  ? 'bg-accent/20 border-accent shadow-[0_0_15px_rgba(220,38,38,0.2)]' 
                  : 'bg-bg-3 border-border hover:border-text-3'
              }`}
            >
              <Clock className={`w-6 h-6 ${selectedPlan?.id === plan.id ? 'text-accent' : 'text-text-3'}`} />
              <span className={`font-mono font-bold text-lg ${selectedPlan?.id === plan.id ? 'text-text' : 'text-text-2'}`}>
                {plan.name}
              </span>
              {plan.price > 0 && <span className="text-xs text-text-3">₹{plan.price}</span>}
              {plan.isPostpaid && <span className="text-xs text-accent">Pay as you go</span>}
            </button>
          ))
        )}
      </div>

      <div className="bg-bg-3 p-4 rounded-xl border border-border">
        <div className="flex justify-between items-center mb-4">
          <span className="text-text-2 font-body">Estimated Cost</span>
          <span className="font-mono font-bold text-text text-2xl">
            {selectedPlan?.isPostpaid ? 'Postpaid' : `₹${(selectedPlan?.price || 0).toFixed(2)}`}
          </span>
        </div>

        <button
          onClick={handleStartSession}
          disabled={isStarting || !selectedPlan || (!selectedPlan.isPostpaid && (profile?.gamingBalance || 0) < selectedPlan.price)}
          className="w-full bg-accent hover:bg-accent-dark text-white font-semibold py-3 px-4 rounded-sm transition-all duration-200 flex items-center justify-center shadow-[0_0_15px_rgba(220,38,38,0.3)] disabled:opacity-50 disabled:cursor-not-allowed border border-accent/50 gap-2"
        >
          {isStarting ? (
            <Loader2 className="w-5 h-5 animate-spin" />
          ) : (
            <>
              <CheckCircle className="w-5 h-5" />
              <span className="font-heading text-lg tracking-wider uppercase font-bold">Start Now</span>
            </>
          )}
        </button>
        
        {!selectedPlan?.isPostpaid && (profile?.gamingBalance || 0) < (selectedPlan?.price || 0) && (
          <p className="text-neon-orange text-sm text-center mt-3 font-body">Insufficient balance for this plan.</p>
        )}
      </div>
    </motion.div>
  );

  // If session is awaiting billing, block everything and show logo
  if (sessionData?.sessionStatus === 'awaiting_billing') {
    return (
      <div className="w-full h-full bg-black/95 flex flex-col items-center justify-center p-8 text-center relative border-2 border-accent shadow-[0_0_50px_rgba(255,51,102,0.3)]">
        <img src="https://appleesports.in/apple-touch-icon.png" alt="Apple Esports" className="w-48 h-48 mb-8 animate-pulse shadow-[0_0_50px_rgba(255,51,102,0.5)] rounded-full" />
        <h1 className="text-5xl font-heading font-bold text-text uppercase tracking-[0.2em] mb-4 drop-shadow-[0_0_15px_rgba(255,51,102,0.8)]">Session Ended</h1>
        <p className="text-xl text-text-2 font-mono uppercase tracking-widest flex items-center gap-3">
            <Loader2 className="w-6 h-6 animate-spin text-neon-orange" />
            Awaiting Billing at Counter...
        </p>
      </div>
    );
  }

  return (
    <div className="fixed inset-0 z-[100] bg-bg flex items-center justify-center p-4 overflow-y-auto overflow-x-hidden min-h-screen">
      {/* Background glow effects matching LandingGatewayPage */}
      <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] bg-accent/20 rounded-full blur-[120px] pointer-events-none" />

      <div className="relative z-10 w-full py-8 my-auto">
        {walletEmpty && (
          <div className="mx-auto bg-neon-red/10 border border-neon-red/30 p-6 rounded-xl max-w-md animate-in zoom-in mb-8 text-center backdrop-blur-md shadow-[0_0_30px_rgba(255,51,102,0.2)]">
            <AlertTriangle className="w-12 h-12 text-neon-red mx-auto mb-4 animate-pulse" />
            <h2 className="font-heading text-2xl font-bold text-neon-red tracking-wide uppercase mb-2">Session Ended</h2>
            <p className="text-neon-red font-body font-bold text-lg">
              You have no balance left.<br/>Please recharge!!!
            </p>
            <button 
              onClick={() => {
                localStorage.removeItem('walletEmptyAlert');
                setWalletEmpty(false);
              }}
              className="mt-6 px-8 py-3 bg-neon-red/20 border border-neon-red/50 text-neon-red font-bold hover:bg-neon-red/30 rounded-md transition-colors uppercase tracking-widest text-sm w-full"
            >
              Acknowledge & Dismiss
            </button>
          </div>
        )}
        
        <AnimatePresence mode="wait">
          {step === 'selection' && renderSelection()}
          {step === 'walkin' && renderWalkin()}
          {step === 'member_login' && renderMemberLogin()}
          {step === 'time_selection' && renderTimeSelection()}
        </AnimatePresence>
      </div>
    </div>
  );
}
