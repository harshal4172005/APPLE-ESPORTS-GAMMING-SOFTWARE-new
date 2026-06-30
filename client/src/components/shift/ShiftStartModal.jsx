// ═══════════════════════════════════════════════════════════
// Gaming Café ERP — Shift Start Modal
// SOP §6.3: Operator shift start → cash register + inventory check
// Shown immediately after operator login — blocks entry until complete
// ═══════════════════════════════════════════════════════════

import { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  Banknote, Package, CheckCircle2, AlertTriangle,
  ChevronRight, Loader2, ArrowRight, Box, ClipboardCheck
} from 'lucide-react';
import api from '../../config/api';

const STEPS = {
  CASH: 'cash',
  INVENTORY: 'inventory',
  DONE: 'done',
};

export default function ShiftStartModal({ onComplete }) {
  const [step, setStep] = useState(STEPS.CASH);

  // ── Cash Step ──
  const [openingBalance, setOpeningBalance] = useState('');
  const [cashLoading, setCashLoading] = useState(false);
  const [cashError, setCashError] = useState(null);
  const [cashAlreadyOpen, setCashAlreadyOpen] = useState(false);

  // ── Inventory Step ──
  const [inventory, setInventory] = useState([]);
  const [stockUpdates, setStockUpdates] = useState({});
  const [invLoading, setInvLoading] = useState(false);
  const [invFetching, setInvFetching] = useState(false);
  const [invError, setInvError] = useState(null);

  // Check if cash register already open (operator refreshed page mid-shift)
  useEffect(() => {
    const checkRegister = async () => {
      try {
        const { data } = await api.get('/cash/active');
        if (data.data) {
          setCashAlreadyOpen(true);
          setStep(STEPS.INVENTORY);
        }
      } catch {
        // 404 = no active register, show cash step normally
      }
    };
    checkRegister();
  }, []);

  // Fetch inventory when on inventory step
  useEffect(() => {
    if (step !== STEPS.INVENTORY) return;
    const fetchInventory = async () => {
      setInvFetching(true);
      setInvError(null);
      try {
        const { data } = await api.get('/inventory');
        const items = data.data || data || [];
        setInventory(items);
        // Initialize updates with current stock
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

  // ── Step 1: Open Cash Register ──
  const handleOpenCash = async () => {
    const amount = Number(openingBalance);
    if (isNaN(amount) || amount < 0) {
      setCashError('Please enter a valid opening balance (0 or greater).');
      return;
    }
    setCashLoading(true);
    setCashError(null);
    try {
      await api.post('/cash/open', { openingBalance: amount });
      setStep(STEPS.INVENTORY);
    } catch (err) {
      const msg = err.response?.data?.error || err.response?.data?.message || '';
      if (msg.toLowerCase().includes('already') || err.response?.status === 409) {
        // Register already open — skip to inventory
        setCashAlreadyOpen(true);
        setStep(STEPS.INVENTORY);
      } else {
        setCashError(msg || 'Failed to open cash register.');
      }
    } finally {
      setCashLoading(false);
    }
  };

  // ── Step 2: Confirm Inventory Stocks ──
  const handleConfirmInventory = async () => {
    setInvLoading(true);
    setInvError(null);
    try {
      // Update each item stock if changed
      const updates = inventory.map(item => ({
        id: item.id,
        currentStock: Number(stockUpdates[item.id] ?? item.currentStock ?? item.CurrentStock ?? 0),
      }));

      // Batch update inventory stock via API
      for (const update of updates) {
        const original = inventory.find(i => i.id === update.id);
        const originalStock = original?.currentStock ?? original?.CurrentStock ?? 0;
        if (update.currentStock !== originalStock) {
          await api.patch(`/inventory/${update.id}/stock`, { currentStock: update.currentStock });
        }
      }
      setStep(STEPS.DONE);
    } catch (err) {
      setInvError(err.response?.data?.error || 'Failed to update inventory. You can still proceed.');
    } finally {
      setInvLoading(false);
    }
  };


  const handleDone = () => {
    onComplete();
  };

  const getStepNum = () => {
    if (step === STEPS.CASH) return 1;
    if (step === STEPS.INVENTORY) return 2;
    return 3;
  };

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-black/80 backdrop-blur-lg">
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
            <div className="w-10 h-10 bg-accent/10 rounded-xl flex items-center justify-center border border-accent/30">
              <ClipboardCheck className="w-5 h-5 text-accent" />
            </div>
            <div>
              <h2 className="font-heading font-bold text-text text-lg tracking-wide">Shift Start Checklist</h2>
              <p className="text-[11px] text-text-3 font-mono">Complete all steps before entering the system</p>
            </div>
          </div>

          {/* Step Progress */}
          <div className="flex items-center gap-2">
            {[
              { num: 1, label: 'Cash Register', icon: Banknote },
              { num: 2, label: 'Inventory', icon: Package },
              { num: 3, label: 'Ready', icon: CheckCircle2 },
            ].map((s, idx) => (
              <div key={s.num} className="flex items-center gap-2 flex-1">
                <div className={`flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[10px] font-bold uppercase tracking-wider transition-all ${
                  getStepNum() === s.num
                    ? 'bg-accent/15 border border-accent text-accent'
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
            {/* ── STEP 1: Cash Register ── */}
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
                  <div className="w-12 h-12 bg-neon-green/10 border border-neon-green/30 rounded-xl flex items-center justify-center">
                    <Banknote className="w-6 h-6 text-neon-green" />
                  </div>
                  <div>
                    <h3 className="font-bold text-text text-base">Open Cash Register</h3>
                    <p className="text-text-3 text-xs">Count the physical cash in the drawer and enter the total</p>
                  </div>
                </div>

                {cashError && (
                  <div className="p-3 mb-4 bg-neon-red/10 border border-neon-red/20 rounded-lg text-neon-red text-xs flex items-start gap-2">
                    <AlertTriangle className="w-3.5 h-3.5 mt-0.5 shrink-0" />
                    <p>{cashError}</p>
                  </div>
                )}

                <div className="space-y-2 mb-6">
                  <label className="text-xs uppercase tracking-wider font-bold text-text-2">
                    Opening Balance (Physical Cash in Drawer)
                  </label>
                  <div className="relative">
                    <span className="absolute left-4 top-1/2 -translate-y-1/2 font-mono text-text-3 text-xl">₹</span>
                    <input
                      type="number"
                      min="0"
                      placeholder="0.00"
                      value={openingBalance}
                      onChange={e => setOpeningBalance(e.target.value)}
                      onKeyDown={e => e.key === 'Enter' && handleOpenCash()}
                      className="w-full bg-bg-3 border border-border text-text font-mono text-2xl rounded-xl py-4 pl-12 pr-4 focus:border-accent focus:ring-1 focus:ring-accent transition-all outline-none"
                      autoFocus
                    />
                  </div>
                  <p className="text-[11px] text-text-3 italic">
                    This will be your shift's opening drawer balance. The system will track all cash movements from this amount.
                  </p>
                </div>

                <button
                  onClick={handleOpenCash}
                  disabled={cashLoading || openingBalance === ''}
                  className="w-full py-3.5 rounded-xl text-sm font-bold uppercase tracking-wider flex items-center justify-center gap-2 transition-all bg-accent/10 border border-accent text-accent hover:bg-accent/20 disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {cashLoading ? (
                    <Loader2 className="w-5 h-5 animate-spin" />
                  ) : (
                    <>
                      <Banknote className="w-4 h-4" />
                      Open Shift Register
                      <ArrowRight className="w-4 h-4" />
                    </>
                  )}
                </button>
              </motion.div>
            )}

            {/* ── STEP 2: Inventory Check ── */}
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
                    <Package className="w-6 h-6 text-neon-purple" />
                  </div>
                  <div>
                    <h3 className="font-bold text-text text-base">Inventory Stock Check</h3>
                    <p className="text-text-3 text-xs">Verify and update current stock levels before shift begins</p>
                  </div>
                </div>

                {cashAlreadyOpen && (
                  <div className="p-2.5 mb-4 bg-neon-blue/10 border border-neon-blue/20 rounded-lg text-neon-blue text-xs flex items-center gap-2">
                    <CheckCircle2 className="w-3.5 h-3.5 shrink-0" />
                    Cash register already open for this shift.
                  </div>
                )}

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
                    <p className="text-sm">No inventory items found for your branch.</p>
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
                                  Low Stock
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
                  <p><strong>Mandatory:</strong> All stock quantities must be verified and confirmed before entering the system. This prevents off-record sales.</p>
                </div>
                <button
                  onClick={handleConfirmInventory}
                  disabled={invLoading || invFetching}
                  className="w-full py-3.5 rounded-xl text-sm font-bold uppercase tracking-wider flex items-center justify-center gap-2 transition-all bg-neon-purple/10 border border-neon-purple/50 text-neon-purple hover:bg-neon-purple/20 disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {invLoading ? (
                    <Loader2 className="w-5 h-5 animate-spin" />
                  ) : (
                    <>
                      <Package className="w-4 h-4" />
                      Confirm Stock & Enter System
                      <ArrowRight className="w-4 h-4" />
                    </>
                  )}
                </button>
              </motion.div>
            )}

            {/* ── STEP 3: Ready ── */}
            {step === STEPS.DONE && (
              <motion.div
                key="done"
                initial={{ opacity: 0, scale: 0.9 }}
                animate={{ opacity: 1, scale: 1 }}
                transition={{ duration: 0.3, type: 'spring', stiffness: 200 }}
                className="p-8 flex flex-col items-center text-center"
              >
                <motion.div
                  initial={{ scale: 0 }}
                  animate={{ scale: 1 }}
                  transition={{ delay: 0.1, type: 'spring', stiffness: 300 }}
                  className="w-20 h-20 bg-neon-green/10 border-2 border-neon-green/40 rounded-full flex items-center justify-center mb-5"
                >
                  <CheckCircle2 className="w-10 h-10 text-neon-green" />
                </motion.div>

                <h3 className="font-heading font-bold text-text text-2xl mb-2">Shift Started!</h3>
                <p className="text-text-3 text-sm mb-2">
                  Cash register is open and inventory has been verified.
                </p>
                <p className="text-text-3 text-xs font-mono">
                  All systems ready. Have a great shift!
                </p>

                <button
                  onClick={handleDone}
                  className="mt-8 w-full max-w-xs py-3.5 rounded-xl text-sm font-bold uppercase tracking-wider flex items-center justify-center gap-2 bg-neon-green/10 border border-neon-green text-neon-green hover:bg-neon-green/20 transition-all"
                >
                  <CheckCircle2 className="w-4 h-4" />
                  Enter System
                </button>
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      </motion.div>
    </div>
  );
}
