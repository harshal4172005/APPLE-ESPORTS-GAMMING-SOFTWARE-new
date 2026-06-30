import { useState, memo } from 'react';
import { Play, Check, Truck, X, Ban, Trash2 } from 'lucide-react';
import { motion, AnimatePresence } from 'framer-motion';
import api from '../../config/api';
import { useAuth } from '../../contexts/AuthContext';

const FoodOrderCard = memo(({ order, onOrderUpdated }) => {
  const { isSuperAdmin } = useAuth();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [showCancelPrompt, setShowCancelPrompt] = useState(false);
  const [cancelReason, setCancelReason] = useState('');

  const status = order.status;
  const isPending = status === 0 || status === 'Pending';
  const isPreparing = status === 1 || status === 'Preparing';
  const isReady = status === 2 || status === 'Ready';
  const isDelivered = status === 3 || status === 'Delivered';

  const canOperatorCancel = isPending;
  const canCancel = canOperatorCancel || isSuperAdmin;

  const updateStatus = async (newStatus, payload = {}) => {
    setLoading(true);
    setError(null);
    try {
      await api.put(`/food-orders/${order.id}/status`, { status: newStatus, ...payload });
      onOrderUpdated(); // Trigger kanban refresh
      setShowCancelPrompt(false);
    } catch (err) {
      setError(err.response?.data?.error || err.response?.data?.message || 'Failed to update order');
    } finally {
      setLoading(false);
    }
  };

  const handleCancel = () => {
    if (!cancelReason.trim()) {
      setError('Cancellation reason is required.');
      return;
    }
    updateStatus('Cancelled', { reason: cancelReason });
  };

  return (
    <motion.div
      layout
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, scale: 0.95 }}
      className="bg-bg-3 border border-border rounded-lg shadow-md overflow-hidden relative"
    >
      {loading && (
        <div className="absolute inset-0 bg-bg-2/50 backdrop-blur-sm z-10 flex items-center justify-center">
          <div className="w-5 h-5 rounded-full border-2 border-accent border-t-transparent animate-spin" />
        </div>
      )}

      {/* Header */}
      <div className="p-3 border-b border-border bg-bg-2 flex justify-between items-start">
        <div>
          <div className="font-heading font-bold text-sm text-text">{order.pcNumber ? `Station ${order.pcNumber}` : 'Walk-in'}</div>
          <div className="text-[10px] text-text-3 font-mono mt-0.5">{order.orderNumber}</div>
        </div>
        <div className="text-right">
          <div className="font-mono font-bold text-accent text-sm">₹{order.totalAmount}</div>
          <div className="text-[9px] text-text-2 uppercase tracking-wider mt-0.5">{order.customerName || 'Guest'}</div>
        </div>
      </div>

      {/* Items */}
      <div className="p-3 bg-bg-3 space-y-2 max-h-[150px] overflow-y-auto">
        {order.items.map(item => (
          <div key={item.id} className="flex justify-between items-start text-xs">
            <div className="flex gap-2">
              <span className="font-mono text-text-3">{item.quantity}x</span>
              <span className="text-text-2">{item.itemName}</span>
            </div>
          </div>
        ))}
        {error && (
          <div className="text-[10px] text-neon-red bg-neon-red/10 p-1.5 rounded-md mt-2 border border-neon-red/20">
            {error}
          </div>
        )}
      </div>

      {/* Action Footer */}
      <AnimatePresence mode="wait">
        {showCancelPrompt ? (
          <motion.div
            key="cancel-prompt"
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: 'auto' }}
            exit={{ opacity: 0, height: 0 }}
            className="p-3 bg-bg-2 border-t border-border flex flex-col gap-2"
          >
            <input
              type="text"
              placeholder="Reason for cancellation..."
              value={cancelReason}
              onChange={e => setCancelReason(e.target.value)}
              className="w-full bg-bg-3 border border-neon-red/30 text-text text-xs rounded p-1.5 focus:border-neon-red focus:outline-none"
            />
            <div className="flex gap-2">
              <button 
                onClick={handleCancel}
                className="flex-1 py-1.5 bg-neon-red/10 border border-neon-red/30 text-neon-red rounded text-[10px] font-bold uppercase tracking-wider hover:bg-neon-red/20 transition-colors"
              >
                Confirm
              </button>
              <button 
                onClick={() => { setShowCancelPrompt(false); setError(null); }}
                className="flex-1 py-1.5 bg-bg-3 border border-border text-text-3 rounded text-[10px] font-bold uppercase tracking-wider hover:text-text transition-colors"
              >
                Back
              </button>
            </div>
          </motion.div>
        ) : (
          <motion.div
            key="actions"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="p-2 bg-bg-2 border-t border-border flex justify-between gap-2"
          >
            {(isPending || isPreparing || isReady) && (
              <button
                onClick={() => updateStatus('Delivered')}
                className="flex-1 py-2 bg-accent/10 border border-accent/30 text-accent rounded-md text-[10px] font-bold uppercase tracking-wider hover:bg-accent/20 transition-colors flex items-center justify-center gap-1.5"
              >
                <Truck className="w-3.5 h-3.5" /> Deliver
              </button>
            )}


            {/* Cancel Button */}
            {canCancel && !isDelivered && (
              <button
                onClick={() => setShowCancelPrompt(true)}
                title={!canOperatorCancel ? "Super Admin Override Cancel" : "Cancel Order"}
                className={`w-9 flex-shrink-0 flex items-center justify-center rounded-md border transition-colors ${
                  !canOperatorCancel 
                    ? 'border-accent/30 text-accent hover:bg-accent/10' 
                    : 'border-neon-red/30 text-neon-red hover:bg-neon-red/10'
                }`}
              >
                {!canOperatorCancel ? <Ban className="w-3.5 h-3.5" /> : <Trash2 className="w-3.5 h-3.5" />}
              </button>
            )}
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
  );
});

FoodOrderCard.displayName = 'FoodOrderCard';
export default FoodOrderCard;
