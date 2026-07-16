import { useState, useEffect, useCallback } from 'react';
import { motion } from 'framer-motion';
import { MonitorPlay, Users, Wallet, CreditCard, Coffee, Wrench, AlertTriangle, Activity, Banknote, Server } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import { useBranch } from '../../contexts/BranchContext';
import { useSocket } from '../../contexts/SocketContext';
import api from '../../config/api';

import DashboardStatsCard from '../../components/ui/DashboardStatsCard';
import RecentTransactionsFeed from './RecentTransactionsFeed';

export default function MainDashboardPage() {
  const { isSuperAdmin, user } = useAuth();
  const { activeBranch, switchBranch } = useBranch();
  const { subscribe, connected, SIGNALR_HUBS } = useSocket();
  
  const [summary, setSummary] = useState(null);
  const [transactions, setTransactions] = useState([]);
  const [branchSummaries, setBranchSummaries] = useState([]);
  const [systemHealth, setSystemHealth] = useState(null);
  const [systemAlerts, setSystemAlerts] = useState([]);
  const [isLoading, setIsLoading] = useState(true);

  // Global feature error listener
  useEffect(() => {
    const handleSystemError = (e) => {
      const newAlert = e.detail;
      setSystemAlerts(prev => {
        // Prevent duplicate spam if same URL fails repeatedly
        const lastAlert = prev[0];
        if (lastAlert && lastAlert.url === newAlert.url && lastAlert.message === newAlert.message) {
          const updated = [...prev];
          updated[0] = { ...lastAlert, count: (lastAlert.count || 1) + 1, timestamp: newAlert.timestamp };
          return updated;
        }
        // Keep only last 10 alerts
        return [{ ...newAlert, count: 1 }, ...prev].slice(0, 10);
      });
    };

    window.addEventListener('system-error', handleSystemError);
    return () => window.removeEventListener('system-error', handleSystemError);
  }, []);

  // The branch we should fetch data for
  // Operators are locked to their own branch via backend, but we pass it anyway.
  // Super Admins pass activeBranch.id or null for global
  const targetBranchId = isSuperAdmin ? activeBranch?.id : user?.branchId;

  const fetchDashboardData = useCallback(async () => {
    try {
      const params = targetBranchId ? { branchId: targetBranchId } : {};
      
      const promises = [
        api.get('/dashboard/summary', { params }),
        api.get('/dashboard/transactions', { params: { ...params, limit: 15 } })
      ];

      // If global view for Super Admin, fetch branch summaries
      const fetchBranchBreakdown = isSuperAdmin && !targetBranchId;
      if (fetchBranchBreakdown) {
        promises.push(api.get('/dashboard/branches-summary'));
      }

      const results = await Promise.all(promises);
      
      setSummary(results[0].data.data);
      setTransactions(results[1].data.data);
      
      if (fetchBranchBreakdown && results[2]) {
        setBranchSummaries(results[2].data.data);
      } else {
        setBranchSummaries([]);
      }
    } catch (err) {
      console.error("Failed to load dashboard data", err);
    } finally {
      setIsLoading(false);
    }
  }, [targetBranchId, isSuperAdmin]);

  // Initial fetch
  useEffect(() => {
    setIsLoading(true);
    fetchDashboardData();
  }, [fetchDashboardData]);

  // System Health Polling
  useEffect(() => {
    let mounted = true;
    const checkHealth = async () => {
      try {
        const start = Date.now();
        const res = await api.get('/health');
        const latency = Date.now() - start;
        if (mounted) {
          const status = res.data.status || res.data.Status;
          const dbStatus = res.data.database || res.data.Database;
          const dbError = res.data.databaseErrorDetails || res.data.DatabaseErrorDetails;
          
          setSystemHealth({
            status: status === 'Healthy' ? 'Online' : 'Degraded',
            dbStatus: dbStatus === 'Connected' ? 'Online' : 'Offline',
            rawDbStatus: dbStatus,
            errorDetails: dbError,
            latency: latency,
          });
        }
      } catch (err) {
        if (mounted) {
          setSystemHealth({
            status: 'Offline',
            dbStatus: 'Unknown',
            latency: 0,
          });
        }
      }
    };

    checkHealth();
    const interval = setInterval(checkHealth, 15000); // Check every 15s
    return () => {
      mounted = false;
      clearInterval(interval);
    };
  }, []);

  // SignalR Realtime Updates
  useEffect(() => {
    if (!connected) return;

    // Listen to dashboard hub events
    // Assuming backend emits 'DashboardUpdated' and 'ActivityFeedUpdated'
    const unsubSummary = subscribe(SIGNALR_HUBS.DASHBOARD, 'DashboardUpdated', (newSummary) => {
      // Only update if it pertains to our current view (handled by hub groups, but we just replace state)
      setSummary(newSummary);
    });

    const unsubFeed = subscribe(SIGNALR_HUBS.DASHBOARD, 'ActivityFeedUpdated', (newActivities) => {
      // Prepend new activities and maintain max limit
      setTransactions(prev => {
        const merged = [...newActivities, ...prev];
        return merged.slice(0, 50); // Keep max 50 in memory
      });
    });

    return () => {
      unsubSummary();
      unsubFeed();
    };
  }, [connected, subscribe, SIGNALR_HUBS.DASHBOARD]);

  if (isLoading && !summary) {
    return (
      <div className="flex items-center justify-center min-h-[60vh]">
        <div className="w-8 h-8 rounded-full border-2 border-accent border-t-transparent animate-spin" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-end justify-between gap-4 mb-2">
        <div>
          <h1 className="text-2xl font-heading font-bold text-text tracking-wide flex items-center gap-2">
            <Activity className="w-6 h-6 text-accent" />
            OPERATIONAL DASHBOARD
          </h1>
          <p className="text-text-2 text-sm mt-1">
            {isSuperAdmin 
              ? (activeBranch ? `Viewing Branch: ${activeBranch.name}` : 'Viewing Global Aggregates (All Branches)')
              : `Viewing Assigned Branch: ${user?.branchName}`
            }
          </p>
        </div>
      </div>

      {/* KPI Grid - Operational */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <DashboardStatsCard 
          title="Active Sessions" 
          value={summary?.totalActiveSessions || 0} 
          icon={MonitorPlay} 
          colorClass="neon-blue"
          delay={0.1}
        />
        <DashboardStatsCard 
          title="PCs Active / Reserved" 
          value={`${summary?.totalActivePcs || 0} / ${summary?.reservedPcs || 0}`} 
          icon={Users} 
          colorClass="neon-purple"
          delay={0.2}
        />
        <DashboardStatsCard 
          title="Awaiting Billing" 
          value={summary?.awaitingBillingPcs || 0} 
          icon={AlertTriangle} 
          colorClass="neon-orange"
          delay={0.3}
        />
        <DashboardStatsCard 
          title="Active Food Orders" 
          value={summary?.activeFoodOrders || 0} 
          icon={Coffee} 
          colorClass="accent"
          delay={0.4}
        />
      </div>

      {/* Branch Breakdown Table (Super Admin only - Global View) */}
      {isSuperAdmin && !activeBranch && (
        <motion.div 
          initial={{ opacity: 0, y: 15 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.4 }}
          className="bg-bg-2 border border-border rounded-lg overflow-hidden shadow-xl"
        >
          <div className="px-5 py-4 border-b border-border flex items-center justify-between bg-bg-3">
            <div>
              <h2 className="text-base font-heading font-bold text-text tracking-wide flex items-center gap-2">
                <Activity className="w-5 h-5 text-accent" />
                BRANCH COMPARISON DASHBOARD
              </h2>
              <p className="text-text-2 text-xs mt-0.5">Real-time performance and financial metrics across all active branches</p>
            </div>
            <span className="text-[10px] font-mono bg-accent/10 border border-accent/20 text-accent px-2 py-0.5 rounded">
              {branchSummaries.length} BRANCHES
            </span>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-xs">
              <thead>
                <tr className="bg-bg-3/50 text-text-2 font-mono uppercase tracking-wider border-b border-border">
                  <th className="py-3 px-4">Branch</th>
                  <th className="py-3 px-4 text-center">PC Status (Active / Idle / Total)</th>
                  <th className="py-3 px-4">Operators</th>
                  <th className="py-3 px-4 text-right">Gaming Rev</th>
                  <th className="py-3 px-4 text-right">Food Rev</th>
                  <th className="py-3 px-4 text-right">Total Rev</th>
                  <th className="py-3 px-4 text-right">Cash in Drawer</th>
                  <th className="py-3 px-4 text-center">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border/60">
                {branchSummaries.length === 0 ? (
                  <tr>
                    <td colSpan="8" className="py-8 text-center text-text-3 font-mono">
                      No branch data available.
                    </td>
                  </tr>
                ) : (
                  branchSummaries.map((b) => (
                    <tr 
                      key={b.branchId} 
                      className="hover:bg-bg-3/30 transition-colors"
                    >
                      <td className="py-3.5 px-4 font-semibold text-text font-heading text-sm">
                        {b.branchName}
                      </td>
                      <td className="py-3.5 px-4">
                        <div className="flex items-center justify-center gap-2">
                          <span className="flex items-center gap-1 font-semibold text-accent" title="Active PCs">
                            <span className="w-1.5 h-1.5 rounded-full bg-accent inline-block" />
                            {b.activePcs}
                          </span>
                          <span className="text-text-3">/</span>
                          <span className="flex items-center gap-1 text-text-2" title="Idle PCs">
                            <span className="w-1.5 h-1.5 rounded-full bg-text-3 inline-block" />
                            {b.idlePcs}
                          </span>
                          <span className="text-text-3">/</span>
                          <span className="text-text-3 font-mono font-medium" title="Total PCs">
                            {b.totalPcs}
                          </span>
                        </div>
                      </td>
                      <td className="py-3.5 px-4">
                        <div className="flex flex-col gap-1">
                          {b.activeOperator !== "None" ? (
                            <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded bg-accent/5 border border-accent/20 text-accent font-medium text-[11px]">
                              <Users className="w-3 h-3" />
                              {b.activeOperator}
                            </span>
                          ) : (
                            <span className="text-text-3 italic text-[11px]">No shift active</span>
                          )}
                          <span className="text-[10px] font-mono text-text-3">
                            {b.assignedOperatorsCount} assigned
                          </span>
                        </div>
                      </td>
                      <td className="py-3.5 px-4 text-right text-text-2 font-mono">
                        ₹{b.gamingSales.toLocaleString()}
                      </td>
                      <td className="py-3.5 px-4 text-right text-text-2 font-mono">
                        ₹{b.foodSales.toLocaleString()}
                      </td>
                      <td className="py-3.5 px-4 text-right text-accent font-bold font-mono">
                        ₹{b.totalSales.toLocaleString()}
                      </td>
                      <td className="py-3.5 px-4 text-right text-text font-mono">
                        ₹{b.cashInDrawer.toLocaleString()}
                      </td>
                      <td className="py-3.5 px-4 text-center">
                        <button
                          onClick={() => switchBranch(b.branchId)}
                          className="px-2.5 py-1 bg-bg-3 border border-border rounded text-[11px] font-semibold text-accent hover:bg-accent hover:text-bg transition-colors"
                        >
                          Manage Branch
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </motion.div>
      )}

      {/* Financial Overview - Split Row */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        
        {/* Left Side: Financial KPIs & Secondary Metrics */}
        <div className="lg:col-span-2 space-y-6">
          
          {/* Revenue Cards */}
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <DashboardStatsCard 
              title="Today's Revenue" 
              value={`₹${(summary?.totalRevenueToday || 0).toLocaleString()}`} 
              subtitle={`Gaming: ₹${summary?.gamingRevenueToday || 0} • Food: ₹${summary?.foodRevenueToday || 0}`}
              icon={Wallet} 
              colorClass="accent"
              delay={0.5}
            />
            <DashboardStatsCard 
              title="Cash Collection" 
              value={`₹${(summary?.cashTotals || 0).toLocaleString()}`} 
              icon={Banknote} 
              colorClass="neon-blue"
              delay={0.6}
            />
            <DashboardStatsCard 
              title="Online / Wallet" 
              value={`₹${(summary?.onlineTotals + summary?.walletTotals || 0).toLocaleString()}`} 
              icon={CreditCard} 
              colorClass="neon-purple"
              delay={0.7}
            />
          </div>

          {/* Secondary Info */}
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            {/* System Health Widget */}
            <div className="bg-bg-2 border border-border rounded-lg p-5 flex flex-col justify-center shadow-sm">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className={`p-2 rounded border ${systemHealth?.status === 'Online' ? 'bg-emerald-500/10 border-emerald-500/20' : 'bg-red-500/10 border-red-500/20'}`}>
                    <Server className={`w-5 h-5 ${systemHealth?.status === 'Online' ? 'text-emerald-500' : 'text-red-500'}`} />
                  </div>
                  <div>
                    <h4 className="text-sm font-medium text-text">System Health</h4>
                    <div className="flex items-center gap-2 mt-0.5">
                      <span className={`w-1.5 h-1.5 rounded-full ${systemHealth?.status === 'Online' ? 'bg-emerald-500' : 'bg-red-500 animate-pulse'}`} />
                      <span className="text-xs text-text-2">
                        {systemHealth?.status === 'Online' ? `API: ${systemHealth?.latency}ms` : 'System Degraded'}
                      </span>
                    </div>
                  </div>
                </div>
                <div className="text-right">
                  <p className="text-[10px] text-text-3 uppercase tracking-wider mb-0.5">Database</p>
                  <span className={`text-xs font-mono font-bold ${systemHealth?.dbStatus === 'Online' ? 'text-emerald-500' : 'text-red-500'}`}>
                    {systemHealth?.dbStatus || 'Checking...'}
                  </span>
                </div>
              </div>
              
              {/* Detailed Error Box (Only shows if there is an error) */}
              {systemHealth?.errorDetails && (
                <div className="mt-4 pt-4 border-t border-border/50">
                  <p className="text-[10px] text-neon-red uppercase tracking-wider font-bold mb-2 flex items-center gap-1">
                    <AlertTriangle className="w-3 h-3" /> Error Details
                  </p>
                  <div className="bg-bg-3/50 p-2 rounded border border-red-500/20 max-h-32 overflow-y-auto custom-scrollbar">
                    <p className="text-xs text-red-400 font-mono whitespace-pre-wrap break-all leading-tight">
                      {systemHealth.rawDbStatus}
                      <br/>
                      {systemHealth.errorDetails}
                    </p>
                  </div>
                </div>
              )}
            </div>

            <div className="bg-bg-2 border border-border rounded-lg p-5 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="p-2 bg-bg-3 border border-border-2 rounded">
                  <Wrench className="w-5 h-5 text-text-3" />
                </div>
                <div className="min-w-0">
                  <h4 className="text-sm font-medium text-text truncate" title="PCs Under Maintenance">Maintenance PCs</h4>
                  <p className="text-xs text-text-2 truncate" title="Requires technical review">Needs tech review</p>
                </div>
              </div>
              <span className="text-xl font-heading font-bold text-text-2">{summary?.pcsUnderMaintenance || 0}</span>
            </div>

            <div className="bg-bg-2 border border-border rounded-lg p-5 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="p-2 bg-neon-red/10 border border-neon-red/20 rounded">
                  <AlertTriangle className="w-5 h-5 text-neon-red" />
                </div>
                <div>
                  <h4 className="text-sm font-medium text-text">Low Stock Alerts</h4>
                  <p className="text-xs text-text-2">Inventory requiring restock</p>
                </div>
              </div>
              <span className="text-xl font-heading font-bold text-neon-red">{summary?.lowStockAlerts || 0}</span>
            </div>
          </div>

          {/* System Alerts & Feature Flags */}
          {systemAlerts.length > 0 && (
            <motion.div 
              initial={{ opacity: 0, height: 0 }}
              animate={{ opacity: 1, height: 'auto' }}
              className="mt-6 bg-bg-2 border border-neon-red/50 rounded-lg p-5 shadow-[0_0_15px_rgba(255,51,102,0.1)]"
            >
              <div className="flex items-center justify-between mb-4 border-b border-border/50 pb-3">
                <div className="flex items-center gap-2 text-neon-red">
                  <AlertTriangle className="w-5 h-5 animate-pulse" />
                  <h3 className="font-heading font-bold text-sm tracking-wider uppercase">System Alerts & Feature Flags</h3>
                </div>
                <button 
                  onClick={() => setSystemAlerts([])}
                  className="text-[11px] font-bold uppercase tracking-wider text-text-3 hover:text-text transition-colors"
                >
                  Clear All
                </button>
              </div>
              <div className="space-y-2 max-h-48 overflow-y-auto custom-scrollbar pr-2">
                {systemAlerts.map((alert, idx) => (
                  <div key={idx} className="bg-bg-3/50 border border-neon-red/20 rounded p-3 flex flex-col gap-1.5">
                    <div className="flex justify-between items-start gap-4">
                      <div className="flex items-center gap-2 min-w-0">
                        <span className="text-[10px] font-bold px-1.5 py-0.5 rounded bg-neon-red/20 text-neon-red uppercase tracking-wider shrink-0">
                          {alert.method || 'ERR'}
                        </span>
                        <span className="text-xs font-mono text-text-2 truncate" title={alert.url}>
                          {alert.url}
                        </span>
                      </div>
                      <div className="flex items-center gap-2 shrink-0">
                        {alert.count > 1 && (
                          <span className="text-[10px] bg-accent/20 text-accent font-bold px-1.5 py-0.5 rounded">
                            {alert.count}x
                          </span>
                        )}
                        <span className="text-[10px] text-text-3 font-mono">
                          {new Date(alert.timestamp).toLocaleTimeString()}
                        </span>
                      </div>
                    </div>
                    <p className="text-xs text-neon-red font-medium leading-relaxed">
                      {alert.message} {alert.status && `(Status: ${alert.status})`}
                    </p>
                  </div>
                ))}
              </div>
            </motion.div>
          )}
        </div>

        {/* Right Side: Activity Feed */}
        <div className="lg:col-span-1 h-full">
          <RecentTransactionsFeed transactions={transactions} />
        </div>
      </div>
    </div>
  );
}
