// ═══════════════════════════════════════════════════════════
// Gaming Café ERP — Toast Notification Component
// Apple Esports style: slide-in from bottom-right, neon glow, progress bar
// ═══════════════════════════════════════════════════════════

import { useState, useEffect, useCallback, useRef, createContext, useContext } from 'react';
import { CheckCircle2, XCircle, AlertTriangle, Info, X } from 'lucide-react';

const ToastContext = createContext(null);

export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([]);

  // Keep a ref to the latest addToast so the stable context object never goes stale
  const addToastRef = useRef(null);

  const addToast = useCallback((message, type = 'success', duration = 4000) => {
    const id = Date.now() + Math.random();
    setToasts((prev) => [...prev, { id, message, type, duration }]);
    setTimeout(() => {
      setToasts((prev) => prev.filter((t) => t.id !== id));
    }, duration);
  }, []);

  // Always keep the ref current — no stale closure
  addToastRef.current = addToast;

  const removeToast = useCallback((id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  // Single stable object — created once, never changes reference
  const stableToast = useRef({
    success: (msg) => addToastRef.current(msg, 'success'),
    error:   (msg) => addToastRef.current(msg, 'error', 6000),
    info:    (msg) => addToastRef.current(msg, 'info'),
    warning: (msg) => addToastRef.current(msg, 'warning', 5000),
  });

  return (
    <ToastContext.Provider value={stableToast.current}>
      {children}
      {/* Toast Container */}
      <div className="fixed bottom-4 right-4 z-[9999] flex flex-col gap-2 max-w-sm">
        {toasts.map((t) => (
          <Toast key={t.id} toast={t} onClose={() => removeToast(t.id)} />
        ))}
      </div>
    </ToastContext.Provider>
  );
}

function Toast({ toast, onClose }) {
  const [progress, setProgress] = useState(100);

  useEffect(() => {
    const startTime = Date.now();
    const interval = setInterval(() => {
      const elapsed = Date.now() - startTime;
      const remaining = Math.max(0, 100 - (elapsed / toast.duration) * 100);
      setProgress(remaining);
      if (remaining === 0) clearInterval(interval);
    }, 30);
    return () => clearInterval(interval);
  }, [toast.duration]);

  const configs = {
    success: {
      icon: <CheckCircle2 className="w-4 h-4 text-accent" />,
      border: 'border-l-accent',
      progressBarBg: 'bg-accent',
      title: 'Success',
      glow: 'shadow-[0_0_20px_rgba(220,38,38,0.18)]',
      titleColor: 'text-accent'
    },
    error: {
      icon: <XCircle className="w-4 h-4 text-neon-red" />,
      border: 'border-l-neon-red',
      progressBarBg: 'bg-neon-red',
      title: 'Error',
      glow: 'shadow-[0_0_20px_rgba(255,77,109,0.18)]',
      titleColor: 'text-neon-red'
    },
    info: {
      icon: <Info className="w-4 h-4 text-neon-blue" />,
      border: 'border-l-neon-blue',
      progressBarBg: 'bg-neon-blue',
      title: 'Info',
      glow: 'shadow-[0_0_20px_rgba(77,166,255,0.18)]',
      titleColor: 'text-neon-blue'
    },
    warning: {
      icon: <AlertTriangle className="w-4 h-4 text-neon-orange" />,
      border: 'border-l-neon-orange',
      progressBarBg: 'bg-neon-orange',
      title: 'Warning',
      glow: 'shadow-[0_0_20px_rgba(255,140,66,0.18)]',
      titleColor: 'text-neon-orange'
    }
  };

  const config = configs[toast.type] || configs.success;

  return (
    <div
      className={`relative flex items-start gap-3.5 px-4 py-3 bg-bg-2 border border-border/60 border-l-4 ${config.border} ${config.glow} rounded-sm text-xs max-w-sm w-80 shadow-2xl transition-all duration-300 animate-[slideIn_0.2s_ease] overflow-hidden`}
    >
      <div className="flex-shrink-0 mt-0.5">{config.icon}</div>
      <div className="flex-1 space-y-0.5">
        <div className={`font-heading font-bold uppercase tracking-wider text-[10px] ${config.titleColor}`}>
          {config.title}
        </div>
        <p className="text-text-2 font-body font-medium leading-relaxed break-words text-[11px]">
          {toast.message}
        </p>
      </div>
      <button
        onClick={onClose}
        className="text-text-3 hover:text-text-2 transition-colors flex-shrink-0 mt-0.5"
      >
        <X className="w-3.5 h-3.5" />
      </button>

      {/* Progress Bar */}
      <div 
        className={`absolute bottom-0 left-0 h-[2px] ${config.progressBarBg} opacity-80`} 
        style={{ width: `${progress}%` }}
      />
    </div>
  );
}

export function useToast() {
  const context = useContext(ToastContext);
  if (!context) throw new Error('useToast must be used within ToastProvider');
  return context;
}
