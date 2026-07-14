import { motion, AnimatePresence } from 'framer-motion';
import { Clock, Gamepad2, Coffee, Banknote, ShieldAlert } from 'lucide-react';

export default function RecentTransactionsFeed({ transactions }) {
  
  const getIconAndColor = (category, type) => {
    if (category === 'Gaming') return { icon: Gamepad2, color: 'text-neon-purple', bg: 'bg-neon-purple/10 border-neon-purple/20' };
    if (category === 'Food') return { icon: Coffee, color: 'text-neon-orange', bg: 'bg-neon-orange/10 border-neon-orange/20' };
    if (category === 'Financial') return { icon: Banknote, color: 'text-accent', bg: 'bg-accent/10 border-accent/20' };
    return { icon: ShieldAlert, color: 'text-text-2', bg: 'bg-bg-3 border-border' };
  };

  if (!transactions || transactions.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center p-8 text-center bg-bg-2 border border-border rounded-lg h-full min-h-[300px]">
        <Clock className="w-8 h-8 text-text-3 mb-3" />
        <p className="text-text-2 text-sm">No recent activity detected.</p>
        <p className="text-text-3 text-xs mt-1">Transactions will appear here in real-time.</p>
      </div>
    );
  }

  return (
    <div className="bg-bg-2 border border-border rounded-lg overflow-hidden flex flex-col h-full max-h-[600px]">
      <div className="p-4 border-b border-border bg-bg-3/50 flex items-center justify-between">
        <h3 className="font-heading font-bold text-text uppercase tracking-wider text-sm flex items-center gap-2">
          <div className="w-1.5 h-1.5 rounded-full bg-accent animate-blink" />
          Live Activity Feed
        </h3>
        <span className="text-[10px] font-mono text-text-3">AUTO-SYNCING</span>
      </div>
      
      <div className="flex-1 overflow-y-auto p-2 scrollbar-thin space-y-1">
        <AnimatePresence initial={false}>
          {transactions.map((tx, idx) => {
            const { icon: Icon, color, bg } = getIconAndColor(tx.category, tx.type);
            const match = tx.description ? tx.description.match(/^(.*) \((.*)\)$/) : null;
            const mainText = match ? match[1] : tx.description;
            const customerName = match ? match[2] : null;
            
            return (
              <motion.div
                key={tx.id}
                initial={{ opacity: 0, x: -20, height: 0 }}
                animate={{ opacity: 1, x: 0, height: 'auto' }}
                transition={{ duration: 0.3 }}
                className="flex items-start gap-3 p-3 hover:bg-bg-3 rounded-md transition-colors group"
              >
                <div className={`p-2 rounded-md border ${bg} mt-0.5`}>
                  <Icon className={`w-4 h-4 ${color}`} />
                </div>
                
                <div className="flex-1 min-w-0">
                  <div className="flex items-center justify-between gap-2 mb-1">
                    <span className="text-text text-sm font-medium truncate">{tx.type}</span>
                    <span className="text-text-3 text-[10px] font-mono whitespace-nowrap">
                      {new Date(tx.timestamp).toLocaleTimeString([], { hour: '2-digit', minute:'2-digit', second:'2-digit' })}
                    </span>
                  </div>
                  
                  <div className="text-text-2 text-xs leading-relaxed flex items-center gap-2 flex-wrap">
                    <span>{mainText}</span>
                    {customerName && (
                      <span className="px-1.5 py-0.5 rounded text-[9px] font-bold uppercase tracking-wider bg-neon-green/10 text-neon-green border border-neon-green/20">
                        {customerName}
                      </span>
                    )}
                  </div>
                  
                  <div className="flex items-center gap-3 mt-2">
                    {tx.amount != null && (
                      <span className="text-accent text-xs font-mono font-semibold bg-accent/5 px-1.5 py-0.5 rounded border border-accent/10">
                        ₹{tx.amount.toFixed(2)}
                      </span>
                    )}
                    {tx.paymentMethod && (
                      <span className="text-text-3 text-[10px] uppercase tracking-wider bg-bg-4 px-1.5 py-0.5 rounded">
                        {tx.paymentMethod}
                      </span>
                    )}
                    <div className="ml-auto text-[10px] text-text-3 flex items-center gap-1">
                      <span className="w-1 h-1 rounded-full bg-text-3" />
                      {tx.operatorName}
                    </div>
                  </div>
                </div>
              </motion.div>
            );
          })}
        </AnimatePresence>
      </div>
    </div>
  );
}
