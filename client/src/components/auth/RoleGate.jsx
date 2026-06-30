// ═══════════════════════════════════════════════════════════
// Gaming Café ERP — Role Gate Component
// SOP §19.2: Component-level permission control
// ═══════════════════════════════════════════════════════════

import { useAuth } from '../../contexts/AuthContext';
import { ROLES } from '../../config/constants';

/**
 * Conditionally render children based on role/permission
 * Usage: <RoleGate roles={[ROLES.SUPER_ADMIN]}>Admin Content</RoleGate>
 * Usage: <RoleGate dashboard="settings">Settings Content</RoleGate>
 */
export default function RoleGate({ children, roles, dashboard, fallback = null }) {
  const { user, hasDashboardAccess } = useAuth();

  if (!user) return fallback;

  // Check role
  const userRole = user.role || user.Role;
  if (roles && !roles.some(r => r === userRole || (typeof userRole === 'string' && userRole.toLowerCase().includes(r.replace('_', ''))))) {
    return fallback;
  }

  // Check dashboard permission
  if (dashboard && !hasDashboardAccess(dashboard)) {
    return fallback;
  }

  return children;
}
