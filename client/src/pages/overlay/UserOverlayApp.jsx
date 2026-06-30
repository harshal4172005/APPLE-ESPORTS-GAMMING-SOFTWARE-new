import React, { useState } from 'react';
import { useParams, Routes, Route, Navigate, useNavigate } from 'react-router-dom';
import { OverlaySocketProvider } from '../../contexts/OverlaySocketContext';
import OverlayNavBar from './components/OverlayNavBar';
import SessionInfoScreen from './screens/SessionInfoScreen';
import FoodOrderScreen from './screens/FoodOrderScreen';
import TimeExtensionScreen from './screens/TimeExtensionScreen';
import CallOperatorScreen from './screens/CallOperatorScreen';
import CurrentBillScreen from './screens/CurrentBillScreen';
import OverlayMemberLoginScreen from './screens/OverlayMemberLoginScreen';
import MemberTimeSelectionScreen from './screens/MemberTimeSelectionScreen';
import PcLockScreen from './components/PcLockScreen';
import { Minimize2, Maximize2 } from 'lucide-react';
import { useOverlaySocket } from '../../contexts/OverlaySocketContext';
import WalletApprovalModal from './components/WalletApprovalModal';

// Extracted inner content to consume socket context
function OverlayContent({ isMinimized, setIsMinimized }) {
  const { sessionData, sessionLoading } = useOverlaySocket();

  // Show full-screen lock screen if there's no active session or time has run out
  const isTimeUp = sessionData && sessionData.remainingTime !== null && sessionData.remainingTime <= 0;

  if (!sessionLoading && (!sessionData || isTimeUp)) {
    return (
      <>
        <PcLockScreen />
        <WalletApprovalModal />
      </>
    );
  }

  // When minimized, we only show a tiny floating widget that can be expanded
  if (isMinimized) {
    return (
      <>
        <div className="fixed bottom-4 right-4 z-50">
          <button 
            onClick={() => setIsMinimized(false)}
            className="w-12 h-12 bg-bg-2/90 backdrop-blur-xl border border-accent/50 rounded-full flex items-center justify-center text-accent shadow-[0_0_15px_rgba(220,38,38,0.3)] hover:bg-accent/20 transition-all"
          >
            <Maximize2 className="w-5 h-5" />
          </button>
        </div>
        <WalletApprovalModal />
      </>
    );
  }

  // Full overlay mode (400x600 floating widget)
  return (
    <>
      <div className="fixed bottom-4 right-4 w-[400px] h-[600px] flex flex-col bg-bg-2/95 backdrop-blur-xl border border-border/60 rounded-xl shadow-2xl shadow-black/80 z-50 overflow-hidden font-body text-text">
      
      {/* Header Strip */}
      <div className="h-10 bg-black/40 border-b border-border/50 flex items-center justify-between px-4 shrink-0">
        <div className="flex items-center gap-2">
          <div className="w-2 h-2 rounded-full bg-neon-green animate-pulse shadow-[0_0_5px_#22d3a6]" />
          <span className="font-heading font-bold text-sm tracking-widest uppercase text-accent">Apple Esports</span>
        </div>
        <button 
          onClick={() => setIsMinimized(true)}
          className="text-text-3 hover:text-white transition-colors"
        >
          <Minimize2 className="w-4 h-4" />
        </button>
      </div>

      {/* Content Area */}
      <div className="flex-1 overflow-y-auto relative bg-bg/50 scrollbar-thin">
        <Routes>
          <Route path="/" element={<SessionInfoScreen />} />
          <Route path="/login" element={<OverlayMemberLoginScreen />} />
          <Route path="/time-select" element={<MemberTimeSelectionScreen />} />
          <Route path="/food" element={<FoodOrderScreen />} />
          <Route path="/extend" element={<TimeExtensionScreen />} />
          <Route path="/call" element={<CallOperatorScreen />} />
          <Route path="/bill" element={<CurrentBillScreen />} />
          <Route path="*" element={<Navigate to="" replace />} />
        </Routes>
      </div>

      {/* Navigation Bar */}
      <div className="shrink-0 h-16 border-t border-border/50 bg-bg-3/80">
        <OverlayNavBar />
      </div>
    </div>
    <WalletApprovalModal />
    </>
  );
}

export default function UserOverlayApp() {
  const { pcId } = useParams();
  const [isMinimized, setIsMinimized] = useState(false);

  return (
    <OverlaySocketProvider pcId={pcId} isMinimized={isMinimized}>
      <OverlayContent isMinimized={isMinimized} setIsMinimized={setIsMinimized} />
    </OverlaySocketProvider>
  );
}
