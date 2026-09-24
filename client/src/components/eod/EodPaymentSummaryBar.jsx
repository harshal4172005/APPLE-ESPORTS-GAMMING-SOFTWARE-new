import { useCallback, useEffect, useRef, useState } from 'react';
import { Wallet, GripHorizontal } from 'lucide-react';

const MIN_HEIGHT = 56;
const MAX_HEIGHT = 420;

const TABS = [
  { key: 'cash', label: 'Cash & Collection' },
];

// ── Fixed-to-viewport financial summary strip for the EOD dashboard, mirroring
// SessionActivityLog's pinned-bottom / drag-resizable pattern ──
//
// Same layout as the original two-column summary. What changed is only where the numbers come
// from: every total here used to be added up in this file, and three of those additions were
// wrong - wallet spending got counted as new money on top of the top-up that paid for it, a
// UPI top-up was shown as cash, and a promotional bonus was shown as if the customer had paid
// it. All of that arithmetic now happens once, on the server, next to the records it is adding
// up - this file only prints what it is told.
export default function EodPaymentSummaryBar({ report, targetDate, height, onHeightChange }) {
  const resizing = useRef(false);
  const [activeTab, setActiveTab] = useState('cash');

  const handlePointerDown = useCallback((e) => {
    e.preventDefault();
    resizing.current = true;
    document.body.style.cursor = 'ns-resize';
    document.body.style.userSelect = 'none';
  }, []);

  useEffect(() => {
    const handleMove = (e) => {
      if (!resizing.current) return;
      const clientY = e.touches ? e.touches[0].clientY : e.clientY;
      const next = Math.min(MAX_HEIGHT, Math.max(MIN_HEIGHT, window.innerHeight - clientY));
      onHeightChange?.(next);
    };
    const handleUp = () => {
      resizing.current = false;
      document.body.style.cursor = '';
      document.body.style.userSelect = '';
    };
    window.addEventListener('mousemove', handleMove);
    window.addEventListener('mouseup', handleUp);
    window.addEventListener('touchmove', handleMove);
    window.addEventListener('touchend', handleUp);
    return () => {
      window.removeEventListener('mousemove', handleMove);
      window.removeEventListener('mouseup', handleUp);
      window.removeEventListener('touchmove', handleMove);
      window.removeEventListener('touchend', handleUp);
    };
  }, [onHeightChange]);

  if (!report) return null;

  const n = (v) => Number(v ?? 0);
  const pm = report.paymentMethods;

  // Cash and online each cover both what was paid at billing AND what was paid at the top-up
  // counter - two counters, one till. Top-ups are split by how they were actually paid (the
  // server tracks this per row) rather than lumped into cash regardless of method, which used
  // to overstate the drawer by the full value of every UPI top-up.
  //
  // Cash In (a manual Cash Desk entry - restocking change, a found note, etc.) was computed on
  // the server all along (report.cash.totalCashInwards) but never actually added in here, so
  // Expected Drawer Total (driven by the register directly) went up the moment someone logged
  // one, while this screen's own Cash/Overall End Total silently did not - the exact mismatch
  // this fixes.
  const cashTotal = n(pm.totalCash) + n(pm.totalWalletTopUpsCash) + n(report.cash.totalCashInwards) - n(report.cash.totalPettyExpenses) - n(report.cash.totalOwnerWithdrawals);
  const onlineTotal = n(pm.totalOnline) + n(pm.totalWalletTopUpsOnline);
  const walletDeductionsTotal = n(pm.totalWalletDeductions);

  // The Total Amount at the bottom is exactly the sum of the two rows above it that are
  // themselves totals - cash (already net of petty expenses and owner withdrawals) plus
  // online. Wallet Deductions is shown for visibility but deliberately left out of this sum:
  // it is a member spending a balance that was already counted as income when they topped up,
  // and adding it again is the original bug this screen used to have.
  const grandTotal = cashTotal + onlineTotal;
  const creditsPending = report.creditLogs?.filter(c => c.status?.toLowerCase() === 'pending').reduce((acc, c) => acc + n(c.creditAmount), 0) || 0;

  return (
    <div
      style={{ height }}
      className="fixed bottom-0 left-0 lg:left-[var(--sidebar-offset,240px)] right-0 z-30 bg-bg-2 border-t border-border flex flex-col shadow-[0_-4px_12px_rgba(0,0,0,0.25)] transition-[left] duration-200 ease-in-out"
    >
      {/* Drag handle */}
      <div
        onMouseDown={handlePointerDown}
        onTouchStart={handlePointerDown}
        className="flex items-center justify-center h-2.5 cursor-ns-resize hover:bg-bg-3 transition-colors flex-shrink-0"
      >
        <GripHorizontal className="w-4 h-4 text-text-3" />
      </div>

      <div className="px-3 py-1 border-b border-border bg-bg-3 flex items-center justify-between flex-shrink-0">
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-1.5 text-[10px] font-mono font-bold uppercase tracking-widest text-text-3">
            <Wallet className="w-3 h-3" />
            {targetDate}
          </div>
          <div className="flex items-center gap-1">
            {TABS.map(tab => (
              <button
                key={tab.key}
                onClick={() => setActiveTab(tab.key)}
                className={`px-2.5 py-1 rounded text-[10px] font-bold uppercase tracking-widest transition-colors ${
                  activeTab === tab.key
                    ? 'bg-accent text-white'
                    : 'text-text-3 hover:text-text-2 hover:bg-bg-2'
                }`}
              >
                {tab.label}
              </button>
            ))}
          </div>
        </div>
        <div className="font-mono text-xs font-bold text-neon-blue">
          End Total: ₹{grandTotal.toFixed(2)}
        </div>
      </div>

      <div className="flex-1 overflow-y-auto px-4 py-2">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          {/* Cash Lifecycle Summary */}
          <div className="space-y-1.5 text-xs">
            <div className="flex justify-between items-center">
              <span className="text-text-2">Opening Balance Total</span>
              <span className="font-mono text-text">₹{report.cash.totalOpeningBalance}</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-text-2">Cash Sales + Member Amount Top-Ups</span>
              <span className="font-mono text-neon-green">+ ₹{report.cash.totalCashSales}</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-text-2">Cash In</span>
              <span className="font-mono text-neon-green">+ ₹{report.cash.totalCashInwards}</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-text-2">Petty Expenses</span>
              <span className="font-mono text-neon-red">- ₹{report.cash.totalPettyExpenses}</span>
            </div>

            {/* Only when there was one. A handover that balanced has nothing to say here, and a
                row reading "- ₹0" invites the reader to look for a problem that is not there.

                It is shown BEFORE the expected total because that is where it belongs in the
                arithmetic: opening plus takings, less what was spent and less what went astray
                earlier, is what the drawer should hold now. Leaving it out is what made the
                column fail to add up. */}
            {Number(report.cash.differencesFoundEarlier ?? 0) !== 0 && (
              <div className="flex justify-between items-center">
                <span className="text-text-2">
                  {Number(report.cash.differencesFoundEarlier) < 0 ? 'Missing at an earlier handover' : 'Extra at an earlier handover'}
                </span>
                <span className={`font-mono ${Number(report.cash.differencesFoundEarlier) < 0 ? 'text-neon-red' : 'text-neon-orange'}`}>
                  {Number(report.cash.differencesFoundEarlier) < 0 ? '- ' : '+ '}₹{Math.abs(Number(report.cash.differencesFoundEarlier)).toFixed(2)}
                </span>
              </div>
            )}

            <div className="flex justify-between items-center border-t border-border pt-1.5">
              <span className="font-bold text-text">Expected Drawer Total</span>
              <span className="font-mono font-bold text-accent">₹{report.cash.expectedCashInDrawer}</span>
            </div>

            {/* "Not counted yet" rather than ₹0. Zero reads as an empty drawer - the whole day's
                takings gone - when the truth is that nobody has looked in it yet. */}
            <div className="flex justify-between items-center">
              <span className="font-bold text-text">Physically Counted</span>
              {report.cash.actualPhysicalCashCounted === null || report.cash.actualPhysicalCashCounted === undefined ? (
                <span className="text-text-3 text-[11px] italic">not counted yet</span>
              ) : (
                <span className="font-mono font-bold text-text">₹{report.cash.actualPhysicalCashCounted}</span>
              )}
            </div>

            <div className="flex justify-between items-center bg-bg-3 px-3 py-1.5 rounded-lg border border-border mt-1">
              <span className="font-bold text-text uppercase tracking-widest text-[10px]">
                {report.cash.actualPhysicalCashCounted === null || report.cash.actualPhysicalCashCounted === undefined ? 'Difference' : 'Total Difference'}
              </span>
              {report.cash.totalDiscrepancy === null || report.cash.totalDiscrepancy === undefined ? (
                <span className="text-text-3 text-[11px] italic">unknown until it is counted</span>
              ) : (
                <span className={`font-mono font-bold ${Number(report.cash.totalDiscrepancy) === 0 ? 'text-neon-blue' : 'text-neon-red'}`}>
                  ₹{report.cash.totalDiscrepancy}
                </span>
              )}
            </div>
          </div>

          {/* Overall Collection & Business */}
          <div className="space-y-1.5 text-xs">
            <div className="flex justify-between items-center">
              <span className="text-text-2">Cash</span>
              <span className="font-mono text-text">₹{(n(pm.totalCash) + n(pm.totalWalletTopUpsCash) + n(report.cash.totalCashInwards)).toFixed(2)}</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-text-2">Online</span>
              <span className="font-mono text-text">₹{onlineTotal.toFixed(2)}</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-text-2">Member Amount Deductions (Gaming/Food)</span>
              <span className="font-mono text-neon-purple">₹{walletDeductionsTotal.toFixed(2)}</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-text-2">Credits Pending</span>
              <span className="font-mono text-neon-red">-₹{creditsPending.toFixed(2)}</span>
            </div>
            <div className="flex justify-between items-center bg-neon-blue/10 px-3 py-1.5 rounded-lg border border-neon-blue/30 mt-1">
              <span className="font-bold text-neon-blue uppercase tracking-widest text-[10px]">Overall End Total</span>
              <span className="font-mono font-bold text-base text-neon-blue">₹{grandTotal.toFixed(2)}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
