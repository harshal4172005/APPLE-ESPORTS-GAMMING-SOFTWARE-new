import React, { useState, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { ShieldCheck, Eye, EyeOff } from 'lucide-react';
import { authAPI } from '../../api/auth.api';

export default function ResetPasswordPage() {
  const [searchParams] = useSearchParams();

  const [email, setEmail] = useState('');
  const [token, setToken] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);

  useEffect(() => {
    // The link now carries email/token in the URL fragment (#...), not the query string
    // (?...) - a fragment is never sent to the server at all, so it can never end up in an
    // access log the way a query string does. Read the fragment first; fall back to the query
    // string only so a reset email already sitting in someone's inbox from before this change
    // still works instead of breaking outright.
    const hashParams = new URLSearchParams(window.location.hash.replace(/^#/, ''));
    const urlEmail = hashParams.get('email') || searchParams.get('email');
    const urlToken = hashParams.get('token') || searchParams.get('token');

    if (!urlEmail || !urlToken) {
      // Try to get from sessionStorage as fallback
      const storedEmail = sessionStorage.getItem('resetEmail');
      const storedToken = sessionStorage.getItem('resetToken');

      if (!storedEmail || !storedToken) {
        setError("Invalid reset link. Missing email or token.");
        return;
      }

      setEmail(storedEmail);
      setToken(storedToken);
    } else {
      // Store in sessionStorage and clean URL
      sessionStorage.setItem('resetEmail', urlEmail);
      sessionStorage.setItem('resetToken', urlToken);
      setEmail(urlEmail);
      setToken(urlToken);

      // Remove sensitive data from URL
      window.history.replaceState({}, document.title, '/reset-password');
    }
  }, [searchParams]);

  // Cleanup on component unmount
  useEffect(() => {
    return () => {
      // Clear sessionStorage after 1 hour or on unmount
      const timeout = setTimeout(() => {
        sessionStorage.removeItem('resetEmail');
        sessionStorage.removeItem('resetToken');
      }, 3600000); // 1 hour
      return () => clearTimeout(timeout);
    };
  }, []);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');

    if (password !== confirmPassword) {
      setError("Passwords do not match.");
      return;
    }

    const passwordRegex = /^(?=.*[A-Z])(?=.*\d).{8,}$/;
    if (!passwordRegex.test(password)) {
      setError("Password must be at least 8 characters long, contain 1 uppercase letter and 1 number.");
      return;
    }

    setLoading(true);
    try {
      await authAPI.resetPassword(email, token, password);
      setSuccess(true);
      // Clear sensitive data from sessionStorage
      sessionStorage.removeItem('resetEmail');
      sessionStorage.removeItem('resetToken');
      const externalUrl = import.meta.env.VITE_EXTERNAL_REDIRECT_URL || 'https://appleesports.in/';
      setTimeout(() => window.location.href = externalUrl, 3000);
    } catch (err) {
      setError(err.response?.data?.error || "Failed to reset password. Link may be expired.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-bg flex items-center justify-center p-6 relative overflow-hidden">
      <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] bg-accent/20 rounded-full blur-[120px] pointer-events-none" />
      
      <div className="relative z-10 w-full max-w-md bg-bg-2/80 backdrop-blur-xl border border-border/60 p-8 shadow-2xl rounded-lg">
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-accent/20 mb-4">
            <ShieldCheck className="w-8 h-8 text-accent" />
          </div>
          <h2 className="font-heading text-2xl font-bold text-text uppercase tracking-wider">New Password</h2>
          <p className="text-text-2 text-sm mt-2">Secure your account with a new password.</p>
        </div>

        {success ? (
          <div className="bg-green-500/10 border border-green-500/50 text-green-500 text-sm p-4 rounded text-center">
            Password reset successfully! Redirecting to login...
          </div>
        ) : (
          <>
            {error && (
              <div className="bg-red-500/10 border border-red-500/50 text-red-500 text-sm p-3 rounded mb-6 text-center">
                {error}
              </div>
            )}

            <form onSubmit={handleSubmit} className="space-y-4">
              <div className="relative">
                <label className="block text-xs font-mono text-text-2 mb-1 uppercase tracking-wider">New Password</label>
                <input 
                  type={showPassword ? "text" : "password"}
                  required
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  className="w-full bg-bg border border-border rounded p-3 text-text focus:outline-none focus:border-accent transition-colors"
                  placeholder="Must contain 8 chars, 1 uppercase, 1 number"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  className="absolute right-3 top-9 text-text-2 hover:text-text transition-colors"
                >
                  {showPassword ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
                </button>
              </div>

              <div className="relative">
                <label className="block text-xs font-mono text-text-2 mb-1 uppercase tracking-wider">Confirm Password</label>
                <input 
                  type={showPassword ? "text" : "password"}
                  required
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  className="w-full bg-bg border border-border rounded p-3 text-text focus:outline-none focus:border-accent transition-colors"
                  placeholder="Repeat new password"
                />
              </div>

              <button
                type="submit"
                disabled={loading || !email || !token}
                className="w-full bg-accent hover:bg-red-700 text-white font-bold py-3 rounded transition-colors uppercase tracking-widest mt-6 disabled:opacity-50"
              >
                {loading ? "Resetting..." : "Save Password"}
              </button>
            </form>
          </>
        )}
      </div>
    </div>
  );
}
