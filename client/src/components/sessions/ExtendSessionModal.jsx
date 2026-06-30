import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { RefreshCw, X, Clock } from 'lucide-react';
import api from '../../config/api';
import { useToast } from '../../components/ui/Toast';

export default function ExtendSessionModal({ pc, onClose, onActionSuccess }) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const toast = useToast();

  const [durationMinutes, setDurationMinutes] = useState(60);

  if (!pc) return null;

  const handleExtend = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    try {
      const ratePerHour = pc.ratePerHour || 0;
      const additionalAmount = (durationMinutes / 60) * ratePerHour;
      
      await api.post(`/sessions/${pc.activeSessionId}/extend`, {
        additionalMinutes: durationMinutes,
        additionalAmount: additionalAmount,
        packageName: `Extension - ${durationMinutes}m`
      });
      toast.success(`Successfully extended session by ${durationMinutes} mins!`);
      onActionSuccess?.();
    } catch (err) {
      setError(err.response?.data?.error || err.response?.data?.message || 'Failed to extend session');
    } finally {
      setLoading(false);
    }
  };

  return (
    <AnimatePresence>
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
        <motion.div
          initial={{ opacity: 0, scale: 0.96, y: 10 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          exit={{ opacity: 0, scale: 0.96, y: 10 }}
          className="w-full max-w-sm bg-bg-2 border border-border rounded-xl shadow-2xl overflow-hidden"
        >
          {/* Header */}
          <div className="px-5 py-4 border-b border-border bg-bg-3 flex items-center justify-between">
            <div>
              <h2 className="font-heading font-bold text-text uppercase tracking-wider text-base flex items-center gap-2">
                <RefreshCw className="w-4 h-4 text-neon-blue" />
                Extend Session — {pc.name}
              </h2>
              <p className="text-text-3 text-[10px] font-mono mt-0.5">
                Current charge rate: ₹{pc.ratePerHour}/hr
              </p>
            </div>
            <button onClick={onClose} className="p-1 text-text-3 hover:text-text rounded transition-colors">
              <X className="w-5 h-5" />
            </button>
          </div>

          <form onSubmit={handleExtend} className="p-5 space-y-4">
            {error && (
              <div className="p-3 bg-neon-red/10 border border-neon-red/20 rounded text-neon-red text-xs">
                {error}
              </div>
            )}

            {/* Duration Selector */}
            <div className="space-y-1.5">
              <label className="text-[10px] font-mono font-semibold text-text-2 uppercase tracking-wider flex items-center gap-1">
                <Clock className="w-3 h-3" /> Additional Time
              </label>
              <div className="grid grid-cols-4 gap-1.5">
                {[30, 60, 120, 180].map(min => {
                  const label = min < 60 ? `${min}m` : (min === 60 ? '1 Hr' : `${min / 60} Hrs`);
                  return (
                    <button
                      key={min}
                      type="button"
                      onClick={() => setDurationMinutes(min)}
                      className={`py-2 rounded border text-xs font-semibold transition-colors ${
                        durationMinutes === min
                          ? 'border-neon-blue bg-neon-blue/10 text-neon-blue'
                          : 'border-border bg-bg-3 text-text-2 hover:border-border-2 hover:text-text'
                      }`}
                    >
                      {label}
                    </button>
                  );
                })}
              </div>
            </div>
            
            {/* Charge Preview */}
            <div className="bg-bg-3 rounded border border-border p-3 flex justify-between items-center">
              <span className="text-xs text-text-2 font-mono uppercase">Additional Charge</span>
              <span className="font-bold text-neon-orange font-mono">
                ₹{Math.ceil((durationMinutes / 60) * (pc.ratePerHour || 0))}
              </span>
            </div>

            {/* Actions */}
            <div className="pt-2 flex gap-2">
              <button
                type="button"
                onClick={onClose}
                className="flex-1 py-2 rounded border border-border text-text-2 hover:bg-bg-3 text-xs font-bold uppercase tracking-wider transition-colors"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={loading}
                className="flex-1 py-2 rounded bg-neon-blue text-black hover:bg-neon-blue/90 text-xs font-bold uppercase tracking-wider transition-colors disabled:opacity-50 flex justify-center items-center"
              >
                {loading ? <span className="w-4 h-4 border-2 border-black/30 border-t-black rounded-full animate-spin" /> : 'Confirm'}
              </button>
            </div>
          </form>
        </motion.div>
      </div>
    </AnimatePresence>
  );
}
