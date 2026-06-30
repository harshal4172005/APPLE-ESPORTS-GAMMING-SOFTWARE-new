import { useState, useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { Shield, User, MapPin, Loader2, KeyRound, WifiOff, Eye, EyeOff } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import api from '../../config/api';

export default function LoginPage() {
  const { loginAdmin, loginOperator, isAuthenticated, isSuperAdmin } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const reason = searchParams.get('reason');

  const [activeTab, setActiveTab] = useState('operator');
  const [branches, setBranches] = useState([]);
  const [loadingBranches, setLoadingBranches] = useState(true);
  
  // Offline State
  const [isOffline, setIsOffline] = useState(false);
  const [offlinePin, setOfflinePin] = useState('');
  
  // Form State
  const [email, setEmail] = useState('');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [selectedBranch, setSelectedBranch] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  // Redirect if already authenticated (page refresh / revisit)
  useEffect(() => {
    if (isAuthenticated && !isOffline) {
      const role = (typeof window !== 'undefined' && JSON.parse(localStorage.getItem('user') || '{}'))?.role || '';
      if (role === 'super_admin') {
        navigate('/app/dashboard', { replace: true });
      } else {
        navigate('/app/billing', { replace: true });
      }
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated, isOffline]);

  // Fetch active branches for Operator dropdown
  useEffect(() => {
    const fetchBranches = async () => {
      try {
        setLoadingBranches(true);
        setIsOffline(false);
        const res = await api.get('/auth/branches');
        setBranches(res.data?.data || []);
      } catch (err) {
        console.warn('Could not fetch branches, falling back to manual or retry logic.');
        // Detect offline mode if network error
        if (err.message === 'Network Error' || !err.response) {
          setIsOffline(true);
        }
      } finally {
        setLoadingBranches(false);
      }
    };
    fetchBranches();
  }, []);

  const handleAdminSubmit = async (e) => {
    e.preventDefault();
    if (!email || !password) {
      setError('Please enter email and password.');
      return;
    }

    try {
      setError('');
      setIsLoading(true);
      const userData = await loginAdmin(email, password);
      // Navigate based on the role in the RETURNED userData, not from context state
      // (context state may not have updated yet due to React batching)
      const role = userData?.role || userData?.Role || '';
      if (role === 'super_admin' || role.toLowerCase().includes('admin')) {
        navigate('/app/dashboard', { replace: true });
      } else {
        navigate('/app/billing', { replace: true });
      }
    } catch (err) {
      setError(err.message || 'Invalid admin credentials');
    } finally {
      setIsLoading(false);
    }
  };

  const handleOperatorSubmit = async (e) => {
    e.preventDefault();
    
    // OFFLINE LOGIN
    if (isOffline) {
      if (!offlinePin || offlinePin.length !== 4) {
        setError('Please enter your 4-digit Emergency PIN.');
        return;
      }
      setIsLoading(true);
      setTimeout(() => {
        setIsLoading(false);
        // Simulate Offline Auth Success
        navigate('/app/billing', { replace: true });
      }, 1000);
      return;
    }

    // NORMAL LOGIN
    if (!username || !password || !selectedBranch) {
      setError('Please select a branch and enter credentials.');
      return;
    }

    try {
      setError('');
      setIsLoading(true);
      await loginOperator(selectedBranch, username.trim(), password.trim());
      // Navigation handled by useEffect
    } catch (err) {
      setError(err.message || 'Invalid operator credentials');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className={`min-h-screen flex items-center justify-center p-4 overflow-hidden relative ${isOffline ? 'bg-black' : 'bg-bg'}`}>
      {/* Background glow effects */}
      <div className={`absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] rounded-full blur-[120px] pointer-events-none ${isOffline ? 'bg-red-600/20' : 'bg-accent/20'}`} />
      
      <motion.div 
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.5, ease: "easeOut" }}
        className={`card w-full max-w-md relative z-10 shadow-2xl shadow-black/50 border-border/60 backdrop-blur-xl p-8 ${isOffline ? 'bg-red-950/40 border-red-500/30' : 'bg-bg-2/80'}`}
      >
        <div className="text-center mb-8">
          <motion.img 
            initial={{ scale: 0.9 }}
            animate={{ scale: 1 }}
            transition={{ duration: 0.5 }}
            src="/logo.png" 
            alt="Apple Esports" 
            className={`h-20 w-auto mx-auto mb-4 ${isOffline ? 'drop-shadow-[0_0_15px_rgba(255,0,0,0.8)] filter grayscale sepia hue-rotate-[-50deg] saturate-200' : 'drop-shadow-[0_0_15px_rgba(220,38,38,0.5)]'}`}
          />
          <h1 className={`font-heading text-3xl font-bold mb-1 tracking-wide ${isOffline ? 'text-red-500' : 'text-text'}`}>APPLE ESPORTS</h1>
          <p className={`${isOffline ? 'text-red-400' : 'text-accent'} text-[11px] font-mono tracking-[0.2em] uppercase`}>
            {isOffline ? 'Emergency Offline Mode' : 'Enterprise ERP System'}
          </p>
        </div>

        {reason === 'forced_logout' && !isOffline && (
          <div className="mb-6 p-3 bg-neon-red/10 border border-neon-red/30 rounded text-neon-red text-xs text-center">
            Your session was terminated by an administrator.
          </div>
        )}

        <AnimatePresence mode="wait">
          {isOffline ? (
            <motion.div
              key="offline"
              initial={{ opacity: 0, x: -20 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: 20 }}
              className="space-y-6"
            >
              <div className="p-4 bg-red-500/10 border border-red-500/30 rounded-lg flex items-start gap-3">
                <WifiOff className="w-5 h-5 text-red-500 flex-shrink-0 mt-0.5" />
                <div className="text-xs text-red-200/80 leading-relaxed">
                  AWS connection lost. The system has automatically fallen back to the local Tri-State Network Engine. Enter your 4-digit Emergency PIN to manage the LAN.
                </div>
              </div>

              <form onSubmit={handleOperatorSubmit} className="space-y-4">
                <div>
                  <label className="block text-xs text-red-400/80 mb-1.5 ml-1 text-center">EMERGENCY OFFLINE PIN</label>
                  <div className="relative">
                    <input 
                      type="password" 
                      maxLength="4"
                      value={offlinePin}
                      onChange={(e) => setOfflinePin(e.target.value.replace(/\D/g, ''))}
                      className="w-full bg-black/50 border-2 border-red-500/30 rounded-lg py-4 text-center text-3xl font-mono text-red-500 tracking-[1em] focus:border-red-500 focus:outline-none transition-colors"
                      placeholder="••••"
                    />
                  </div>
                </div>

                {error && <p className="text-red-500 text-xs mt-2 text-center">{error}</p>}

                <button 
                  type="submit" 
                  disabled={isLoading}
                  className="w-full mt-6 relative overflow-hidden group flex items-center justify-center gap-2 bg-red-600 hover:bg-red-500 text-white py-3 rounded-lg font-bold tracking-widest text-sm transition-all shadow-[0_0_20px_rgba(220,38,38,0.3)] hover:shadow-[0_0_30px_rgba(220,38,38,0.5)] disabled:opacity-50"
                >
                  {isLoading ? <Loader2 className="w-5 h-5 animate-spin" /> : 'DECRYPT LAN TOKEN'}
                </button>
              </form>
            </motion.div>
          ) : (
            <motion.div
              key="online"
              initial={{ opacity: 0, x: 20 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: -20 }}
            >
              <form onSubmit={handleOperatorSubmit} className="space-y-4">
                <div>
                  <label className="block text-xs text-text-2 mb-1.5 ml-1">Branch Location</label>
                  <div className="relative">
                    <MapPin className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-text-3" />
                    <select 
                      value={selectedBranch}
                      onChange={(e) => setSelectedBranch(e.target.value)}
                      className="input w-full pl-10 appearance-none bg-bg-3"
                      disabled={loadingBranches}
                    >
                      <option value="" disabled>Select Branch...</option>
                      {branches.map(b => (
                        <option key={b.id} value={b.id}>{b.name}</option>
                      ))}
                    </select>
                    {loadingBranches && <Loader2 className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-text-3 animate-spin" />}
                  </div>
                </div>
                
                <div>
                  <label className="block text-xs text-text-2 mb-1.5 ml-1">Username</label>
                  <div className="relative">
                    <User className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-text-3" />
                    <input 
                      type="text" 
                      value={username}
                      onChange={(e) => setUsername(e.target.value)}
                      className="input w-full pl-10"
                      placeholder="Enter operator username"
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-xs text-text-2 mb-1.5 ml-1">Password</label>
                  <div className="relative">
                    <KeyRound className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-text-3" />
                    <input 
                      type={showPassword ? "text" : "password"} 
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                      className="input w-full pl-10 pr-10"
                      placeholder="••••••••"
                    />
                    <button
                      type="button"
                      onClick={() => setShowPassword(!showPassword)}
                      className="absolute right-3 top-1/2 -translate-y-1/2 text-text-3 hover:text-text transition-colors"
                      tabIndex="-1"
                    >
                      {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                    </button>
                  </div>
                  <div className="flex justify-center mt-1">
                    <button 
                      type="button" 
                      onClick={() => navigate('/forgot-password')} 
                      className="text-accent hover:text-red-400 transition-colors text-[10px] uppercase tracking-wider"
                    >
                      Forgot Password?
                    </button>
                  </div>
                </div>

                {error && <p className="text-neon-red text-xs mt-2 text-center">{error}</p>}

                <button 
                  type="submit" 
                  disabled={isLoading}
                  className="btn-primary w-full mt-6 relative overflow-hidden group flex items-center justify-center gap-2"
                >
                  {isLoading ? <Loader2 className="w-5 h-5 animate-spin" /> : 'Login as Operator'}
                </button>
              </form>
            </motion.div>
          )}
        </AnimatePresence>

        <div className="mt-8 text-center text-[10px] text-text-3 font-mono">
          <p>{isOffline ? 'LAN God-Mode Active' : 'Branch-specific Access Only'}</p>
          <p className="mt-1">Activity is monitored per SOP guidelines</p>
          {!isOffline && (
            <div className="mt-4 pt-4 border-t border-border/30">
              <button 
                onClick={() => navigate('/')} 
                className="text-accent hover:text-white transition-colors uppercase tracking-widest text-[10px]"
              >
                ← Change Role
              </button>
            </div>
          )}
          {isOffline && (
            <div className="mt-4 pt-4 border-t border-red-500/30">
              <button 
                onClick={() => setIsOffline(false)} 
                className="text-red-500 hover:text-red-400 transition-colors uppercase tracking-widest text-[10px]"
              >
                RETRY AWS CONNECTION
              </button>
            </div>
          )}
        </div>
      </motion.div>
    </div>
  );
}
