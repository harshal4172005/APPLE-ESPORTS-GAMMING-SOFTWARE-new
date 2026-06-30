import React, { useState, useEffect } from 'react';
import { useOverlaySocket } from '../../../contexts/OverlaySocketContext';
import { PhoneCall, Loader2, CheckCircle2 } from 'lucide-react';

export default function CallOperatorScreen() {
  const { callOperator } = useOverlaySocket();
  const [isCalling, setIsCalling] = useState(false);
  const [called, setCalled] = useState(false);
  const [cooldown, setCooldown] = useState(0);

  useEffect(() => {
    let timer;
    if (cooldown > 0) {
      timer = setInterval(() => setCooldown(c => c - 1), 1000);
    }
    return () => clearInterval(timer);
  }, [cooldown]);

  const handleCall = async () => {
    if (cooldown > 0 || isCalling) return;
    
    setIsCalling(true);
    try {
      const res = await callOperator();
      if (res.success) {
        setCalled(true);
        setCooldown(60); // 60 seconds cooldown
        setTimeout(() => setCalled(false), 5000); // show success for 5s
      }
    } catch (err) {
      console.error(err);
    } finally {
      setIsCalling(false);
    }
  };

  if (called) {
    return (
      <div className="p-6 text-center h-full flex flex-col items-center justify-center">
        <div className="w-24 h-24 bg-neon-green/20 rounded-full flex items-center justify-center mb-6 shadow-[0_0_30px_rgba(34,211,166,0.3)]">
          <CheckCircle2 className="w-12 h-12 text-neon-green" />
        </div>
        <h2 className="font-heading text-2xl font-bold text-text tracking-wide uppercase">Operator Notified</h2>
        <p className="text-text-2 font-body mt-2">Someone will be at your desk shortly.</p>
      </div>
    );
  }

  return (
    <div className="p-6 h-full flex flex-col items-center justify-center">
      <div className="text-center mb-12">
        <h2 className="font-heading text-3xl font-bold text-text tracking-wider uppercase">Need Assistance?</h2>
        <p className="text-text-2 font-body mt-2">Press the button below to call an operator to your station.</p>
      </div>

      <button
        onClick={handleCall}
        disabled={cooldown > 0 || isCalling}
        className={`
          relative w-48 h-48 rounded-full flex flex-col items-center justify-center transition-all duration-300
          ${cooldown > 0 
            ? 'bg-bg-3 border-2 border-border opacity-50 cursor-not-allowed' 
            : 'bg-accent/10 border-2 border-accent shadow-[0_0_50px_rgba(220,38,38,0.3)] hover:scale-105 active:scale-95 hover:bg-accent/20'
          }
        `}
      >
        {isCalling ? (
          <Loader2 className="w-16 h-16 text-accent animate-spin" />
        ) : (
          <>
            <PhoneCall className={`w-16 h-16 mb-2 ${cooldown > 0 ? 'text-text-3' : 'text-accent animate-pulse'}`} />
            <span className={`font-heading font-bold uppercase tracking-wider text-lg ${cooldown > 0 ? 'text-text-3' : 'text-accent'}`}>
              {cooldown > 0 ? `${cooldown}s` : 'Call Operator'}
            </span>
          </>
        )}
        
        {/* Ripple effects for active state */}
        {cooldown === 0 && !isCalling && (
          <>
            <div className="absolute inset-0 rounded-full border border-accent/50 animate-ping opacity-20" style={{ animationDuration: '2s' }} />
            <div className="absolute inset-[-20px] rounded-full border border-accent/30 animate-ping opacity-10" style={{ animationDuration: '3s', animationDelay: '1s' }} />
          </>
        )}
      </button>

      {cooldown > 0 && (
        <p className="mt-8 text-text-3 font-body text-sm text-center">
          Please wait {cooldown} seconds before calling again.
        </p>
      )}
    </div>
  );
}
