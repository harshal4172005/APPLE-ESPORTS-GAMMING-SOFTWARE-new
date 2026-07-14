import { useLocation } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { useBranch } from '../../contexts/BranchContext';
import { Store, MonitorPlay, Users, Clock } from 'lucide-react';

// Paths that intentionally work in "All Branches" global view
const GLOBAL_PATHS = ['/app/dashboard', '/app/settings', '/app/employee-forms'];

export default function BranchRequired({ children }) {
  const { isSuperAdmin } = useAuth();
  const { activeBranch, branches, switchBranch } = useBranch();
  const { pathname } = useLocation();

  const isGlobalPath = GLOBAL_PATHS.some(p => pathname.startsWith(p));

  // Only block Super Admins on operational pages when no branch is selected
  if (isSuperAdmin && !activeBranch && !isGlobalPath) {
    return <BranchPickerOverlay branches={branches} onSelect={switchBranch} />;
  }

  return children;
}

export function BranchPickerOverlay({ branches, onSelect }) {
  return (
    <div className="min-h-[70vh] flex flex-col items-center justify-center px-4">
      {/* Header */}
      <div className="text-center mb-8">
        <div className="inline-flex items-center justify-center w-14 h-14 bg-accent/10 border border-accent/30 rounded-lg mb-4">
          <Store className="w-7 h-7 text-accent" />
        </div>
        <h2 className="font-heading text-2xl font-bold text-text tracking-wider uppercase mb-2">
          Select a Branch to Continue
        </h2>
        <p className="text-text-2 text-sm max-w-md">
          This module requires an active branch context. Pick a location below or switch from the branch selector in the top bar.
        </p>
      </div>

      {/* Branch Cards */}
      {branches.length === 0 ? (
        <div className="text-text-3 font-mono text-sm">No branches configured yet.</div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 w-full max-w-4xl">
          {branches.map((b) => (
            <BranchCard key={b.id} branch={b} onSelect={onSelect} />
          ))}
        </div>
      )}
    </div>
  );
}

function BranchCard({ branch, onSelect }) {
  const isActive = branch.status === 'Active' || branch.status === 'active';

  return (
    <button
      onClick={() => onSelect(branch.id)}
      disabled={!isActive}
      className={`
        group relative text-left w-full p-5 rounded-lg border transition-all duration-200
        ${isActive
          ? 'bg-bg-2 border-border hover:border-accent hover:shadow-lg hover:shadow-accent/10 cursor-pointer'
          : 'bg-bg-2/50 border-border/40 opacity-50 cursor-not-allowed'
        }
      `}
    >
      {/* Status dot */}
      <div className="flex items-start justify-between mb-4">
        <div className="p-2 bg-bg-3 border border-border rounded">
          <Store className="w-5 h-5 text-accent" />
        </div>
        <span className={`text-[10px] font-mono font-semibold px-2 py-0.5 rounded-full border ${
          isActive
            ? 'bg-accent/10 border-accent/30 text-accent'
            : 'bg-text-3/10 border-text-3/30 text-text-3'
        }`}>
          {isActive ? 'ACTIVE' : 'INACTIVE'}
        </span>
      </div>

      {/* Branch name */}
      <h3 className="font-heading font-bold text-text text-base tracking-wide group-hover:text-accent transition-colors mb-1">
        {branch.name}
      </h3>

      {/* Address */}
      {branch.address && (
        <p className="text-text-3 text-[11px] font-mono mb-3 line-clamp-1">{branch.address}</p>
      )}

      {/* Hours */}
      <div className="flex items-center gap-1.5 text-text-2 text-[11px]">
        <Clock className="w-3 h-3 text-text-3 flex-shrink-0" />
        <span className="font-mono">{branch.openingTime} – {branch.closingTime}</span>
      </div>

      {/* Hover arrow */}
      {isActive && (
        <div className="absolute right-4 top-1/2 -translate-y-1/2 opacity-0 group-hover:opacity-100 transition-opacity">
          <svg className="w-4 h-4 text-accent" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
          </svg>
        </div>
      )}
    </button>
  );
}
