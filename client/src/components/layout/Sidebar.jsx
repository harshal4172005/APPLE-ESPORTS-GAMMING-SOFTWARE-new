// ═══════════════════════════════════════════════════════════
// Gaming Café ERP — Sidebar Navigation
// SOP §19: Dashboard-level permission control
// Apple Esports — collapsible sidebar with role-based visibility
// ═══════════════════════════════════════════════════════════

import { NavLink, useLocation } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { ROLES, DASHBOARDS } from '../../config/constants';

const NAV_ITEMS = [
  {
    section: 'Operations',
    items: [
      {
        label: 'Billing Counter',
        route: '/app/billing',
        dashboard: DASHBOARDS.BILLING_COUNTER,
        roles: [ROLES.SUPER_ADMIN, ROLES.ADMIN, ROLES.OPERATOR],
        icon: 'M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01',
      },
      {
        label: 'Sessions',
        route: '/app/sessions',
        dashboard: DASHBOARDS.SESSIONS,
        roles: [ROLES.SUPER_ADMIN, ROLES.ADMIN, ROLES.OPERATOR],
        icon: 'M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z',
      },
      {
        label: 'Reservations',
        route: '/app/reservations',
        dashboard: DASHBOARDS.RESERVATIONS,
        roles: [ROLES.SUPER_ADMIN, ROLES.ADMIN, ROLES.OPERATOR],
        icon: 'M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z',
      },
      {
        label: 'Food Orders',
        route: '/app/food-orders',
        dashboard: DASHBOARDS.FOOD_ORDERS,
        roles: [ROLES.SUPER_ADMIN, ROLES.ADMIN, ROLES.OPERATOR],
        icon: 'M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2.293 2.293c-.63.63-.184 1.707.707 1.707H17m0 0a2 2 0 100 4 2 2 0 000-4zm-8 2a2 2 0 100 4 2 2 0 000-4z',
      },
    ],
  },
  {
    section: 'Finance',
    items: [
      {
        label: 'Cash Desk',
        route: '/app/cash-register',
        dashboard: DASHBOARDS.CASH_REGISTER,
        roles: [ROLES.SUPER_ADMIN, ROLES.ADMIN, ROLES.OPERATOR],
        icon: 'M17 9V7a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2m2 4h10a2 2 0 002-2v-6a2 2 0 00-2-2H9a2 2 0 00-2 2v6a2 2 0 002 2zm7-5a2 2 0 11-4 0 2 2 0 014 0z',
      },
      {
        label: 'Cash Register',
        route: '/app/cash-desk',
        dashboard: DASHBOARDS.CASH_DESK,
        roles: [ROLES.SUPER_ADMIN, ROLES.ADMIN, ROLES.OPERATOR],
        icon: 'M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4',
      },
      {
        label: 'Online Desk',
        route: '/app/online-desk',
        dashboard: DASHBOARDS.ONLINE_DESK,
        roles: [ROLES.SUPER_ADMIN, ROLES.ADMIN, ROLES.OPERATOR],
        icon: 'M21 12a9 9 0 01-9 9m9-9a9 9 0 00-9-9m9 9H3m9 9a9 9 0 01-9-9m9 9c1.657 0 3-4.03 3-9s-1.343-9-3-9m0 18c-1.657 0-3-4.03-3-9s1.343-9 3-9m-9 9a9 9 0 019-9',
      },
      {
        label: 'Wallet Desk',
        route: '/app/wallet-desk',
        dashboard: DASHBOARDS.WALLET_DESK,
        roles: [ROLES.SUPER_ADMIN, ROLES.ADMIN, ROLES.OPERATOR],
        icon: 'M3 10h18M7 15h1m4 0h1m-7 4h12a3 3 0 003-3V8a3 3 0 00-3-3H6a3 3 0 00-3 3v8a3 3 0 003 3z',
      },
      {
        label: 'Credits',
        route: '/app/credits',
        dashboard: DASHBOARDS.CREDITS,
        roles: [ROLES.SUPER_ADMIN, ROLES.ADMIN, ROLES.OPERATOR],
        icon: 'M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z',
      },
      {
        label: 'End of Day',
        route: '/app/eod',
        dashboard: DASHBOARDS.EOD,
        roles: [ROLES.SUPER_ADMIN, ROLES.ADMIN, ROLES.OPERATOR],
        icon: 'M9 17v-2m3 2v-4m3 4v-6m2 10H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z',
      },
    ],
  },
  {
    section: 'Management',
    items: [
      {
        label: 'Members',
        route: '/app/members',
        dashboard: DASHBOARDS.MEMBERS,
        roles: [ROLES.SUPER_ADMIN, ROLES.ADMIN, ROLES.OPERATOR],
        icon: 'M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z',
      },
      {
        label: 'Menu Editor',
        route: '/app/menu-editor',
        dashboard: DASHBOARDS.MENU_EDITOR,
        roles: [ROLES.SUPER_ADMIN, ROLES.ADMIN, ROLES.OPERATOR],
        icon: 'M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253',
      },
    ],
  },
  {
    section: 'Admin',
    items: [
      {
        label: 'Dashboard',
        route: '/app/dashboard',
        dashboard: DASHBOARDS.MAIN_DASHBOARD,
        roles: [ROLES.SUPER_ADMIN, ROLES.ADMIN, ROLES.OPERATOR],
        icon: 'M4 5a1 1 0 011-1h14a1 1 0 011 1v2a1 1 0 01-1 1H5a1 1 0 01-1-1V5zM4 13a1 1 0 011-1h6a1 1 0 011 1v6a1 1 0 01-1 1H5a1 1 0 01-1-1v-6zM16 13a1 1 0 011-1h2a1 1 0 011 1v6a1 1 0 01-1 1h-2a1 1 0 01-1-1v-6z',
      },
      {
        label: 'PC Status',
        route: '/app/pc-status',
        dashboard: DASHBOARDS.PC_STATUS,
        roles: [ROLES.SUPER_ADMIN, ROLES.ADMIN, ROLES.OPERATOR],
        icon: 'M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z',
      },
      {
        label: 'Reports',
        route: '/app/reports',
        dashboard: DASHBOARDS.REPORTS,
        roles: [ROLES.SUPER_ADMIN, ROLES.ADMIN, ROLES.OPERATOR],
        icon: 'M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 002 2h2a2 2 0 002-2',
      },
      {
        label: 'Settings',
        route: '/app/settings',
        dashboard: DASHBOARDS.SETTINGS,
        roles: [ROLES.SUPER_ADMIN, ROLES.ADMIN, ROLES.OPERATOR],
        icon: 'M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.066 2.573c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.573 1.066c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.066-2.573c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z',
        secondIcon: 'M15 12a3 3 0 11-6 0 3 3 0 016 0z',
      },
    ],
  },
  {
    section: 'HR',
    items: [
      {
        label: 'Employee Forms',
        route: '/app/employee-forms',
        dashboard: DASHBOARDS.EMPLOYEE_FORMS,
        roles: [ROLES.SUPER_ADMIN, ROLES.ADMIN],
        icon: 'M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z',
      },
    ],
  },
];

export default function Sidebar({ isOpen, onClose }) {
  const { user, isSuperAdmin, hasDashboardAccess } = useAuth();
  const location = useLocation();

  /**
   * Check if nav item is visible to current user
   * SOP §19.2: Dashboard access is permission-controlled per operator
   */
  function isItemVisible(item) {
    // Role check
    if (!item.roles.includes(user?.role)) return false;
    // Dashboard permission check (Super Admin always passes)
    if (isSuperAdmin) return true;
    return hasDashboardAccess(item.dashboard);
  }

  return (
    <>
      {/* Mobile overlay */}
      {isOpen && (
        <div
          className="fixed inset-0 bg-black/60 z-40 lg:hidden backdrop-blur-sm"
          onClick={onClose}
        />
      )}

      {/* Sidebar */}
      <aside
        className={`
          fixed top-0 left-0 h-full bg-bg-2 border-r border-border z-50
          w-[240px] flex flex-col
          transition-transform duration-200 ease-in-out
          lg:sticky lg:top-[53px] lg:h-[calc(100vh-53px)] lg:translate-x-0
          ${isOpen ? 'translate-x-0' : '-translate-x-full'}
        `}
      >
        {/* Mobile close button */}
        <div className="lg:hidden flex items-center justify-between p-3 border-b border-border">
          <span className="text-xs font-heading font-semibold text-text-2 tracking-wider">NAVIGATION</span>
          <button onClick={onClose} className="text-text-2 hover:text-text">
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Navigation Links */}
        <nav className="flex-1 overflow-y-auto py-2 px-2 scrollbar-thin">
          {NAV_ITEMS.map((section) => {
            // Permissions handle visibility instead
            // if (section.adminOnly && user?.role === ROLES.OPERATOR) return null;

            const visibleItems = section.items.filter(isItemVisible);
            if (visibleItems.length === 0) return null;

            return (
              <div key={section.section} className="mb-1">
                {/* Section Header */}
                <div className="px-3 py-2 mt-1">
                  <span className="text-[9px] font-heading font-semibold text-text-3 tracking-[0.15em] uppercase">
                    {section.section}
                  </span>
                </div>

                {/* Nav Items */}
                {visibleItems.map((item) => (
                  <NavLink
                    key={item.route}
                    to={item.route}
                    onClick={onClose}
                    className={({ isActive }) =>
                      `flex items-center gap-2.5 px-3 py-2 rounded-sm text-[12px] font-medium transition-all duration-150 group relative ${
                        isActive
                          ? 'bg-accent/8 text-accent border-l-2 border-accent ml-0'
                          : 'text-text-2 hover:text-text hover:bg-bg-3 border-l-2 border-transparent'
                      }`
                    }
                  >
                    {/* Icon */}
                    <svg
                      className="w-4 h-4 flex-shrink-0 transition-colors"
                      fill="none"
                      stroke="currentColor"
                      viewBox="0 0 24 24"
                      strokeWidth={1.8}
                    >
                      <path strokeLinecap="round" strokeLinejoin="round" d={item.icon} />
                      {item.secondIcon && (
                        <path strokeLinecap="round" strokeLinejoin="round" d={item.secondIcon} />
                      )}
                    </svg>

                    {/* Label */}
                    <span className="truncate">{item.label}</span>

                    {/* Active indicator dot */}
                    {location.pathname === item.route && (
                      <span className="absolute right-3 w-1 h-1 rounded-full bg-accent" />
                    )}
                  </NavLink>
                ))}
              </div>
            );
          })}
        </nav>

        {/* Bottom: Shift Info / Footer */}
        <div className="border-t border-border p-3">
          {/* Operator shift indicator */}
          {user?.role === ROLES.OPERATOR && (
            <div className="flex items-center gap-2 mb-2 px-1">
              <span className="w-1.5 h-1.5 rounded-full bg-accent animate-blink" />
              <span className="text-[10px] text-text-2 font-mono tracking-wider">SHIFT ACTIVE</span>
            </div>
          )}
          <div className="text-[9px] text-text-3 font-mono tracking-wide px-1">
            v2.0 · SOP Compliant
          </div>
        </div>
      </aside>
    </>
  );
}
