import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { X, Tag, Percent, Minus, AlertTriangle } from 'lucide-react';
import { applyDiscount } from '../../api/billing.api';
import { useToast } from '../ui/Toast';

/**
 * DiscountModal — SuperAdmin-only discount application (SOP §9.6)
 * Props: bill, onClose, onSuccess
 */
export default function DiscountModal({ bill, onClose, onSuccess }) {
  const toast = useToast();
  const [discountType, setDiscountType] = useState('Percentage'); // 'Percentage' | 'Flat'
  const [discountValue, setDiscountValue] = useState('');
  const [reason, setReason] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  if (!bill) return null;

  const numericValue = parseFloat(discountValue) || 0;

  // Preview calculation
  const previewDiscount = discountType === 'Percentage'
    ? (bill.subtotal * numericValue) / 100
    : numericValue;
  const previewTotal = Math.max(0, bill.subtotal - previewDiscount);

  const isValid = numericValue > 0
    && reason.trim().length >= 3
    && previewDiscount <= bill.subtotal
    && (discountType === 'Flat' || numericValue <= 100);

  const handleApply = async () => {
    setError(null);
    setLoading(true);
    try {
      await applyDiscount(bill.id, {
        discountType,
        discountValue: numericValue,
        reason: reason.trim(),
      });
      toast.success(`Discount applied — new total ₹${previewTotal.toFixed(0)}`);
      onSuccess?.();
      onClose();
    } catch (err) {
      setError(err.response?.data?.error || err.response?.data?.error || err.response?.data?.message || 'Failed to apply discount');
    } finally {
      setLoading(false);
    }
  };

  return (
    <AnimatePresence>
      <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm">
        <motion.div
          initial={{ opacity: 0, scale: 0.95, y: 8 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          exit={{ opacity: 0, scale: 0.95, y: 8 }}
          className="w-full max-w-md bg-bg-2 border border-border rounded-xl shadow-2xl overflow-hidden"
        >
          {/* Header */}
          <div className="p-4 border-b border-border bg-bg-3 flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Tag className="w-5 h-5 text-neon-purple" />
              <h2 className="font-heading font-bold text-text uppercase tracking-wider">Apply Discount</h2>
            </div>
            <button onClick={onClose} className="p-1 text-text-3 hover:text-text rounded transition-colors">
              <X className="w-5 h-5" />
            </button>
          </div>

          <div className="p-5 space-y-4">
            {error && (
              <div className="flex items-start gap-2 p-3 bg-neon-red/10 border border-neon-red/20 rounded-lg text-neon-red text-sm">
                <AlertTriangle className="w-4 h-4 mt-0.5 shrink-0" />
                <p>{error}</p>
              </div>
            )}

            {/* Current subtotal */}
            <div className="flex justify-between items-center text-sm text-text-2 bg-bg-3 p-3 rounded-lg border border-border">
              <span className="uppercase tracking-wider font-bold text-xs">Current Subtotal</span>
              <span className="font-mono text-text font-bold text-lg">₹{bill.subtotal}</span>
            </div>

            {/* Discount type toggle */}
            <div>
              <label className="block text-[10px] text-text-3 uppercase tracking-widest font-bold mb-2">Discount Type</label>
              <div className="grid grid-cols-2 gap-2">
                {['Percentage', 'Flat'].map(type => (
                  <button
                    key={type}
                    type="button"
                    onClick={() => { setDiscountType(type); setDiscountValue(''); }}
                    className={`py-2.5 rounded-lg border text-sm font-bold uppercase tracking-wider flex items-center justify-center gap-1.5 transition-colors ${
                      discountType === type
                        ? 'bg-neon-purple/15 border-neon-purple/60 text-neon-purple'
                        : 'bg-bg-3 border-border text-text-2 hover:border-neon-purple/30'
                    }`}
                  >
                    {type === 'Percentage' ? <Percent className="w-3.5 h-3.5" /> : <Minus className="w-3.5 h-3.5" />}
                    {type === 'Percentage' ? '% Off' : 'Flat ₹'}
                  </button>
                ))}
              </div>
            </div>

            {/* Value input */}
            <div>
              <label className="block text-[10px] text-text-3 uppercase tracking-widest font-bold mb-2">
                {discountType === 'Percentage' ? 'Percentage (0–100)' : 'Amount (₹)'}
              </label>
              <div className="relative">
                <input
                  type="number"
                  min="0"
                  max={discountType === 'Percentage' ? 100 : bill.subtotal}
                  step="1"
                  value={discountValue}
                  onChange={e => setDiscountValue(e.target.value)}
                  placeholder={discountType === 'Percentage' ? 'e.g. 10' : 'e.g. 50'}
                  className="w-full bg-bg-3 border border-border text-text font-mono text-xl rounded-lg p-3 pr-10 focus:border-neon-purple focus:ring-1 focus:ring-neon-purple transition-all"
                />
                <span className="absolute right-3 top-1/2 -translate-y-1/2 text-text-3 font-bold text-sm">
                  {discountType === 'Percentage' ? '%' : '₹'}
                </span>
              </div>
            </div>

            {/* Reason */}
            <div>
              <label className="block text-[10px] text-text-3 uppercase tracking-widest font-bold mb-2">Reason *</label>
              <input
                type="text"
                value={reason}
                onChange={e => setReason(e.target.value)}
                placeholder="e.g. Family/Friend discount, loyalty reward..."
                className="w-full bg-bg-3 border border-border text-text rounded-lg p-3 text-sm focus:border-neon-purple focus:ring-1 focus:ring-neon-purple transition-all"
              />
            </div>

            {/* Live preview */}
            {numericValue > 0 && previewDiscount <= bill.subtotal && (
              <div className="bg-neon-purple/5 border border-neon-purple/20 rounded-lg p-3 space-y-1.5 text-sm">
                <div className="flex justify-between text-text-2">
                  <span>Subtotal</span>
                  <span className="font-mono">₹{bill.subtotal}</span>
                </div>
                <div className="flex justify-between text-neon-purple font-medium">
                  <span>Discount ({discountType === 'Percentage' ? `${numericValue}%` : `₹${numericValue}`})</span>
                  <span className="font-mono">−₹{previewDiscount.toFixed(0)}</span>
                </div>
                <div className="flex justify-between font-bold text-text border-t border-border/50 pt-1.5 text-base">
                  <span>New Total</span>
                  <span className="font-mono text-neon-purple">₹{previewTotal.toFixed(0)}</span>
                </div>
              </div>
            )}
          </div>

          {/* Footer */}
          <div className="p-4 border-t border-border bg-bg-3 flex gap-3">
            <button
              onClick={onClose}
              className="flex-1 py-2.5 rounded-lg border border-border text-text-2 text-sm font-bold uppercase tracking-wider hover:bg-bg-2 transition-colors"
            >
              Cancel
            </button>
            <button
              onClick={handleApply}
              disabled={!isValid || loading}
              className={`flex-[2] py-2.5 rounded-lg border text-sm font-bold uppercase tracking-wider flex items-center justify-center gap-2 transition-all ${
                isValid && !loading
                  ? 'bg-neon-purple/10 border-neon-purple/50 text-neon-purple hover:bg-neon-purple/20'
                  : 'bg-bg-2 border-border text-text-3 cursor-not-allowed'
              }`}
            >
              {loading
                ? <div className="w-4 h-4 rounded-full border-2 border-current border-t-transparent animate-spin" />
                : <><Tag className="w-4 h-4" /> Apply Discount</>
              }
            </button>
          </div>
        </motion.div>
      </div>
    </AnimatePresence>
  );
}
