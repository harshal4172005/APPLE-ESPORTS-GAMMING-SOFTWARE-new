// ═══════════════════════════════════════════════════════════
// Gaming Café ERP — Topbar Component
// Apple Esports style: logo, live badge, clock, date,
// branch selector, user info, notifications
// ═══════════════════════════════════════════════════════════

import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { useBranch } from '../../contexts/BranchContext';
import { useSocket } from '../../contexts/SocketContext';
import { ROLES } from '../../config/constants';
import AdminSwitchModal from '../auth/AdminSwitchModal';

export default function Topbar({ onToggleSidebar, sidebarOpen, onLogoutClick }) {
  const navigate = useNavigate();
  const { user, baseUser, adminSwitchUser, logout, isSuperAdmin, exitAdminSwitch } = useAuth();
  const { branches, activeBranch, switchBranch } = useBranch();
  const { connected } = useSocket();
  const [clock, setClock] = useState('');
  const [date, setDate] = useState('');
  const [showUserMenu, setShowUserMenu] = useState(false);
  const [showBranchMenu, setShowBranchMenu] = useState(false);
  const [showAdminSwitchModal, setShowAdminSwitchModal] = useState(false);

  // ── Live Clock ──
  useEffect(() => {
    function tick() {
      const now = new Date();
      setClock(now.toLocaleTimeString('en-IN', {
        timeZone: 'Asia/Kolkata',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: true,
      }));
      setDate(now.toLocaleDateString('en-IN', {
        timeZone: 'Asia/Kolkata',
        weekday: 'short',
        day: '2-digit',
        month: 'short',
        year: 'numeric',
      }));
    }
    tick();
    const id = setInterval(tick, 1000);
    return () => clearInterval(id);
  }, []);

  // Close menus on outside click
  useEffect(() => {
    function handler(e) {
      if (!e.target.closest('.user-menu-wrap')) setShowUserMenu(false);
      if (!e.target.closest('.branch-menu-wrap')) setShowBranchMenu(false);
    }
    document.addEventListener('click', handler);
    return () => document.removeEventListener('click', handler);
  }, []);

  const handleLogout = useCallback(async () => {
    if (onLogoutClick) {
      onLogoutClick();
    } else {
      await logout();
    }
  }, [logout, onLogoutClick]);

  const handleExitAdminSwitch = useCallback(async () => {
    await exitAdminSwitch();
    navigate('/app/billing');
    setShowUserMenu(false);
  }, [exitAdminSwitch, navigate]);

  return (
    <>
    {/* Admin Mode Banner */}
    {adminSwitchUser && (
      <div className="bg-neon-red text-white text-[11px] font-bold tracking-widest py-1.5 px-4 flex items-center justify-center gap-4 z-[60] relative">
        <span className="animate-pulse">⚠️ ADMIN MODE ACTIVE ⚠️</span>
        <span className="font-medium opacity-80">|</span>
        <span>Original Operator: {baseUser?.fullName || baseUser?.full_name}</span>
        <span className="font-medium opacity-80">|</span>
        <button 
          onClick={handleExitAdminSwitch}
          className="bg-white/20 hover:bg-white/30 px-3 py-0.5 rounded transition-colors ml-4 uppercase"
        >
          Exit Admin Mode
        </button>
      </div>
    )}

    <header id="topbar" className="bg-bg-2 border-b border-border px-4 py-2.5 flex items-center justify-between gap-3 sticky top-0 z-50">
      {/* Left: Hamburger + Logo */}
      <div className="flex items-center gap-3">
        {/* Mobile hamburger */}
        <button
          onClick={onToggleSidebar}
          className="lg:hidden text-text-2 hover:text-text p-1 -ml-1"
          aria-label="Toggle sidebar"
        >
          <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            {sidebarOpen ? (
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            ) : (
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
            )}
          </svg>
        </button>

        {/* Logo */}
        <div className="flex items-center gap-2.5">
          <img src="/logo.png" alt="Apple Esports" className="h-8 w-auto flex-shrink-0" />
          <div className="hidden sm:block">
            <div className="font-heading text-lg font-bold tracking-wider leading-tight text-text">
              Apple Esports
            </div>
            <div className="text-[9px] text-text-2 font-mono tracking-widest">
              GAMING CAFÉ ERP · v2.0
            </div>
          </div>
        </div>
      </div>

      {/* Right: Branch Selector, Date, Clock, Live, User */}
      <div className="flex items-center gap-3">
        {/* Branch Selector (Super Admin only) */}
        {isSuperAdmin && branches.length > 0 && (
          <div className="branch-menu-wrap relative hidden md:block">
            <button
              onClick={(e) => { e.stopPropagation(); setShowBranchMenu(!showBranchMenu); }}
              className="flex items-center gap-2 px-3 py-1.5 bg-bg-3 border border-border rounded-sm text-xs hover:border-accent transition-colors"
            >
              <svg className="w-3.5 h-3.5 text-accent" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
              </svg>
              <span className="text-text font-medium max-w-[120px] truncate">
                {activeBranch?.name || 'All Branches'}
              </span>
              <svg className="w-3 h-3 text-text-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
              </svg>
            </button>

            {showBranchMenu && (
              <div className="absolute right-0 top-full mt-1.5 bg-bg-2 border border-border rounded-md shadow-xl min-w-[180px] py-1 z-50">
                <div className="px-3 py-1.5 text-[10px] text-text-2 font-mono tracking-wider border-b border-border">
                  SWITCH BRANCH
                </div>
                
                <button
                  onClick={() => { switchBranch(null); setShowBranchMenu(false); }}
                  className={`w-full text-left px-3 py-2 text-xs hover:bg-bg-3 transition-colors flex items-center gap-2 border-b border-border/40 ${
                    activeBranch === null ? 'text-accent bg-accent/5' : 'text-text'
                  }`}
                >
                  {activeBranch === null && (
                    <span className="w-1.5 h-1.5 rounded-full bg-accent flex-shrink-0" />
                  )}
                  <span className="font-semibold">All Branches (Global)</span>
                </button>

                {branches.map((b) => (
                  <button
                    key={b.id}
                    onClick={() => { switchBranch(b.id); setShowBranchMenu(false); }}
                    className={`w-full text-left px-3 py-2 text-xs hover:bg-bg-3 transition-colors flex items-center gap-2 ${
                      activeBranch?.id === b.id ? 'text-accent bg-accent/5' : 'text-text'
                    }`}
                  >
                    {activeBranch?.id === b.id && (
                      <span className="w-1.5 h-1.5 rounded-full bg-accent flex-shrink-0" />
                    )}
                    <span>{b.name}</span>
                  </button>
                ))}
              </div>
            )}
          </div>
        )}

        {/* Operator Branch Badge */}
        {!isSuperAdmin && user?.branchName && (
          <div className="hidden md:flex items-center gap-1.5 px-2.5 py-1 bg-accent/5 border border-accent/20 rounded-sm text-xs">
            <svg className="w-3 h-3 text-accent" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
            </svg>
            <span className="text-accent font-medium">{user.branchName}</span>
          </div>
        )}

        {/* Date */}
        <span className="hidden lg:block text-[11px] text-text-2">{date}</span>

        {/* Clock */}
        <span className="hidden sm:block font-mono text-xs text-text-2">{clock}</span>

        {/* Live Badge */}
        <div className={`flex items-center gap-1 px-2 py-0.5 rounded-full text-[9px] font-semibold tracking-wider border ${
          connected
            ? 'bg-accent/5 border-accent text-accent'
            : 'bg-neon-red/5 border-neon-red text-neon-red'
        }`}>
          <span className={`w-1.5 h-1.5 rounded-full ${
            connected ? 'bg-accent animate-blink' : 'bg-neon-red'
          }`} />
          {connected ? 'LIVE' : 'OFFLINE'}
        </div>

        {/* User Menu */}
        <div className="user-menu-wrap relative">
          <button
            onClick={(e) => { e.stopPropagation(); setShowUserMenu(!showUserMenu); }}
            className="flex items-center gap-2 pl-3 border-l border-border"
          >
            <div className="w-7 h-7 bg-gradient-to-br from-accent/20 to-neon-purple/20 border border-border rounded-full flex items-center justify-center text-xs font-heading font-bold text-accent">
              {user?.fullName?.[0] || user?.full_name?.[0] || '?'}
            </div>
            <div className="hidden md:block text-left">
              <div className="text-xs font-medium text-text leading-tight max-w-[100px] truncate">
                {user?.fullName || user?.full_name || 'User'}
              </div>
              <div className="text-[9px] text-text-2 font-mono">
                {user?.role === ROLES.SUPER_ADMIN ? 'SUPER ADMIN' : isSuperAdmin ? 'ADMIN' : 'OPERATOR'}
              </div>
            </div>
          </button>

          {showUserMenu && (
            <div className="absolute right-0 top-full mt-1.5 bg-bg-2 border border-border rounded-md shadow-xl min-w-[200px] py-1 z-50">
              <div className="px-3 py-2 border-b border-border">
                <div className="text-xs font-medium text-text">
                  {user?.fullName || user?.full_name}
                </div>
                <div className="text-[10px] text-text-2 font-mono mt-0.5">
                  {user?.role === ROLES.SUPER_ADMIN ? 'Super Admin' : isSuperAdmin ? 'Global Admin' : `Operator · ${user?.branchName || ''}`}
                </div>
              </div>
              <button
                onClick={() => { setShowUserMenu(false); navigate('/setup-pc'); }}
                className="w-full text-left px-3 py-2 text-xs text-text hover:bg-bg-3 transition-colors flex items-center gap-2 border-b border-border/40"
              >
                <svg className="w-3.5 h-3.5 text-accent" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                </svg>
                Setup Dedicated PC
              </button>
              {baseUser?.role === ROLES.OPERATOR && !adminSwitchUser && (
                <button
                  onClick={() => { setShowUserMenu(false); setShowAdminSwitchModal(true); }}
                  className="w-full text-left px-3 py-2 text-xs text-text hover:bg-bg-3 transition-colors flex items-center gap-2 border-b border-border/40"
                >
                  <svg className="w-3.5 h-3.5 text-accent" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
                  </svg>
                  Admin Switch
                </button>
              )}
              {adminSwitchUser && (
                <button
                  onClick={handleExitAdminSwitch}
                  className="w-full text-left px-3 py-2 text-xs text-text hover:bg-neon-red/10 transition-colors flex items-center gap-2 border-b border-border/40 text-neon-red"
                >
                  <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 19l-7-7m0 0l7-7m-7 7h18" />
                  </svg>
                  Exit Admin Mode
                </button>
              )}
              <button
                onClick={handleLogout}
                className="w-full text-left px-3 py-2 text-xs text-neon-red hover:bg-neon-red/5 transition-colors flex items-center gap-2"
              >
                <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
                </svg>
                Logout
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
    <AdminSwitchModal isOpen={showAdminSwitchModal} onClose={() => setShowAdminSwitchModal(false)} />
    </>
  );
}
