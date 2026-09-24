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
  const [adminSwitchUser, setAdminSwitchUser] = useState(null); // SOP §22: Admin Quick-Switch
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const fetchCurrentUser = useCallback(async () => {
    try {
      const res = await api.get('/auth/me');
      const userData = res.data.data;
      setUser(userData);
      localStorage.setItem('user', JSON.stringify(userData));
    } catch (error) {
      if (error.response && (error.response.status === 401 || error.response.status === 403)) {
        setUser(null);
        localStorage.removeItem('user');
      }
    }
  }, []);

  // ── Initialize auth state from localStorage (tokens are now in HTTP-only cookies) ──
  useEffect(() => {
    const storedUser = localStorage.getItem('user');

    // adminSwitchUser is React state only, so a reload always loses it - there is no admin
    // identity to restore client-side anyway. If a switch was active, the adminSwitchToken
    // cookie survives the reload and the server prefers it over accessToken, so /auth/me
    // below would come back as the ADMIN and silently overwrite `user`, losing the operator
    // (the exact failure this whole mechanism exists to prevent). Dropping back to the
    // operator on every reload is the simple, safe choice - nobody has to re-authenticate as
    // the admin just because the page refreshed.
    if (sessionStorage.getItem('adminSwitchActive')) {
      sessionStorage.removeItem('adminSwitchActive');
      api.post('/auth/admin-switch/clear-cookie').catch(() => {});
    }

    if (storedUser && storedUser !== 'undefined') {
      try {
        setUser(JSON.parse(storedUser));
        // Verify token is still valid by fetching current user
        fetchCurrentUser().finally(() => setLoading(false));
      } catch (e) {
        console.error("Failed to parse stored user", e);
        localStorage.removeItem('user');
        setLoading(false);
      }
    } else {
      setLoading(false);
    }
  }, [fetchCurrentUser]);

  // A switch token expired mid-switch (see api.js's response interceptor) - the cookie is
  // already cleared server-side by the time this fires. Drop the client's admin identity and
  // resume the operator, exactly like a normal exitAdminSwitch, minus the already-done
  // network call.
  useEffect(() => {
    const onSwitchExpired = () => {
      setAdminSwitchUser(null);
      fetchCurrentUser();
    };
    window.addEventListener('admin-switch-expired', onSwitchExpired);
    return () => window.removeEventListener('admin-switch-expired', onSwitchExpired);
  }, [fetchCurrentUser]);

  // ── Super Admin Login (SOP §6.2) ──
  // Tokens are now set as HTTP-only cookies by the backend
  const loginAdmin = useCallback(async (email, password) => {
    try {
      setError(null);
      const response = await api.post('/auth/admin/login', { email, password });
      const { user: userData } = response.data.data;

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
  // Tokens are now set as HTTP-only cookies by the backend
  const loginOperator = useCallback(async (branchId, username, password) => {
    try {
      setError(null);
      const response = await api.post('/auth/operator/login', { branchId, username, password });
      const {
        user: userData, resumedShift, unattendedMinutes, needsGapExplanation, pendingTakeover,
      } = response.data.data;

      // Somebody else's shift was left open here. The server has deliberately not issued a shift
      // for this login, so there is nothing to trade with until the drawer in front of them has
      // been counted. Held in sessionStorage for the same reason as the gap question: a refresh
      // must not lose it.
      if (pendingTakeover) {
        sessionStorage.setItem('pendingShiftTakeover', JSON.stringify(pendingTakeover));
      } else {
        sessionStorage.removeItem('pendingShiftTakeover');
      }

      // A resumed shift with a hole in it has to be explained before the operator can work.
      // Kept in sessionStorage rather than state so a page refresh cannot lose the question -
      // refreshing past it would be the easiest way to avoid answering.
      if (needsGapExplanation) {
        sessionStorage.setItem('pendingShiftGap', JSON.stringify({
          shiftId: userData?.shiftId,
          unattendedMinutes: unattendedMinutes || 0,
        }));
      } else {
        sessionStorage.removeItem('pendingShiftGap');
      }

      // A resumed shift never actually closed - the drawer is still open and inventory was
      // already checked when the shift genuinely started. Mark the shift-start checklist done
      // right away so it doesn't reappear. Without this, an app restart mid-shift (a crash, a
      // power cut, or now an auto-update) wipes the sessionStorage flag the checklist normally
      // relies on, and the operator who logs back in gets asked to "open" a drawer they never
      // closed. A genuinely new shift (resumedShift false) still gets the checklist as normal.
      const shiftStartKey = `shift_start_done_${userData?.id || userData?.username}`;
      if (resumedShift) {
        sessionStorage.setItem(shiftStartKey, 'true');
      } else {
        sessionStorage.removeItem(shiftStartKey);
      }

      localStorage.setItem('user', JSON.stringify(userData));
      setUser(userData);
      return { ...userData, resumedShift, unattendedMinutes, needsGapExplanation, pendingTakeover };
    } catch (err) {
      const message = err.response?.data?.error || 'Login failed';
      setError(message);
      throw new Error(message);
    }
  }, []);

  // ── Admin Quick-Switch In (SOP §22) ──
  // Tokens are now set as HTTP-only cookies by the backend
  const adminSwitchIn = useCallback(async (adminId, accessPin) => {
    try {
      setError(null);
      const response = await api.post('/auth/admin-switch/in', { adminId, accessPin });
      const { user: adminData } = response.data.data;

      sessionStorage.setItem('adminSwitchActive', '1');
      setAdminSwitchUser(adminData);
      return adminData;
    } catch (err) {
      const message = err.response?.data?.error || 'Admin switch failed';
      setError(message);
      throw new Error(message);
    }
  }, []);

  const fetchAvailableAdminsForSwitch = useCallback(async () => {
    try {
      const res = await api.get('/auth/admin-switch/available');
      return res.data.data || [];
    } catch (err) {
      console.error('Failed to fetch available admins for switch:', err);
      return [];
    }
  }, []);

  // ── Exit Admin Mode (SOP §22) ──
  const exitAdminSwitch = useCallback(async () => {
    try {
      await api.post('/auth/admin-switch/out').catch(() => {});
    } finally {
      sessionStorage.removeItem('adminSwitchActive');
      setAdminSwitchUser(null);
      // Re-fetch operator user data. The server has already deleted adminSwitchToken by the
      // time this response lands, so /auth/me resolves against the operator's own
      // never-touched accessToken cookie - this always comes back as the operator, not a race.
      await fetchCurrentUser();
    }
  }, [fetchCurrentUser]);

  // ── Logout (SOP §10: shift closure for operators) ──
  const logout = useCallback(async (options = {}) => {
    // Capture role to determine where to redirect after logout
    const role = user?.role || user?.Role || '';

    try {
      await api.post('/auth/logout', {
        shiftId: user?.shiftId,
        // Operator ticked "last shift of the day" - closes the trading day and sends its summary.
        closesTradingDay: options.closesTradingDay === true,
      }).catch(() => {});
    } finally {
      localStorage.removeItem('user');
      localStorage.removeItem('activeBranchId');
      sessionStorage.removeItem('pendingShiftTakeover');
      setUser(null);
      setAdminSwitchUser(null);
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

  /// Ends the session without navigating anywhere.
  ///
  /// `logout` sends the browser to a login portal, which is right for a person clicking
  /// "log out" but useless when a login page needs to discard a stale session it is already
  /// showing — that would bounce the page away and, on a portal that redirects authenticated
  /// users, loop. The server call matters: the token lives in an HttpOnly cookie the page
  /// cannot clear itself, so skipping it would leave the old session usable.
  const clearSession = useCallback(async () => {
    try {
      // Deliberately /auth/session/clear, not /auth/logout - this only drops the cookie the
      // page cannot clear itself. /auth/logout closes an Operator's active shift, which is
      // exactly what must NOT happen just because someone looked at the wrong login portal.
      await api.post('/auth/session/clear', {}).catch(() => {});
    } finally {
      localStorage.removeItem('user');
      localStorage.removeItem('activeBranchId');
      sessionStorage.removeItem('pendingShiftTakeover');
      sessionStorage.removeItem('adminSwitchActive');
      setUser(null);
      setAdminSwitchUser(null);
    }
  }, []);

  // ── Role checks ──
  const activeUser = adminSwitchUser || user;
  const userRole = activeUser?.role || activeUser?.Role;
  const isSuperAdmin = userRole === ROLES.SUPER_ADMIN || (typeof userRole === 'string' && userRole.toLowerCase().includes('admin'));
  const isOperator = userRole === ROLES.OPERATOR || (typeof userRole === 'string' && userRole.toLowerCase().includes('operator'));
  const isAuthenticated = !!activeUser;

  // ── Dashboard permission check (SOP §19.2) ──
  const hasDashboardAccess = useCallback((dashboardKey) => {
    const checkUser = adminSwitchUser || user;
    if (!checkUser) return false;
    const role = checkUser.role || checkUser.Role;
    if (role === ROLES.SUPER_ADMIN) return true;
    
    // Operator dashboards that Admins should implicitly have access to
    const operatorDashboards = [
      'billing_counter', 'sessions', 'reservations', 'food_orders', 
      'cash_register', 'cash_desk', 'online_desk', 'wallet_desk', 'credits', 'eod',
      // Everyone can see Updates. It is where you find out what changed and whether this
      // branch took it; an operator who cannot see that has no way to answer "are you on the
      // new version?" except by guessing.
      'updates'
    ];

    if (role === ROLES.ADMIN) {
      if (dashboardKey === 'main_dashboard' || operatorDashboards.includes(dashboardKey)) return true;
    }

    return checkUser.dashboardPermissions?.[dashboardKey] === true;
  }, [user, adminSwitchUser]);

  // ── Can this person actually apply a discount? ──
  //
  // Mirrors BillingController.ApplyDiscount exactly: Super Admin always, an Admin only
  // with an explicit `discount` permission, an Operator never.
  //
  // The discount controls used to be gated on `isSuperAdmin`, which is deliberately loose
  // (any role containing "admin") because the rest of the app uses it to mean "elevated,
  // not an operator". For discount that looseness offered the button to Admins the server
  // then refused with a bare 403 - no body, so the client could only say "Failed to apply
  // discount". Asking the same question the server asks means the button is simply not
  // there instead.
  const canApplyDiscount = useCallback(() => {
    const checkUser = adminSwitchUser || user;
    if (!checkUser) return false;
    const role = checkUser.role || checkUser.Role;
    if (role === ROLES.SUPER_ADMIN) return true;
    if (role === ROLES.ADMIN) return checkUser.dashboardPermissions?.discount === true;
    return false;
  }, [user, adminSwitchUser]);

  // ── Can this person correct a completed bill's payment method? ──
  // Mirrors BillingController.EditPaymentMethod exactly: open to every logged-in role,
  // Operator included, per the owner's explicit instruction that whoever is at the counter
  // when the mistake is noticed should be able to fix it on the spot.
  const canCorrectPaymentMethod = useCallback(() => {
    const checkUser = adminSwitchUser || user;
    return !!checkUser;
  }, [user, adminSwitchUser]);

  const value = {
    user: adminSwitchUser || user,
    baseUser: user, // Keep track of the original operator
    adminSwitchUser,
    loading,
    error,
    isAuthenticated,
    isSuperAdmin,
    isOperator,
    loginAdmin,
    loginOperator,
    logout,
    clearSession,
    adminSwitchIn,
    exitAdminSwitch,
    fetchAvailableAdminsForSwitch,
    hasDashboardAccess,
    canApplyDiscount,
    canCorrectPaymentMethod,
    fetchCurrentUser,
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
