import { useState, useEffect, useMemo } from 'react';
import { createPortal } from 'react-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { RefreshCw, X, Clock } from 'lucide-react';
import api from '../../config/api';
import { useToast } from '../../components/ui/Toast';
import { logActivity } from '../../utils/sessionLog';

export default function ExtendSessionModal({ pc, onClose, onActionSuccess }) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const toast = useToast();

  const [durationMinutes, setDurationMinutes] = useState(60);

  // The branch's real plans for this PC - a 4-hour extension has to cost what a 4-hour plan
  // actually costs (Rs 180), not (4 x hourly rate). Fetched once per PC rather than assumed
  // from ratePerHour alone, the same plans a customer's own session-start screen shows, so this
  // preview can never say a different number than what the operator is about to actually charge.
  const [plans, setPlans] = useState(null);
  useEffect(() => {
    setPlans(null);
    if (!pc?.id) return;
    let alive = true;
    api.get(`/public/pcs/${pc.id}/plans`)
      .then(({ data }) => { if (alive && data?.success) setPlans(data.data); })
      .catch(() => { /* falls back to the hourly-rate estimate below */ });
    return () => { alive = false; };
  }, [pc?.id]);

  // Exact-duration package wins, same rule the server itself now applies
  // (SessionService.ExtendSessionAsync) - this is only ever a preview of that, never the
  // authority on what gets charged.
  const matchedPlan = useMemo(
    () => plans?.find(p => p.duration === durationMinutes && !p.isPostpaid),
    [plans, durationMinutes]
  );
  const additionalAmount = useMemo(() => {
    if (matchedPlan) return matchedPlan.price;
    return (durationMinutes / 60) * (pc?.ratePerHour || 0);
  }, [matchedPlan, durationMinutes, pc?.ratePerHour]);

  // The branch's own real packages, not a fixed 30/60/120/180 guess - a profile whose actual
  // packages are, say, 30/60/240/"Full Day" had its 4-hour and Full-Day plans completely
  // unreachable from this modal, and picking the closest hardcoded button (180m) silently
  // billed hourly with nothing on screen saying so. Falls back to the old fixed list only if
  // this PC's plans haven't loaded (or genuinely has none), so the picker is never empty.
  const presetMinutes = useMemo(() => {
    const real = (plans || [])
      .filter(p => !p.isPostpaid && p.duration > 0)
      .map(p => p.duration);
    return real.length > 0 ? Array.from(new Set(real)).sort((a, b) => a - b) : [30, 60, 120, 180];
  }, [plans]);

  useEffect(() => {
    // Once the real packages are in, land on one of them rather than leaving the picker
    // sitting on a duration (from the old hardcoded default) that may not even be one of
    // this profile's actual plans.
    if (presetMinutes.length > 0 && !presetMinutes.includes(durationMinutes)) {
      setDurationMinutes(presetMinutes[0]);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [presetMinutes]);

  if (!pc) return null;

  const handleExtend = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    try {
      await api.post(`/sessions/${pc.activeSessionId}/extend`, {
        additionalMinutes: durationMinutes,
        additionalAmount: additionalAmount,
        packageName: `Extension - ${durationMinutes}m`
      });
      toast.success(`Successfully extended session by ${durationMinutes} mins!`);
      logActivity(`${pc.name}: Session extended by ${durationMinutes} mins. Additional charge: ₹${Math.ceil(additionalAmount)}`, 'success');
      onActionSuccess?.();
    } catch (err) {
      setError(err.response?.data?.error || err.response?.data?.message || 'Failed to extend session');
    } finally {
      setLoading(false);
    }
  };

  return createPortal(
    <AnimatePresence>
      <div className="fixed inset-0 z-[9999] flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
        <motion.div
          initial={{ opacity: 0, scale: 0.96, y: 10 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          exit={{ opacity: 0, scale: 0.96, y: 10 }}
          className="w-full max-w-md bg-bg-2 border border-border rounded-xl shadow-2xl overflow-hidden"
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
            <div className="space-y-3">
              <label className="text-xs font-mono font-bold text-text-2 uppercase tracking-widest">Select Time to Extend</label>
              <div className="grid grid-cols-2 gap-2.5">
                {presetMinutes.map(min => {
                  const label = min < 60 ? `${min}m` : (min % 60 === 0 ? `${min / 60} Hour${min === 60 ? '' : 's'}` : `${min}m`);
                  return (
                    <button
                      key={min}
                      type="button"
                      onClick={() => setDurationMinutes(min)}
                      className={`w-full py-3 px-2 rounded-lg border-2 font-bold text-sm uppercase tracking-wide transition-all duration-200 ${
                        durationMinutes === min
                          ? 'border-neon-blue bg-neon-blue/25 text-neon-blue shadow-[0_0_12px_rgba(30,144,255,0.4)]'
                          : 'border-border bg-bg-3 text-text-2 hover:border-neon-blue/60 hover:bg-bg-2'
                      }`}
                    >
                      {label}
                    </button>
                  );
                })}
              </div>
            </div>
            
            {/* Charge Preview */}
            <div className="bg-bg-3 rounded border border-border p-4 flex justify-between items-center gap-4">
              <div>
                <span className="text-xs text-text-2 font-mono uppercase tracking-wider block">Additional Charge</span>
                {/* No package covers this exact duration - said plainly rather than silently
                    billing hourly with nothing on screen to show it. */}
                {!matchedPlan && (
                  <span className="text-[10px] text-neon-orange font-mono uppercase tracking-wider">
                    Custom / Hourly rate
                  </span>
                )}
              </div>
              <span className="font-bold text-neon-orange font-mono text-lg">
                ₹{Math.ceil(additionalAmount)}
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
    </AnimatePresence>,
    document.body
  );
}
