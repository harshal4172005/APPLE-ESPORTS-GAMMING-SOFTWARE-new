import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { UserCheck, Lock, ArrowLeft, Loader2 } from 'lucide-react';
import axios from 'axios';
import { useAuth } from '../../contexts/AuthContext';
import { useToast } from '../../components/ui/Toast';
import api from '../../config/api';
import PasswordInput from '../../components/ui/PasswordInput';

export default function MemberLoginPage() {
  const navigate = useNavigate();
  const { login } = useAuth();
  const toast = useToast();
  
  const [identifier, setIdentifier] = useState('');
  const [password, setPassword] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const [isForgotPassword, setIsForgotPassword] = useState(false);
  const [forgotEmail, setForgotEmail] = useState('');
  const [forgotSuccess, setForgotSuccess] = useState(false);

  const handleForgotPassword = async (e) => {
    e.preventDefault();
    if (!forgotEmail) {
      toast.error('Please enter your email address.');
      return;
    }
    try {
      setIsLoading(true);
      await api.post('/auth/forgot-password', { email: forgotEmail });
      setForgotSuccess(true);
      toast.success('Reset link sent! Please check your inbox.');
      setTimeout(() => {
        setIsForgotPassword(false);
        setForgotSuccess(false);
        setForgotEmail('');
      }, 5000);
    } catch (error) {
      toast.error(error.response?.data?.error || 'Failed to send reset link.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!identifier || !password) return;
    
    setIsLoading(true);
    try {
      // Direct API call to the new Member login endpoint
      const response = await axios.post('/api/members/login', {
        identifier: identifier, // Username or Email — backend matches by password if shared
        password
      });

      if (response.data.success) {
        // We don't use useAuth's login because AuthContext is mainly for Staff/Operators.
        // We set the member token and profile manually here.
        localStorage.setItem('memberToken', response.data.data.token);
        localStorage.setItem('memberProfile', JSON.stringify({
          memberId: response.data.data.memberId,
          fullName: response.data.data.fullName,
          gamingBalance: response.data.data.gamingBalance,
          foodBalance: response.data.data.foodBalance,
        }));
        navigate('/user/member-portal');
      } else {
        toast.error(response.data.error || 'Login failed');
      }
    } catch (error) {
      toast.error(error.response?.data?.error || 'Invalid credentials');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center p-6 relative overflow-hidden bg-bg">
      <div className="absolute inset-0 z-0 bg-[radial-gradient(ellipse_at_bottom,_var(--tw-gradient-stops))] from-neon-secondary/15 via-bg to-bg" />

      <div className="relative z-10 w-full max-w-md">
        <button 
          onClick={() => navigate('/user/select')}
          className="absolute -top-12 left-0 flex items-center gap-2 text-text-muted hover:text-white transition-colors"
        >
          <ArrowLeft className="w-5 h-5" />
          <span className="font-outfit text-sm">Back</span>
        </button>

        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          className="glass-panel p-8"
        >
          <div className="text-center mb-8">
            <div className="w-16 h-16 bg-neon-secondary/10 border border-neon-secondary/30 rounded-full flex items-center justify-center mx-auto mb-4 shadow-[0_0_15px_rgba(176,38,255,0.2)]">
              <UserCheck className="w-8 h-8 text-neon-secondary" />
            </div>
            <h1 className="font-outfit text-3xl font-bold text-white tracking-wide">Member Login</h1>
            <p className="text-text-muted font-inter mt-2 text-sm">Access your wallet and start sessions directly.</p>
          </div>

          {isForgotPassword ? (
            <motion.form 
              initial={{ opacity: 0, scale: 0.95 }}
              animate={{ opacity: 1, scale: 1 }}
              onSubmit={handleForgotPassword} 
              className="space-y-6"
            >
              <div className="text-center mb-4">
                <p className="text-text-muted text-sm">Enter your account email to receive a reset link.</p>
              </div>

              {forgotSuccess ? (
                <div className="bg-green-500/10 border border-green-500/30 text-green-500 text-sm p-4 rounded text-center">
                  Link sent! Please check your inbox.
                </div>
              ) : (
                <div>
                  <label className="block text-sm font-medium text-text-muted mb-2 font-inter">Account Email</label>
                  <div className="relative">
                    <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                      <UserCheck className="h-5 w-5 text-text-muted" />
                    </div>
                    <input
                      type="email"
                      required
                      value={forgotEmail}
                      onChange={(e) => setForgotEmail(e.target.value)}
                      className="input w-full pl-10 bg-black/20 border-glass-border focus:border-neon-secondary/50 focus:ring-neon-secondary/30"
                      placeholder="member@example.com"
                    />
                  </div>
                </div>
              )}

              {!forgotSuccess && (
                <button
                  type="submit"
                  disabled={isLoading || !forgotEmail}
                  className="w-full bg-neon-secondary hover:bg-neon-secondary/80 text-white font-semibold py-3 px-4 rounded-lg transition-all duration-200 flex items-center justify-center shadow-[0_0_15px_rgba(176,38,255,0.3)] disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {isLoading ? (
                    <Loader2 className="w-5 h-5 animate-spin" />
                  ) : (
                    <span className="font-outfit text-lg tracking-wide">Send Reset Link</span>
                  )}
                </button>
              )}

              <div className="text-center mt-4">
                <button 
                  type="button" 
                  onClick={() => setIsForgotPassword(false)}
                  className="text-text-muted hover:text-white text-sm transition-colors"
                >
                  Back to Login
                </button>
              </div>
            </motion.form>
          ) : (
            <form onSubmit={handleSubmit} className="space-y-6">
              <div>
                <label className="block text-sm font-medium text-text-muted mb-2 font-inter">Username or Email</label>
              <div className="relative">
                <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                  <UserCheck className="h-5 w-5 text-text-muted" />
                </div>
                <input
                  type="text"
                  required
                  value={identifier}
                  onChange={(e) => setIdentifier(e.target.value)}
                  className="input w-full pl-10 bg-black/20 border-glass-border focus:border-neon-secondary/50 focus:ring-neon-secondary/30"
                  placeholder="e.g. rahul123 or member@example.com"
                />
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-text-muted mb-2 font-inter">Password</label>
              <div className="relative">
                <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                  <Lock className="h-5 w-5 text-text-muted" />
                </div>
                <PasswordInput
                  required
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  className="input w-full pl-10 bg-black/20 border-glass-border focus:border-neon-secondary/50 focus:ring-neon-secondary/30"
                  placeholder="••••••••"
                />
              </div>
              <div className="text-right mt-2">
                <button 
                  type="button" 
                  onClick={() => setIsForgotPassword(true)}
                  className="text-sm text-text-muted hover:text-neon-secondary transition-colors"
                >
                  Forgot Password?
                </button>
              </div>
            </div>

            <button
              type="submit"
              disabled={isLoading || !identifier || !password}
              className="w-full bg-neon-secondary hover:bg-neon-secondary/80 text-white font-semibold py-3 px-4 rounded-lg transition-all duration-200 flex items-center justify-center shadow-[0_0_15px_rgba(176,38,255,0.3)] disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {isLoading ? (
                <Loader2 className="w-5 h-5 animate-spin" />
              ) : (
                <span className="font-outfit text-lg tracking-wide">Enter Portal</span>
              )}
            </button>
          </form>
          )}
        </motion.div>
      </div>
    </div>
  );
}
