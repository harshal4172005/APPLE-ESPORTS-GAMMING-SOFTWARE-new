// ═══════════════════════════════════════════════════════════
// Gaming Café ERP — Auth Context
// SOP §6: Login System + §21: Security Model
// Manages auth state, login/logout, token refresh, role detection
// ═══════════════════════════════════════════════════════════

import { createContext, useContext, useState, useCallback, useEffect } from 'react';
import api from '../config/api';
import { ROLES } from '../config/constants';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // ── Initialize auth state from localStorage ──
  useEffect(() => {
    const storedUser = localStorage.getItem('user');
    const token = localStorage.getItem('accessToken');

    if (storedUser && token) {
      setUser(JSON.parse(storedUser));
      // Verify token is still valid by fetching current user
      api.get('/auth/me')
        .then((res) => {
          const userData = res.data.data;
          setUser(userData);
          localStorage.setItem('user', JSON.stringify(userData));
        })
        .catch((error) => {
          // Token invalid or network error.
          // If it's explicitly a 401/403, log them out. 
          // (Though the interceptor handles most 401s, this is a fallback).
          // If it's a network error (no response), KEEP them logged in so they don't lose session on brief disconnects.
          if (error.response && (error.response.status === 401 || error.response.status === 403)) {
            logout();
          }
        })
        .finally(() => setLoading(false));
    } else {
      setLoading(false);
    }
  }, []);

  // ── Super Admin Login (SOP §6.2) ──
  const loginAdmin = useCallback(async (email, password) => {
    try {
      setError(null);
      const response = await api.post('/auth/admin/login', { email, password });
      const { user: userData, accessToken, refreshToken } = response.data.data;

      localStorage.setItem('accessToken', accessToken);
      localStorage.setItem('refreshToken', refreshToken);
      localStorage.setItem('user', JSON.stringify(userData));

      setUser(userData);
      return userData;
    } catch (err) {
      const message = err.response?.data?.error || 'Login failed';
      setError(message);
      throw new Error(message);
    }
  }, []);

  // ── Operator Login (SOP §6.3) ──
  const loginOperator = useCallback(async (branchId, username, password) => {
    try {
      setError(null);
      const response = await api.post('/auth/operator/login', { branchId, username, password });
      const { user: userData, accessToken, refreshToken } = response.data.data;

      localStorage.setItem('accessToken', accessToken);
      localStorage.setItem('refreshToken', refreshToken);
      localStorage.setItem('user', JSON.stringify(userData));

      setUser(userData);
      return userData;
    } catch (err) {
      const message = err.response?.data?.error || 'Login failed';
      setError(message);
      throw new Error(message);
    }
  }, []);

  // ── Logout (SOP §10: shift closure for operators) ──
  const logout = useCallback(async () => {
    // Capture role to determine where to redirect after logout
    const role = user?.role || user?.Role || '';
    
    try {
      const token = localStorage.getItem('accessToken');
      if (token) {
        await api.post('/auth/logout', { shiftId: user?.shiftId }).catch(() => {});
      }
    } finally {
      localStorage.removeItem('accessToken');
      localStorage.removeItem('refreshToken');
      localStorage.removeItem('user');
      localStorage.removeItem('activeBranchId');
      setUser(null);
      setError(null);

      // Explicitly navigate to the correct login portal
      let redirectPath = '/';
      if (role === ROLES.SUPER_ADMIN) {
        redirectPath = '/login/superadmin';
      } else if (typeof role === 'string' && role.toLowerCase().includes('admin')) {
        redirectPath = '/login/admin';
      } else if (typeof role === 'string' && role.toLowerCase().includes('operator')) {
        redirectPath = '/login/operator';
      }
      
      window.location.href = redirectPath;
    }
  }, [user]);

  // ── Role checks ──
  const userRole = user?.role || user?.Role;
  const isSuperAdmin = userRole === ROLES.SUPER_ADMIN || (typeof userRole === 'string' && userRole.toLowerCase().includes('admin'));
  const isOperator = userRole === ROLES.OPERATOR || (typeof userRole === 'string' && userRole.toLowerCase().includes('operator'));
  const isAuthenticated = !!user;

  // ── Dashboard permission check (SOP §19.2) ──
  const hasDashboardAccess = useCallback((dashboardKey) => {
    if (!user) return false;
    const role = user.role || user.Role;
    if (role === ROLES.SUPER_ADMIN) return true;
    
    // For Admin, they have access to all branches, but we still check their dashboard UI permissions.
    // However, they always have access to the main dashboard.
    if (dashboardKey === 'main_dashboard' && role === ROLES.ADMIN) return true;

    return user.dashboardPermissions?.[dashboardKey] === true;
  }, [user]);

  const value = {
    user,
    loading,
    error,
    isAuthenticated,
    isSuperAdmin,
    isOperator,
    loginAdmin,
    loginOperator,
    logout,
    hasDashboardAccess,
    setError,
  };

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}

export default AuthContext;
