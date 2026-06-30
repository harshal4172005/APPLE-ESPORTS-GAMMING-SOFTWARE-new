// ═══════════════════════════════════════════════════════════
// Gaming Café ERP — Branch Context
// SOP §6.4: Branch Isolation + Super Admin branch switching
// ═══════════════════════════════════════════════════════════

import { createContext, useContext, useState, useCallback, useEffect } from 'react';
import api from '../config/api';
import { useAuth } from './AuthContext';

const BranchContext = createContext(null);

export function BranchProvider({ children }) {
  const { user, isSuperAdmin } = useAuth();
  const [branches, setBranches] = useState([]);
  const [activeBranchId, setActiveBranchId] = useState(null);
  const [activeBranch, setActiveBranch] = useState(null);
  const [loading, setLoading] = useState(false);

  // ── Set branch based on user role ──
  useEffect(() => {
    if (!user) {
      setActiveBranchId(null);
      setActiveBranch(null);
      return;
    }

    // Operators are locked to their assigned branch
    if (user.branchId) {
      setActiveBranchId(user.branchId);
      setActiveBranch({
        id: user.branchId,
        name: user.branchName || 'Branch',
      });
    }

    // Super Admin — load all branches
    if (isSuperAdmin) {
      loadBranches();
    }
  }, [user, isSuperAdmin]);

  // ── Load all branches (Super Admin) ──
  const loadBranches = useCallback(async () => {
    try {
      setLoading(true);
      const response = await api.get('/auth/branches');
      const branchList = response.data.data;
      setBranches(branchList);

      // Restore previously selected branch or default to 'All Branches' (null)
      const savedBranch = localStorage.getItem('activeBranchId');
      if (savedBranch && branchList.find((b) => b.id === savedBranch)) {
        switchBranch(savedBranch, branchList);
      } else {
        switchBranch(null, branchList);
      }
    } catch (error) {
      console.error('Failed to load branches:', error);
    } finally {
      setLoading(false);
    }
  }, []);

  // ── Switch active branch (Super Admin only) ──
  const switchBranch = useCallback((branchId, branchList = branches) => {
    setActiveBranchId(branchId);
    if (branchId) {
      localStorage.setItem('activeBranchId', branchId);
    } else {
      localStorage.removeItem('activeBranchId');
    }

    const branch = branchList.find((b) => b.id === branchId);
    setActiveBranch(branch || null);
  }, [branches]);

  const value = {
    branches,
    activeBranchId,
    activeBranch,
    loading,
    switchBranch,
    loadBranches,
  };

  return (
    <BranchContext.Provider value={value}>
      {children}
    </BranchContext.Provider>
  );
}

export function useBranch() {
  const context = useContext(BranchContext);
  if (!context) {
    throw new Error('useBranch must be used within a BranchProvider');
  }
  return context;
}

export default BranchContext;
