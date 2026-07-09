import { useState, useEffect } from 'react';
import { 
  TrendingUp, 
  Calendar, 
  DollarSign, 
  Utensils, 
  Gamepad2, 
  AlertTriangle,
  User,
  Clock,
  Search,
  RefreshCw,
  Printer,
  Monitor
} from 'lucide-react';
import PageHeader from '../../components/layout/PageHeader';
import api from '../../config/api';
import { getRangeReport, getDiscrepancies } from '../../api/food.api';
import { getCashReconciliationReport } from '../../api/reports.api';
import { useBranch } from '../../contexts/BranchContext';
import { useAuth } from '../../contexts/AuthContext';

export default function ReportsPage() {
  const { activeBranch } = useBranch();
  const { isSuperAdmin } = useAuth();

  // Date range selectors
  const [startDate, setStartDate] = useState(
    new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString().split('T')[0]
  );
  const [endDate, setEndDate] = useState(
    new Date().toISOString().split('T')[0]
  );

  // Data states
  const [reportData, setReportData] = useState({ daily: [], monthly: [] });
  const [discrepancies, setDiscrepancies] = useState([]);
  const [loading, setLoading] = useState(false);
  const [discrepancyLoading, setDiscrepancyLoading] = useState(false);
  const [reconciliationData, setReconciliationData] = useState([]);
  const [reconciliationLoading, setReconciliationLoading] = useState(false);
  const [error, setError] = useState(null);

  const fetchReports = async () => {
    if (!activeBranch) return;
    setLoading(true);
    setError(null);
    try {
      const data = await getRangeReport({ 
        startDate: `${startDate}T00:00:00Z`, 
        endDate: `${endDate}T23:59:59Z`, 
        branchId: activeBranch.id 
      });
      setReportData(data?.data || { daily: [], monthly: [] });
    } catch (err) {
      setError('Failed to fetch range report data.');
    } finally {
      setLoading(false);
    }
  };

  const fetchDiscrepancies = async () => {
    if (!activeBranch) return;
    setDiscrepancyLoading(true);
    try {
      const data = await getDiscrepancies({ branchId: activeBranch.id });
      setDiscrepancies(data?.data || []);
    } catch (err) {
      console.error('Failed to load discrepancies log:', err);
    } finally {
      setDiscrepancyLoading(false);
    }
  };

  const fetchReconciliation = async () => {
    if (!activeBranch) return;
    setReconciliationLoading(true);
    try {
      const data = await getCashReconciliationReport({ 
        startDate: `${startDate}T00:00:00Z`, 
        endDate: `${endDate}T23:59:59Z` 
      });
      setReconciliationData(data?.data?.data || []);
    } catch (err) {
      console.error('Failed to load reconciliation report:', err);
    } finally {
      setReconciliationLoading(false);
    }
  };

  useEffect(() => {
    if (activeBranch) {
      fetchReports();
      fetchDiscrepancies();
      fetchReconciliation();
    }
  }, [activeBranch]);

  // Aggregate totals
  const totalGaming = reportData.daily?.reduce((sum, d) => sum + d.gamingRevenue, 0) || 0;
  const totalFood = reportData.daily?.reduce((sum, d) => sum + d.foodRevenue, 0) || 0;
  const totalDiscount = reportData.daily?.reduce((sum, d) => sum + (d.discountAmount || 0), 0) || 0;
  const totalRevenue = reportData.daily?.reduce((sum, d) => sum + d.totalRevenue, 0) || 0;

  if (!isSuperAdmin) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[60vh] text-center">
        <AlertTriangle className="w-12 h-12 text-neon-red mb-4 animate-bounce" />
        <h2 className="text-xl font-heading font-bold text-text mb-2">Access Denied</h2>
        <p className="text-text-2">This reports page is restricted to Super Administrators only.</p>
      </div>
    );
  }

  const handlePrint = () => {
    const originalTitle = document.title;
    const dateStr = new Date().toLocaleString('en-IN', {
      year: 'numeric', month: '2-digit', day: '2-digit',
      hour: '2-digit', minute: '2-digit', second: '2-digit',
      hour12: false
    }).replace(/[\/,\s:]+/g, '-');
    document.title = `Apple_Esports_Report_${dateStr}`;
    window.print();
    document.title = originalTitle;
  };

  return (
    <div className="space-y-8 p-1">
      {/* Header */}
      <div className="flex justify-between items-start">
        <PageHeader
          title="Revenue & Inventory Reports"
          subtitle="Super Admin EOM comparisons, gaming vs. food breakdowns, and reconciliation logs"
          icon="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 002 2h2a2 2 0 002-2"
        />

        {/* Top Actions Panel */}
        <div className="flex flex-col items-end gap-2 print-hidden">
        <div className="flex items-center gap-3 bg-bg-2 border border-border p-2 rounded-xl shadow-lg">
          <div className="flex items-center gap-2">
            <input
              type="date"
              value={startDate}
              onChange={e => setStartDate(e.target.value)}
              className="bg-bg-3 border border-border text-text text-xs rounded-md p-1.5 focus:border-accent outline-none cursor-pointer"
              style={{ colorScheme: 'dark' }}
            />
            <span className="text-text-3 text-xs">to</span>
            <input
              type="date"
              value={endDate}
              onChange={e => setEndDate(e.target.value)}
              className="bg-bg-3 border border-border text-text text-xs rounded-md p-1.5 focus:border-accent outline-none cursor-pointer"
              style={{ colorScheme: 'dark' }}
            />
          </div>
          <button
            onClick={fetchReports}
            className="btn-primary py-1.5 px-3 flex items-center gap-1.5 text-xs font-bold"
          >
            <RefreshCw className="w-3.5 h-3.5" /> Apply
          </button>
        </div>
        <button
            onClick={handlePrint}
            className="btn-secondary py-1.5 px-3 flex items-center gap-1.5 text-xs font-bold"
          >
            <Printer className="w-3.5 h-3.5" /> Download / Print PDF
          </button>
        </div>
      </div>

      {/* Stats Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
        <div className="card bg-bg-2/55 border border-border p-6 rounded-xl flex items-center gap-4 relative overflow-hidden">
          <div className="absolute top-0 right-0 w-24 h-24 bg-neon-blue/5 rounded-full blur-xl" />
          <div className="p-3.5 bg-neon-blue/10 border border-neon-blue/20 text-neon-blue rounded-lg">
            <Gamepad2 className="w-6 h-6" />
          </div>
          <div>
            <div className="text-text-3 text-xs font-bold uppercase tracking-wider">Gaming Revenue</div>
            <div className="text-2xl font-mono font-extrabold text-text mt-1">₹{totalGaming.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</div>
          </div>
        </div>

        <div className="card bg-bg-2/55 border border-border p-6 rounded-xl flex items-center gap-4 relative overflow-hidden">
          <div className="absolute top-0 right-0 w-24 h-24 bg-accent/5 rounded-full blur-xl" />
          <div className="p-3.5 bg-accent/10 border border-accent/20 text-accent rounded-lg">
            <Utensils className="w-6 h-6" />
          </div>
          <div>
            <div className="text-text-3 text-xs font-bold uppercase tracking-wider">Food & Drink Revenue</div>
            <div className="text-2xl font-mono font-extrabold text-text mt-1">₹{totalFood.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</div>
          </div>
        </div>

        <div className="card bg-bg-2/55 border border-border p-6 rounded-xl flex items-center gap-4 relative overflow-hidden">
          <div className="absolute top-0 right-0 w-24 h-24 bg-neon-green/5 rounded-full blur-xl" />
          <div className="p-3.5 bg-neon-green/10 border border-neon-green/20 text-neon-green rounded-lg">
            <DollarSign className="w-6 h-6" />
          </div>
          <div>
            <div className="text-text-3 text-xs font-bold uppercase tracking-wider">Total Combined Revenue</div>
            <div className="text-2xl font-mono font-extrabold text-neon-green mt-1">₹{totalRevenue.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</div>
          </div>
        </div>

        <div className="card bg-bg-2/55 border border-border p-6 rounded-xl flex items-center gap-4 relative overflow-hidden">
          <div className="absolute top-0 right-0 w-24 h-24 bg-neon-orange/5 rounded-full blur-xl" />
          <div className="p-3.5 bg-neon-orange/10 border border-neon-orange/20 text-neon-orange rounded-lg">
            <DollarSign className="w-6 h-6 line-through" />
          </div>
          <div>
            <div className="text-text-3 text-xs font-bold uppercase tracking-wider">Total Discounts</div>
            <div className="text-2xl font-mono font-extrabold text-neon-orange mt-1">₹{totalDiscount.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</div>
          </div>
        </div>
      </div>

      {/* Revenue Split Breakdown Tables */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-8 print:block print:break-before-page">
        
        {/* Daily Revenue Split */}
        <div className="card bg-bg-2 border border-border p-6 rounded-xl flex flex-col min-h-[400px]">
          <h2 className="font-heading font-extrabold text-sm uppercase tracking-wider text-text mb-4 flex items-center gap-2">
            <TrendingUp className="w-4 h-4 text-accent" />
            Daily Revenue Breakdown
          </h2>
          <div className="flex-1 overflow-auto">
            {loading ? (
              <div className="flex justify-center items-center h-full">
                <div className="w-6 h-6 border-2 border-accent border-t-transparent rounded-full animate-spin" />
              </div>
            ) : reportData.daily?.length === 0 ? (
              <div className="flex items-center justify-center h-48 text-text-3 text-xs italic">
                No billing records found in selected range.
              </div>
            ) : (
              <table className="w-full text-left border-collapse text-xs">
                <thead>
                  <tr className="border-b border-border text-text-3 uppercase tracking-wider font-bold text-[10px]">
                    <th className="py-2.5">Date</th>
                    <th className="py-2.5 text-right">Net Gaming</th>
                    <th className="py-2.5 text-right">Net Food & Drink</th>
                    <th className="py-2.5 text-right">Total</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border/40 font-mono">
                  {reportData.daily.map(day => (
                    <tr key={day.date} className="hover:bg-bg-3/40 transition-colors">
                      <td className="py-2 text-text-2">{day.date}</td>
                      <td className="py-2 text-right text-text">₹{day.gamingRevenue.toFixed(2)}</td>
                      <td className="py-2 text-right text-text">₹{day.foodRevenue.toFixed(2)}</td>
                      <td className="py-2 text-right text-neon-green font-bold">₹{day.totalRevenue.toFixed(2)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>

        {/* Monthly Revenue Comparison (EOM) */}
        <div className="card bg-bg-2 border border-border p-6 rounded-xl flex flex-col min-h-[400px] print:break-before-page">
          <h2 className="font-heading font-extrabold text-sm uppercase tracking-wider text-text mb-4 flex items-center gap-2">
            <Calendar className="w-4 h-4 text-neon-blue" />
            Monthly EOM Summary
          </h2>
          <div className="flex-1 overflow-auto">
            {loading ? (
              <div className="flex justify-center items-center h-full">
                <div className="w-6 h-6 border-2 border-accent border-t-transparent rounded-full animate-spin" />
              </div>
            ) : reportData.monthly?.length === 0 ? (
              <div className="flex items-center justify-center h-48 text-text-3 text-xs italic">
                No monthly data aggregated.
              </div>
            ) : (
              <table className="w-full text-left border-collapse text-xs">
                <thead>
                  <tr className="border-b border-border text-text-3 uppercase tracking-wider font-bold text-[10px]">
                    <th className="py-2.5">Month</th>
                    <th className="py-2.5 text-right">Net Gaming</th>
                    <th className="py-2.5 text-right">Net Food & Drink</th>
                    <th className="py-2.5 text-right">Total</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border/40 font-mono">
                  {reportData.monthly.map(month => (
                    <tr key={month.month} className="hover:bg-bg-3/40 transition-colors">
                      <td className="py-2 text-text-2">{month.month}</td>
                      <td className="py-2 text-right text-text">₹{month.gamingRevenue.toFixed(2)}</td>
                      <td className="py-2 text-right text-text">₹{month.foodRevenue.toFixed(2)}</td>
                      <td className="py-2 text-right text-neon-green font-bold">₹{month.totalRevenue.toFixed(2)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>

      </div>

      {/* Discrepancy Logs Table */}
      <div className="card bg-bg-2 border border-border p-6 rounded-xl print:break-before-page">
        <div className="flex justify-between items-center mb-6">
          <h2 className="font-heading font-extrabold text-sm uppercase tracking-wider text-text flex items-center gap-2">
            <AlertTriangle className="w-4.5 h-4.5 text-neon-orange" />
            Inventory Discrepancy Logs
          </h2>
          <button
            onClick={fetchDiscrepancies}
            className="btn-secondary py-1 px-3 text-[11px] font-bold uppercase tracking-wider print-hidden"
          >
            Refresh Logs
          </button>
        </div>

        <div className="overflow-x-auto">
          {discrepancyLoading ? (
            <div className="flex justify-center items-center py-12">
              <div className="w-6 h-6 border-2 border-accent border-t-transparent rounded-full animate-spin" />
            </div>
          ) : discrepancies.length === 0 ? (
            <div className="text-center text-text-3 text-xs italic py-8 border border-dashed border-border rounded-lg">
              No physical count discrepancies logged.
            </div>
          ) : (
            <table className="w-full text-left border-collapse text-xs">
              <thead>
                <tr className="border-b border-border text-text-3 uppercase tracking-wider font-bold text-[10px]">
                  <th className="py-3 px-4">Timestamp</th>
                  <th className="py-3 px-4">Item Name</th>
                  <th className="py-3 px-4 text-right">Expected</th>
                  <th className="py-3 px-4 text-right">Physical Count</th>
                  <th className="py-3 px-4 text-center">Delta / Discrepancy</th>
                  <th className="py-3 px-4">Reason</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border/40 font-mono">
                {discrepancies.map(log => {
                  const delta = log.quantity || 0;
                  const deltaColor = delta > 0 ? 'text-neon-green' : delta < 0 ? 'text-neon-red font-bold' : 'text-text-3';
                  const sign = delta > 0 ? '+' : '';
                  return (
                    <tr key={log.id} className="hover:bg-bg-3/40 transition-colors">
                      <td className="py-3 px-4 text-text-2 flex items-center gap-1">
                        <Clock className="w-3 h-3 text-text-3" />
                        {new Date(log.createdAt).toLocaleString()}
                      </td>
                      <td className="py-3 px-4 text-text font-bold font-heading">{log.itemName}</td>
                      <td className="py-3 px-4 text-right text-text">{log.oldValue}</td>
                      <td className="py-3 px-4 text-right text-text">{log.newValue}</td>
                      <td className={`py-3 px-4 text-center ${deltaColor}`}>
                        {sign}{delta}
                      </td>
                      <td className="py-3 px-4 text-text-3 italic font-sans max-w-[200px] truncate" title={log.reason}>
                        {log.reason || 'None'}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      </div>

      {/* Discount Audit Logs Table */}
      <div className="card bg-bg-2 border border-border p-6 rounded-xl print:break-before-page">
        <h2 className="font-heading font-extrabold text-sm uppercase tracking-wider text-text flex items-center gap-2 mb-6">
          <User className="w-4.5 h-4.5 text-neon-purple" />
          Discount Audit Logs
        </h2>

        <div className="overflow-x-auto">
          {loading ? (
            <div className="flex justify-center items-center py-12">
              <div className="w-6 h-6 border-2 border-accent border-t-transparent rounded-full animate-spin" />
            </div>
          ) : !reportData.discounts || reportData.discounts.length === 0 ? (
            <div className="text-center text-text-3 text-xs italic py-8 border border-dashed border-border rounded-lg">
              No discounts given in the selected date range.
            </div>
          ) : (
            <table className="w-full text-left border-collapse text-xs">
              <thead>
                <tr className="border-b border-border text-text-3 uppercase tracking-wider font-bold text-[10px]">
                  <th className="py-3 px-4">Date/Time</th>
                  <th className="py-3 px-4">Bill Number</th>
                  <th className="py-3 px-4 text-right">Subtotal</th>
                  <th className="py-3 px-4 text-right">Discount</th>
                  <th className="py-3 px-4 text-center">Type</th>
                  <th className="py-3 px-4">Given By</th>
                  <th className="py-3 px-4">Reason</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border/40 font-mono">
                {reportData.discounts.map(discount => (
                  <tr key={discount.billId} className="hover:bg-bg-3/40 transition-colors">
                    <td className="py-3 px-4 text-text-2 flex items-center gap-1">
                      <Clock className="w-3 h-3 text-text-3" />
                      {new Date(discount.date).toLocaleString()}
                    </td>
                    <td className="py-3 px-4 text-text font-bold">{discount.billId}</td>
                    <td className="py-3 px-4 text-right text-text">₹{discount.subtotal.toFixed(2)}</td>
                    <td className="py-3 px-4 text-right text-neon-red font-bold">-₹{discount.discountAmount.toFixed(2)}</td>
                    <td className="py-3 px-4 text-center text-neon-purple">
                      {discount.discountType === 'Percentage' ? `${discount.discountValue}% OFF` : `FLAT ₹${discount.discountValue}`}
                    </td>
                    <td className="py-3 px-4 text-neon-blue font-bold flex items-center gap-1">
                      <User className="w-3 h-3" />
                      {discount.givenBy}
                    </td>
                    <td className="py-3 px-4 text-text-3 italic font-sans truncate max-w-[150px]" title={discount.discountReason}>
                      {discount.discountReason || 'No reason provided'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>

      {/* Complete Billing Audit Logs Table */}
      <div className="card bg-bg-2 border border-border p-6 rounded-xl print:break-before-page">
        <h2 className="font-heading font-extrabold text-sm uppercase tracking-wider text-text flex items-center gap-2 mb-6">
          <Clock className="w-4.5 h-4.5 text-accent" />
          Complete Billing Audit Logs
        </h2>

        <div className="overflow-x-auto">
          {loading ? (
            <div className="flex justify-center items-center py-12">
              <div className="w-6 h-6 border-2 border-accent border-t-transparent rounded-full animate-spin" />
            </div>
          ) : !reportData.allBills || reportData.allBills.length === 0 ? (
            <div className="text-center text-text-3 text-xs italic py-8 border border-dashed border-border rounded-lg">
              No bills found in the selected date range.
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
                </tr>
              </thead>
              <tbody className="divide-y divide-border/40 font-mono">
                {reportData.allBills.map(bill => (
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
                              <span className="text-neon-orange text-[9px] bg-neon-orange/10 px-1.5 py-0.5 rounded border border-neon-orange/20 uppercase tracking-wider font-bold">
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
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>

      {/* ── Credit Audit Logs ── */}
      <div className="card bg-bg-2 border border-border p-6 rounded-xl shadow-lg mt-8 print:break-before-page">
        <div className="flex justify-between items-center mb-6">
          <h2 className="font-heading font-extrabold text-sm uppercase tracking-wider text-text flex items-center gap-2">
            <Clock className="w-4.5 h-4.5 text-accent" />
            Credit Audit Logs
          </h2>
        </div>

        <div className="overflow-x-auto">
          {!reportData.allCredits || reportData.allCredits.length === 0 ? (
            <div className="text-center text-text-3 text-xs italic py-8 border border-dashed border-border rounded-lg">
              No credit records found for the selected date range.
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
                {reportData.allCredits.map(credit => (
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
                    <td className="py-3 px-4 text-right text-neon-orange font-bold">₹{credit.creditAmount.toFixed(2)}</td>
                    <td className="py-3 px-4 text-center">
                      {credit.status.toLowerCase() === 'cleared' ? (
                        <span className="text-neon-green font-bold uppercase tracking-wider text-[10px] bg-neon-green/10 px-2 py-1 rounded border border-neon-green/20">Cleared</span>
                      ) : (
                        <span className="text-neon-orange font-bold uppercase tracking-wider text-[10px] bg-neon-orange/10 px-2 py-1 rounded border border-neon-orange/20">Pending</span>
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

      {/* Cash Reconciliation & Denominations Table */}
      <div className="card bg-bg-2 border border-border p-6 rounded-xl print:break-before-page">
        <div className="flex justify-between items-center mb-6">
          <h2 className="font-heading font-extrabold text-sm uppercase tracking-wider text-text flex items-center gap-2">
            <DollarSign className="w-4.5 h-4.5 text-neon-green" />
            Shift Cash Reconciliation & Denominations
          </h2>
          <button
            onClick={fetchReconciliation}
            className="btn-secondary py-1 px-3 text-[11px] font-bold uppercase tracking-wider print-hidden"
          >
            Refresh Log
          </button>
        </div>

        <div className="overflow-x-auto">
          {reconciliationLoading ? (
            <div className="flex justify-center items-center py-12">
              <div className="w-6 h-6 border-2 border-accent border-t-transparent rounded-full animate-spin" />
            </div>
          ) : reconciliationData.length === 0 ? (
            <div className="text-center text-text-3 text-xs italic py-8 border border-dashed border-border rounded-lg">
              No shifts found in the selected date range.
            </div>
          ) : (
            <table className="w-full text-left border-collapse text-xs whitespace-nowrap">
              <thead>
                <tr className="border-b border-border text-text-3 uppercase tracking-wider font-bold text-[10px]">
                  <th className="py-3 px-4">Shift Date/Time</th>
                  <th className="py-3 px-4">Operator</th>
                  <th className="py-3 px-4 text-right">Expected (Register)</th>
                  <th className="py-3 px-4 text-right">Physical (Cash Desk)</th>
                  <th className="py-3 px-4 text-center">Status</th>
                  <th className="py-3 px-4 text-center">Denomination Breakdown</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border/40 font-mono">
                {reconciliationData.map(report => (
                  <tr key={report.shiftId} className="hover:bg-bg-3/40 transition-colors">
                    <td className="py-3 px-4 text-text-2 flex items-center gap-1">
                      <Clock className="w-3 h-3 text-text-3" />
                      {new Date(report.openedAt).toLocaleString()}
                    </td>
                    <td className="py-3 px-4 text-neon-blue font-bold">{report.operatorName}</td>
                    <td className="py-3 px-4 text-right text-text">₹{report.expectedDrawerCash.toFixed(2)}</td>
                    <td className="py-3 px-4 text-right text-text">₹{report.physicalCashCounted.toFixed(2)}</td>
                    <td className="py-3 px-4 text-center">
                      {report.isVerified ? (
                        <span className="text-neon-green font-bold uppercase tracking-wider text-[10px] bg-neon-green/10 px-2 py-1 rounded border border-neon-green/20">Match</span>
                      ) : (
                        <span className="text-neon-red font-bold uppercase tracking-wider text-[10px] bg-neon-red/10 px-2 py-1 rounded border border-neon-red/20">Mismatch: {report.difference}</span>
                      )}
                    </td>
                    <td className="py-3 px-4 text-center">
                      <div className="flex flex-wrap gap-1 justify-center max-w-[200px]">
                        {report.notes500 > 0 && <span className="bg-bg-3 px-1.5 py-0.5 rounded text-[10px] text-text-2 border border-border/50">500x{report.notes500}</span>}
                        {report.notes200 > 0 && <span className="bg-bg-3 px-1.5 py-0.5 rounded text-[10px] text-text-2 border border-border/50">200x{report.notes200}</span>}
                        {report.notes100 > 0 && <span className="bg-bg-3 px-1.5 py-0.5 rounded text-[10px] text-text-2 border border-border/50">100x{report.notes100}</span>}
                        {report.notes50 > 0 && <span className="bg-bg-3 px-1.5 py-0.5 rounded text-[10px] text-text-2 border border-border/50">50x{report.notes50}</span>}
                        {report.notes20 > 0 && <span className="bg-bg-3 px-1.5 py-0.5 rounded text-[10px] text-text-2 border border-border/50">20x{report.notes20}</span>}
                        {report.notes10 > 0 && <span className="bg-bg-3 px-1.5 py-0.5 rounded text-[10px] text-text-2 border border-border/50">10x{report.notes10}</span>}
                        {(report.coins5 > 0 || report.coins2 > 0 || report.coins1 > 0) && <span className="bg-bg-3 px-1.5 py-0.5 rounded text-[10px] text-text-2 border border-border/50">Coins</span>}
                        {report.notes500 === 0 && report.notes200 === 0 && report.notes100 === 0 && report.notes50 === 0 && report.notes20 === 0 && report.notes10 === 0 && report.coins5 === 0 && report.coins2 === 0 && report.coins1 === 0 && <span className="text-text-3 italic text-[10px]">None</span>}
                      </div>
                      {report.mismatchReason && (
                        <div className="text-[10px] text-neon-red/80 mt-1 italic">
                          Reason: {report.mismatchReason}
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>

    </div>
  );
}
