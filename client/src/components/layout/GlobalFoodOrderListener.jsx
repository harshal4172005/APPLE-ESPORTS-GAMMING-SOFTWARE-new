import { useEffect, useRef, useCallback } from 'react';
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
    
    const unsubNew = subscribe(SIGNALR_HUBS.FOOD_ORDERS, 'NewFoodOrder', () => {
      checkOrders();
    });

    return () => {
      unsubUpdated();
      unsubNew();
    };
  }, [connected, targetBranchId, subscribe, checkOrders]);

  return null;
}
