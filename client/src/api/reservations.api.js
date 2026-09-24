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

/** PUT /reservations/:id/arrived — a plain hand-set reminder flag, not a gate on anything.
    Starting a session for this customer goes through the ordinary Start Session flow either
    way, same as a walk-in. */
export const setReservationArrived = (id, arrived) =>
  api.put(`/reservations/${id}/arrived`, { arrived }).then(r => r.data?.data);

/** DELETE /reservations/:id — the "Remove" button. Permanent, no reason kept. */
export const deleteReservation = (id) =>
  api.delete(`/reservations/${id}`).then(r => r.data);
