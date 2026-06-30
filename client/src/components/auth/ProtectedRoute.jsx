// ═══════════════════════════════════════════════════════════
// Gaming Café ERP — Protected Route Component
// SOP §21: NEVER TRUST FRONTEND — role-based route guard
// ═══════════════════════════════════════════════════════════

import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';

/**
 * Route guard that requires authentication and optionally specific roles
 * @param {string[]} allowedRoles - Roles allowed to access this route
 * @param {string} dashboardKey - Optional dashboard key for permission checking
 */
export default function ProtectedRoute({ children, allowedRoles, dashboardKey }) {
  const { isAuthenticated, user, loading, hasDashboardAccess } = useAuth();
  const location = useLocation();

  // Show nothing while checking auth state
  if (loading) {
    return (
      <div className="min-h-screen bg-bg flex items-center justify-center">
        <div className="text-center">
          <div className="w-8 h-8 border-2 border-accent border-t-transparent rounded-full animate-spin mx-auto mb-3" />
          <p className="text-text-2 text-sm font-mono">Authenticating...</p>
        </div>
      </div>
    );
  }

  // Not logged in → redirect to login
  if (!isAuthenticated) {
    return <Navigate to="/" state={{ from: location }} replace />;
  }

  // Check role restriction
  const userRole = user.role || user.Role;
  if (allowedRoles && !allowedRoles.some(r => r === userRole || (typeof userRole === 'string' && userRole.toLowerCase().includes(r.replace('_', ''))))) {
    return <Navigate to="/unauthorized" replace />;
  }

  // Check dashboard permission (SOP §19.2)
  if (dashboardKey && !hasDashboardAccess(dashboardKey)) {
    return <Navigate to="/unauthorized" replace />;
  }

  return children;
}
