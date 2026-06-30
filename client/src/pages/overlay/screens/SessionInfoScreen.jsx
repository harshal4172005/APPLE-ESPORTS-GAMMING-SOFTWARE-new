import React, { useState, useEffect } from 'react';
import { useOverlaySocket } from '../../../contexts/OverlaySocketContext';
import { MonitorPlay, Clock, IndianRupee, User, AlertTriangle } from 'lucide-react';
import { format } from 'date-fns';

export default function SessionInfoScreen() {
  const { sessionData, pcId, connectionStatus } = useOverlaySocket();
  const [now, setNow] = useState(Date.now());

  useEffect(() => {
    if (!sessionData) return;
    const interval = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(interval);
  }, [sessionData]);

  const formatTime = (seconds) => {
    if (seconds <= 0) return '00:00:00';
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = Math.floor(seconds % 60);
    return `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  };

  if (!sessionData) {
    return (
      <div className="flex flex-col items-center justify-center h-full p-6 text-center">
        <MonitorPlay className="w-16 h-16 text-text-3 mb-4 opacity-50" />
        <h2 className="font-heading text-2xl font-bold text-text-2 tracking-wide uppercase">No Active Session</h2>
        <p className="text-text-3 font-body mt-2">Please see the counter to start a session on {pcId}.</p>
        
        <div className="mt-8">
          <p className="text-text-3 text-sm mb-3">Or login to start session automatically</p>
          <button 
            onClick={() => window.location.href = `/pc-overlay/${pcId}/login`}
            className="flex items-center gap-2 bg-accent/10 hover:bg-accent/20 text-accent border border-accent/30 py-2 px-6 rounded transition-colors"
          >
            <User className="w-4 h-4" />
            <span className="font-heading tracking-wider uppercase font-bold text-sm">Member Login</span>
          </button>
        </div>

        {connectionStatus === 'disconnected' && (
          <div className="mt-8 bg-neon-orange/10 border border-neon-orange/30 p-3 rounded-md flex items-center gap-2">
            <AlertTriangle className="w-4 h-4 text-neon-orange" />
            <span className="text-neon-orange text-sm font-body">Disconnected from server</span>
          </div>
        )}
      </div>
    );
  }

  const isPayAsYouGo = !sessionData.plannedDurationMin || sessionData.plannedDurationMin === 0;
  
  let displayTime = '';
  let timeLabel = '';
  let isLowTime = false;
  let liveBill = sessionData.totalBill || 0;

  if (isPayAsYouGo) {
    const elapsedSeconds = Math.max(0, Math.floor((now - new Date(sessionData.sessionStart).getTime()) / 1000));
    displayTime = formatTime(elapsedSeconds);
    timeLabel = 'Elapsed Time';
    isLowTime = false;
    
    // Calculate live bill for Pay As You Go (100 per hour default if ratePerHour not specified)
    const elapsedMin = elapsedSeconds / 60;
    const ratePerHour = sessionData.ratePerHour || 100;
    const hours = Math.max(elapsedMin / 60, 1 / 60);
    const liveGamingCharge = Number((hours * ratePerHour).toFixed(2));
    liveBill = (sessionData.foodCharges || 0) + liveGamingCharge;
  } else {
    // Dynamically calculate remaining time based on start time and planned duration
    const expectedEndTimeMs = new Date(sessionData.sessionStart).getTime() + (sessionData.plannedDurationMin * 60 * 1000);
    const remainingSeconds = Math.max(0, Math.floor((expectedEndTimeMs - now) / 1000));
    
    displayTime = formatTime(remainingSeconds);
    timeLabel = 'Remaining Time';
    isLowTime = remainingSeconds < 900;
    liveBill = sessionData.totalBill;
  }

  return (
    <div className="p-6 h-full flex flex-col">
      {/* Session Status Header */}
      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="font-heading text-3xl font-bold text-text tracking-wider uppercase">
            {sessionData.pcName || pcId}
          </h1>
          <div className="flex items-center gap-2 mt-1">
            <span className="w-2 h-2 rounded-full bg-neon-green shadow-[0_0_5px_#22d3a6]" />
            <span className="text-neon-green font-body text-sm uppercase tracking-wide font-bold">Session Active</span>
          </div>
        </div>
        <div className="bg-bg-3 p-3 rounded-xl border border-border shadow-inner">
          <User className="w-6 h-6 text-accent" />
        </div>
      </div>

      {/* Main Info Grid */}
      <div className="grid grid-cols-2 gap-4 mb-6">
        
        {/* Time Display */}
        <div className={`col-span-2 p-6 rounded-xl border relative overflow-hidden ${isLowTime ? 'bg-neon-orange/10 border-neon-orange/50 shadow-[0_0_15px_rgba(255,165,0,0.2)]' : 'bg-bg-3 border-border shadow-inner'}`}>
          <div className="flex items-center justify-between mb-2 relative z-10">
            <span className="text-text-2 font-heading tracking-widest uppercase text-sm font-bold">{timeLabel}</span>
            <Clock className={`w-5 h-5 ${isLowTime ? 'text-neon-orange' : 'text-accent'}`} />
          </div>
          <div className={`font-mono text-5xl font-bold relative z-10 ${isLowTime ? 'text-neon-orange' : 'text-text'}`}>
            {displayTime}
          </div>
          
          {/* Progress bar background indicator */}
          <div className="absolute bottom-0 left-0 h-1 bg-accent/20 w-full" />
        </div>

        {/* Current Bill */}
        <div className="p-4 rounded-xl border border-border bg-bg-3 shadow-inner">
          <div className="flex items-center justify-between mb-2">
            <span className="text-text-3 font-heading tracking-widest uppercase text-xs font-bold">Current Bill</span>
            <IndianRupee className="w-4 h-4 text-text-2" />
          </div>
          <div className="font-mono text-2xl font-bold text-text">
            ₹{liveBill.toFixed(2)}
          </div>
        </div>

        {/* Start Time */}
        <div className="p-4 rounded-xl border border-border bg-bg-3 shadow-inner">
          <div className="flex items-center justify-between mb-2">
            <span className="text-text-3 font-heading tracking-widest uppercase text-xs font-bold">Started At</span>
            <Clock className="w-4 h-4 text-text-2" />
          </div>
          <div className="font-mono text-lg font-bold text-text-2">
            {format(new Date(sessionData.sessionStart), 'hh:mm a')}
          </div>
        </div>
      </div>

      <div className="mt-auto">
        <div className="bg-bg-3 border border-border rounded-xl p-4 flex items-center justify-between shadow-inner">
          <div>
            <p className="text-text-3 text-xs font-heading tracking-widest uppercase mb-1">Customer</p>
            <p className="text-text font-body font-bold">{sessionData.customerName}</p>
          </div>
          {sessionData.memberLinked && (
            <span className="px-2 py-1 bg-accent/20 text-accent border border-accent/30 rounded text-xs font-bold uppercase tracking-wider">
              Member
            </span>
          )}
        </div>
      </div>
      
    </div>
  );
}
