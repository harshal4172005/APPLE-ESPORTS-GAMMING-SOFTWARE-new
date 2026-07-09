// ═══════════════════════════════════════════════════════════
// Gaming Café ERP — Frontend Constants
// Mirrors backend constants.js for consistency
// ═══════════════════════════════════════════════════════════

export const ROLES = {
  SUPER_ADMIN: 'super_admin',
  ADMIN: 'admin',
  OPERATOR: 'operator',
  USER_PANEL: 'user_panel',
};

export const PC_STATES = {
  IDLE: 'idle',
  ACTIVE: 'active',
  RESERVED: 'reserved',
  AWAITING_BILLING: 'awaiting_billing',
  OFFLINE: 'offline',
};

export const SESSION_STATES = {
  ACTIVE: 'active',
  RESERVED: 'reserved',
  AWAITING_BILLING: 'awaiting_billing',
  COMPLETED: 'completed',
  EXPIRED: 'expired',
};

export const PAYMENT_TYPES = {
  CASH: 'cash',
  ONLINE: 'online',
  SPLIT: 'split',
  WALLET: 'wallet',
};

export const ORDER_STATUSES = {
  PENDING: 'pending',
  PREPARING: 'preparing',
  READY: 'ready',
  DELIVERED: 'delivered',
  COMPLETED: 'completed',
  CANCELLED: 'cancelled',
};

// SOP §19.2: Dashboard keys for permission checking
export const DASHBOARDS = {
  BILLING_COUNTER: 'billing_counter',
  SESSIONS: 'sessions',
  RESERVATIONS: 'reservations',
  FOOD_ORDERS: 'food_orders',
  CREDITS: 'credits',
  CASH_REGISTER: 'cash_register',
  CASH_DESK: 'cash_desk',
  ONLINE_DESK: 'online_desk',
  WALLET_DESK: 'wallet_desk',
  MEMBERS: 'members',
  MENU_EDITOR: 'menu_editor',
  MAIN_DASHBOARD: 'main_dashboard',
  PC_STATUS: 'pc_status',
  EOD: 'eod',
  SETTINGS: 'settings',
  REPORTS: 'reports',
};

// PC State color mapping (matches tailwind.config.js)
export const PC_STATE_COLORS = {
  [PC_STATES.IDLE]: 'pc-idle',
  [PC_STATES.ACTIVE]: 'pc-active',
  [PC_STATES.RESERVED]: 'pc-reserved',
  [PC_STATES.AWAITING_BILLING]: 'pc-awaiting',
  [PC_STATES.OFFLINE]: 'pc-offline',
};

export const PC_STATE_LABELS = {
  [PC_STATES.IDLE]: 'Idle',
  [PC_STATES.ACTIVE]: 'Active',
  [PC_STATES.RESERVED]: 'Reserved',
  [PC_STATES.AWAITING_BILLING]: 'Awaiting Bill',
  [PC_STATES.OFFLINE]: 'Shut Down',
};
