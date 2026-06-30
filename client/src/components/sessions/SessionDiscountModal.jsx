import React, { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { X, Tag } from 'lucide-react';
import { useToast } from '../ui/Toast';
import api from '../../config/api';

const DISCOUNT_OPTIONS = [
  { label: 'NONE', value: 0, type: 'Percentage' },
  { label: '5% OFF', value: 5, type: 'Percentage' },
  { label: '10% OFF', value: 10, type: 'Percentage' },
  { label: '15% OFF', value: 15, type: 'Percentage' },
  { label: '20% OFF', value: 20, type: 'Percentage' },
];

export default function SessionDiscountModal({ isOpen, onClose, pc, onRefresh }) {
  const [selectedDiscount, setSelectedDiscount] = useState(0);
  const [loading, setLoading] = useState(false);
  const toast = useToast();

  if (!isOpen || !pc) return null;

  const handleApply = async () => {
    if (!pc.activeBillId) {
      toast.error('No active bill found for this session.');
      return;
    }

    setLoading(true);
    try {
      const opt = DISCOUNT_OPTIONS.find(o => o.value === selectedDiscount);
      await api.post(`/bills/${pc.activeBillId}/discount`, {
        discountType: opt.type,
        discountValue: opt.value,
        reason: 'Session Discount applied by Admin'
      });
      toast.success(`Discount ${opt.label !== 'NONE' ? opt.label : 'removed'} applied successfully!`);
      onRefresh?.();
      onClose();
    } catch (err) {
      console.error('Failed to apply discount', err);
      toast.error(err.response?.data?.error || err.response?.data?.message || 'Failed to apply discount');
    } finally {
      setLoading(false);
    }
  };

  return (
    <AnimatePresence>
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          className="absolute inset-0 bg-bg/80 backdrop-blur-sm"
          onClick={onClose}
        />
        <motion.div
          initial={{ opacity: 0, scale: 0.95, y: 20 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          exit={{ opacity: 0, scale: 0.95, y: 20 }}
          className="relative w-full max-w-md bg-bg-2 border border-border rounded-lg shadow-2xl overflow-hidden"
        >
          {/* Header */}
          <div className="flex items-center justify-between p-4 border-b border-border bg-bg-3">
            <div className="flex items-center gap-2 text-text">
              <Tag className="w-5 h-5 text-neon-orange" />
              <h2 className="font-heading font-bold tracking-widest text-lg">APPLY DISCOUNT</h2>
            </div>
            <button onClick={onClose} className="p-1 hover:bg-bg/50 rounded-lg transition-colors text-text-3 hover:text-text">
              <X className="w-5 h-5" />
            </button>
          </div>

          <div className="p-6 space-y-6">
            <div className="text-sm text-text-2">
              Apply a discount to the active session on <span className="text-neon-orange font-bold font-mono">{pc.name}</span>.
            </div>

            <div className="grid grid-cols-3 gap-2">
              {DISCOUNT_OPTIONS.map((opt) => (
                <button
                  key={opt.label}
                  onClick={() => setSelectedDiscount(opt.value)}
                  className={`py-2 px-1 rounded border text-xs font-mono font-bold tracking-wider transition-all duration-200 ${
                    selectedDiscount === opt.value
                      ? 'bg-neon-orange/20 border-neon-orange text-neon-orange shadow-[0_0_10px_rgba(255,87,34,0.3)]'
                      : 'bg-bg border-border text-text-3 hover:bg-bg-3 hover:border-border/80'
                  }`}
                >
                  {opt.label}
                </button>
              ))}
            </div>

            <button
              onClick={handleApply}
              disabled={loading}
              className="w-full py-3 rounded border border-pc-active/50 bg-pc-active/10 text-pc-active font-bold tracking-widest hover:bg-pc-active/20 transition-all disabled:opacity-50 disabled:cursor-not-allowed uppercase"
            >
              {loading ? 'APPLYING...' : 'APPLY DISCOUNT'}
            </button>
          </div>
        </motion.div>
      </div>
    </AnimatePresence>
  );
}
