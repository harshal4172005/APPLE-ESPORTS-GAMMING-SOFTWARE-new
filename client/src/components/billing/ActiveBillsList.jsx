import { Monitor, CreditCard, Clock } from 'lucide-react';
import { formatTimeDelta } from '../../utils/timeUtils';
import { useEffect, useState } from 'react';

export default function ActiveBillsList({ bills, activeSessions, reservations = [], selectedId, onSelect }) {
  // A local component timer for active sessions to tick visually
  const [now, setNow] = useState(Date.now());
  useEffect(() => {
    const interval = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(interval);
  }, []);

  const activeSessionIds = new Set(activeSessions.map(s => s.id));
  const unpaidFinalizedBills = bills.filter(b => !b.sessionId || !activeSessionIds.has(b.sessionId));

  return (
    <div className="flex flex-col h-full space-y-6">
      
      {/* SECTION 1: Finalized Unpaid Bills */}
      <div>
        <div className="flex items-center gap-2 mb-3 px-1">
          <CreditCard className="w-4 h-4 text-neon-orange" />
          <h3 className="font-heading font-bold text-text uppercase tracking-wider text-sm">Finalized / Unpaid</h3>
          <span className="bg-neon-orange/20 text-neon-orange text-[10px] px-2 py-0.5 rounded-full font-bold ml-auto">
            {unpaidFinalizedBills.length}
          </span>
        </div>
        
        {unpaidFinalizedBills.length === 0 ? (
          <div className="p-4 bg-bg-2 border border-border rounded-xl text-center text-text-3 text-xs italic">
            No pending bills.
          </div>
        ) : (
          <div className="space-y-2">
            {unpaidFinalizedBills.map(bill => (
              <button
                key={bill.id}
                onClick={() => onSelect({ type: 'bill', id: bill.id })}
                className={`w-full text-left p-3 rounded-xl border transition-all ${
                  selectedId === bill.id 
                    ? 'bg-neon-orange/10 border-neon-orange/50 shadow-[0_0_15px_rgba(255,153,0,0.1)]' 
                    : 'bg-bg-3 border-border hover:border-neon-orange/30'
                }`}
              >
                <div className="flex justify-between items-center mb-1.5">
                  <span className="font-heading font-bold text-text">{bill.pcNumber || 'Walk-in'}</span>
                  <span className="font-mono text-neon-orange font-bold">₹{bill.totalAmount}</span>
                </div>
                <div className="flex justify-between items-center text-[10px] text-text-3 uppercase tracking-wider">
                  <span className="truncate max-w-[120px]">{bill.customerName || 'Guest'}</span>
                  <span>{bill.billNumber}</span>
                </div>
              </button>
            ))}
          </div>
        )}
      </div>

      {/* SECTION 2: Active Sessions (Prepaid/Ongoing) */}
      <div>
        <div className="flex items-center gap-2 mb-3 px-1 mt-4 border-t border-border pt-4">
          <Monitor className="w-4 h-4 text-neon-blue" />
          <h3 className="font-heading font-bold text-text uppercase tracking-wider text-sm">Active Sessions</h3>
          <span className="bg-neon-blue/20 text-neon-blue text-[10px] px-2 py-0.5 rounded-full font-bold ml-auto">
            {activeSessions.length}
          </span>
        </div>

        {activeSessions.length === 0 ? (
          <div className="p-4 bg-bg-2 border border-border rounded-xl text-center text-text-3 text-xs italic">
            No active sessions.
          </div>
        ) : (
          <div className="grid grid-cols-2 gap-2">
            {activeSessions.map(session => {
              // Calculate Time Left or Elapsed
              let timeStr = '';
              let isOvertime = false;
              if (session.endTime) {
                const diff = new Date(session.endTime).getTime() - now;
                isOvertime = diff < 0;
                timeStr = formatTimeDelta(Math.abs(diff));
              } else {
                const elapsed = now - new Date(session.startTime).getTime();
                timeStr = formatTimeDelta(elapsed);
              }

              return (
                <button
                  key={session.id}
                  onClick={() => onSelect({ type: 'session', id: session.billId })} // We fetch the bill attached to the session
                  className={`text-left p-3 rounded-xl border transition-all ${
                    selectedId === session.billId 
                      ? 'bg-neon-blue/10 border-neon-blue/50 shadow-[0_0_15px_rgba(0,255,255,0.1)]' 
                      : 'bg-bg-3 border-border hover:border-neon-blue/30'
                  }`}
                >
                  <div className="flex justify-between items-center mb-1">
                    <span className="font-heading font-bold text-text">{session.pcName}</span>
                    {isOvertime ? (
                      <span className="w-2 h-2 rounded-full bg-neon-red animate-pulse" />
                    ) : (
                      <span className="w-2 h-2 rounded-full bg-neon-blue" />
                    )}
                  </div>
                  <div className="font-mono text-xs mb-1 text-text-2">
                    {timeStr}
                  </div>
                  <div className="text-[9px] text-text-3 uppercase tracking-wider truncate">
                    {session.customerName || 'Guest'}
                  </div>
                </button>
              );
            })}
          </div>
        )}
      </div>

      {/* SECTION 3: Upcoming Reservations */}
      <div>
        <div className="flex items-center gap-2 mb-3 px-1 mt-4 border-t border-border pt-4">
          <Clock className="w-4 h-4 text-neon-purple" />
          <h3 className="font-heading font-bold text-text uppercase tracking-wider text-sm">Upcoming Reservations</h3>
          <span className="bg-neon-purple/20 text-neon-purple text-[10px] px-2 py-0.5 rounded-full font-bold ml-auto">
            {reservations.filter(r => r.state === 'Pending').length}
          </span>
        </div>

        {reservations.filter(r => r.state === 'Pending').length === 0 ? (
          <div className="p-4 bg-bg-2 border border-border rounded-xl text-center text-text-3 text-xs italic">
            No upcoming reservations.
          </div>
        ) : (
          <div className="grid grid-cols-2 gap-2">
            {reservations
              .filter(r => r.state === 'Pending')
              .map(res => {
                const resTime = new Date(res.reservationTime).getTime();
                const diffMs = resTime - now;
                let timerText = '';
                
                if (diffMs > 0) {
                  const totalMin = Math.floor(diffMs / 60000);
                  if (totalMin < 60) {
                    timerText = `Starts in ${totalMin}m`;
                  } else {
                    const hrs = Math.floor(totalMin / 60);
                    const mins = totalMin % 60;
                    timerText = `Starts in ${hrs}h ${mins}m`;
                  }
                } else {
                  const graceEnd = resTime + (res.gracePeriodMin || 15) * 60000;
                  const remainingMs = graceEnd - now;
                  if (remainingMs > 0) {
                    const remainingMin = Math.ceil(remainingMs / 60000);
                    timerText = `Grace: ${remainingMin}m left`;
                  } else {
                    timerText = 'Expired';
                  }
                }

                return (
                  <div
                    key={res.id}
                    className="p-3 rounded-xl border bg-bg-3 border-border flex flex-col justify-between"
                  >
                    <div className="flex justify-between items-center mb-1">
                      <span className="font-heading font-bold text-text truncate max-w-[80px]">
                        {res.pcName || 'PC'}
                      </span>
                      <span className="w-1.5 h-1.5 rounded-full bg-neon-purple animate-pulse" />
                    </div>
                    <div className="font-mono text-xs mb-1 text-neon-purple font-semibold">
                      {timerText}
                    </div>
                    <div className="text-[9px] text-text-3 uppercase tracking-wider truncate">
                      {res.customerName}
                    </div>
                  </div>
                );
              })}
          </div>
        )}
      </div>

    </div>
  );
}
