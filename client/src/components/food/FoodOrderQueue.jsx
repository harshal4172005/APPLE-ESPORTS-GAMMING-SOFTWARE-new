import { useState } from 'react';
import { Play, Check, Truck, X, Ban, Trash2, Clock } from 'lucide-react';
import { motion, AnimatePresence } from 'framer-motion';
import api from '../../config/api';

export default function FoodOrderQueue({ orders, onOrderUpdated }) {
  const [loadingOrderId, setLoadingOrderId] = useState(null);
  const [error, setError] = useState(null);
  const [cancelOrderId, setCancelOrderId] = useState(null);
  const [cancelReason, setCancelReason] = useState('');

  const updateStatus = async (orderId, newStatus, payload = {}) => {
    setLoadingOrderId(orderId);
    setError(null);
    try {
      await api.put(`/food-orders/${orderId}/status`, { status: newStatus, ...payload });
      onOrderUpdated();
      setCancelOrderId(null);
      setCancelReason('');
    } catch (err) {
      setError(err.response?.data?.error || err.response?.data?.message || 'Failed to update order status');
    } finally {
      setLoadingOrderId(null);
    }
  };

  const handleCancel = (orderId) => {
    if (!cancelReason.trim()) {
      setError('Cancellation reason is required.');
      return;
    }
    updateStatus(orderId, 'Cancelled', { reason: cancelReason });
  };

  return (
    <div className="card bg-bg-2 border border-border rounded-xl p-6 h-full flex flex-col overflow-hidden">
      {error && (
        <div className="mb-4 p-3 bg-neon-red/10 border border-neon-red/20 text-neon-red text-xs rounded-lg">
          {error}
        </div>
      )}

      <div className="flex-1 overflow-auto">
        <table className="w-full text-left border-collapse text-xs">
          <thead>
            <tr className="border-b border-border text-text-3 uppercase tracking-wider font-bold text-[10px] pb-3">
              <th className="py-3 px-4">PC No.</th>
              <th className="py-3 px-4">Customer</th>
              <th className="py-3 px-4">Ordered Items</th>
              <th className="py-3 px-4 text-center">Qty</th>
              <th className="py-3 px-4 text-right">Total Amount</th>
              <th className="py-3 px-4 text-center">Status</th>
              <th className="py-3 px-4 text-right">Action</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border/40 font-heading">
            {orders.length === 0 ? (
              <tr>
                <td colSpan="7" className="text-center py-12 text-text-3 text-sm italic">
                  No active orders in queue.
                </td>
              </tr>
            ) : (
              orders.map(order => {
                const status = order.status;
                const isPending = status === 0 || status === 'Pending';
                const isPreparing = status === 1 || status === 'Preparing';
                const isReady = status === 2 || status === 'Ready';
                const isDelivered = status === 3 || status === 'Delivered';

                let statusColor = 'text-text-3 bg-text-3/10';
                let statusLabel = status;
                if (isPending) {
                  statusColor = 'text-neon-orange bg-neon-orange/10';
                  statusLabel = 'Pending';
                } else if (isPreparing) {
                  statusColor = 'text-neon-purple bg-neon-purple/10';
                  statusLabel = 'Preparing';
                } else if (isReady) {
                  statusColor = 'text-neon-blue bg-neon-blue/10';
                  statusLabel = 'Ready';
                } else if (isDelivered) {
                  statusColor = 'text-neon-green bg-neon-green/10';
                  statusLabel = 'Delivered';
                }

                const totalQty = order.items?.reduce((sum, item) => sum + item.quantity, 0) || 0;

                return (
                  <tr key={order.id} className="hover:bg-bg-3/45 transition-colors relative">
                    <td className="py-3.5 px-4 font-bold text-text text-sm">
                      {order.pcNumber ? `Station ${order.pcNumber}` : 'Walk-in'}
                      <div className="text-[10px] text-text-3 font-mono font-normal mt-0.5">{order.orderNumber}</div>
                    </td>
                    <td className="py-3.5 px-4 text-text-2 font-medium">
                      {order.customerName || 'Guest'}
                    </td>
                    <td className="py-3.5 px-4 text-text-2">
                      <div className="max-w-xs truncate" title={order.items?.map(i => `${i.itemName} (x${i.quantity})`).join(', ')}>
                        {order.items?.map(i => `${i.itemName} (x${i.quantity})`).join(', ')}
                      </div>
                    </td>
                    <td className="py-3.5 px-4 text-center font-mono font-bold text-text-2">
                      {totalQty}
                    </td>
                    <td className="py-3.5 px-4 text-right font-mono font-bold text-accent text-sm">
                      ₹{order.totalAmount}
                    </td>
                    <td className="py-3.5 px-4 text-center">
                      <span className={`px-2 py-0.5 rounded font-mono font-bold text-[9px] uppercase ${statusColor}`}>
                        {statusLabel}
                      </span>
                    </td>
                    <td className="py-3.5 px-4 text-right">
                      {loadingOrderId === order.id ? (
                        <div className="flex justify-end pr-4">
                          <div className="w-4 h-4 rounded-full border-2 border-accent border-t-transparent animate-spin" />
                        </div>
                      ) : cancelOrderId === order.id ? (
                        <div className="flex items-center justify-end gap-1.5 fade-in">
                          <input
                            type="text"
                            placeholder="Reason..."
                            value={cancelReason}
                            onChange={e => setCancelReason(e.target.value)}
                            className="bg-bg-3 border border-neon-red/30 text-text text-[10px] rounded px-1.5 py-1 focus:border-neon-red focus:outline-none w-28"
                          />
                          <button
                            onClick={() => handleCancel(order.id)}
                            className="px-2 py-1 bg-neon-red/10 border border-neon-red/30 text-neon-red rounded text-[9px] font-bold uppercase hover:bg-neon-red/20 transition-colors"
                          >
                            OK
                          </button>
                          <button
                            onClick={() => { setCancelOrderId(null); setCancelReason(''); }}
                            className="px-2 py-1 bg-bg-3 border border-border text-text-3 rounded text-[9px] font-bold uppercase hover:text-text transition-colors"
                          >
                            X
                          </button>
                        </div>
                      ) : (
                        <div className="flex justify-end gap-1.5">
                          {(isPending || isPreparing || isReady) && (
                            <button
                              onClick={() => updateStatus(order.id, 'Delivered')}
                              className="px-2.5 py-1 bg-neon-green/10 border border-neon-green/30 text-neon-green rounded-md text-[10px] font-bold uppercase hover:bg-neon-green/20 transition-all flex items-center gap-1"
                            >
                              <Truck className="w-3 h-3" /> Mark as Delivered
                            </button>
                          )}

                          {!isDelivered && (
                            <button
                              onClick={() => setCancelOrderId(order.id)}
                              title="Cancel Order"
                              className="p-1 rounded-md border border-neon-red/30 text-neon-red hover:bg-neon-red/10 transition-colors"
                            >
                              <Ban className="w-3.5 h-3.5" />
                            </button>
                          )}
                        </div>
                      )}
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
