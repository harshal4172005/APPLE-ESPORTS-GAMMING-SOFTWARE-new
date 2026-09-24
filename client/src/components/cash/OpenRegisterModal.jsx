import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Banknote, AlertTriangle } from 'lucide-react';
import api from '../../config/api';

export default function OpenRegisterModal({ onRegisterOpened }) {
  const [openingBalance, setOpeningBalance] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  // Set only when the operator's own count disagreed with what the last shift left and no
  // reason has been given yet — the server refused to open the drawer and handed the difference
  // back instead of opening it.
  const [mismatch, setMismatch] = useState(null);
  const [reason, setReason] = useState('');

  const handleSubmit = async () => {
    const amount = Number(openingBalance);
    if (isNaN(amount) || amount < 0) {
      setError('Please enter a valid opening balance (0 or greater).');
      return;
    }
    if (mismatch && !reason.trim()) {
      setError('Please explain the difference before continuing.');
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const { data } = await api.post('/cash/open', {
        openingBalance: amount,
        reason: mismatch ? reason.trim() : undefined,
      });
      const result = data.data;
      if (result?.opened === false) {
        setMismatch({
          expected: result.expectedBalance,
          counted: result.countedBalance,
          difference: result.difference,
        });
        return;
      }
      onRegisterOpened();
    } catch (err) {
      setError(err.response?.data?.error || err.response?.data?.message || 'Failed to open register.');
    } finally {
      setLoading(false);
    }
  };

  const handleRecount = () => {
    setMismatch(null);
    setReason('');
    setError(null);
  };

  return (
    <AnimatePresence>
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/70 backdrop-blur-md">
        <motion.div
          initial={{ opacity: 0, scale: 0.95, y: 10 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          exit={{ opacity: 0, scale: 0.95, y: 10 }}
          className="w-full max-w-sm bg-bg-2 border border-border rounded-xl shadow-2xl overflow-hidden flex flex-col"
        >
          <div className="p-4 border-b border-border bg-bg-3 flex flex-col items-center pt-6">
            <div className={`w-12 h-12 rounded-full flex items-center justify-center mb-3 ${
              mismatch ? 'bg-neon-orange/10 text-neon-orange' : 'bg-accent/10 text-accent'
            }`}>
              {mismatch ? <AlertTriangle className="w-6 h-6" /> : <Banknote className="w-6 h-6" />}
            </div>
            <h2 className="font-heading font-bold text-text uppercase tracking-wider text-lg">
              {mismatch ? "Doesn't Match" : 'Open Cash Register'}
            </h2>
            <p className="text-xs text-text-3 mt-1 text-center">
              {mismatch
                ? 'Explain the difference before the drawer opens.'
                : 'Enter the physical cash count in the drawer to start your shift.'}
            </p>
          </div>

          <div className="p-5">
            {error && (
              <div className="p-2 mb-4 bg-neon-red/10 border border-neon-red/20 rounded-lg text-neon-red text-xs flex items-start gap-2">
                <AlertTriangle className="w-3.5 h-3.5 mt-0.5 shrink-0" />
                <p>{error}</p>
              </div>
            )}

            {mismatch ? (
              <div className="space-y-3 mb-6">
                <div className="p-4 bg-bg-3 rounded-xl border border-neon-orange/30 space-y-2">
                  <div className="flex items-center justify-between text-sm">
                    <span className="text-text-3">Expected in the drawer</span>
                    <span className="font-mono font-bold text-text">₹{Number(mismatch.expected || 0).toLocaleString('en-IN')}</span>
                  </div>
                  <div className="flex items-center justify-between text-sm">
                    <span className="text-text-3">You counted</span>
                    <span className="font-mono font-bold text-text">₹{Number(mismatch.counted || 0).toLocaleString('en-IN')}</span>
                  </div>
                  <div className="flex items-center justify-between text-sm pt-2 border-t border-border">
                    <span className="text-text-3">{mismatch.difference < 0 ? 'Missing' : 'Extra'}</span>
                    <span className={`font-mono font-bold ${mismatch.difference < 0 ? 'text-neon-red' : 'text-neon-orange'}`}>
                      ₹{Math.abs(Number(mismatch.difference || 0)).toLocaleString('en-IN')}
                    </span>
                  </div>
                </div>
                <div className="space-y-2">
                  <label className="text-xs uppercase tracking-wider font-bold text-text-2">
                    Why doesn't it match?
                  </label>
                  <textarea
                    value={reason}
                    onChange={e => setReason(e.target.value)}
                    placeholder="e.g. change was given out of the drawer earlier"
                    rows={3}
                    className="w-full bg-bg-3 border border-border text-text text-sm rounded-md py-3 px-4 focus:border-accent focus:ring-1 focus:ring-accent transition-all outline-none resize-none"
                    autoFocus
                  />
                </div>
                <p className="text-[11px] text-text-3 italic">
                  This gets sent to the owner along with your name and branch.
                </p>
                <button onClick={handleRecount} className="text-xs text-text-3 hover:text-text underline">
                  Recount instead
                </button>
              </div>
            ) : (
              <div className="space-y-2 mb-6">
                <label className="text-xs uppercase tracking-wider font-bold text-text-2">
                  Opening Balance
                </label>
                <div className="relative">
                  <span className="absolute left-4 top-1/2 -translate-y-1/2 font-mono text-text-3 text-lg">₹</span>
                  <input
                    type="number"
                    min="0"
                    placeholder="0.00"
                    value={openingBalance}
                    onChange={e => setOpeningBalance(e.target.value)}
                    className="w-full bg-bg-3 border border-border text-text font-mono text-2xl rounded-md py-3 pl-10 pr-4 focus:border-accent focus:ring-1 focus:ring-accent transition-all outline-none"
                    autoFocus
                  />
                </div>
              </div>
            )}

            <button
              onClick={handleSubmit}
              disabled={loading || openingBalance === '' || (mismatch && !reason.trim())}
              className="w-full py-3.5 rounded-lg text-sm font-bold uppercase tracking-wider flex items-center justify-center gap-2 transition-all bg-accent/10 border border-accent text-accent hover:bg-accent/20 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {loading ? (
                <div className="w-5 h-5 rounded-full border-2 border-current border-t-transparent animate-spin" />
              ) : mismatch ? (
                <>✓ SUBMIT & OPEN</>
              ) : (
                <>✓ OPEN SHIFT</>
              )}
            </button>
          </div>
        </motion.div>
      </div>
    </AnimatePresence>
  );
}
