import { useState, useEffect, useCallback } from 'react';
import { ShieldCheck, AlertTriangle, FileText, CheckCircle, Lock, Monitor, Utensils, Clock, Printer } from 'lucide-react';
import { printBill } from '../../utils/printBill';
import { useAuth } from '../../contexts/AuthContext';
import { useBranch } from '../../contexts/BranchContext';
import api from '../../config/api';
import PageHeader from '../../components/layout/PageHeader';
import { useSocket } from '../../contexts/SocketContext';

export default function EodDashboardPage() {
  const { isSuperAdmin, user } = useAuth();
  const { activeBranch } = useBranch();
  const { subscribe, connected, SIGNALR_HUBS } = useSocket();

  const [targetDate, setTargetDate] = useState(new Date().toISOString().split('T')[0]); // YYYY-MM-DD
  const [report, setReport] = useState(null);
  const [validation, setValidation] = useState(null);
  const [isHistorical, setIsHistorical] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [isFinalizing, setIsFinalizing] = useState(false);
  const [error, setError] = useState(null);

  const [pcs, setPcs] = useState([]);
  const [allBills, setAllBills] = useState([]);
  const [selectedPcId, setSelectedPcId] = useState(null);

  const targetBranchId = isSuperAdmin ? activeBranch?.id : user?.branchId;

  const fetchEodData = useCallback(async () => {
    if (isSuperAdmin && !targetBranchId) {
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    setError(null);
    setValidation(null);

    try {
      // First try to fetch historical snapshot
      try {
        const { data: historyData } = await api.get('/eod/history', {
          params: { date: targetDate, branchId: targetBranchId }
        });
        
        setReport(historyData.data.data); // historyData.data is EodSnapshotDto, .data is EodReportDto
        setIsHistorical(true);
      } catch (historyErr) {
        if (historyErr.response?.status === 404) {
          // No snapshot exists. It is either today or an unfinalized past date.
          setIsHistorical(false);
          
          // Fetch Preview
          const { data: previewData } = await api.get('/eod/preview', {
            params: { date: targetDate, branchId: targetBranchId }
          });
          setReport(previewData.data);

          // Fetch Validation Status
          const { data: validationData } = await api.get('/eod/validation', {
            params: { date: targetDate, branchId: targetBranchId }
          });
          setValidation(validationData.data);
        } else {
          throw historyErr;
        }
      }

      // Also fetch range-report to get allBills and PCs for PC-Wise Grid
      const [pcsRes, billsRes] = await Promise.all([
        api.get('/pcs', { params: { branchId: targetBranchId } }),
        api.get('/eod/range-report', {
          params: {
            startDate: `${targetDate}T00:00:00Z`,
            endDate: `${targetDate}T23:59:59Z`,
            branchId: targetBranchId
          }
        })
      ]);
      setPcs(pcsRes.data?.data || []);
      setAllBills(billsRes.data?.data?.allBills || []);

    } catch (err) {
      setError(err.response?.data?.error || err.response?.data?.message || 'Failed to fetch EOD data.');
    } finally {
      setIsLoading(false);
    }
  }, [targetDate, targetBranchId, isSuperAdmin]);

  useEffect(() => {
    fetchEodData();
  }, [fetchEodData]);

  // Real-time EOD updates via SignalR
  useEffect(() => {
    if (!connected || isHistorical) return;

    // Listen to changes that impact EOD (Bills, Cash, Sessions)
    const unsubCash = subscribe(SIGNALR_HUBS.CASH, 'CashRegisterUpdated', () => fetchEodData());
    const unsubBill = subscribe(SIGNALR_HUBS.BILLING, 'BillUpdated', () => fetchEodData());
    const unsubSession = subscribe(SIGNALR_HUBS.SESSIONS, 'SessionUpdated', () => fetchEodData());

    return () => {
      unsubCash();
      unsubBill();
      unsubSession();
    };
  }, [connected, subscribe, SIGNALR_HUBS.CASH, SIGNALR_HUBS.BILLING, SIGNALR_HUBS.SESSIONS, fetchEodData, isHistorical]);

  const handleFinalize = async () => {
    if (!window.confirm("Are you sure? This will generate a permanent immutable snapshot for this date. It cannot be undone.")) return;

    setIsFinalizing(true);
    try {
      await api.post('/eod/finalize', { date: targetDate });
      await fetchEodData(); // Re-fetch to show historical locked view
    } catch (err) {
      setError(err.response?.data?.error || err.response?.data?.message || 'Failed to finalize EOD.');
    } finally {
      setIsFinalizing(false);
    }
  };

  if (isSuperAdmin && !activeBranch) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[60vh] text-center">
        <Lock className="w-12 h-12 text-text-3 mb-4" />
        <h2 className="text-xl font-heading font-bold text-text mb-2">Select a Branch</h2>
        <p className="text-text-2">You must select a branch to view EOD Reports.</p>
      </div>
    );
  }

  return (
    <div className="h-full flex flex-col max-w-6xl mx-auto space-y-6 overflow-y-auto pb-10">
      <div className="flex justify-between items-center bg-bg-2 p-6 rounded-xl border border-border">
        <PageHeader
          title="End of Day Dashboard"
          subtitle={isHistorical ? 'Immutable Financial Snapshot' : 'Live Preview & Finalization'}
          icon="M9 17v-2m3 2v-4m3 4v-6m2 10H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"
        />
        <div className="flex flex-col items-end gap-2">
          <input
            type="date"
            value={targetDate}
            onChange={(e) => setTargetDate(e.target.value)}
            className="bg-bg-3 border border-border rounded-lg px-4 py-2 text-text outline-none focus:border-accent"
          />
          {isHistorical && (
            <span className="bg-neon-green/10 text-neon-green px-3 py-1 rounded border border-neon-green/30 text-xs font-bold uppercase tracking-widest flex items-center gap-2">
              <ShieldCheck className="w-4 h-4" /> Finalized
            </span>
          )}
        </div>
      </div>

      {/* PC-Wise Personalised Billing Section */}
      <div className="card bg-bg-2 border border-border p-6 rounded-xl">
        <h2 className="font-heading font-extrabold text-sm uppercase tracking-wider text-text flex items-center gap-2 mb-6">
          <Monitor className="w-4.5 h-4.5 text-neon-blue" />
          PC-Wise Personalised Billing
        </h2>

        {/* PC Grid */}
        <div className="grid grid-cols-3 md:grid-cols-4 lg:grid-cols-8 gap-3 mb-6">
          {pcs.map(pc => {
            const pcBills = allBills?.filter(b => b.pcId === pc.id) || [];
            const pcDayTotal = pcBills.reduce((sum, b) => sum + (b.totalRevenue || 0), 0);
            const pcBillCount = pcBills.length;
            const hasEarnings = pcDayTotal > 0;

            return (
              <button
                key={pc.id}
                onClick={() => setSelectedPcId(pc.id)}
                className={`p-3 rounded-lg border flex flex-col items-center justify-center transition-all relative ${
                  selectedPcId === pc.id 
                    ? 'bg-neon-blue/20 border-neon-blue shadow-[0_0_15px_rgba(0,240,255,0.3)]' 
                    : hasEarnings 
                      ? 'bg-bg-3 border-neon-green/30 hover:border-neon-green/60' 
                      : 'bg-bg-3 border-border hover:border-neon-blue/50'
                }`}
              >
                {pcBillCount > 0 && (
                  <span className="absolute -top-1.5 -right-1.5 bg-accent text-white text-[9px] font-bold w-5 h-5 rounded-full flex items-center justify-center shadow-md">
                    {pcBillCount}
                  </span>
                )}
                <div className={`w-8 h-8 rounded-full flex items-center justify-center mb-1.5 ${
                  selectedPcId === pc.id ? 'bg-neon-blue/20' : hasEarnings ? 'bg-neon-green/10' : 'bg-bg-2'
                }`}>
                  <Monitor className={`w-4 h-4 ${
                    selectedPcId === pc.id ? 'text-neon-blue' : hasEarnings ? 'text-neon-green' : 'text-text-3'
                  }`} />
                </div>
                <div className="font-heading font-bold text-[10px] text-text truncate w-full text-center mb-1">
                  {pc.name || pc.pcName || pc.pcNumber}
                </div>
                <div className={`font-mono font-bold text-xs ${
                  hasEarnings ? 'text-neon-green' : 'text-text-3'
                }`}>
                  ₹{pcDayTotal.toFixed(0)}
                </div>
              </button>
            );
          })}
        </div>

        {/* Selected PC Details */}
        {selectedPcId && (
          <div className="space-y-6 animate-fade-in border-t border-border/50 pt-6">
            {(() => {
              const pcBills = allBills?.filter(b => 
                selectedPcId === 'walkin' ? (!b.pcId) : (b.pcId === selectedPcId)
              ) || [];
              
              const pcTotalGaming = pcBills.reduce((sum, d) => sum + d.gamingRevenue, 0);
              const pcTotalFood = pcBills.reduce((sum, d) => sum + d.foodRevenue, 0);
              const pcTotalDiscount = pcBills.reduce((sum, d) => sum + d.discount, 0);
              const pcTotalNet = pcBills.reduce((sum, d) => sum + d.totalRevenue, 0);

              return (
                <>
                  <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
                    <div className="bg-bg-3 p-4 rounded-lg border border-border flex flex-col justify-center">
                      <div className="text-[10px] text-text-3 font-bold uppercase tracking-wider">Total Bills</div>
                      <div className="text-xl font-mono font-bold text-text">{pcBills.length}</div>
                    </div>
                    <div className="bg-bg-3 p-4 rounded-lg border border-border flex flex-col justify-center">
                      <div className="text-[10px] text-text-3 font-bold uppercase tracking-wider">Net Gaming</div>
                      <div className="text-xl font-mono font-bold text-neon-blue">₹{pcTotalGaming.toFixed(2)}</div>
                    </div>
                    <div className="bg-bg-3 p-4 rounded-lg border border-border flex flex-col justify-center">
                      <div className="text-[10px] text-text-3 font-bold uppercase tracking-wider">Net Food</div>
                      <div className="text-xl font-mono font-bold text-accent">₹{pcTotalFood.toFixed(2)}</div>
                    </div>
                    <div className="bg-bg-3 p-4 rounded-lg border border-border flex flex-col justify-center">
                      <div className="text-[10px] text-text-3 font-bold uppercase tracking-wider">Discounts</div>
                      <div className="text-xl font-mono font-bold text-neon-orange">₹{pcTotalDiscount.toFixed(2)}</div>
                    </div>
                    <div className="bg-bg-3 p-4 rounded-lg border border-border flex flex-col justify-center relative overflow-hidden">
                      <div className="absolute top-0 right-0 w-16 h-16 bg-neon-green/10 rounded-full blur-xl" />
                      <div className="text-[10px] text-text-3 font-bold uppercase tracking-wider relative">Net Revenue</div>
                      <div className="text-xl font-mono font-bold text-neon-green relative">₹{pcTotalNet.toFixed(2)}</div>
                    </div>
                  </div>

                  <div className="overflow-x-auto mt-4">
                    {pcBills.length === 0 ? (
                      <div className="text-center text-text-3 text-xs italic py-8 border border-dashed border-border rounded-lg">
                        No billing records found for this PC.
                      </div>
                    ) : (
                      <table className="w-full text-left border-collapse text-xs whitespace-nowrap">
                        <thead>
                          <tr className="border-b border-border text-text-3 uppercase tracking-wider font-bold text-[10px]">
                            <th className="py-3 px-4">Start Time</th>
                            <th className="py-3 px-4">End Time</th>
                            <th className="py-3 px-4">Duration</th>
                            <th className="py-3 px-4">Bill Number</th>
                            <th className="py-3 px-4">Operator</th>
                            <th className="py-3 px-4">Customer</th>
                            <th className="py-3 px-4 text-center">Payment</th>
                            <th className="py-3 px-4 text-right">Gaming</th>
                            <th className="py-3 px-4 text-right">Food</th>
                            <th className="py-3 px-4 text-right">Total</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-border/40 font-mono">
                          {pcBills.map(bill => {
                            const startStr = bill.sessionStartTime ? new Date(bill.sessionStartTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '-';
                            const endStr = bill.sessionEndTime ? new Date(bill.sessionEndTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '-';
                            const durationMinutes = Math.floor(bill.sessionDurationMinutes || 0);
                            const h = Math.floor(durationMinutes / 60);
                            const m = durationMinutes % 60;
                            const durationStr = h > 0 ? `${h}h ${m}m` : `${m}m`;

                            return (
                              <tr key={bill.billId} className="hover:bg-bg-3/40 transition-colors">
                                <td className="py-3 px-4 text-text-2">{startStr}</td>
                                <td className="py-3 px-4 text-text-2">{endStr}</td>
                                <td className="py-3 px-4 text-neon-blue font-bold">{bill.sessionStartTime ? durationStr : '-'}</td>
                                <td className="py-3 px-4 text-text font-bold">{bill.billId}</td>
                                <td className="py-3 px-4 text-text-2">{bill.operator}</td>
                                <td className="py-3 px-4 text-text-2 font-sans">{bill.customer}</td>
                                <td className="py-3 px-4 text-center text-text-3 uppercase">{bill.paymentType}</td>
                                <td className="py-3 px-4 text-right text-text">₹{bill.gamingRevenue.toFixed(2)}</td>
                                <td className="py-3 px-4 text-right text-text">₹{bill.foodRevenue.toFixed(2)}</td>
                                <td className="py-3 px-4 text-right text-neon-green font-bold">₹{bill.totalRevenue.toFixed(2)}</td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    )}
                  </div>
                </>
              );
            })()}
          </div>
        )}
      </div>

      {error && (
        <div className="bg-neon-red/10 border border-neon-red/30 text-neon-red p-4 rounded-xl flex items-center gap-3">
          <AlertTriangle className="w-5 h-5 shrink-0" />
          <p>{error}</p>
        </div>
      )}

      {isLoading ? (
        <div className="flex justify-center items-center min-h-[40vh]">
          <div className="w-8 h-8 rounded-full border-2 border-accent border-t-transparent animate-spin" />
        </div>
      ) : report ? (
        <>
          {/* Validation Panel (Only if not historical) */}
          {!isHistorical && validation && (
            <div className={`p-6 rounded-xl border ${validation.isReady ? 'bg-neon-green/5 border-neon-green/20' : 'bg-neon-red/5 border-neon-red/20'}`}>
              <h3 className={`text-sm uppercase font-bold tracking-widest mb-4 flex items-center gap-2 ${validation.isReady ? 'text-neon-green' : 'text-neon-red'}`}>
                {validation.isReady ? <CheckCircle className="w-5 h-5" /> : <AlertTriangle className="w-5 h-5" />}
                Financial Validation Status
              </h3>
              
              {validation.isReady ? (
                <p className="text-text-2 text-sm">All shifts are closed. All registers verified. Financials are balanced. You may proceed to finalize.</p>
              ) : (
                <ul className="space-y-2">
                  {validation.blockers.map((blocker, idx) => (
                    <li key={idx} className="text-sm text-neon-red flex items-start gap-2">
                      <span className="text-neon-red/50 mt-0.5">•</span>
                      {blocker}
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}

          {/* Revenue & Operations Summary Grid */}
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div className="bg-bg-2 p-5 rounded-xl border border-border shadow-lg">
              <div className="text-text-3 text-xs uppercase font-bold tracking-widest mb-1">Total Net Revenue</div>
              <div className="text-3xl font-mono font-bold text-accent">₹{report.revenue.netRevenue}</div>
            </div>
            <div className="bg-bg-2 p-5 rounded-xl border border-border shadow-lg">
              <div className="text-text-3 text-xs uppercase font-bold tracking-widest mb-1">Gaming Revenue</div>
              <div className="text-2xl font-mono font-bold text-text">₹{report.revenue.totalGamingRevenue}</div>
            </div>
            <div className="bg-bg-2 p-5 rounded-xl border border-border shadow-lg">
              <div className="text-text-3 text-xs uppercase font-bold tracking-widest mb-1">Food Revenue</div>
              <div className="text-2xl font-mono font-bold text-text">₹{report.revenue.totalFoodRevenue}</div>
            </div>
            <div className="bg-bg-2 p-5 rounded-xl border border-border shadow-lg">
              <div className="text-text-3 text-xs uppercase font-bold tracking-widest mb-1">Discounts Applied</div>
              <div className="text-2xl font-mono font-bold text-text">₹{report.revenue.totalDiscounts}</div>
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {/* Cash Summary */}
            <div className="bg-bg-2 rounded-xl border border-border shadow-lg p-6">
              <h3 className="text-sm uppercase font-bold text-text-2 tracking-widest mb-6 border-b border-border pb-3 flex items-center gap-2">
                <FileText className="w-4 h-4" /> Cash Lifecycle Summary
              </h3>
              <div className="space-y-4">
                <div className="flex justify-between items-center text-sm">
                  <span className="text-text-2">Opening Balance Total</span>
                  <span className="font-mono text-text">₹{report.cash.totalOpeningBalance}</span>
                </div>
                <div className="flex justify-between items-center text-sm">
                  <span className="text-text-2">Cash Sales + Wallet TopUps</span>
                  <span className="font-mono text-neon-green">+ ₹{report.cash.totalCashSales}</span>
                </div>
                <div className="flex justify-between items-center text-sm">
                  <span className="text-text-2">Petty Expenses</span>
                  <span className="font-mono text-neon-red">- ₹{report.cash.totalPettyExpenses}</span>
                </div>
                <div className="flex justify-between items-center text-sm border-t border-border pt-4">
                  <span className="font-bold text-text">Expected Drawer Total</span>
                  <span className="font-mono font-bold text-accent">₹{report.cash.expectedCashInDrawer}</span>
                </div>
                <div className="flex justify-between items-center text-sm">
                  <span className="font-bold text-text">Physically Counted</span>
                  <span className="font-mono font-bold text-text">₹{report.cash.actualPhysicalCashCounted}</span>
                </div>
                <div className="flex justify-between items-center text-sm bg-bg-3 p-3 rounded-lg border border-border mt-2">
                  <span className="font-bold text-text uppercase tracking-widest text-xs">Total Difference</span>
                  <span className={`font-mono font-bold ${report.cash.totalDiscrepancy === 0 ? 'text-neon-blue' : 'text-neon-red'}`}>
                    ₹{report.cash.totalDiscrepancy}
                  </span>
                </div>
              </div>
            </div>

            {/* Payment Methods */}
            <div className="bg-bg-2 rounded-xl border border-border shadow-lg p-6 flex flex-col justify-between">
              <div>
                <h3 className="text-sm uppercase font-bold text-text-2 tracking-widest mb-6 border-b border-border pb-3 flex items-center gap-2">
                  <FileText className="w-4 h-4" /> Overall Collection & Business
                </h3>
                <div className="space-y-4">
                  <div className="flex justify-between items-center text-sm">
                    <span className="text-text-2">Cash</span>
                    <span className="font-mono text-text">₹{report.paymentMethods.totalCash}</span>
                  </div>
                  <div className="flex justify-between items-center text-sm">
                    <span className="text-text-2">Online</span>
                    <span className="font-mono text-text">₹{report.paymentMethods.totalOnline}</span>
                  </div>
                  <div className="flex justify-between items-center text-sm">
                    <span className="text-text-2">Wallet</span>
                    <span className="font-mono text-neon-purple">₹{report.paymentMethods.totalWalletDeductions}</span>
                  </div>
                  <div className="flex justify-between items-center text-sm">
                    <span className="text-text-2">Credits Pending</span>
                    <span className="font-mono text-neon-red">
                      -₹{(report.creditLogs?.filter(c => c.status?.toLowerCase() === 'pending').reduce((acc, c) => acc + c.creditAmount, 0) || 0).toFixed(2)}
                    </span>
                  </div>
                  
                  <div className="flex justify-between items-center text-sm bg-neon-blue/10 p-4 rounded-lg border border-neon-blue/30 mt-6">
                    <span className="font-bold text-neon-blue uppercase tracking-widest text-xs">Overall End Total</span>
                    <span className="font-mono font-bold text-xl text-neon-blue">
                      ₹{(report.paymentMethods.totalCash + report.paymentMethods.totalOnline + report.paymentMethods.totalWalletDeductions).toFixed(2)}
                    </span>
                  </div>
                </div>
              </div>

              <div className="mt-8">
                <h3 className="text-sm uppercase font-bold text-text-2 tracking-widest mb-6 border-b border-border pb-3 flex items-center gap-2">
                  <FileText className="w-4 h-4" /> Operations Overview
                </h3>
                <div className="grid grid-cols-2 gap-4">
                  <div className="bg-bg-3 p-3 rounded-lg border border-border text-center">
                    <div className="text-2xl font-bold text-text">{report.operations.totalSessions}</div>
                    <div className="text-[10px] uppercase font-bold text-text-3 tracking-widest mt-1">Sessions</div>
                  </div>
                  <div className="bg-bg-3 p-3 rounded-lg border border-border text-center">
                    <div className="text-2xl font-bold text-text">{report.operations.totalFoodOrders}</div>
                    <div className="text-[10px] uppercase font-bold text-text-3 tracking-widest mt-1">Food Orders</div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* ── Complete Billing Audit Logs ── */}
          <div className="card bg-bg-2 border border-border p-6 rounded-xl shadow-lg mt-8">
            <div className="flex justify-between items-center mb-6">
              <h2 className="font-heading font-extrabold text-sm uppercase tracking-wider text-text flex items-center gap-2">
                <Clock className="w-4.5 h-4.5 text-accent" />
                Complete Billing Audit Logs ({targetDate})
              </h2>
            </div>

            <div className="overflow-x-auto">
              {!allBills || allBills.length === 0 ? (
                <div className="text-center text-text-3 text-xs italic py-8 border border-dashed border-border rounded-lg">
                  No bills found for the selected date.
                </div>
              ) : (
                <table className="w-full text-left border-collapse text-xs whitespace-nowrap">
                  <thead>
                    <tr className="border-b border-border text-text-3 uppercase tracking-wider font-bold text-[10px]">
                      <th className="py-3 px-4">Date/Time</th>
                      <th className="py-3 px-4">Bill Number</th>
                      <th className="py-3 px-4">Operator</th>
                      <th className="py-3 px-4">Customer</th>
                      <th className="py-3 px-4 text-center">Payment</th>
                      <th className="py-3 px-4 text-right">Gaming</th>
                      <th className="py-3 px-4 text-right">Food</th>
                      <th className="py-3 px-4 text-right">Discount</th>
                      <th className="py-3 px-4 text-right">Total</th>
                      <th className="py-3 px-4">Notes</th>
                      <th className="py-3 px-4 text-center">Print</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border/40 font-mono">
                    {allBills.map(bill => (
                      <tr key={bill.billId} className="hover:bg-bg-3/40 transition-colors">
                        <td className="py-3 px-4 text-text-2 flex items-center gap-1">
                          {new Date(bill.date).toLocaleString()}
                        </td>
                        <td className="py-3 px-4 text-text font-bold">{bill.billId}</td>
                        <td className="py-3 px-4 text-neon-blue font-bold">{bill.operator}</td>
                        <td className="py-3 px-4 text-text-2 font-sans">{bill.customer}</td>
                        <td className="py-3 px-4 text-center">
                          {bill.paymentType?.toUpperCase() === 'CREDIT' ? (
                            <div className="flex flex-col items-center gap-1">
                              <span className="text-text-3 font-bold uppercase text-[10px]">Credit</span>
                              {bill.creditStatus?.toLowerCase() === 'cleared' ? (
                                <>
                                  <span className="text-neon-green text-[9px] bg-neon-green/10 px-1.5 py-0.5 rounded border border-neon-green/20 uppercase tracking-wider font-bold">Cleared</span>
                                  <span className="text-text-3 text-[9px]">Total Paid: ₹{bill.totalRevenue.toFixed(2)}</span>
                                </>
                              ) : (
                                <>
                                  <span className="text-neon-red text-[9px] bg-neon-red/10 px-1.5 py-0.5 rounded border border-neon-red/20 uppercase tracking-wider font-bold">
                                    ₹{(bill.creditAmount || 0).toFixed(2)} Pending
                                  </span>
                                  <span className="text-text-3 text-[9px]">Upfront: ₹{(bill.amountPaidInitially || 0).toFixed(2)}</span>
                                </>
                              )}
                            </div>
                          ) : (
                            <span className="text-text-3 uppercase">{bill.paymentType}</span>
                          )}
                        </td>
                        <td className="py-3 px-4 text-right text-text">₹{bill.gamingRevenue.toFixed(2)}</td>
                        <td className="py-3 px-4 text-right text-text">₹{bill.foodRevenue.toFixed(2)}</td>
                        <td className="py-3 px-4 text-right text-neon-red">{bill.discount > 0 ? `-₹${bill.discount.toFixed(2)}` : '-'}</td>
                        <td className="py-3 px-4 text-right text-neon-green font-bold">₹{bill.totalRevenue.toFixed(2)}</td>
                        <td className="py-3 px-4 text-text-3 text-[10px] whitespace-pre-wrap">{bill.sessionNotes || '-'}</td>
                        <td className="py-3 px-4 text-center">
                          <button 
                            onClick={() => printBill(bill.billId || bill.id, bill)} 
                            className="p-1.5 bg-bg-3 hover:bg-accent hover:text-bg transition-colors rounded-lg text-text-2 tooltip-trigger"
                            title="Print Bill"
                          >
                            <Printer className="w-4 h-4" />
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>

          {/* ── Credit Audit Logs ── */}
          <div className="card bg-bg-2 border border-border p-6 rounded-xl shadow-lg mt-8">
            <div className="flex justify-between items-center mb-6">
              <h2 className="font-heading font-extrabold text-sm uppercase tracking-wider text-text flex items-center gap-2">
                <Clock className="w-4.5 h-4.5 text-accent" />
                Credit Audit Logs ({targetDate})
              </h2>
            </div>

            <div className="overflow-x-auto">
              {!report.creditLogs || report.creditLogs.length === 0 ? (
                <div className="text-center text-text-3 text-xs italic py-8 border border-dashed border-border rounded-lg">
                  No credit records found for the selected date.
                </div>
              ) : (
                <table className="w-full text-left border-collapse text-xs whitespace-nowrap">
                  <thead>
                    <tr className="border-b border-border text-text-3 uppercase tracking-wider font-bold text-[10px]">
                      <th className="py-3 px-4">Date Created</th>
                      <th className="py-3 px-4">Customer</th>
                      <th className="py-3 px-4">PC</th>
                      <th className="py-3 px-4 text-right">Original Bill</th>
                      <th className="py-3 px-4 text-right">Initial Paid</th>
                      <th className="py-3 px-4 text-right">Amount Due</th>
                      <th className="py-3 px-4 text-center">Status</th>
                      <th className="py-3 px-4">Date Cleared</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border/40 font-mono">
                    {report.creditLogs.map(credit => (
                      <tr key={credit.creditId} className="hover:bg-bg-3/40 transition-colors">
                        <td className="py-3 px-4 text-text-2 flex items-center gap-1">
                          {new Date(credit.createdAt).toLocaleString()}
                        </td>
                        <td className="py-3 px-4 text-neon-blue font-bold">
                          {credit.customerName}
                          <div className="text-[10px] text-text-3 font-sans font-normal">{credit.customerPhone}</div>
                        </td>
                        <td className="py-3 px-4 text-text-2">{credit.pcNumber}</td>
                        <td className="py-3 px-4 text-right text-text">₹{credit.originalBillAmount.toFixed(2)}</td>
                        <td className="py-3 px-4 text-right text-text">₹{credit.amountPaidInitially.toFixed(2)}</td>
                        <td className="py-3 px-4 text-right text-neon-red font-bold">₹{credit.creditAmount.toFixed(2)}</td>
                        <td className="py-3 px-4 text-center">
                          {credit.status.toLowerCase() === 'cleared' ? (
                            <span className="text-neon-green font-bold uppercase tracking-wider text-[10px] bg-neon-green/10 px-2 py-1 rounded border border-neon-green/20">Cleared</span>
                          ) : (
                            <span className="text-neon-red font-bold uppercase tracking-wider text-[10px] bg-neon-red/10 px-2 py-1 rounded border border-neon-red/20">Pending</span>
                          )}
                        </td>
                        <td className="py-3 px-4 text-text-3">
                          {credit.clearedAt ? new Date(credit.clearedAt).toLocaleString() : '-'}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>

          {/* Finalize Button */}
          {!isHistorical && isSuperAdmin && (
            <div className="mt-8">
              <button
                onClick={handleFinalize}
                disabled={!validation?.isReady || isFinalizing}
                className="w-full py-5 rounded-xl font-bold uppercase tracking-widest text-sm transition-all bg-accent hover:bg-accent-hover text-white shadow-lg shadow-accent/20 flex justify-center items-center gap-3 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isFinalizing ? (
                  <div className="w-5 h-5 rounded-full border-2 border-white border-t-transparent animate-spin" />
                ) : (
                  <>
                    <ShieldCheck className="w-5 h-5" />
                    Finalize EOD & Create Immutable Snapshot
                  </>
                )}
              </button>
            </div>
          )}
        </>
      ) : null}
    </div>
  );
}
