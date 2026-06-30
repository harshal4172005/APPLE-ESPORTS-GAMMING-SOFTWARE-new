import { memo } from 'react';
import { ArrowDownRight, ArrowUpRight, HelpCircle, Gamepad2, Receipt, Monitor, Wallet } from 'lucide-react';
import { formatTime } from '../../utils/timeUtils';

const TX_ICONS = {
  'Cash': Receipt, // from billing
  'Split': Receipt, // from billing
  'cash_sale': Gamepad2, // Sometimes billing sends this? Wait, billing sends enum "Cash" or "Split", which logs as "CashSale"? Let's assume standard names.
  'wallet_topup': Wallet,
  'inward': ArrowDownRight,
  'petty_expense': ArrowUpRight,
  'withdrawal': ArrowUpRight,
  'adjustment': HelpCircle
};

const TransactionFeed = memo(({ transactions }) => {
  // Sorting descending by date to show newest first
  const sorted = [...transactions].sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt));

  return (
    <div className="flex-1 overflow-y-auto space-y-2 pr-2">
      {sorted.length === 0 ? (
        <div className="text-center text-text-3 text-xs italic py-8">
          No transactions in this shift yet.
        </div>
      ) : (
        sorted.map(tx => {
          const isNegative = tx.cashAmount < 0;
          const isBilling = !!tx.billId;
          const Icon = TX_ICONS[tx.transactionType] || Receipt;
          
          return (
            <div 
              key={tx.id} 
              className="flex justify-between items-center p-3 rounded-lg border border-border bg-bg-3 hover:bg-bg-4 transition-colors"
            >
              <div className="flex items-center gap-3">
                <div className={`p-2 rounded-full ${
                  isNegative ? 'bg-neon-orange/10 text-neon-orange' : 'bg-neon-blue/10 text-neon-blue'
                }`}>
                  <Icon className="w-4 h-4" />
                </div>
                <div>
                  <div className="flex items-center gap-2">
                    <span className="font-heading font-bold text-text uppercase tracking-wider text-sm">
                      {tx.transactionType.replace('_', ' ')}
                    </span>
                    {tx.pcNumber && (
                      <span className="bg-bg-2 border border-border text-text-2 text-[9px] px-1.5 py-0.5 rounded font-mono flex items-center gap-1">
                        <Monitor className="w-3 h-3" /> {tx.pcNumber}
                      </span>
                    )}
                  </div>
                  <div className="text-[10px] text-text-3 font-mono mt-0.5">
                    {formatTime(tx.createdAt)}
                  </div>
                </div>
              </div>

              <div className={`font-mono font-bold text-lg ${
                isNegative ? 'text-neon-orange drop-shadow-[0_0_5px_rgba(255,153,0,0.3)]' : 'text-neon-blue drop-shadow-[0_0_5px_rgba(0,255,255,0.3)]'
              }`}>
                {isNegative ? '-' : '+'}₹{Math.abs(tx.cashAmount)}
              </div>
            </div>
          );
        })
      )}
    </div>
  );
});

TransactionFeed.displayName = 'TransactionFeed';
export default TransactionFeed;
