import { useState, useEffect, useCallback, useRef, Fragment } from 'react';
import { History, Search, ChevronLeft, ChevronRight, RefreshCw, ChevronDown, ChevronUp, AlertTriangle } from 'lucide-react';
import { useBranch } from '../../contexts/BranchContext';
import { getAuditLog } from '../../api/audit.api';
import { summarize, parseDetails, KNOWN_ACTIONS } from '../../utils/auditSummary';

const PAGE_SIZE = 50;
const REFRESH_EVERY_MS = 15000;

// A day preset, so "what happened today" or "this week" does not require picking two dates by
// hand every time - that is the question this screen answers most often.
const RANGE_PRESETS = [
  { label: 'Today', days: 0 },
  { label: 'Last 7 days', days: 7 },
  { label: 'Last 30 days', days: 30 },
  { label: 'All time', days: null },
];

function startOfRange(days) {
  if (days === null) return undefined;
  const d = new Date();
  d.setDate(d.getDate() - days);
  d.setHours(0, 0, 0, 0);
  return d.toISOString();
}

/**
 * Every login, session, payment, wallet change and edit, across every branch, in one place.
 *
 * This did not exist as a screen before - the data was always being written (AuthService,
 * SessionService, BillingService, WalletService and half a dozen others have logged every
 * action for as long as the system has existed), it just never left the branch it happened at
 * and nothing ever displayed it. "What happened at Katargam this afternoon" could only be
 * answered by driving to Katargam. It can now be answered from here.
 */
export default function AuditTrailPage() {
  const { branches } = useBranch();

  const [branchId, setBranchId] = useState('');
  const [userName, setUserName] = useState('');
  const [action, setAction] = useState('');
  const [failedOnly, setFailedOnly] = useState(false);
  const [rangeDays, setRangeDays] = useState(0);
  const [page, setPage] = useState(1);

  const [rows, setRows] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [isLoading, setIsLoading] = useState(false);
  const [expandedId, setExpandedId] = useState(null);

  const searchTimer = useRef(null);
  const [userNameInput, setUserNameInput] = useState('');

  const fetchLogs = useCallback(async (quiet = false) => {
    if (!quiet) setIsLoading(true);
    try {
      const res = await getAuditLog({
        branchId: branchId || undefined,
        userName: userName || undefined,
        action: action || undefined,
        failedOnly: failedOnly || undefined,
        from: startOfRange(rangeDays),
        page,
        pageSize: PAGE_SIZE,
      });
      setRows(res?.items ?? []);
      setTotalCount(res?.totalCount ?? 0);
      setTotalPages(Math.max(1, res?.totalPages ?? 1));
    } catch (err) {
      console.error('Failed to load audit trail:', err);
    } finally {
      if (!quiet) setIsLoading(false);
    }
  }, [branchId, userName, action, failedOnly, rangeDays, page]);

  useEffect(() => { fetchLogs(); }, [fetchLogs]);

  // Reset to page 1 whenever a filter actually changes, so a narrower search never lands you
  // on an empty "page 4 of 1" from the previous, wider one.
  useEffect(() => { setPage(1); }, [branchId, userName, action, failedOnly, rangeDays]);

  // Near-live, same pattern as the Members screen: keeps going regardless of the live
  // connection, pauses only when the tab is hidden.
  useEffect(() => {
    const tick = () => {
      if (document.visibilityState !== 'visible') return;
      fetchLogs(true);
    };
    const timer = setInterval(tick, REFRESH_EVERY_MS);
    document.addEventListener('visibilitychange', tick);
    return () => {
      clearInterval(timer);
      document.removeEventListener('visibilitychange', tick);
    };
  }, [fetchLogs]);

  const handleUserNameChange = (val) => {
    setUserNameInput(val);
    clearTimeout(searchTimer.current);
    searchTimer.current = setTimeout(() => setUserName(val), 400);
  };

  return (
    <div className="p-4 md:p-6 max-w-[1600px] mx-auto">
      <div className="flex items-center gap-2 mb-1">
        <History className="w-5 h-5 text-accent" />
        <h1 className="text-lg font-heading font-bold text-text">Audit Trail</h1>
      </div>
      <p className="text-xs text-text-3 mb-5">
        Every login, session, payment, Member Amount change and edit, at every branch. Updates on its own every 15 seconds.
      </p>

      {/* Filters */}
      <div className="flex flex-wrap items-end gap-3 mb-4 bg-bg-2 border border-border rounded-lg p-3">
        <div className="flex flex-col gap-1">
          <label className="text-[9px] font-bold uppercase tracking-widest text-text-3">Branch</label>
          <select
            value={branchId}
            onChange={(e) => setBranchId(e.target.value)}
            className="bg-bg-3 border border-border rounded px-2.5 py-1.5 text-xs text-text min-w-[140px]"
          >
            <option value="">All branches</option>
            {branches.map((b) => (
              <option key={b.id} value={b.id}>{b.name}</option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1">
          <label className="text-[9px] font-bold uppercase tracking-widest text-text-3">Person</label>
          <div className="relative">
            <Search className="w-3.5 h-3.5 text-text-3 absolute left-2 top-1/2 -translate-y-1/2" />
            <input
              value={userNameInput}
              onChange={(e) => handleUserNameChange(e.target.value)}
              placeholder="Search by name"
              className="bg-bg-3 border border-border rounded pl-7 pr-2.5 py-1.5 text-xs text-text w-40"
            />
          </div>
        </div>

        <div className="flex flex-col gap-1">
          <label className="text-[9px] font-bold uppercase tracking-widest text-text-3">Action</label>
          <select
            value={action}
            onChange={(e) => setAction(e.target.value)}
            className="bg-bg-3 border border-border rounded px-2.5 py-1.5 text-xs text-text min-w-[160px]"
          >
            <option value="">Any action</option>
            {KNOWN_ACTIONS.map((a) => (
              <option key={a.value} value={a.value}>{a.label}</option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1">
          <label className="text-[9px] font-bold uppercase tracking-widest text-text-3">When</label>
          <div className="flex gap-1">
            {RANGE_PRESETS.map((p) => (
              <button
                key={p.label}
                onClick={() => setRangeDays(p.days)}
                className={`px-2.5 py-1.5 rounded text-[11px] font-medium transition-colors ${
                  rangeDays === p.days
                    ? 'bg-accent text-white'
                    : 'bg-bg-3 text-text-2 hover:text-text border border-border'
                }`}
              >
                {p.label}
              </button>
            ))}
          </div>
        </div>

        <div className="flex flex-col gap-1">
          <label className="text-[9px] font-bold uppercase tracking-widest text-text-3">&nbsp;</label>
          <button
            onClick={() => setFailedOnly((v) => !v)}
            className={`flex items-center gap-1.5 px-2.5 py-1.5 rounded text-[11px] font-bold uppercase tracking-widest transition-colors ${
              failedOnly
                ? 'bg-neon-red text-white'
                : 'bg-bg-3 text-text-2 hover:text-text border border-border'
            }`}
          >
            <AlertTriangle className="w-3.5 h-3.5" />
            Failures only
          </button>
        </div>

        <button
          onClick={() => fetchLogs()}
          className="ml-auto flex items-center gap-1.5 px-3 py-1.5 rounded text-xs font-medium bg-bg-3 border border-border text-text-2 hover:text-text transition-colors"
        >
          <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin' : ''}`} />
          Refresh
        </button>
      </div>

      {/* Table */}
      <div className="bg-bg-2 border border-border rounded-lg overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse text-[12px]">
            <thead>
              <tr className="border-b border-border text-text-3 uppercase tracking-wider font-bold text-[9px] bg-bg-3">
                <th className="py-2 px-3 w-[150px]">When</th>
                <th className="py-2 px-3 w-[130px]">Branch</th>
                <th className="py-2 px-3 w-[150px]">Who</th>
                <th className="py-2 px-3">What happened</th>
                <th className="py-2 px-3 w-[36px]" />
              </tr>
            </thead>
            <tbody className="divide-y divide-border/40">
              {rows.length === 0 && !isLoading && (
                <tr>
                  <td colSpan={5} className="py-8 text-center text-text-3 text-xs">
                    Nothing matches these filters.
                  </td>
                </tr>
              )}
              {rows.map((row) => {
                const details = parseDetails(row.details);
                const isOpen = expandedId === row.id;
                // Success defaults true for rows written before this column existed - see the
                // migration's own note on why that default, not false, is the honest one for
                // history.
                const failed = row.success === false;
                return (
                  <Fragment key={row.id}>
                    <tr
                      onClick={() => setExpandedId(isOpen ? null : row.id)}
                      className={`cursor-pointer transition-colors border-l-2 ${
                        failed
                          ? 'bg-neon-red/10 hover:bg-neon-red/15 border-l-neon-red'
                          : 'hover:bg-bg-3/60 border-l-transparent'
                      }`}
                    >
                      <td className="py-2 px-3 text-text-3 font-mono text-[11px] whitespace-nowrap">
                        {new Date(row.createdAt).toLocaleString('en-IN', {
                          day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit',
                        })}
                      </td>
                      <td className="py-2 px-3 text-text-2 whitespace-nowrap">{row.branchName ?? '—'}</td>
                      <td className="py-2 px-3 text-text whitespace-nowrap">
                        {row.userName}
                        <span className="text-text-3 text-[10px] ml-1">{row.userRole}</span>
                      </td>
                      <td className={`py-2 px-3 first-letter:uppercase ${failed ? 'text-neon-red' : 'text-text-2'}`}>
                        {failed && (
                          <span className="inline-block text-[9px] font-bold uppercase tracking-widest bg-neon-red/20 text-neon-red rounded px-1.5 py-0.5 mr-1.5 align-middle">
                            Failed
                          </span>
                        )}
                        {summarize(row.action, details, row.success)}
                      </td>
                      <td className="py-2 px-3 text-text-3">
                        {isOpen ? <ChevronUp className="w-3.5 h-3.5" /> : <ChevronDown className="w-3.5 h-3.5" />}
                      </td>
                    </tr>
                    {isOpen && (
                      <tr className={failed ? 'bg-neon-red/5' : 'bg-bg-3/40'}>
                        <td colSpan={5} className="py-2 px-3">
                          <pre className="text-[10px] text-text-3 font-mono whitespace-pre-wrap break-all">
                            {JSON.stringify({ action: row.action, success: row.success, targetType: row.targetType, targetId: row.targetId, ip: row.ipAddress, ...details }, null, 2)}
                          </pre>
                        </td>
                      </tr>
                    )}
                  </Fragment>
                );
              })}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        <div className="flex items-center justify-between px-3 py-2 border-t border-border text-[11px] text-text-3">
          <span>{totalCount.toLocaleString('en-IN')} action{totalCount === 1 ? '' : 's'}</span>
          <div className="flex items-center gap-2">
            <button
              disabled={page <= 1}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              className="p-1 rounded hover:bg-bg-3 disabled:opacity-30 disabled:cursor-not-allowed"
            >
              <ChevronLeft className="w-4 h-4" />
            </button>
            <span>Page {page} of {totalPages}</span>
            <button
              disabled={page >= totalPages}
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              className="p-1 rounded hover:bg-bg-3 disabled:opacity-30 disabled:cursor-not-allowed"
            >
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
