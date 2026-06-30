// ═══════════════════════════════════════════════════════════
// Gaming Café ERP — Shift End Modal
// SOP §10: Shift closure — cash count verification + inventory check
// Shown when operator clicks Logout — blocks logout until complete
// ═══════════════════════════════════════════════════════════

import { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  Banknote, Package, CheckCircle2, AlertTriangle,
  ChevronRight, Loader2, ArrowRight, LogOut,
  Calculator, ClipboardList, Box
} from 'lucide-react';
import api from '../../config/api';

const STEPS = {
  CASH: 'cash',
  INVENTORY: 'inventory',
  CONFIRM: 'confirm',
};

export default function ShiftEndModal({ onComplete, onCancel }) {
  const [step, setStep] = useState(STEPS.CASH);

  // ── Cash Step ──
  const [register, setRegister] = useState(null);
  const [physicalCash, setPhysicalCash] = useState('');
  const [mismatchReason, setMismatchReason] = useState('');
  const [cashLoading, setCashLoading] = useState(false);
  const [cashFetching, setCashFetching] = useState(true);
  const [cashError, setCashError] = useState(null);

  // ── Inventory Step ──
  const [inventory, setInventory] = useState([]);
  const [stockUpdates, setStockUpdates] = useState({});
  const [invLoading, setInvLoading] = useState(false);
  const [invFetching, setInvFetching] = useState(false);
  const [invError, setInvError] = useState(null);

  // ── Confirm Step ──
  const [confirmLoading, setConfirmLoading] = useState(false);

  // Fetch active cash register
  useEffect(() => {
    const fetchRegister = async () => {
      setCashFetching(true);
      try {
        const { data } = await api.get('/cash/active');
        setRegister(data.data);
      } catch {
        setRegister(null);
      } finally {
        setCashFetching(false);
      }
    };
    fetchRegister();
  }, []);

  // Fetch inventory on inventory step
  useEffect(() => {
    if (step !== STEPS.INVENTORY) return;
    const fetchInventory = async () => {
      setInvFetching(true);
      setInvError(null);
      try {
        const { data } = await api.get('/inventory');
        const items = data.data || data || [];
        setInventory(items);
        const initUpdates = {};
        items.forEach(item => {
          initUpdates[item.id] = item.currentStock ?? item.CurrentStock ?? '';
        });
        setStockUpdates(initUpdates);
      } catch (err) {
        setInvError(err.response?.data?.error || 'Failed to load inventory.');
      } finally {
        setInvFetching(false);
      }
    };
    fetchInventory();
  }, [step]);

  const physicalCashNum = Number(physicalCash) || 0;
  const expectedCash = register?.expectedDrawerCash ?? 0;
  const cashDifference = physicalCashNum - expectedCash;
  const hasMismatch = physicalCash !== '' && Math.abs(cashDifference) > 0;

  // ── Step 1: Cash Count Verification ──
  const handleCashSubmit = () => {
    if (physicalCash === '') {
      setCashError('Please enter the physical cash count.');
      return;
    }
    if (hasMismatch && !mismatchReason.trim()) {
      setCashError('There is a cash mismatch. Please provide a reason.');
      return;
    }
    setCashError(null);
    setStep(STEPS.INVENTORY);
  };

  // ── Step 2: Inventory End-of-Shift Update ──
  const handleInvSubmit = async () => {
    setInvLoading(true);
    setInvError(null);
    try {
      for (const item of inventory) {
        const original = item.currentStock ?? item.CurrentStock ?? 0;
        const updated = Number(stockUpdates[item.id] ?? original);
        if (updated !== original) {
          await api.patch(`/inventory/${item.id}/stock`, { currentStock: updated });
        }
      }
      setStep(STEPS.CONFIRM);
    } catch (err) {
      setInvError(err.response?.data?.error || 'Failed to update inventory.');
    } finally {
      setInvLoading(false);
    }
  };


  // ── Step 3: Final Logout ──
  const handleFinalLogout = () => {
    // Store cash verification data in session for backend logging if needed
    onComplete({ physicalCash: physicalCashNum, mismatchReason });
  };

  const getStepNum = () => {
    if (step === STEPS.CASH) return 1;
    if (step === STEPS.INVENTORY) return 2;
    return 3;
  };

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-black/85 backdrop-blur-lg">
      <motion.div
        initial={{ opacity: 0, scale: 0.92, y: 20 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        transition={{ duration: 0.35, ease: 'easeOut' }}
        className="w-full max-w-lg bg-bg-2 border border-border rounded-2xl shadow-2xl shadow-black/60 overflow-hidden flex flex-col"
        style={{ maxHeight: '90vh' }}
      >
        {/* Header */}
        <div className="px-6 pt-6 pb-4 border-b border-border bg-bg-3 flex-shrink-0">
          <div className="flex items-center gap-3 mb-3">
            <div className="w-10 h-10 bg-neon-red/10 rounded-xl flex items-center justify-center border border-neon-red/30">
              <LogOut className="w-5 h-5 text-neon-red" />
            </div>
            <div>
              <h2 className="font-heading font-bold text-text text-lg tracking-wide">Shift End Checklist</h2>
              <p className="text-[11px] text-text-3 font-mono">Complete all steps to end your shift safely</p>
            </div>
          </div>

          {/* Step Progress */}
          <div className="flex items-center gap-2">
            {[
              { num: 1, label: 'Cash Count', icon: Banknote },
              { num: 2, label: 'Inventory', icon: Package },
              { num: 3, label: 'End Shift', icon: LogOut },
            ].map((s, idx) => (
              <div key={s.num} className="flex items-center gap-2 flex-1">
                <div className={`flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[10px] font-bold uppercase tracking-wider transition-all ${
                  getStepNum() === s.num
                    ? 'bg-neon-red/15 border border-neon-red text-neon-red'
                    : getStepNum() > s.num
                    ? 'bg-neon-green/10 border border-neon-green/50 text-neon-green'
                    : 'bg-bg border border-border-2 text-text-3'
                }`}>
                  {getStepNum() > s.num ? (
                    <CheckCircle2 className="w-3 h-3" />
                  ) : (
                    <s.icon className="w-3 h-3" />
                  )}
                  <span className="hidden sm:inline">{s.label}</span>
                  <span className="sm:hidden">{s.num}</span>
                </div>
                {idx < 2 && <ChevronRight className="w-3 h-3 text-border flex-shrink-0" />}
              </div>
            ))}
          </div>
        </div>

        {/* Body */}
        <div className="flex-1 overflow-y-auto">
          <AnimatePresence mode="wait">
            {/* ── STEP 1: Cash Count ── */}
            {step === STEPS.CASH && (
              <motion.div
                key="cash"
                initial={{ opacity: 0, x: -20 }}
                animate={{ opacity: 1, x: 0 }}
                exit={{ opacity: 0, x: 20 }}
                transition={{ duration: 0.2 }}
                className="p-6"
              >
                <div className="flex items-center gap-3 mb-5">
                  <div className="w-12 h-12 bg-neon-orange/10 border border-neon-orange/30 rounded-xl flex items-center justify-center">
                    <Calculator className="w-6 h-6 text-neon-orange" />
                  </div>
                  <div>
                    <h3 className="font-bold text-text text-base">Cash Drawer Verification</h3>
                    <p className="text-text-3 text-xs">Count physical cash in the drawer and enter the total</p>
                  </div>
                </div>

                {cashFetching ? (
                  <div className="flex items-center justify-center py-6 gap-2 text-text-3">
                    <Loader2 className="w-5 h-5 animate-spin" />
                    <span className="text-sm">Loading register data...</span>
                  </div>
                ) : (
                  <>
                    {/* Expected vs Actual */}
                    {register && (
                      <div className="p-4 bg-bg-3 rounded-xl border border-border mb-5">
                        <div className="text-[10px] text-text-3 uppercase tracking-wider font-bold mb-3 flex items-center gap-1.5">
                          <Calculator className="w-3 h-3" /> System Expected Cash
                        </div>
                        <div className="text-3xl font-mono font-bold text-accent mb-1">
                          ₹{expectedCash.toLocaleString()}
                        </div>
                        <p className="text-[11px] text-text-3 italic">
                          This is the system-calculated amount that should be in the drawer.
                        </p>
                      </div>
                    )}

                    {cashError && (
                      <div className="p-3 mb-4 bg-neon-red/10 border border-neon-red/20 rounded-lg text-neon-red text-xs flex items-start gap-2">
                        <AlertTriangle className="w-3.5 h-3.5 mt-0.5 shrink-0" />
                        <p>{cashError}</p>
                      </div>
                    )}

                    <div className="space-y-2 mb-4">
                      <label className="text-xs uppercase tracking-wider font-bold text-text-2">
                        Physical Cash Count (What you actually have)
                      </label>
                      <div className="relative">
                        <span className="absolute left-4 top-1/2 -translate-y-1/2 font-mono text-text-3 text-xl">₹</span>
                        <input
                          type="number"
                          min="0"
                          placeholder="0.00"
                          value={physicalCash}
                          onChange={e => setPhysicalCash(e.target.value)}
                          className={`w-full bg-bg-3 border text-text font-mono text-2xl rounded-xl py-4 pl-12 pr-4 focus:ring-1 transition-all outline-none ${
                            hasMismatch
                              ? 'border-neon-orange focus:border-neon-orange focus:ring-neon-orange'
                              : physicalCash !== ''
                              ? 'border-neon-green focus:border-neon-green focus:ring-neon-green'
                              : 'border-border focus:border-accent focus:ring-accent'
                          }`}
                          autoFocus
                        />
                      </div>
                    </div>

                    {/* Mismatch indicator */}
                    {physicalCash !== '' && (
                      <div className={`p-3 rounded-lg mb-4 flex items-center justify-between ${
                        cashDifference === 0
                          ? 'bg-neon-green/10 border border-neon-green/30'
                          : 'bg-neon-orange/10 border border-neon-orange/30'
                      }`}>
                        <span className="text-xs font-bold uppercase tracking-wider text-text-2">
                          Difference
                        </span>
                        <span className={`font-mono font-bold text-lg ${
                          cashDifference === 0 ? 'text-neon-green' : cashDifference > 0 ? 'text-neon-blue' : 'text-neon-orange'
                        }`}>
                          {cashDifference >= 0 ? '+' : ''}₹{cashDifference.toLocaleString()}
                        </span>
                      </div>
                    )}

                    {/* Mismatch Reason */}
                    {hasMismatch && (
                      <div className="space-y-2 mb-5">
                        <label className="text-xs uppercase tracking-wider font-bold text-neon-orange flex items-center gap-1.5">
                          <AlertTriangle className="w-3 h-3" />
                          Mismatch Reason <span className="text-neon-red">*</span>
                        </label>
                        <textarea
                          placeholder="Explain the cash discrepancy..."
                          value={mismatchReason}
                          onChange={e => setMismatchReason(e.target.value)}
                          rows={2}
                          className="w-full bg-bg-3 border border-neon-orange/40 text-text text-sm rounded-xl p-3 focus:border-neon-orange outline-none focus:ring-1 focus:ring-neon-orange/30 resize-none"
                        />
                      </div>
                    )}
                  </>
                )}

                <div className="flex gap-3">
                  <button
                    onClick={onCancel}
                    className="flex-1 py-3 rounded-xl text-xs font-bold uppercase tracking-wider border border-border text-text-3 hover:text-text hover:border-text-3 transition-all"
                  >
                    Cancel
                  </button>
                  <button
                    onClick={handleCashSubmit}
                    disabled={cashFetching || physicalCash === ''}
                    className="flex-[2] py-3 rounded-xl text-sm font-bold uppercase tracking-wider flex items-center justify-center gap-2 transition-all bg-neon-orange/10 border border-neon-orange/50 text-neon-orange hover:bg-neon-orange/20 disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    <Banknote className="w-4 h-4" />
                    Next: Inventory
                    <ArrowRight className="w-4 h-4" />
                  </button>
                </div>
              </motion.div>
            )}

            {/* ── STEP 2: Inventory ── */}
            {step === STEPS.INVENTORY && (
              <motion.div
                key="inventory"
                initial={{ opacity: 0, x: -20 }}
                animate={{ opacity: 1, x: 0 }}
                exit={{ opacity: 0, x: 20 }}
                transition={{ duration: 0.2 }}
                className="p-6"
              >
                <div className="flex items-center gap-3 mb-4">
                  <div className="w-12 h-12 bg-neon-purple/10 border border-neon-purple/30 rounded-xl flex items-center justify-center">
                    <ClipboardList className="w-6 h-6 text-neon-purple" />
                  </div>
                  <div>
                    <h3 className="font-bold text-text text-base">End-of-Shift Stock Count</h3>
                    <p className="text-text-3 text-xs">Update actual stock quantities before shift ends</p>
                  </div>
                </div>

                {invError && (
                  <div className="p-3 mb-4 bg-neon-orange/10 border border-neon-orange/20 rounded-lg text-neon-orange text-xs flex items-start gap-2">
                    <AlertTriangle className="w-3.5 h-3.5 mt-0.5 shrink-0" />
                    <p>{invError}</p>
                  </div>
                )}

                {invFetching ? (
                  <div className="flex items-center justify-center py-10 gap-3 text-text-3">
                    <Loader2 className="w-6 h-6 animate-spin" />
                    <span className="text-sm">Loading inventory...</span>
                  </div>
                ) : inventory.length === 0 ? (
                  <div className="text-center py-8 text-text-3">
                    <Box className="w-10 h-10 mx-auto mb-3 opacity-40" />
                    <p className="text-sm">No inventory items found.</p>
                  </div>
                ) : (
                  <div className="space-y-2 mb-5 max-h-64 overflow-y-auto pr-1">
                    {inventory.map(item => {
                      const itemId = item.id;
                      const name = item.itemName || item.ItemName || item.name || 'Item';
                      const category = item.category || item.Category || '';
                      const minStock = item.minStockLimit ?? item.MinStockLimit ?? 0;
                      const currentVal = stockUpdates[itemId] ?? '';
                      const currentNum = Number(currentVal);
                      const isLow = currentVal !== '' && currentNum <= minStock;

                      return (
                        <div key={itemId} className={`flex items-center gap-3 p-3 rounded-lg border transition-all ${
                          isLow ? 'bg-neon-orange/5 border-neon-orange/30' : 'bg-bg-3 border-border'
                        }`}>
                          <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-2">
                              <span className="text-sm font-semibold text-text truncate">{name}</span>
                              {isLow && (
                                <span className="text-[9px] bg-neon-orange/20 text-neon-orange px-1.5 py-0.5 rounded-full font-bold uppercase">
                                  Low
                                </span>
                              )}
                            </div>
                            <div className="text-[10px] text-text-3 font-mono mt-0.5">
                              {category} · Min: {minStock}
                            </div>
                          </div>
                          <div className="flex items-center gap-2">
                            <span className="text-xs text-text-3 hidden sm:block">Qty:</span>
                            <input
                              type="number"
                              min="0"
                              value={currentVal}
                              onChange={e => setStockUpdates(prev => ({ ...prev, [itemId]: e.target.value }))}
                              className={`w-20 bg-bg border text-text font-mono text-sm rounded-lg py-1.5 px-2 text-center focus:outline-none focus:ring-1 transition-all ${
                                isLow
                                  ? 'border-neon-orange/50 focus:border-neon-orange focus:ring-neon-orange/30'
                                  : 'border-border focus:border-accent focus:ring-accent/30'
                              }`}
                            />
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}

                <div className="p-3 mb-4 bg-neon-orange/10 border border-neon-orange/20 rounded-lg text-neon-orange text-xs flex items-start gap-2">
                  <AlertTriangle className="w-3.5 h-3.5 mt-0.5 shrink-0" />
                  <p><strong>Mandatory:</strong> All stock quantities must be verified before shift ends. This prevents off-record sales and theft.</p>
                </div>
                <button
                  onClick={handleInvSubmit}
                  disabled={invLoading || invFetching}
                  className="w-full py-3.5 rounded-xl text-sm font-bold uppercase tracking-wider flex items-center justify-center gap-2 transition-all bg-neon-purple/10 border border-neon-purple/50 text-neon-purple hover:bg-neon-purple/20 disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {invLoading ? (
                    <Loader2 className="w-5 h-5 animate-spin" />
                  ) : (
                    <>
                      <Package className="w-4 h-4" />
                      Confirm Stock & End Shift
                      <ArrowRight className="w-4 h-4" />
                    </>
                  )}
                </button>
              </motion.div>
            )}

            {/* ── STEP 3: Final Confirm ── */}
            {step === STEPS.CONFIRM && (
              <motion.div
                key="confirm"
                initial={{ opacity: 0, scale: 0.9 }}
                animate={{ opacity: 1, scale: 1 }}
                transition={{ duration: 0.3, type: 'spring', stiffness: 200 }}
                className="p-8 flex flex-col items-center text-center"
              >
                <motion.div
                  initial={{ scale: 0 }}
                  animate={{ scale: 1 }}
                  transition={{ delay: 0.1, type: 'spring', stiffness: 300 }}
                  className="w-20 h-20 bg-neon-red/10 border-2 border-neon-red/40 rounded-full flex items-center justify-center mb-5"
                >
                  <LogOut className="w-10 h-10 text-neon-red" />
                </motion.div>

                <h3 className="font-heading font-bold text-text text-2xl mb-2">Ready to End Shift</h3>

                {/* Summary */}
                <div className="w-full bg-bg-3 rounded-xl border border-border p-4 mb-5 text-left space-y-2">
                  <div className="flex justify-between items-center text-xs">
                    <span className="text-text-3">Expected Cash</span>
                    <span className="font-mono font-bold text-text">₹{expectedCash.toLocaleString()}</span>
                  </div>
                  <div className="flex justify-between items-center text-xs">
                    <span className="text-text-3">Physical Count</span>
                    <span className="font-mono font-bold text-text">₹{physicalCashNum.toLocaleString()}</span>
                  </div>
                  <div className={`flex justify-between items-center text-xs pt-2 border-t border-border`}>
                    <span className="text-text-2 font-bold">Difference</span>
                    <span className={`font-mono font-bold ${cashDifference === 0 ? 'text-neon-green' : 'text-neon-orange'}`}>
                      {cashDifference >= 0 ? '+' : ''}₹{cashDifference.toLocaleString()}
                    </span>
                  </div>
                  {mismatchReason && (
                    <div className="pt-1 text-[10px] text-text-3 italic">
                      Note: "{mismatchReason}"
                    </div>
                  )}
                </div>

                <p className="text-text-3 text-xs mb-6">
                  Click below to end your shift. You will be logged out of the system.
                </p>

                <button
                  onClick={handleFinalLogout}
                  disabled={confirmLoading}
                  className="w-full max-w-xs py-3.5 rounded-xl text-sm font-bold uppercase tracking-wider flex items-center justify-center gap-2 bg-neon-red/10 border border-neon-red text-neon-red hover:bg-neon-red/20 transition-all disabled:opacity-50"
                >
                  {confirmLoading ? (
                    <Loader2 className="w-5 h-5 animate-spin" />
                  ) : (
                    <>
                      <LogOut className="w-4 h-4" />
                      End Shift & Logout
                    </>
                  )}
                </button>

              </motion.div>
            )}
          </AnimatePresence>
        </div>
      </motion.div>
    </div>
  );
}
