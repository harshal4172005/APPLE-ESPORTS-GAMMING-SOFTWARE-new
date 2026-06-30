import { useState, useEffect } from 'react';
import {
  Receipt, Gamepad2, Coffee, Tag, CheckCircle,
  Banknote, CreditCard, Wallet, ArrowLeftRight, Smartphone,
  KeyRound, ShieldCheck, ShieldAlert, Eye, EyeOff, Trash2
} from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import { applyDiscount, processPayment, getMemberById, removeBillItem, requestWalletApproval } from '../../api/billing.api';
import { useSocket } from '../../contexts/SocketContext';
import { useToast } from '../ui/Toast';

const DENOMINATIONS = [20, 50, 100, 200, 500, 1000, 2000];
const DISC_PRESETS   = [0, 5, 10, 15, 20];

export default function BillDetailsPanel({ bill, onBillUpdate, onPaymentSuccess }) {
  const { isSuperAdmin } = useAuth();
  const { subscribe, SIGNALR_HUBS } = useSocket();
  const toast = useToast();

  /* ── All hooks MUST come before any conditional return ── */
  const [discLoading, setDiscLoading] = useState(false);
  const [activeDisc,  setActiveDisc]  = useState(0);

  const [payMethod,   setPayMethod]   = useState('cash');
  const [cashReceived,setCashReceived]= useState('');
  const [splitCash,   setSplitCash]   = useState('');
  const [splitUpi,    setSplitUpi]    = useState('');
  const [memberInfo,  setMemberInfo]  = useState(null);
  const [processing,  setProcessing]  = useState(false);
  const [payError,    setPayError]    = useState(null);

  const [walletWaiting, setWalletWaiting] = useState(false);

  // Reset form whenever the selected bill changes
  useEffect(() => {
    setActiveDisc(
      (bill?.discountType === 1 || bill?.discountType === 'Percentage')
        ? (bill?.discountValue ?? 0)
        : 0
    );
    setCashReceived(bill?.totalAmount ? String(bill.totalAmount) : '');
    setSplitCash('');
    setSplitUpi('');
    setPayError(null);
    setPayMethod('cash');
    setWalletWaiting(false);
  }, [bill?.id]);

  useEffect(() => {
    if (!bill?.id) return;
    const unsub = subscribe(SIGNALR_HUBS.BILLING, 'WalletApprovalDeclined', (data) => {
      if (data.billId === bill.id) {
        setWalletWaiting(false);
        setPayError(data.reason || 'Member declined the wallet payment request.');
      }
    });
    return () => unsub();
  }, [bill?.id, subscribe, SIGNALR_HUBS]);

  // Fetch member wallet whenever the linked member changes
  useEffect(() => {
    if (!bill?.memberId) { setMemberInfo(null); return; }
    getMemberById(bill.memberId).then(setMemberInfo).catch(() => setMemberInfo(null));
  }, [bill?.memberId]);

  /* ── Early return for empty state (after ALL hooks) ── */
  if (!bill) {
    return (
      <div className="flex flex-col items-center justify-center h-full min-h-[400px] text-text-3 bg-bg-2 border border-border rounded-xl">
        <Receipt className="w-12 h-12 mb-3 opacity-40" />
        <p className="font-heading uppercase tracking-widest text-sm">Select a bill to view</p>
        <p className="text-[11px] mt-1 font-mono text-text-3/60">
          Click any bill or active session on the left
        </p>
      </div>
    );
  }

  /* ── Derived values (safe — bill is non-null here) ── */
  const isPaid = bill.status === 1 || bill.status === 'Completed';
  const total  = Number(bill.totalAmount) || 0;

  const gamingItems = (bill.items || []).filter(i => i.itemType?.toLowerCase() === 'gaming');
  const foodItems   = (bill.items || []).filter(
    i => i.itemType?.toLowerCase() === 'food' || i.itemType?.toLowerCase() === 'drink'
  );

  const cashChange  = parseFloat(cashReceived) > total ? parseFloat(cashReceived) - total : 0;
  const splitTotal  = (parseFloat(splitCash) || 0) + (parseFloat(splitUpi) || 0);
  const splitDiff   = total - splitTotal;

  const canComplete = (() => {
    if (payMethod === 'cash')   return (parseFloat(cashReceived) || 0) >= total;
    if (payMethod === 'upi')    return true;
    if (payMethod === 'wallet') return !!bill.memberId && !!memberInfo &&
      (memberInfo.gamingBalance ?? 0) >= (bill.gamingAmount || 0) &&
      (memberInfo.foodBalance ?? 0) >= (bill.foodAmount || 0) && !walletWaiting;
    if (payMethod === 'split')  return Math.abs(splitDiff) <= 0.01;
    return false;
  })();

  /* ── Event handlers (plain functions — no hooks after this point) ── */

  const handleWalletRequest = async () => {
    setPayError(null);
    setWalletWaiting(true);
    try {
      await requestWalletApproval(bill.id);
      toast.success('Approval request sent to PC.');
    } catch (err) {
      setWalletWaiting(false);
      setPayError(err.response?.data?.error || 'Failed to send approval request.');
    }
  };
  const handleDiscount = async (pct) => {
    if (discLoading) return;
    setDiscLoading(true);
    try {
      const updated = await applyDiscount(bill.id, {
        discountType:  'Percentage',
        discountValue: pct,
        reason: pct === 0 ? 'Discount removed' : `${pct}% admin discount`,
      });
      setActiveDisc(pct);
      onBillUpdate?.(updated);
      toast.success(pct === 0 ? 'Discount removed' : `${pct}% discount applied`);
    } catch (err) {
      toast.error(err.response?.data?.error || err.response?.data?.message || 'Failed to apply discount');
    } finally {
      setDiscLoading(false);
    }
  };

  const handleComplete = async () => {
    setPayError(null);
    setProcessing(true);
    try {
      let payload;
      if (payMethod === 'cash') {
        const received = parseFloat(cashReceived) || total;
        if (received < total) {
          setPayError('Cash received cannot be less than the total amount due.');
          setProcessing(false);
          return;
        }
        payload = { paymentType: 'Cash', cashAmount: total, onlineAmount: 0, walletAmount: 0, cashReceived: received };

      } else if (payMethod === 'upi') {
        payload = { paymentType: 'Online', cashAmount: 0, onlineAmount: total, walletAmount: 0, cashReceived: 0 };

      } else if (payMethod === 'wallet') {
        if (!bill.memberId || !bill.pcId) { setPayError('No member or PC linked to this bill.'); setProcessing(false); return; }
        // Wallet payment will now be handled via handleWalletRequest instead of handleComplete.
        // If handleComplete is somehow clicked (should be hidden for Wallet), we just return.
        setProcessing(false);
        return;

      } else if (payMethod === 'split') {
        const sc = parseFloat(splitCash) || 0;
        const su = parseFloat(splitUpi)  || 0;
        if (Math.abs(sc + su - total) > 0.01) {
          setPayError(`Split must total ₹${total}. Currently ₹${(sc + su).toFixed(0)}.`);
          setProcessing(false);
          return;
        }
        payload = { paymentType: 'Split', cashAmount: sc, onlineAmount: su, walletAmount: 0, cashReceived: sc };
      }

      await processPayment(bill.id, payload);
      toast.success('Transaction saved — PC released!');
      onPaymentSuccess?.();
    } catch (err) {
      // Backend sends { error: "..." } — NOT { message: "..." }
      const msg = err.response?.data?.error || err.response?.data?.error || err.response?.data?.message || err.message || 'Payment processing failed';
      // Give a clear hint for the cash register requirement
      if (msg.toLowerCase().includes('cash register')) {
        setPayError('⚠ No open cash register for this shift. Go to Cash Desk → Open Register before accepting cash. Or switch to UPI payment.');
      } else {
        setPayError(msg);
      }
    } finally {
      setProcessing(false);
    }
  };

  const handleRemoveItem = async (itemId) => {
    try {
      setProcessing(true);
      const updatedBill = await removeBillItem(bill.id, itemId);
      toast.success('Item removed from bill');
      onBillUpdate?.(updatedBill);
    } catch (err) {
      toast.error(err.response?.data?.error || err.message || 'Failed to remove item');
    } finally {
      setProcessing(false);
    }
  };

  /* ── Render ── */
  return (
    <div className="flex flex-col h-full bg-bg-2 border border-border rounded-xl overflow-hidden shadow-lg">

      {/* Header */}
      <div className="bg-bg-3 px-5 py-3.5 border-b border-border flex items-center justify-between shrink-0">
        <div className="flex items-center gap-2">
          <Receipt className="w-4 h-4 text-accent" />
          <span className="font-heading font-bold text-text uppercase tracking-wider text-sm">
            Current Bill
          </span>
        </div>
        <div className="flex items-center gap-3">
          <span className="font-mono text-[11px] text-text-3">{bill.billNumber}</span>
          <span className={`px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider border ${
            isPaid
              ? 'bg-neon-blue/10 text-neon-blue border-neon-blue/30'
              : 'bg-neon-orange/10 text-neon-orange border-neon-orange/30'
          }`}>
            {isPaid ? '✓ PAID' : (bill.pcNumber ? `PC ${bill.pcNumber}` : 'Walk-in')}
          </span>
        </div>
      </div>

      {/* Customer strip */}
      <div className="px-5 py-2 border-b border-border bg-bg-3/40 flex justify-between text-[11px] text-text-3 font-mono shrink-0">
        <span>{bill.customerName || 'Walk-in Guest'}</span>
        <span className="opacity-60">
          {bill.createdAt
            ? new Date(bill.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
            : ''}
        </span>
      </div>

      {/* Bill body — scrollable */}
      <div className="flex-1 overflow-y-auto px-5 py-4 space-y-4">

        {/* Gaming charges */}
        <ItemSection
          icon={<Gamepad2 className="w-3.5 h-3.5 text-neon-purple" />}
          label="Gaming Charges"
          items={gamingItems}
          accentCls="text-neon-purple"
        />

        {/* Food & drink */}
        <ItemSection
          icon={<Coffee className="w-3.5 h-3.5 text-neon-orange" />}
          label="Food & Drink"
          items={foodItems}
          accentCls="text-neon-orange"
          canRemove={!isPaid}
          onRemove={handleRemoveItem}
        />

        {/* Subtotals */}
        <div className="text-xs text-text-2 space-y-1 pt-1 border-t border-border">
          <TRow label="Gaming" value={`₹${bill.gamingAmount}`} />
          <TRow label="Food & Drink" value={`₹${bill.foodAmount}`} />
          {bill.discountAmount > 0 && (
            <TRow
              label={`Discount (${bill.discountValue}%)`}
              value={`−₹${bill.discountAmount}`}
              cls="text-neon-purple"
            />
          )}
        </div>

        {/* Paid: show payment breakdown */}
        {isPaid && bill.payments?.length > 0 && (
          <PaidBreakdown payments={bill.payments} />
        )}
      </div>

      {/* ── Pay area (only when unpaid) ── */}
      {!isPaid && (
        <div className="shrink-0 border-t border-border px-5 py-4 space-y-4 bg-bg-3/30 overflow-y-auto max-h-[55vh]">

          {/* Discount quick-tags — SuperAdmin only */}
          {isSuperAdmin && (
            <div>
              <div className="flex items-center justify-between mb-2">
                <span className="text-[10px] text-text-3 font-bold uppercase tracking-widest flex items-center gap-1">
                  <Tag className="w-3 h-3" /> Discount / Coupon
                  <span className="text-accent/60 ml-1 normal-case">(Admin Only)</span>
                </span>
                {activeDisc > 0 && (
                  <span className="text-neon-orange text-[10px] font-bold font-mono">−{activeDisc}% applied</span>
                )}
              </div>
              <div className="flex flex-wrap gap-1.5">
                {DISC_PRESETS.map(pct => (
                  <button
                    key={pct}
                    disabled={discLoading}
                    onClick={() => handleDiscount(pct)}
                    className={`px-3 py-1 rounded border text-[11px] font-bold uppercase tracking-wider transition-all ${
                      activeDisc === pct
                        ? 'bg-neon-purple/20 border-neon-purple text-neon-purple'
                        : 'bg-bg-2 border-border text-text-3 hover:border-neon-purple/50 hover:text-neon-purple/80'
                    } ${discLoading ? 'opacity-50 cursor-wait' : ''}`}
                  >
                    {pct === 0 ? 'None' : `${pct}% Off`}
                  </button>
                ))}
              </div>
            </div>
          )}

          {/* Total amount due */}
          <div className="flex items-center justify-between py-2.5 border-y border-border">
            <span className="font-heading font-bold text-text uppercase tracking-wider text-sm">
              Total Amount Due
            </span>
            <span className="font-mono font-bold text-2xl text-neon-orange drop-shadow-[0_0_8px_rgba(255,153,0,0.3)]">
              ₹{total}
            </span>
          </div>

          {/* Payment method 2×2 grid */}
          <div className="grid grid-cols-2 gap-2">
            {[
              { id: 'cash',   label: 'Cash',   Icon: Banknote      },
              { id: 'upi',    label: 'UPI',    Icon: Smartphone    },
              { id: 'split',  label: 'Split',  Icon: ArrowLeftRight},
              { id: 'wallet', label: 'Wallet', Icon: Wallet        },
            ].map(({ id, label, Icon }) => (
              <button
                key={id}
                onClick={() => {
                  setPayMethod(id);
                  setPayError(null);
                  if (id !== 'wallet') {
                    setWalletWaiting(false);
                  }
                }}
                className={`py-2 rounded-lg border text-xs font-bold uppercase tracking-wider flex items-center justify-center gap-1.5 transition-all ${
                  payMethod === id
                    ? 'bg-accent/15 border-accent text-accent shadow-[0_0_8px_rgba(255,51,102,0.2)]'
                    : 'bg-bg-2 border-border text-text-3 hover:border-accent/40 hover:text-text-2'
                }`}
              >
                <Icon className="w-3.5 h-3.5" /> {label}
              </button>
            ))}
          </div>

          {/* ── Cash section ── */}
          {payMethod === 'cash' && (
            <div className="bg-bg-2 border border-border rounded-lg p-3 space-y-3">
              <label className="text-[10px] text-text-3 uppercase font-bold tracking-widest block">
                Cash Received from Customer (₹)
              </label>
              <input
                type="number"
                min={total}
                value={cashReceived}
                onChange={e => setCashReceived(e.target.value)}
                placeholder={`e.g. ${total}`}
                className="w-full bg-bg-3 border border-border text-text font-mono text-xl rounded-lg p-2.5 focus:border-accent focus:ring-1 focus:ring-accent transition-all"
              />
              {/* Quick denomination buttons */}
              <div className="flex flex-wrap gap-1.5">
                {DENOMINATIONS.map(d => (
                  <button
                    key={d}
                    onClick={() => setCashReceived(String(d))}
                    className={`px-2.5 py-1 rounded border text-[11px] font-mono font-bold transition-colors ${
                      parseFloat(cashReceived) === d
                        ? 'bg-accent/15 border-accent text-accent'
                        : 'bg-bg-3 border-border text-text-2 hover:border-accent/40'
                    }`}
                  >
                    ₹{d}
                  </button>
                ))}
              </div>
              {/* Change */}
              {cashChange > 0 && (
                <div className="flex justify-between items-center bg-neon-orange/10 border border-neon-orange/30 rounded-lg px-3 py-2.5">
                  <span className="text-xs font-bold uppercase tracking-wider text-neon-orange">Return Change</span>
                  <span className="font-mono text-xl font-bold text-neon-orange">₹{cashChange.toFixed(0)}</span>
                </div>
              )}
            </div>
          )}

          {/* ── UPI section ── */}
          {payMethod === 'upi' && (
            <div className="bg-bg-2 border border-neon-purple/30 rounded-lg p-4 text-center space-y-2">
              <CreditCard className="w-8 h-8 text-neon-purple mx-auto" />
              <div className="font-bold text-neon-purple uppercase tracking-wider text-sm">UPI / Online Gateway</div>
              <div className="font-mono font-bold text-3xl text-neon-orange">₹{total}</div>
              <p className="text-[11px] text-text-3">
                Verify funds cleared on bank account before saving
              </p>
            </div>
          )}

          {/* ── Split section ── */}
          {payMethod === 'split' && (
            <div className="bg-bg-2 border border-border rounded-lg p-3 space-y-3">
              <div className="text-[10px] text-text-3 uppercase font-bold tracking-widest">
                Enter Split Proportions
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="text-[10px] text-text-3 uppercase font-bold block mb-1">Cash Amount</label>
                  <input
                    type="number" min="0"
                    value={splitCash}
                    onChange={e => setSplitCash(e.target.value)}
                    placeholder="0"
                    className="w-full bg-bg-3 border border-border text-text font-mono text-base rounded-lg p-2 focus:border-neon-blue focus:ring-1 focus:ring-neon-blue transition-all"
                  />
                </div>
                <div>
                  <label className="text-[10px] text-text-3 uppercase font-bold block mb-1">UPI Amount</label>
                  <input
                    type="number" min="0"
                    value={splitUpi}
                    onChange={e => setSplitUpi(e.target.value)}
                    placeholder="0"
                    className="w-full bg-bg-3 border border-border text-text font-mono text-base rounded-lg p-2 focus:border-neon-purple focus:ring-1 focus:ring-neon-purple transition-all"
                  />
                </div>
              </div>
              <div className={`text-center text-[11px] font-bold py-1.5 rounded font-mono border ${
                Math.abs(splitDiff) <= 0.01
                  ? 'text-neon-blue bg-neon-blue/10 border-neon-blue/20'
                  : splitDiff > 0
                    ? 'text-neon-red bg-neon-red/10 border-neon-red/20'
                    : 'text-neon-orange bg-neon-orange/10 border-neon-orange/20'
              }`}>
                {Math.abs(splitDiff) <= 0.01
                  ? `✓ Balanced — ₹${total}`
                  : splitDiff > 0
                    ? `₹${splitDiff.toFixed(0)} short`
                    : `₹${Math.abs(splitDiff).toFixed(0)} over`}
              </div>
            </div>
          )}

          {/* ── Wallet section ── */}
          {payMethod === 'wallet' && (
            <div className="bg-bg-2 border border-accent/20 rounded-lg p-3 space-y-3">
              <div className="text-[10px] text-neon-orange uppercase font-bold tracking-widest">
                Deduct from Member Wallet
              </div>

              {!bill.memberId ? (
                <div className="text-neon-red text-xs font-bold bg-neon-red/10 border border-neon-red/20 px-3 py-2 rounded-lg">
                  ⚠ No member is linked to this bill. Walk-in customers cannot pay with wallet.
                </div>
              ) : memberInfo ? (
                <div className="space-y-3">
                  <div className="space-y-1 text-xs font-mono">
                    <TRow
                      label={memberInfo.fullName || 'Member'}
                      value={`G: ₹${(memberInfo.gamingBalance ?? 0).toFixed(0)} | F: ₹${(memberInfo.foodBalance ?? 0).toFixed(0)}`}
                      cls="text-neon-blue font-bold"
                    />
                    <TRow label="Gaming to deduct" value={`₹${bill.gamingAmount || 0}`} cls="text-text-2" />
                    <TRow label="Food to deduct"   value={`₹${bill.foodAmount || 0}`}   cls="text-text-2" />
                    {(memberInfo.gamingBalance ?? 0) < (bill.gamingAmount || 0) && (
                      <p className="text-neon-red font-bold pt-1">⚠ Insufficient gaming balance (short ₹{((bill.gamingAmount || 0) - (memberInfo.gamingBalance ?? 0)).toFixed(0)})</p>
                    )}
                    {(memberInfo.foodBalance ?? 0) < (bill.foodAmount || 0) && (
                      <p className="text-neon-red font-bold pt-1">⚠ Insufficient food balance (short ₹{((bill.foodAmount || 0) - (memberInfo.foodBalance ?? 0)).toFixed(0)})</p>
                    )}
                  </div>
                  
                  {walletWaiting ? (
                    <div className="w-full py-2.5 rounded-lg border border-accent/40 bg-accent/10 flex flex-col items-center justify-center gap-2">
                      <div className="w-4 h-4 rounded-full border-2 border-accent border-t-transparent animate-spin" />
                      <span className="text-[11px] font-bold text-accent uppercase tracking-wider">Waiting for Member on PC...</span>
                    </div>
                  ) : (
                    <button
                      onClick={handleWalletRequest}
                      disabled={!canComplete || !bill.pcId}
                      className="w-full py-2.5 rounded-lg bg-accent border border-accent/80 text-white text-xs font-bold uppercase tracking-wider flex items-center justify-center gap-2 hover:bg-accent-dark transition-all disabled:opacity-40 disabled:cursor-not-allowed shadow-[0_0_10px_rgba(255,51,102,0.3)]"
                    >
                      <Smartphone className="w-4 h-4" /> Request PC Approval
                    </button>
                  )}
                  {!bill.pcId && (
                     <p className="text-neon-orange text-[10px] font-bold">This bill is not linked to an active PC to receive the approval request.</p>
                  )}
                </div>
              ) : (
                <div className="text-text-3 text-xs flex items-center gap-2">
                  <div className="w-3.5 h-3.5 border-2 border-current border-t-transparent rounded-full animate-spin" /> Fetching member balance...
                </div>
              )}
            </div>
          )}

          {/* Error message */}
          {payError && (
            <div className="text-neon-red text-xs font-semibold bg-neon-red/10 border border-neon-red/20 px-3 py-2 rounded-lg">
              ⚠ {payError}
            </div>
          )}

          {/* Complete button */}
          {payMethod !== 'wallet' && (
            <button
              onClick={handleComplete}
              disabled={processing || !canComplete}
              className={`w-full py-3.5 rounded-lg text-sm font-bold uppercase tracking-widest flex items-center justify-center gap-2 transition-all ${
                canComplete && !processing
                  ? 'bg-accent/15 border border-accent text-accent hover:bg-accent/25 shadow-[0_0_14px_rgba(255,51,102,0.15)]'
                  : 'bg-bg-2 border border-border text-text-3 cursor-not-allowed opacity-60'
              }`}
            >
              {processing
                ? <span className="w-5 h-5 border-2 border-current border-t-transparent rounded-full animate-spin" />
                : <><CheckCircle className="w-5 h-5" /> Complete &amp; Save Transaction</>
              }
            </button>
          )}

        </div>
      )}

      {/* Paid confirmation footer */}
      {isPaid && (
        <div className="shrink-0 border-t border-border px-5 py-3 bg-bg-3/30 flex items-center gap-2 text-neon-blue text-xs font-bold">
          <CheckCircle className="w-4 h-4 shrink-0" />
          Bill completed • PC released back to fleet
        </div>
      )}
    </div>
  );
}

/* ── Sub-components ─────────────────────────────────────────────────────────── */

function ItemSection({ icon, label, items, accentCls, canRemove, onRemove }) {
  if (items.length === 0) return null;
  return (
    <div>
      <div className={`flex items-center gap-1.5 text-[10px] font-bold uppercase tracking-widest mb-1.5 ${accentCls}`}>
        {icon} {label}
      </div>
      <div className="bg-bg-3 border border-border rounded-lg overflow-hidden">
        {items.map((item, idx) => (
          <div
            key={item.id}
            className={`flex justify-between items-center px-3 py-2 text-sm ${
              idx < items.length - 1 ? 'border-b border-border/50' : ''
            }`}
          >
            <div>
              <div className="text-text">{item.itemName}</div>
              <div className="text-[10px] text-text-3 font-mono">{item.quantity} × ₹{item.unitPrice}</div>
            </div>
            <div className="flex items-center gap-3">
              <span className="font-mono text-text font-bold">₹{item.totalPrice}</span>
              {canRemove && (
                <button
                  onClick={() => onRemove?.(item.id)}
                  title="Remove item"
                  className="text-text-3 hover:text-neon-red transition-colors p-1"
                >
                  <Trash2 className="w-4 h-4" />
                </button>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function PaidBreakdown({ payments }) {
  const p = payments[0];
  if (!p) return null;
  return (
    <div className="bg-neon-blue/5 border border-neon-blue/20 rounded-lg p-3 space-y-1.5 text-xs">
      <div className="text-[10px] font-bold text-neon-blue uppercase tracking-widest mb-1">Payment Received</div>
      {p.cashAmount > 0    && <TRow label="Cash"           value={`₹${p.cashAmount}`}    />}
      {p.cashReceived > p.cashAmount && <>
        <TRow label="  Tendered"       value={`₹${p.cashReceived}`}  cls="text-text-3" />
        <TRow label="  Change Returned" value={`₹${p.changeReturned}`} cls="text-neon-orange font-bold" />
      </>}
      {p.onlineAmount > 0  && <TRow label="UPI / Online"   value={`₹${p.onlineAmount}`}  />}
      {p.walletAmount > 0  && <TRow label="Member Wallet"  value={`₹${p.walletAmount}`}  />}
    </div>
  );
}

function TRow({ label, value, cls = 'text-text-2' }) {
  return (
    <div className={`flex justify-between font-mono ${cls}`}>
      <span>{label}</span>
      <span>{value}</span>
    </div>
  );
}
