import { useState, useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { Shield, User, MapPin, Loader2, KeyRound, Eye, EyeOff } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import api from '../../config/api';

export default function LoginPage() {
  const { loginAdmin, loginOperator, isAuthenticated, isSuperAdmin } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const reason = searchParams.get('reason');

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  // Redirect if already authenticated (page refresh / revisit)
  useEffect(() => {
    if (isAuthenticated) {
      const role = (typeof window !== 'undefined' && JSON.parse(localStorage.getItem('user') || '{}'))?.role || '';
      if (role === 'super_admin' || role.toLowerCase().includes('admin')) {
        navigate('/app/sessions', { replace: true });
      } else {
        navigate('/app/sessions', { replace: true });
      }
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated]);

  const handleAdminSubmit = async (e) => {
    e.preventDefault();
    if (!email || !password) {
      setError('Please enter email and password.');
      return;
    }

    try {
      setError('');
      setIsLoading(true);
      await loginAdmin(email, password);
      // Navigation is now handled by the useEffect above once AuthContext updates
    } catch (err) {
      setError(err.message || 'Invalid admin credentials');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-bg flex items-center justify-center p-4 overflow-hidden relative">
      {/* Background glow effects */}
      <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] bg-accent/20 rounded-full blur-[120px] pointer-events-none" />
      
      <motion.div 
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.5, ease: "easeOut" }}
        className="card w-full max-w-md relative z-10 shadow-2xl shadow-black/50 border-border/60 bg-bg-2/80 backdrop-blur-xl p-8"
      >
        <div className="text-center mb-8">
          <motion.img 
            initial={{ scale: 0.9 }}
            animate={{ scale: 1 }}
            transition={{ duration: 0.5 }}
            src="/logo.png" 
            alt="Apple Esports" 
            className="h-20 w-auto mx-auto mb-4 drop-shadow-[0_0_15px_rgba(220,38,38,0.5)]"
          />
          <h1 className="font-heading text-3xl font-bold text-text mb-1 tracking-wide">SUPER ADMIN LOGIN</h1>
          <p className="text-accent text-[11px] font-mono tracking-[0.2em] uppercase">Enterprise ERP System</p>
        </div>

        {reason === 'forced_logout' && (
          <div className="mb-6 p-3 bg-neon-red/10 border border-neon-red/30 rounded text-neon-red text-xs text-center">
            Your session was terminated by an administrator.
          </div>
        )}

        <form onSubmit={handleAdminSubmit} className="space-y-4">
          <div>
            <label className="block text-xs text-text-2 mb-1.5 ml-1">Email Address</label>
            <div className="relative">
              <User className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-text-3" />
              <input 
                type="email" 
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="input w-full pl-10"
                placeholder="admin@appleesports.com"
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
            className="btn-primary w-full mt-6 flex items-center justify-center gap-2 shadow-[0_0_15px_rgba(220,38,38,0.3)]"
          >
            {isLoading ? <Loader2 className="w-5 h-5 animate-spin" /> : 'Admin Access'}
          </button>
        </form>

        <div className="mt-8 text-center text-[10px] text-text-3 font-mono">
          <p>Strictly Authorized Personnel Only</p>
          <p className="mt-1">All actions are recorded securely per SOP</p>
          <div className="mt-4 pt-4 border-t border-border/30">
            <button 
              onClick={() => navigate('/')} 
              className="text-accent hover:text-white transition-colors uppercase tracking-widest text-[10px]"
            >
              ← Change Role
            </button>
          </div>
        </div>
      </motion.div>
    </div>
  );
}
