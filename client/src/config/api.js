// ═══════════════════════════════════════════════════════════
// Gaming Café ERP — API Client Configuration
// Axios instance with JWT interceptor + error handling
// ═══════════════════════════════════════════════════════════

import axios from 'axios';

const API_BASE_URL = '/api';

const api = axios.create({
  baseURL: API_BASE_URL,
  timeout: 15000,
  headers: {
    'Content-Type': 'application/json',
  },
});

// ── Request Interceptor — attach JWT token ──
api.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('accessToken');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }

    // Attach branch header for Super Admin branch switching
    const activeBranch = localStorage.getItem('activeBranchId');
    if (activeBranch) {
      config.headers['X-Branch-Id'] = activeBranch;
    }

    return config;
  },
  (error) => Promise.reject(error)
);

let isRefreshing = false;
let refreshSubscribers = [];

const subscribeTokenRefresh = (cb) => {
  refreshSubscribers.push(cb);
};

const onRefreshed = (token) => {
  refreshSubscribers.map((cb) => cb(token));
  refreshSubscribers = [];
};

// ── Response Interceptor — handle token expiry + errors ──
api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    // Do not attempt refresh for login requests or invalid credentials
    if (originalRequest.url.includes('/auth/admin/login') || 
        originalRequest.url.includes('/auth/operator/login') ||
        error.response?.data?.code === 'INVALID_CREDENTIALS') {
      return Promise.reject(error);
    }

    // Token expired — attempt refresh
    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;

      if (!isRefreshing) {
        isRefreshing = true;

        try {
          const refreshToken = localStorage.getItem('refreshToken');
          if (!refreshToken) throw new Error('No refresh token');

          const response = await axios.post(`${API_BASE_URL}/auth/refresh`, {
            refreshToken,
          });

          const { accessToken } = response.data.data;
          localStorage.setItem('accessToken', accessToken);
          api.defaults.headers.common['Authorization'] = `Bearer ${accessToken}`;
          
          isRefreshing = false;
          
          // Queue the original request FIRST before flushing
          const retryOriginalRequest = new Promise((resolve, reject) => {
            subscribeTokenRefresh((token) => {
              if (token instanceof Error) {
                reject(token);
              } else {
                originalRequest.headers.Authorization = `Bearer ${token}`;
                resolve(api(originalRequest));
              }
            });
          });
          
          onRefreshed(accessToken);
          return retryOriginalRequest;

        } catch (refreshError) {
          isRefreshing = false;
          
          // Reject all queued requests
          onRefreshed(new Error('Refresh failed'));
          refreshSubscribers = [];
          
          // Refresh failed — force logout
          localStorage.removeItem('accessToken');
          localStorage.removeItem('refreshToken');
          localStorage.removeItem('user');
          window.location.href = '/';
          return Promise.reject(refreshError);
        }
      } else {
        // Already refreshing, just join the queue
        return new Promise((resolve, reject) => {
          subscribeTokenRefresh((token) => {
            if (token instanceof Error) {
              reject(token);
            } else {
              originalRequest.headers.Authorization = `Bearer ${token}`;
              resolve(api(originalRequest));
            }
          });
        });
      }
    }

    // Force logout response (SOP §11: Live Access Revocation)
    // Only redirect if this wasn't a login attempt (login attempts should just show an error)
    const isLoginRequest = originalRequest.url?.includes('/login');
    if (error.response?.status === 403 && error.response?.data?.code === 'ACCOUNT_INACTIVE' && !isLoginRequest) {
      localStorage.clear();
      window.location.href = '/?reason=account_inactive';
      return Promise.reject(error);
    }

    return Promise.reject(error);
  }
);

export default api;
