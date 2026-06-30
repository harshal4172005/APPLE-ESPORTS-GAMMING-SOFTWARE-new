import api from '../config/api';

// --- Food Inventory Management (Super Admin only except GET) ---
export const getInventory = async (params = {}) => {
  const response = await api.get('/inventory', { params });
  return response.data;
};

export const createInventoryItem = async (itemData) => {
  const response = await api.post('/inventory', itemData);
  return response.data;
};

export const updateInventoryItem = async (id, itemData) => {
  const response = await api.put(`/inventory/${id}`, itemData);
  return response.data;
};

export const deleteInventoryItem = async (id) => {
  const response = await api.delete(`/inventory/${id}`);
  return response.data;
};

export const reconcileStock = async (id, reconcileData) => {
  const response = await api.post(`/inventory/${id}/reconcile`, reconcileData);
  return response.data;
};

export const getDiscrepancies = async (params = {}) => {
  const response = await api.get('/inventory/discrepancies', { params });
  return response.data;
};

// --- Food Orders ---
export const getActiveOrders = async (params = {}) => {
  const response = await api.get('/food-orders', { params });
  return response.data;
};

export const getOrder = async (id) => {
  const response = await api.get(`/food-orders/${id}`);
  return response.data;
};

export const placeOrder = async (orderData) => {
  const response = await api.post('/food-orders', orderData);
  return response.data;
};

export const updateOrderStatus = async (id, statusData) => {
  const response = await api.put(`/food-orders/${id}/status`, statusData);
  return response.data;
};

// --- Eod Range Report ---
export const getRangeReport = async (params = {}) => {
  const response = await api.get('/eod/range-report', { params });
  return response.data;
};
