import api from '../config/api';

// ── Reservation System API ───────────────────────────────────────────────────

/** GET /reservations?page=&pageSize= */
export const getActiveReservations = (page = 1, pageSize = 50) =>
  api.get('/reservations', { params: { page, pageSize } })
     .then(r => r.data?.data?.items || []);

/** GET /reservations/:id */
export const getReservation = (id) =>
  api.get(`/reservations/${id}`).then(r => r.data?.data);

/** POST /reservations */
export const createReservation = (payload) =>
  api.post('/reservations', payload).then(r => r.data?.data);

/** POST /reservations/:id/cancel */
export const cancelReservation = (id, payload) =>
  api.post(`/reservations/${id}/cancel`, payload).then(r => r.data?.data);

/** POST /reservations/:id/start */
export const startReservedSession = (id) =>
  api.post(`/reservations/${id}/start`).then(r => r.data?.data);

/** POST /reservations/:id/override */
export const overrideReservation = (id, payload) =>
  api.post(`/reservations/${id}/override`, payload).then(r => r.data?.data);
