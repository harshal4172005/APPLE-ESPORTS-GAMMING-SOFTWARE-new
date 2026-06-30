import api from '../config/api';

export const getCashReconciliationReport = (params) => {
  return api.get('/reports/cash-reconciliation', { params });
};
