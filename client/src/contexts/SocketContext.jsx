// ═══════════════════════════════════════════════════════════
// Gaming Café ERP — Socket Context (SignalR)
// SOP §20: Real-Time Synchronization Engine
// Manages SignalR Hub connections and lifecycle
// ═══════════════════════════════════════════════════════════

import { createContext, useContext, useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { useAuth } from './AuthContext';

const SocketContext = createContext(null);

// SignalR Hub endpoints mapped from .NET 8 backend
export const SIGNALR_HUBS = {
  NOTIFICATIONS: '/hubs/notifications',
  PC_STATUS: '/hubs/pc-status',
  SESSIONS: '/hubs/sessions',
  RESERVATIONS: '/hubs/reservations',
  FOOD_ORDERS: '/hubs/food-orders',
  BILLING: '/hubs/billing',
  CASH: '/hubs/cash',
  DASHBOARD: '/hubs/dashboard'
};

const HUB_BASE_URL = import.meta.env.VITE_API_URL 
  ? import.meta.env.VITE_API_URL.replace('/api', '') 
  : '';

export function SocketProvider({ children }) {
  const { isAuthenticated, logout } = useAuth();
  const [connected, setConnected] = useState(false);
  const hubsRef = useRef({});

  // ── Connect to all hubs when authenticated ──
  useEffect(() => {
    if (!isAuthenticated) {
      // Disconnect all on logout
      Object.values(hubsRef.current).forEach(hub => hub?.stop());
      hubsRef.current = {};
      setConnected(false);
      return;
    }

    let isSubscribed = true;
    const token = localStorage.getItem('accessToken');

    const connectHubs = async () => {
      try {
        const hubs = Object.values(SIGNALR_HUBS);
        
        for (const hubPath of hubs) {
          if (!hubsRef.current[hubPath]) {
            const connection = new signalR.HubConnectionBuilder()
              .withUrl(`${HUB_BASE_URL}${hubPath}`, {
                accessTokenFactory: () => token
              })
              .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]) // Exponential backoff
              .configureLogging(signalR.LogLevel.Warning)
              .build();

            // Store connection
            hubsRef.current[hubPath] = connection;

            // Handle lifecycle events
            connection.onreconnecting(() => {
              if (hubPath === SIGNALR_HUBS.NOTIFICATIONS) setConnected(false);
            });
            
            connection.onreconnected(() => {
              if (hubPath === SIGNALR_HUBS.NOTIFICATIONS) setConnected(true);
            });
            
            connection.onclose(() => {
              if (hubPath === SIGNALR_HUBS.NOTIFICATIONS) setConnected(false);
            });

            // Store the start promise so we can await it during cleanup
            connection.startPromise = connection.start().then(() => {
              if (isSubscribed && hubPath === SIGNALR_HUBS.NOTIFICATIONS) setConnected(true);
            }).catch(err => {
              if (err.name !== 'AbortError') console.error('SignalR start error:', err);
            });
          }
        }

        // Handle forced logout (SOP §11: Live Access Revocation)
        const notifHub = hubsRef.current[SIGNALR_HUBS.NOTIFICATIONS];
        if (notifHub) {
          notifHub.on('ForceLogout', (reason) => {
            console.warn('Forced logout received:', reason);
            logout();
            window.location.href = '/?reason=forced_logout';
          });
        }
      } catch (err) {
        console.error('SignalR Connection Error:', err);
      }
    };

    connectHubs();

    return () => {
      isSubscribed = false;
      Object.values(hubsRef.current).forEach(hub => {
        if (hub.startPromise) {
          hub.startPromise.then(() => hub.stop().catch(console.error));
        } else {
          hub.stop().catch(console.error);
        }
      });
      hubsRef.current = {};
      setConnected(false);
    };
  }, [isAuthenticated, logout]);

  // ── Get a specific hub connection ──
  const getHub = useCallback((hubPath) => {
    return hubsRef.current[hubPath] || null;
  }, []);

  // ── Subscribe to a hub event ──
  const subscribe = useCallback((hubPath, eventName, handler) => {
    const hub = hubsRef.current[hubPath];
    if (hub) {
      hub.on(eventName, handler);
      return () => hub.off(eventName, handler);
    }
    // If not connected yet, we could queue it, but usually component mounts after connection
    return () => {};
  }, []);

  // ── Emit event to a hub ──
  const emit = useCallback(async (hubPath, methodName, ...args) => {
    const hub = hubsRef.current[hubPath];
    if (hub && hub.state === signalR.HubConnectionState.Connected) {
      return await hub.invoke(methodName, ...args);
    }
    throw new Error('Hub is not connected');
  }, []);

  const value = {
    connected,
    getHub,
    subscribe,
    emit,
    SIGNALR_HUBS,
  };

  return (
    <SocketContext.Provider value={value}>
      {children}
    </SocketContext.Provider>
  );
}

export function useSocket() {
  const context = useContext(SocketContext);
  if (!context) {
    throw new Error('useSocket must be used within a SocketProvider');
  }
  return context;
}

export default SocketContext;
