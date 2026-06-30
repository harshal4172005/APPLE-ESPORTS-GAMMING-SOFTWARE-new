import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Banknote, AlertTriangle } from 'lucide-react';
import api from '../../config/api';

export default function OpenRegisterModal({ onRegisterOpened }) {
  const [openingBalance, setOpeningBalance] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const handleSubmit = async () => {
    const amount = Number(openingBalance);
    if (isNaN(amount) || amount < 0) {
      setError('Please enter a valid opening balance (0 or greater).');
      return;
    }

    setLoading(true);
    setError(null);
    try {
      await api.post('/cash/open', { openingBalance: amount });
      onRegisterOpened();
    } catch (err) {
      setError(err.response?.data?.error || err.response?.data?.message || 'Failed to open register.');
    } finally {
      setLoading(false);
    }
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
            <div className="w-12 h-12 bg-accent/10 text-accent rounded-full flex items-center justify-center mb-3">
              <Banknote className="w-6 h-6" />
            </div>
            <h2 className="font-heading font-bold text-text uppercase tracking-wider text-lg">
              Open Cash Register
            </h2>
            <p className="text-xs text-text-3 mt-1 text-center">
              Enter the physical cash count in the drawer to start your shift.
            </p>
          </div>

          <div className="p-5">
            {error && (
              <div className="p-2 mb-4 bg-neon-red/10 border border-neon-red/20 rounded-lg text-neon-red text-xs flex items-start gap-2">
                <AlertTriangle className="w-3.5 h-3.5 mt-0.5 shrink-0" />
                <p>{error}</p>
              </div>
            )}

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

            <button
              onClick={handleSubmit}
              disabled={loading || openingBalance === ''}
              className="w-full py-3.5 rounded-lg text-sm font-bold uppercase tracking-wider flex items-center justify-center gap-2 transition-all bg-accent/10 border border-accent text-accent hover:bg-accent/20 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {loading ? (
                <div className="w-5 h-5 rounded-full border-2 border-current border-t-transparent animate-spin" />
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
