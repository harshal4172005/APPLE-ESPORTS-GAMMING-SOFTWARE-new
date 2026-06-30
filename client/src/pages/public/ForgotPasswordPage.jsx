import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Mail, ArrowLeft } from 'lucide-react';
import { authAPI } from '../../api/auth.api';

export default function ForgotPasswordPage() {
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setMessage('');
    setError('');

    try {
      await authAPI.forgotPassword(email);
      setMessage("If that email exists, a reset link has been sent. Check your inbox.");
    } catch (err) {
      setError(err.response?.data?.error || "Failed to initiate reset.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-bg flex items-center justify-center p-6 relative overflow-hidden">
      <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] bg-accent/20 rounded-full blur-[120px] pointer-events-none" />
      
      <div className="relative z-10 w-full max-w-md bg-bg-2/80 backdrop-blur-xl border border-border/60 p-8 shadow-2xl rounded-lg">
        <button 
          onClick={() => navigate(-1)}
          className="flex items-center text-text-2 hover:text-accent transition-colors mb-6 text-sm"
        >
          <ArrowLeft className="w-4 h-4 mr-2" />
          Back
        </button>

        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-accent/20 mb-4">
            <Mail className="w-8 h-8 text-accent" />
          </div>
          <h2 className="font-heading text-2xl font-bold text-text uppercase tracking-wider">Reset Password</h2>
          <p className="text-text-2 text-sm mt-2">Enter your email address to receive a reset link.</p>
        </div>

        {message && (
          <div className="bg-green-500/10 border border-green-500/50 text-green-500 text-sm p-3 rounded mb-6 text-center">
            {message}
          </div>
        )}
        {error && (
          <div className="bg-red-500/10 border border-red-500/50 text-red-500 text-sm p-3 rounded mb-6 text-center">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-6">
          <div>
            <label className="block text-xs font-mono text-text-2 mb-1 uppercase tracking-wider">Email Address</label>
            <input 
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="w-full bg-bg border border-border rounded p-3 text-text focus:outline-none focus:border-accent transition-colors"
              placeholder="operator@example.com"
            />
          </div>

          <button
            type="submit"
            disabled={loading}
            className="w-full bg-accent hover:bg-red-700 text-white font-bold py-3 rounded transition-colors uppercase tracking-widest disabled:opacity-50"
          >
            {loading ? "Sending..." : "Send Reset Link"}
          </button>
        </form>
      </div>
    </div>
  );
}
