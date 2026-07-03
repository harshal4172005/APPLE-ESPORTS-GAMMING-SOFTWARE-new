import React, { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Wallet, CheckCircle, XCircle, Loader2, KeyRound } from 'lucide-react';
import { useOverlaySocket } from '../../../contexts/OverlaySocketContext';

export default function WalletApprovalModal() {
  const { walletApprovalRequest, respondToWalletApproval } = useOverlaySocket();
  const [processing, setProcessing] = useState(false);
  const [error, setError] = useState('');
  const [isSuccess, setIsSuccess] = useState(false);

  if (!walletApprovalRequest) return null;

  const handleApprove = async () => {
    setProcessing(true);
    setError('');
    const res = await respondToWalletApproval(walletApprovalRequest.billId, true);
    if (!res?.success) {
      setError(res?.error || 'Failed to approve payment.');
      setProcessing(false);
    } else {
      setIsSuccess(true);
      // The modal will unmount automatically after 2.5s due to OverlaySocketContext
    }
  };

  const handleDecline = async () => {
    setProcessing(true);
    await respondToWalletApproval(walletApprovalRequest.billId, false);
    setProcessing(false);
  };

  return (
    <div className="fixed inset-0 z-[9999] bg-black/80 backdrop-blur-sm flex items-center justify-center p-4">
      <motion.div 
        initial={{ opacity: 0, scale: 0.9, y: 20 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        className="w-full max-w-sm bg-bg-2 border border-accent/40 shadow-[0_0_30px_rgba(255,51,102,0.2)] rounded-2xl p-6 flex flex-col items-center text-center relative overflow-hidden"
      >
        {isSuccess ? (
          <div className="flex flex-col items-center justify-center py-6">
            <CheckCircle className="w-16 h-16 text-neon-green animate-bounce mb-4 drop-shadow-[0_0_15px_rgba(34,211,166,0.6)]" />
            <h3 className="text-xl font-bold text-neon-green mb-2">Payment Successful!</h3>
            <p className="text-text-2 text-sm">Thank you. Session is ending...</p>
          </div>
        ) : (
          <>
            <div className="absolute top-0 left-0 w-full h-1 bg-gradient-to-r from-transparent via-accent to-transparent" />
            
            <div className="w-16 h-16 rounded-full bg-accent/10 border border-accent/30 flex items-center justify-center mb-4">
              <Wallet className="w-8 h-8 text-accent animate-pulse" />
            </div>
            
            <h2 className="font-heading font-bold text-2xl uppercase tracking-wider text-text mb-2">Payment Request</h2>
            <p className="text-text-2 font-body text-sm mb-6">
              The operator requested a wallet deduction of 
              <strong className="text-neon-orange text-lg ml-2 font-mono drop-shadow-[0_0_8px_rgba(255,153,0,0.5)]">
                ₹{walletApprovalRequest.amount?.toFixed(0)}
              </strong>
            </p>

            {error && <p className="text-neon-red text-xs mb-4">{error}</p>}

            <div className="flex w-full gap-3">
              <button 
                onClick={handleDecline}
                disabled={processing}
                className="flex-1 py-3 rounded-xl bg-bg-3 border border-border text-text-3 font-bold uppercase tracking-widest text-xs hover:border-neon-red/50 hover:text-neon-red hover:bg-neon-red/5 transition-all disabled:opacity-50 flex items-center justify-center gap-2"
              >
                <XCircle className="w-4 h-4" /> Decline
              </button>
              
              <button 
                onClick={handleApprove}
                disabled={processing}
                className="flex-[2] py-3 rounded-xl bg-accent border border-accent/80 text-white font-bold uppercase tracking-widest text-xs hover:bg-accent-dark shadow-[0_0_15px_rgba(255,51,102,0.4)] transition-all disabled:opacity-50 flex items-center justify-center gap-2"
              >
                {processing ? (
                  <Loader2 className="w-4 h-4 animate-spin" />
                ) : (
                  <><CheckCircle className="w-4 h-4" /> Approve</>
                )}
              </button>
            </div>
          </>
        )}
      </motion.div>
    </div>
  );
}
