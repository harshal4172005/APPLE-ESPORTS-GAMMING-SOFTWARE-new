import React from 'react';
import { useOverlaySocket } from '../../../contexts/OverlaySocketContext';
import { Receipt, Coffee, MonitorPlay } from 'lucide-react';

export default function CurrentBillScreen() {
  const { sessionData } = useOverlaySocket();

  if (!sessionData) {
    return (
      <div className="p-6 text-center h-full flex flex-col items-center justify-center">
        <Receipt className="w-16 h-16 text-text-3 mb-4 opacity-50" />
        <h2 className="font-heading text-xl font-bold text-text-2 tracking-wide uppercase">No Active Bill</h2>
      </div>
    );
  }

  const { gamingCharges, foodCharges, foodItems = [], totalBill, sessionStatus } = sessionData;

  return (
    <div className="flex flex-col h-full bg-bg">
      {/* Header */}
      <div className="shrink-0 p-6 bg-bg-3 border-b border-border shadow-sm flex items-center justify-between">
        <div>
          <h2 className="font-heading text-2xl font-bold text-text tracking-wider uppercase flex items-center gap-2">
            <Receipt className="w-6 h-6 text-accent" />
            Current Bill
          </h2>
          {sessionStatus === 'awaiting_billing' && (
            <span className="text-neon-orange text-xs font-bold uppercase tracking-wider mt-1 block">Awaiting Payment</span>
          )}
        </div>
        <div className="text-right">
          <p className="text-text-3 text-xs font-heading uppercase tracking-widest">Total Amount</p>
          <p className="font-mono text-3xl font-bold text-accent">₹{totalBill.toFixed(2)}</p>
        </div>
      </div>

      {/* Bill Breakdown */}
      <div className="flex-1 overflow-y-auto p-6 space-y-6 scrollbar-thin">
        
        {/* Gaming Charges */}
        <div className="bg-bg-2 rounded-xl border border-border/50 p-4">
          <div className="flex items-center justify-between mb-4 pb-3 border-b border-border/30">
            <div className="flex items-center gap-2 text-text-2">
              <MonitorPlay className="w-4 h-4" />
              <span className="font-heading uppercase tracking-wider font-bold">Gaming Session</span>
            </div>
            <span className="font-mono text-lg font-bold text-text">₹{gamingCharges.toFixed(2)}</span>
          </div>
          <p className="text-xs text-text-3 font-body">Calculated based on active time. {sessionStatus === 'awaiting_billing' && '(Frozen)'}</p>
        </div>

        {/* Food Charges */}
        <div className="bg-bg-2 rounded-xl border border-border/50 p-4">
          <div className="flex items-center justify-between mb-4 pb-3 border-b border-border/30">
            <div className="flex items-center gap-2 text-text-2">
              <Coffee className="w-4 h-4" />
              <span className="font-heading uppercase tracking-wider font-bold">Food & Drinks</span>
            </div>
            <span className="font-mono text-lg font-bold text-text">₹{foodCharges.toFixed(2)}</span>
          </div>
          
          {foodItems.length === 0 ? (
            <p className="text-xs text-text-3 font-body italic">No food orders placed yet.</p>
          ) : (
            <div className="space-y-3">
              {foodItems.map((item, idx) => {
                const qty = item.quantity || item.qty || 1;
                const name = item.itemName || item.name || 'Unknown Item';
                const price = item.unitPrice || item.price || 0;
                return (
                  <div key={idx} className="flex items-center justify-between text-sm">
                    <div className="flex items-center gap-2">
                      <span className="text-text-3 font-mono">{qty}x</span>
                      <span className="text-text-2 font-body">{name}</span>
                    </div>
                    <span className="text-text font-mono font-semibold">₹{(price * qty).toFixed(2)}</span>
                  </div>
                );
              })}
            </div>
          )}
        </div>

      </div>

      {/* Footer Info */}
      <div className="shrink-0 p-4 bg-bg-3 border-t border-border/50 text-center">
        <p className="text-xs text-text-3 font-body">
          * This bill is synchronized live with the counter. You cannot make payments from this panel. Please proceed to the billing desk when finished.
        </p>
      </div>
    </div>
  );
}
