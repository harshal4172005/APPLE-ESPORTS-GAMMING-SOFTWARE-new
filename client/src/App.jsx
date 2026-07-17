// ═══════════════════════════════════════════════════════════
// Gaming Café ERP — Main App with Complete Routing
// SOP §5: Role hierarchy → route protection
// SOP §19.2: Dashboard-level permission control
// ═══════════════════════════════════════════════════════════

import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import { SocketProvider } from './contexts/SocketContext';
import { BranchProvider } from './contexts/BranchContext';
import { ToastProvider } from './components/ui/Toast';
import ProtectedRoute from './components/auth/ProtectedRoute';
import AppShell from './components/layout/AppShell';
import { ROLES, DASHBOARDS } from './config/constants';

// ── Auth Pages ──
import OperatorLoginPage from './pages/auth/OperatorLoginPage';
import AdminLoginPage from './pages/auth/AdminLoginPage';
import SuperAdminLoginPage from './pages/auth/SuperAdminLoginPage';
import UnauthorizedPage from './pages/auth/UnauthorizedPage';
import NotFoundPage from './pages/NotFoundPage';

// ── Public User Pages ──
import LandingGatewayPage from './pages/public/LandingGatewayPage';
import UserFlowSelectionPage from './pages/public/UserFlowSelectionPage';
import MemberLoginPage from './pages/public/MemberLoginPage';
import LimitedUserPage from './pages/public/LimitedUserPage';
import MemberPortalPage from './pages/public/MemberPortalPage';
import SetupPage from './pages/public/SetupPage';
import ForgotPasswordPage from './pages/public/ForgotPasswordPage';
import ResetPasswordPage from './pages/public/ResetPasswordPage';
import UserOverlayApp from './pages/overlay/UserOverlayApp';
import SetupPcPage from './pages/admin/SetupPcPage';

// ── Operations ──
import BillingCounterPage from './pages/billing/BillingCounterPage';
import SessionsPage from './pages/sessions/SessionsPage';
import ReservationsPage from './pages/reservations/ReservationsPage';
import FoodOrdersPage from './pages/food/FoodOrdersPage';
import CustomerPanelPage from './pages/food/CustomerPanelPage';

// ── Finance ──
import CashRegisterPage from './pages/cash/CashRegisterPage';
import CashDeskPage from './pages/cash/CashDeskPage';
import OnlineDeskPage from './pages/finance/OnlineDeskPage';
import WalletDeskPage from './pages/finance/WalletDeskPage';
import CreditsPage from './pages/credits/CreditsPage';
import EodDashboardPage from './pages/eod/EodDashboardPage';

// ── Management ──
import MembersPage from './pages/members/MembersPage';
import MenuEditorPage from './pages/menu/MenuEditorPage';

// ── Admin ──
import MainDashboardPage from './pages/dashboard/MainDashboardPage';
import PcStatusPage from './pages/admin/PcStatusPage';
import SettingsPage from './pages/admin/SettingsPage';
import ReportsPage from './pages/admin/ReportsPage';

// ── HR ──
import EmployeeFormsPage from './pages/hr/EmployeeFormsPage';

// ── End of imports ──
function HomeRedirect() {
  const { isAuthenticated } = useAuth();
  if (!isAuthenticated) {
    return <Navigate to="/" replace />;
  }
  // All roles (Operator, Admin, SuperAdmin) should land on Sessions first
  return <Navigate to="/app/sessions" replace />;
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <SocketProvider>
          <BranchProvider>
            <ToastProvider>
              <Routes>
                {/* ══════════ Public Routes ══════════ */}
                <Route path="/login/operator" element={<OperatorLoginPage />} />
                <Route path="/login/admin" element={<AdminLoginPage />} />
                <Route path="/login/superadmin" element={<SuperAdminLoginPage />} />
                {/* Fallback to clear out stuck /login URLs */}
                <Route path="/login" element={<Navigate to="/" replace />} />
                <Route path="/unauthorized" element={<UnauthorizedPage />} />
                <Route path="/customer-panel" element={<CustomerPanelPage />} />
                <Route path="/user/select" element={<UserFlowSelectionPage />} />
                <Route path="/user/member-login" element={<MemberLoginPage />} />
                <Route path="/user/limited" element={<LimitedUserPage />} />
                <Route path="/user/member-portal" element={<MemberPortalPage />} />
                <Route path="/pc-overlay/:pcId/*" element={<UserOverlayApp />} />
                <Route path="/setup-pc" element={<SetupPcPage />} />
                <Route path="/setup/:role" element={<SetupPage />} />
                <Route path="/forgot-password" element={<ForgotPasswordPage />} />
                <Route path="/reset-password" element={<ResetPasswordPage />} />

                {/* ══════════ Protected App Shell ══════════ */}
                <Route
                  path="/app"
                  element={
                    <ProtectedRoute allowedRoles={[ROLES.SUPER_ADMIN, ROLES.ADMIN, ROLES.OPERATOR]}>
                      <AppShell />
                    </ProtectedRoute>
                  }
                >
                  {/* Default redirect based on role */}
                  <Route index element={<HomeRedirect />} />

                  {/* ── Operations Dashboards ── */}
                  <Route
                    path="billing"
                    element={
                      <ProtectedRoute dashboardKey={DASHBOARDS.BILLING_COUNTER}>
                        <BillingCounterPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="sessions"
                    element={
                      <ProtectedRoute dashboardKey={DASHBOARDS.SESSIONS}>
                        <SessionsPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="reservations"
                    element={
                      <ProtectedRoute dashboardKey={DASHBOARDS.RESERVATIONS}>
                        <ReservationsPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="food-orders"
                    element={
                      <ProtectedRoute dashboardKey={DASHBOARDS.FOOD_ORDERS}>
                        <FoodOrdersPage />
                      </ProtectedRoute>
                    }
                  />

                  {/* ── Finance Dashboards ── */}
                  <Route
                    path="cash-register"
                    element={
                      <ProtectedRoute dashboardKey={DASHBOARDS.CASH_REGISTER}>
                        <CashRegisterPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="cash-desk"
                    element={
                      <ProtectedRoute dashboardKey={DASHBOARDS.CASH_DESK}>
                        <CashDeskPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="online-desk"
                    element={
                      <ProtectedRoute dashboardKey={DASHBOARDS.ONLINE_DESK}>
                        <OnlineDeskPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="credits"
                    element={
                      <ProtectedRoute dashboardKey={DASHBOARDS.CREDITS}>
                        <CreditsPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="wallet-desk"
                    element={
                      <ProtectedRoute dashboardKey={DASHBOARDS.WALLET_DESK}>
                        <WalletDeskPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="eod"
                    element={
                      <ProtectedRoute dashboardKey={DASHBOARDS.EOD}>
                        <EodDashboardPage />
                      </ProtectedRoute>
                    }
                  />

                  {/* ── Management Dashboards ── */}
                  <Route
                    path="members"
                    element={
                      <ProtectedRoute dashboardKey={DASHBOARDS.MEMBERS}>
                        <MembersPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="menu-editor"
                    element={
                      <ProtectedRoute dashboardKey={DASHBOARDS.MENU_EDITOR}>
                        <MenuEditorPage />
                      </ProtectedRoute>
                    }
                  />

                  {/* ── Admin Only Dashboards ── */}
                  <Route
                    path="dashboard"
                    element={
                      <ProtectedRoute allowedRoles={[ROLES.SUPER_ADMIN, ROLES.ADMIN, ROLES.OPERATOR]}>
                        <MainDashboardPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="reports"
                    element={
                      <ProtectedRoute allowedRoles={[ROLES.SUPER_ADMIN, ROLES.ADMIN]} dashboardKey={DASHBOARDS.REPORTS}>
                        <ReportsPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="pc-status"
                    element={
                      <ProtectedRoute allowedRoles={[ROLES.SUPER_ADMIN, ROLES.ADMIN]} dashboardKey={DASHBOARDS.PC_STATUS}>
                        <PcStatusPage />
                      </ProtectedRoute>
                    }
                  />
                  <Route
                    path="settings"
                    element={
                      <ProtectedRoute allowedRoles={[ROLES.SUPER_ADMIN, ROLES.ADMIN]} dashboardKey={DASHBOARDS.SETTINGS}>
                        <SettingsPage />
                      </ProtectedRoute>
                    }
                  />

                  {/* Catch-all inside /app */}
                  <Route path="*" element={<NotFoundPage />} />

                  {/* ── HR Module ── */}
                  <Route
                    path="employee-forms"
                    element={
                      <ProtectedRoute allowedRoles={[ROLES.SUPER_ADMIN, ROLES.ADMIN]}>
                        <EmployeeFormsPage />
                      </ProtectedRoute>
                    }
                  />
                </Route>

                {/* ══════════ Root Redirects ══════════ */}
                <Route path="/" element={<LandingGatewayPage />} />
                <Route path="*" element={<NotFoundPage />} />
              </Routes>
            </ToastProvider>
          </BranchProvider>
        </SocketProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}
