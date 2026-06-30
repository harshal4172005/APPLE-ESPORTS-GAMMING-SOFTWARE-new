// ═══════════════════════════════════════════════════════════
// Gaming Café ERP — Auth API Layer
// ═══════════════════════════════════════════════════════════

import api from '../config/api';

export const authAPI = {
  // SOP §6.2: Super Admin Login
  adminLogin: (email, password) =>
    api.post('/auth/admin/login', { email, password }),

  // SOP §6.3: Operator Login
  operatorLogin: (branchId, username, password) =>
    api.post('/auth/operator/login', { branchId, username, password }),

  // Logout
  logout: (shiftId) =>
    api.post('/auth/logout', { shiftId }),

  // Force logout (Super Admin only)
  forceLogout: (operatorId) =>
    api.post('/auth/force-logout', { operatorId }),

  // Token refresh
  refreshToken: (refreshToken) =>
    api.post('/auth/refresh', { refreshToken }),

  // Get current user
  getMe: () =>
    api.get('/auth/me'),

  // Get active branches
  getBranches: () =>
    api.get('/auth/branches'),

  // Get operators for a branch
  getBranchOperators: (branchId) =>
    api.get(`/auth/operators/${branchId}`),

  // Setup & Security
  checkSetup: () => api.get('/auth/check-setup'),
  setupMaster: (data) => api.post('/auth/setup-master', data),
  setupOperator: (data) => api.post('/auth/setup-operator', data),
  forgotPassword: (email) => api.post('/auth/forgot-password', { email }),
  resetPassword: (email, token, newPassword) => api.post('/auth/reset-password', { email, token, newPassword }),
  changeCredentials: (data) => api.post('/auth/change-credentials', data),
};
