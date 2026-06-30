// ═══════════════════════════════════════════════════════════
// Gaming Café ERP — AppShell Layout
// Wraps all authenticated pages with Topbar + Sidebar + Content area
// SOP §6.3: Operator shift start/end modals
// ═══════════════════════════════════════════════════════════

import { useState, useEffect, useCallback } from 'react';
import { Outlet } from 'react-router-dom';
import Topbar from './Topbar';
import Sidebar from './Sidebar';
import BranchRequired from './BranchRequired';
import { useAuth } from '../../contexts/AuthContext';
import ShiftStartModal from '../shift/ShiftStartModal';
import ShiftEndModal from '../shift/ShiftEndModal';
import GlobalFoodOrderListener from './GlobalFoodOrderListener';
import GlobalNotificationListener from './GlobalNotificationListener';

export default function AppShell() {
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const { user, isOperator, logout } = useAuth();

  // ── Shift Start Modal: show for operators on first login ──
  const [showShiftStart, setShowShiftStart] = useState(false);
  const [shiftStartDone, setShiftStartDone] = useState(false);

  // ── Shift End Modal: shown when operator requests logout ──
  const [showShiftEnd, setShowShiftEnd] = useState(false);

  // Check if operator needs to do shift start checklist
  // We store a flag in sessionStorage so it doesn't show again on page refresh mid-shift
  useEffect(() => {
    if (!isOperator || !user) return;

    const sessionKey = `shift_start_done_${user.id || user.username}`;
    const alreadyDone = sessionStorage.getItem(sessionKey);

    if (!alreadyDone) {
      setShowShiftStart(true);
      setShiftStartDone(false);
    } else {
      setShiftStartDone(true);
    }
  }, [isOperator, user]);

  const handleShiftStartComplete = useCallback(() => {
    if (user) {
      const sessionKey = `shift_start_done_${user.id || user.username}`;
      sessionStorage.setItem(sessionKey, 'true');
    }
    setShowShiftStart(false);
    setShiftStartDone(true);
  }, [user]);

  // Called from Topbar when operator clicks Logout
  const handleRequestLogout = useCallback(() => {
    if (isOperator) {
      // Show shift end modal for operators
      setShowShiftEnd(true);
    } else {
      // Super admin: logout directly
      logout();
    }
  }, [isOperator, logout]);

  const handleShiftEndComplete = useCallback(async () => {
    setShowShiftEnd(false);
    // Clear the shift start session flag so next login shows it again
    if (user) {
      const sessionKey = `shift_start_done_${user.id || user.username}`;
      sessionStorage.removeItem(sessionKey);
    }
    await logout();
  }, [logout, user]);

  const handleShiftEndCancel = useCallback(() => {
    setShowShiftEnd(false);
  }, []);

  return (
    <div className="min-h-screen bg-bg flex flex-col">
      {/* Fixed Topbar */}
      <Topbar
        onToggleSidebar={() => setSidebarOpen(!sidebarOpen)}
        sidebarOpen={sidebarOpen}
        onLogoutClick={handleRequestLogout}
      />

      {/* Content area: Sidebar + Main */}
      <div className="flex flex-1">
        {/* Sidebar */}
        <Sidebar
          isOpen={sidebarOpen}
          onClose={() => setSidebarOpen(false)}
        />

        {/* Main content area */}
        <main className="flex-1 min-w-0 overflow-auto">
          <div className="p-3 sm:p-4 max-w-[1600px]">
            <BranchRequired>
              <Outlet />
            </BranchRequired>
          </div>
        </main>
      </div>

      {/* ── Shift Start Modal (blocks operator until complete) ── */}
      {isOperator && showShiftStart && (
        <ShiftStartModal onComplete={handleShiftStartComplete} />
      )}

      {/* ── Shift End Modal (shown on logout request) ── */}
      {showShiftEnd && (
        <ShiftEndModal
          onComplete={handleShiftEndComplete}
          onCancel={handleShiftEndCancel}
        />
      )}

      {/* Global Background Listeners */}
      <GlobalFoodOrderListener />
      <GlobalNotificationListener />
    </div>
  );
}
