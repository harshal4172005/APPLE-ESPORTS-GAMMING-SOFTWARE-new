import { useState, useEffect, useCallback } from 'react';
import { Globe, AlertTriangle, ArrowRightLeft } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import { useBranch } from '../../contexts/BranchContext';
import api from '../../config/api';
import PageHeader from '../../components/layout/PageHeader';
import { format } from 'date-fns';

export default function OnlineDeskPage() {
  const { isSuperAdmin, user } = useAuth();
  const { activeBranch } = useBranch();

  const [data, setData] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  
  const targetBranchId = isSuperAdmin ? activeBranch?.id : user?.branchId;

  const fetchOnlineDesk = useCallback(async () => {
    if (isSuperAdmin && !targetBranchId) {
      setData(null);
      setIsLoading(false);
      return;
    }

    try {
      setError(null);
      const response = await api.get('/system-desks/online/active', { params: { branchId: targetBranchId } });
      setData(response.data.data);
    } catch (err) {
      setError(err.response?.data?.message || 'Failed to fetch online desk summary');
    } finally {
      setIsLoading(false);
    }
  }, [targetBranchId, isSuperAdmin]);

  useEffect(() => {
    setIsLoading(true);
    fetchOnlineDesk();
  }, [fetchOnlineDesk]);

  if (isSuperAdmin && !activeBranch) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[60vh] text-center">
        <Globe className="w-12 h-12 text-text-3 mb-4" />
        <h2 className="text-xl font-heading font-bold text-text mb-2">Select a Branch</h2>
        <p className="text-text-2">You must select a branch to access the Online Desk.</p>
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
          title="Online Desk"
          subtitle="Real-time Online Payment Tracking"
          icon="M21 12a9 9 0 01-9 9m9-9a9 9 0 00-9-9m9 9H3m9 9a9 9 0 01-9-9m9 9c1.657 0 3-4.03 3-9s-1.343-9-3-9m0 18c-1.657 0-3-4.03-3-9s1.343-9 3-9m-9 9a9 9 0 019-9"
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
          {/* Summary Card */}
          <div className="col-span-1 border border-accent/30 bg-bg-2 rounded-xl p-6 shadow-[0_0_30px_rgba(255,51,102,0.05)] h-fit">
            <h3 className="text-sm font-heading font-bold uppercase tracking-wider text-text-2 mb-4">Total Online Collected</h3>
            <div className="text-4xl font-mono font-bold text-accent mb-2">
              ₹{data.totalOnlineSales.toFixed(2)}
            </div>
            <p className="text-xs text-text-3">System calculated from active shift transactions</p>
          </div>

          {/* Transactions List */}
          <div className="col-span-1 md:col-span-2 border border-border bg-bg-2 rounded-xl p-6 h-fit max-h-[60vh] flex flex-col">
            <h3 className="text-sm font-heading font-bold uppercase tracking-wider text-text mb-4">Recent Online Transactions</h3>
            {data.transactions.length === 0 ? (
              <div className="flex-1 flex items-center justify-center text-text-3 text-sm py-8">
                No online transactions in this shift yet.
              </div>
            ) : (
              <div className="flex-1 overflow-y-auto pr-2 scrollbar-thin">
                <div className="space-y-3">
                  {data.transactions.map((tx) => (
                    <div key={tx.id} className="flex items-center justify-between p-3 rounded-lg bg-bg-3 border border-border">
                      <div className="flex items-center gap-3">
                        <div className="w-8 h-8 rounded-full bg-accent/10 flex items-center justify-center text-accent">
                          <ArrowRightLeft className="w-4 h-4" />
                        </div>
                        <div>
                          <p className="text-sm font-medium text-text">{tx.description}</p>
                          <p className="text-xs text-text-3">{format(new Date(tx.timestamp), 'MMM d, hh:mm a')}</p>
                        </div>
                      </div>
                      <div className="font-mono font-bold text-accent">
                        +₹{tx.amount.toFixed(2)}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
