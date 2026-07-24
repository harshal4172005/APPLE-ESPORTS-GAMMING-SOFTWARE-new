import React, { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { UserCheck, Lock, Loader2, ArrowLeft } from 'lucide-react';
import axios from 'axios';
import { useToast } from '../../../components/ui/Toast';
import PasswordInput from '../../../components/ui/PasswordInput';

export default function OverlayMemberLoginScreen() {
  const { pcId } = useParams();
  const navigate = useNavigate();
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
      await axios.post('/api/auth/forgot-password', { email: forgotEmail });
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
      const res = await axios.post('/api/members/login', {
        identifier: identifier,
        password,
      });

      if (res.data.success) {
        const data = res.data.data;
        // Save token and profile
        localStorage.setItem('memberToken', data.token);
        localStorage.setItem('memberProfile', JSON.stringify({
          memberId: data.memberId,
          memberNumber: data.memberNumber,
          fullName: data.fullName,
          gamingBalance: data.gamingBalance,
          foodBalance: data.foodBalance,
        }));
        
        // Auto-start postpaid session directly
        try {
          const pcRes = await axios.get(`/api/public/pcs/${pcId}`);
          const actualPcId = pcRes.data.success ? pcRes.data.data.id : pcId;
          const branchId = pcRes.data.success ? pcRes.data.data.branchId : null;

          const sessionRes = await axios.post('/api/public/sessions/member-start', {
            pcId: actualPcId,
            memberId: data.memberId,
            customerName: data.fullName,
            durationMinutes: 0,
            packageName: 'Postpaid',
            expectedAmount: 0
          }, {
            headers: {
              Authorization: `Bearer ${data.token}`,
              ...(branchId && { 'X-Branch-Id': branchId })
            }
          });

          if (sessionRes.data.success) {
            toast.success(`Welcome back, ${data.fullName}! Postpaid session started.`);
            navigate(`/pc-overlay/${pcId}/`);
          } else {
            toast.error(sessionRes.data.error || 'Failed to start session automatically.');
          }
        } catch (err) {
          toast.error(err.response?.data?.error || err.response?.data?.message || 'Failed to start session automatically.');
        }

      } else {
        toast.error(res.data.error || 'Login failed');
      }
    } catch (err) {
      const msg = err.response?.data?.error || err.response?.data?.message || 'Invalid credentials';
      toast.error(msg);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="h-full flex flex-col p-6 overflow-y-auto">
      <div className="flex items-center gap-4 mb-8">
        <button 
          onClick={() => navigate(`/pc-overlay/${pcId}/`)}
          className="text-text-3 hover:text-text transition-colors"
        >
          <ArrowLeft className="w-5 h-5" />
        </button>
        <h2 className="font-heading text-xl font-bold text-text uppercase tracking-wider">Member Login</h2>
      </div>

      <div className="flex-1 flex flex-col">
        <div className="text-center mb-6">
          <div className="w-12 h-12 bg-accent/10 border border-accent/30 rounded-full flex items-center justify-center mx-auto mb-3 shadow-[0_0_15px_rgba(220,38,38,0.2)]">
            <UserCheck className="w-6 h-6 text-accent" />
          </div>
          <p className="text-text-2 font-body text-xs">Login to start a session directly from your wallet.</p>
        </div>

        {isForgotPassword ? (
          <form onSubmit={handleForgotPassword} className="space-y-4">
            <div className="text-center mb-4">
              <p className="text-text-2 text-xs">Enter your account email to receive a reset link.</p>
            </div>

            {forgotSuccess ? (
              <div className="bg-green-500/10 border border-green-500/30 text-green-500 text-xs p-3 rounded text-center">
                Link sent! Please check your inbox.
              </div>
            ) : (
              <div>
                <label className="block text-xs font-medium text-text-2 mb-1.5 font-body tracking-wide">ACCOUNT EMAIL</label>
                <div className="relative">
                  <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                    <UserCheck className="h-4 w-4 text-text-3" />
                  </div>
                  <input
                    type="email"
                    required
                    value={forgotEmail}
                    onChange={(e) => setForgotEmail(e.target.value)}
                    className="input w-full pl-9 py-2 text-sm focus:border-accent focus:ring-accent/30 bg-bg-3"
                    placeholder="member@example.com"
                  />
                </div>
              </div>
            )}

            {!forgotSuccess && (
              <button
                type="submit"
                disabled={isLoading || !forgotEmail}
                className="w-full bg-accent hover:bg-accent-dark text-white font-semibold py-2.5 px-4 rounded-sm transition-all duration-200 flex items-center justify-center mt-6 shadow-[0_0_15px_rgba(220,38,38,0.3)] disabled:opacity-50 disabled:cursor-not-allowed border border-accent/50"
              >
                {isLoading ? (
                  <Loader2 className="w-4 h-4 animate-spin" />
                ) : (
                  <span className="font-heading text-sm tracking-wider uppercase font-bold">Send Reset Link</span>
                )}
              </button>
            )}

            <div className="text-center mt-4">
              <button 
                type="button" 
                onClick={() => setIsForgotPassword(false)}
                className="text-text-3 hover:text-white text-xs transition-colors"
              >
                Back to Login
              </button>
            </div>
          </form>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="block text-xs font-medium text-text-2 mb-1.5 font-body tracking-wide">USERNAME OR EMAIL</label>
            <div className="relative">
              <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                <UserCheck className="h-4 w-4 text-text-3" />
              </div>
              <input
                type="text"
                required
                value={identifier}
                onChange={(e) => setIdentifier(e.target.value)}
                className="input w-full pl-9 py-2 text-sm focus:border-accent focus:ring-accent/30 bg-bg-3"
                placeholder="e.g. rahul123 or member@example.com"
              />
            </div>
          </div>

          <div>
            <label className="block text-xs font-medium text-text-2 mb-1.5 font-body tracking-wide">PASSWORD</label>
            <div className="relative">
              <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                <Lock className="h-4 w-4 text-text-3" />
              </div>
              <PasswordInput
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="input w-full pl-9 py-2 text-sm focus:border-accent focus:ring-accent/30 bg-bg-3"
                placeholder="••••••••"
              />
            </div>
            <div className="text-right mt-1.5">
              <button 
                type="button" 
                onClick={() => setIsForgotPassword(true)}
                className="text-xs text-text-3 hover:text-accent transition-colors"
              >
                Forgot Password?
              </button>
            </div>
          </div>

          <button
            type="submit"
            disabled={isLoading || !identifier || !password}
            className="w-full bg-accent hover:bg-accent-dark text-white font-semibold py-2.5 px-4 rounded-sm transition-all duration-200 flex items-center justify-center mt-6 shadow-[0_0_15px_rgba(220,38,38,0.3)] disabled:opacity-50 disabled:cursor-not-allowed border border-accent/50"
          >
            {isLoading ? (
              <Loader2 className="w-4 h-4 animate-spin" />
            ) : (
              <span className="font-heading text-sm tracking-wider uppercase font-bold">Login & Continue</span>
            )}
          </button>
        </form>
        )}
      </div>
    </div>
  );
}
