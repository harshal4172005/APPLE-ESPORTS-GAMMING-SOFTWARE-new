// ═══════════════════════════════════════════════════════════
// Gaming Café ERP — Socket.IO Client Configuration
// SOP §20: Real-Time Synchronization
// ═══════════════════════════════════════════════════════════

import { io } from 'socket.io-client';

const SOCKET_URL = import.meta.env.VITE_SOCKET_URL || '/hubs';

/**
 * Create authenticated socket connection
 * JWT token is passed in handshake for backend authentication
 */
export function createSocket(namespace = '') {
  const token = localStorage.getItem('accessToken');

  const socket = io(`${SOCKET_URL}${namespace}`, {
    auth: { token },
    reconnection: true,
    reconnectionAttempts: 10,
    reconnectionDelay: 1000,
    reconnectionDelayMax: 5000,
    timeout: 10000,
  });

  socket.on('connect', () => {
    console.log(`[Socket] Connected to ${namespace || 'default'}`);
  });

  socket.on('disconnect', (reason) => {
    console.log(`[Socket] Disconnected from ${namespace}: ${reason}`);
  });

  socket.on('connect_error', (error) => {
    console.error(`[Socket] Connection error on ${namespace}:`, error.message);
  });

  return socket;
}

/**
 * Socket namespace constants matching backend
 */
export const SOCKET_NS = {
  SESSIONS: '/sessions',
  BILLING: '/billing',
  RESERVATIONS: '/reservations',
  FOOD_ORDERS: '/food-orders',
  PC_STATUS: '/pc-status',
  CASH: '/cash',
  NOTIFICATIONS: '/notifications',
};
