import api from '../config/api';

// ── Billing Counter API ──────────────────────────────────────────────────────

/** GET /bills?branchId=&page=&pageSize= */
export const getActiveBills = (branchId, page = 1, pageSize = 100) =>
  api.get('/bills', { params: { branchId, page, pageSize } })
     .then(r => r.data?.data?.items || []);

/** GET /bills/:id */
export const getBill = (id) =>
  api.get(`/bills/${id}`).then(r => r.data?.data);

const generateIdempotencyKey = () => 
  Math.random().toString(36).substring(2) + Date.now().toString(36);

/** POST /bills/:id/discount  (SuperAdmin only) */
export const applyDiscount = (id, payload) =>
  api.post(`/bills/${id}/discount`, payload, {
    headers: { 'X-Idempotency-Key': generateIdempotencyKey() }
  }).then(r => r.data?.data);

/** POST /bills/:id/pay */
export const processPayment = (id, payload) =>
  api.post(`/bills/${id}/pay`, payload, {
    headers: { 'X-Idempotency-Key': generateIdempotencyKey() }
  }).then(r => r.data?.data);

export const requestWalletApproval = (id) =>
  api.post(`/bills/${id}/request-wallet-approval`).then(r => r.data);

// ── Member lookup (for wallet balance display) ───────────────────────────────

/** GET /members/:id */
export const getMemberById = async (id) => {
  const res = await api.get(`/members/${id}`);
  return res.data?.data;
};

// Remove a specific food/drink item from a bill
export const removeBillItem = async (billId, itemId) => {
  const res = await api.delete(`/bills/${billId}/items/${itemId}`);
  return res.data?.data;
};
