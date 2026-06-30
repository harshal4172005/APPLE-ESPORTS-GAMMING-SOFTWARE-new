import { useState, useEffect, useCallback } from 'react';
import { Wallet, AlertTriangle, ArrowUpRight, ArrowDownRight } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import { useBranch } from '../../contexts/BranchContext';
import api from '../../config/api';
import PageHeader from '../../components/layout/PageHeader';
import { format } from 'date-fns';

export default function WalletDeskPage() {
  const { isSuperAdmin, user } = useAuth();
  const { activeBranch } = useBranch();

  const [data, setData] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  
  const targetBranchId = isSuperAdmin ? activeBranch?.id : user?.branchId;

  const fetchWalletDesk = useCallback(async () => {
    if (isSuperAdmin && !targetBranchId) {
      setData(null);
      setIsLoading(false);
      return;
    }

    try {
      setError(null);
      const response = await api.get('/system-desks/wallet/active', { params: { branchId: targetBranchId } });
      setData(response.data.data);
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to fetch wallet desk summary');
    } finally {
      setIsLoading(false);
    }
  }, [targetBranchId, isSuperAdmin]);

  useEffect(() => {
    setIsLoading(true);
    fetchWalletDesk();
  }, [fetchWalletDesk]);

  if (isSuperAdmin && !activeBranch) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[60vh] text-center">
        <Wallet className="w-12 h-12 text-text-3 mb-4" />
        <h2 className="text-xl font-heading font-bold text-text mb-2">Select a Branch</h2>
        <p className="text-text-2">You must select a branch to access the Wallet Desk.</p>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="flex justify-center items-center min-h-[60vh]">
        <div className="w-8 h-8 rounded-full border-2 border-accent border-t-transparent animate-spin" />
      </div>
    );
  }

  return (
    <div className="h-full flex flex-col max-w-4xl mx-auto">
      <div className="mb-6">
        <PageHeader
          title="Wallet Desk"
          subtitle="Member Wallet Top-ups and Deductions"
          icon="M3 10h18M7 15h1m4 0h1m-7 4h12a3 3 0 003-3V8a3 3 0 00-3-3H6a3 3 0 00-3 3v8a3 3 0 003 3z"
          badge="SYSTEM"
        />
      </div>

      {error && (
        <div className="bg-neon-red/10 border border-neon-red/30 text-neon-red p-3 rounded-xl mb-4 flex items-center gap-2 text-sm">
          <AlertTriangle className="w-4 h-4" /> {error}
        </div>
      )}

      {data && (
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {/* Summary Cards */}
          <div className="col-span-1 space-y-4">
            <div className="border border-neon-blue/30 bg-bg-2 rounded-xl p-6 shadow-[0_0_30px_rgba(0,195,255,0.05)]">
              <h3 className="text-sm font-heading font-bold uppercase tracking-wider text-text-2 mb-4">Total Top-Ups</h3>
              <div className="text-3xl font-mono font-bold text-neon-blue mb-2">
                ₹{data.totalWalletTopUps.toFixed(2)}
              </div>
            </div>
            
            <div className="border border-neon-orange/30 bg-bg-2 rounded-xl p-6 shadow-[0_0_30px_rgba(255,102,0,0.05)]">
              <h3 className="text-sm font-heading font-bold uppercase tracking-wider text-text-2 mb-4">Total Deductions</h3>
              <div className="text-3xl font-mono font-bold text-neon-orange mb-2">
                ₹{data.totalWalletDeductions.toFixed(2)}
              </div>
            </div>
          </div>

          {/* Transactions List */}
          <div className="col-span-1 md:col-span-2 border border-border bg-bg-2 rounded-xl p-6 h-fit max-h-[60vh] flex flex-col">
            <h3 className="text-sm font-heading font-bold uppercase tracking-wider text-text mb-4">Wallet Transactions History</h3>
            {data.transactions.length === 0 ? (
              <div className="flex-1 flex items-center justify-center text-text-3 text-sm py-8">
                No wallet transactions in this shift yet.
              </div>
            ) : (
              <div className="flex-1 overflow-y-auto pr-2 scrollbar-thin">
                <div className="space-y-3">
                  {data.transactions.map((tx) => {
                    const isTopUp = tx.action.includes('Recharge');
                    return (
                      <div key={tx.id} className="flex items-center justify-between p-3 rounded-lg bg-bg-3 border border-border">
                        <div className="flex items-center gap-3">
                          <div className={`w-8 h-8 rounded-full flex items-center justify-center ${isTopUp ? 'bg-neon-blue/10 text-neon-blue' : 'bg-neon-orange/10 text-neon-orange'}`}>
                            {isTopUp ? <ArrowUpRight className="w-4 h-4" /> : <ArrowDownRight className="w-4 h-4" />}
                          </div>
                          <div>
                            <p className="text-sm font-medium text-text">{tx.description}</p>
                            <p className="text-xs text-text-3">{format(new Date(tx.timestamp), 'MMM d, hh:mm a')}</p>
                          </div>
                        </div>
                        <div className={`font-mono font-bold ${isTopUp ? 'text-neon-blue' : 'text-neon-orange'}`}>
                          {isTopUp ? '+' : '-'}₹{tx.amount.toFixed(2)}
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
