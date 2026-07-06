import { useEffect, useRef, useCallback, useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Utensils, Check } from 'lucide-react';
import { useSocket, SIGNALR_HUBS } from '../../contexts/SocketContext';
import { useBranch } from '../../contexts/BranchContext';
import { useAuth } from '../../contexts/AuthContext';
import api from '../../config/api';

export const playNotificationSound = () => {
  try {
    const audioCtx = new (window.AudioContext || window.webkitAudioContext)();
    const oscillator = audioCtx.createOscillator();
    const gainNode = audioCtx.createGain();
    
    oscillator.connect(gainNode);
    gainNode.connect(audioCtx.destination);
    
    oscillator.type = 'sine';
    oscillator.frequency.setValueAtTime(880, audioCtx.currentTime); // A5 note
    gainNode.gain.setValueAtTime(0.1, audioCtx.currentTime);
    
    oscillator.start();
    oscillator.stop(audioCtx.currentTime + 0.2);
  } catch (err) {
    console.warn('AudioContext failed to play sound:', err);
  }
};

export default function GlobalFoodOrderListener() {
  const { subscribe, connected } = useSocket();
  const { activeBranch } = useBranch();
  const { isSuperAdmin, user } = useAuth();
  
  const targetBranchId = isSuperAdmin ? activeBranch?.id : user?.branchId;
  const prevPendingCount = useRef(0);
  const [newOrderAlerts, setNewOrderAlerts] = useState([]);

  // Request browser notification permission globally on load
  useEffect(() => {
    if (typeof window !== 'undefined' && 'Notification' in window && Notification.permission === 'default') {
      Notification.requestPermission();
    }
  }, []);

  const checkOrders = useCallback(async () => {
    if (!targetBranchId) return;
    try {
      const { data } = await api.get('/food-orders', { params: { page: 1, pageSize: 50, branchId: targetBranchId } });
      const orders = data?.data?.items || [];
      const pendingOrders = orders.filter(o => o.status === 0 || o.status === 'Pending');
      const pendingCount = pendingOrders.length;
      
      if (pendingCount > prevPendingCount.current) {
         // Check if sound is enabled globally (defaults to true)
         const soundEnabled = localStorage.getItem('food_order_sound_enabled') !== 'false';
         
         if (soundEnabled) {
           playNotificationSound();
         }

         if (typeof window !== 'undefined' && 'Notification' in window && Notification.permission === 'granted') {
           const lastOrder = pendingOrders[pendingOrders.length - 1];
           if (lastOrder) {
             new Notification('New Food Order!', {
               body: `${lastOrder.pcNumber ? `Station ${lastOrder.pcNumber}` : 'Walk-in'} ordered: ${lastOrder.items?.map(i => `${i.itemName} (x${i.quantity})`).join(', ')}`,
               icon: '/logo.png'
             });
           }
         }
      }
      prevPendingCount.current = pendingCount;
    } catch (err) {
      console.warn("Failed to fetch food orders for global notification", err);
    }
  }, [targetBranchId]);

  useEffect(() => {
    if (!connected || !targetBranchId) return;
    
    // Initial check to baseline the count
    checkOrders();

    const unsubUpdated = subscribe(SIGNALR_HUBS.FOOD_ORDERS, 'FoodOrderUpdated', () => {
      checkOrders();
    });
    
    const unsubNew = subscribe(SIGNALR_HUBS.FOOD_ORDERS, 'NewFoodOrder', (payload) => {
      const order = payload.data || payload.Data || payload;
      const actualOrderId = order.orderId || order.OrderId || order.id || order.Id;
      if (order && actualOrderId) {
        setNewOrderAlerts(prev => {
          if (prev.some(o => (o.orderId || o.OrderId || o.id || o.Id) === actualOrderId)) return prev;
          // Normalize the object to always have an id property for consistency
          return [...prev, { ...order, id: actualOrderId }];
        });
      }
      checkOrders();
    });

    return () => {
      unsubUpdated();
      unsubNew();
    };
  }, [connected, targetBranchId, subscribe, checkOrders]);

  const handleAcknowledge = (orderId) => {
    setNewOrderAlerts(prev => prev.filter(o => o.id !== orderId && o.Id !== orderId));
  };

  if (newOrderAlerts.length === 0) return null;

  return (
    <div className="fixed bottom-6 left-1/2 -translate-x-1/2 z-[100] flex flex-col gap-4">
      <AnimatePresence>
        {newOrderAlerts.map((order) => {
          const orderId = order.id || order.Id;
          const pcId = order.pcId || order.PcId;
          const pcNumber = order.pcName || order.PcName || order.pcNumber || order.PcNumber;
          const customerName = order.customerName || order.CustomerName || 'Walk-in';
          const items = order.items || order.Items || [];
          const totalAmount = order.totalAmount || order.TotalAmount || 0;
          
          return (
            <motion.div
              key={orderId}
              initial={{ opacity: 0, y: 50, scale: 0.9 }}
              animate={{ opacity: 1, y: 0, scale: 1 }}
              exit={{ opacity: 0, scale: 0.9 }}
              className="bg-bg-2 border border-accent shadow-[0_0_30px_rgba(220,38,38,0.3)] rounded-lg p-5 w-[400px]"
            >
              <div className="flex items-center gap-3 mb-4">
                <div className="w-10 h-10 bg-accent/20 rounded-full flex items-center justify-center shrink-0">
                  <Utensils className="w-5 h-5 text-accent animate-pulse" />
                </div>
                <div>
                  <h3 className="font-heading font-bold text-text uppercase tracking-widest text-sm">
                    New Food Order
                  </h3>
                  <p className="text-text-2 font-body text-xs mt-0.5">
                    {pcNumber ? `Station ` : ''}<span className="text-accent font-semibold">{pcNumber || pcId || 'Walk-in'}</span>
                  </p>
                </div>
              </div>

              <div className="bg-bg-3 border border-border p-3 rounded-md mb-5">
                <div className="flex justify-between items-center mb-2">
                  <span className="text-text-3 font-body text-xs">Customer</span>
                  <span className="text-text font-bold font-heading">{customerName}</span>
                </div>
                <div className="flex flex-col gap-1 mb-2">
                  <span className="text-text-3 font-body text-xs mb-1">Items</span>
                  {items.map((item, idx) => (
                    <div key={idx} className="flex justify-between text-sm">
                      <span className="text-text">{item.itemName || item.ItemName} x{item.quantity || item.Quantity}</span>
                      <span className="text-text-2">₹{(item.price || item.Price) * (item.quantity || item.Quantity)}</span>
                    </div>
                  ))}
                </div>
                <div className="flex justify-between items-center mt-3 pt-3 border-t border-border">
                  <span className="text-text-3 font-body text-xs font-bold">Total Amount</span>
                  <span className="text-accent font-mono font-bold text-lg">
                    ₹{totalAmount}
                  </span>
                </div>
              </div>

              <div className="flex gap-3">
                <button
                  onClick={() => handleAcknowledge(orderId)}
                  className="flex-1 bg-accent hover:bg-accent-dark text-white rounded-sm py-2 text-sm font-semibold flex items-center justify-center gap-2 shadow-[0_0_15px_rgba(220,38,38,0.4)] transition-colors"
                >
                  <Check className="w-4 h-4" /> Acknowledge
                </button>
              </div>
            </motion.div>
          );
        })}
      </AnimatePresence>
    </div>
  );
}
