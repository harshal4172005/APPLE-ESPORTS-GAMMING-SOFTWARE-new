import React, { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../../contexts/AuthContext';
import { useToast } from '../ui/Toast';
import { useNavigate } from 'react-router-dom';
import { ShieldAlert, X, ChevronLeft, UserCircle2 } from 'lucide-react';

export default function AdminSwitchModal({ isOpen, onClose }) {
  const [admins, setAdmins] = useState([]);
  const [selectedAdmin, setSelectedAdmin] = useState(null);
  const [pin, setPin] = useState('');
  const [loading, setLoading] = useState(false);
  
  const { adminSwitchIn, fetchAvailableAdminsForSwitch } = useAuth();
  const toast = useToast();
  const navigate = useNavigate();

  // Fetch available admins when modal opens
  useEffect(() => {
    if (isOpen) {
      setPin('');
      setSelectedAdmin(null);
      setLoading(true);
      fetchAvailableAdminsForSwitch().then(data => {
        setAdmins(data);
        setLoading(false);
      });
    }
  }, [isOpen, fetchAvailableAdminsForSwitch]);

  const handleSubmit = useCallback(async (currentPin) => {
    if (!selectedAdmin) return;
    const submitPin = currentPin || pin;
    
    if (submitPin.length < selectedAdmin.pinLength) {
      toast.error(`PIN must be ${selectedAdmin.pinLength} digits`);
      return;
    }

    setLoading(true);
    try {
      await adminSwitchIn(selectedAdmin.id, submitPin);
      toast.success('Switched to Admin Mode');
      onClose();
      navigate('/app/dashboard');
    } catch (err) {
      toast.error(err.message || 'Invalid PIN or server error');
      setPin('');
    } finally {
      setLoading(false);
    }
  }, [selectedAdmin, pin, adminSwitchIn, onClose, toast]);

  const appendPin = useCallback((num) => {
    if (!selectedAdmin) return;
    
    setPin(p => {
      const newPin = p.length < selectedAdmin.pinLength ? p + num : p;
      // Auto-submit if we hit the exact length
      if (newPin.length === selectedAdmin.pinLength) {
        // Use timeout to allow the final dot to render before freezing UI
        setTimeout(() => handleSubmit(newPin), 50);
      }
      return newPin;
    });
  }, [selectedAdmin, handleSubmit]);

  const removePin = useCallback(() => {
    setPin(p => p.slice(0, -1));
  }, []);

  useEffect(() => {
    if (!isOpen || !selectedAdmin) return;
    
    const handleKeyDown = (e) => {
      if (loading) return;

      if (e.key === 'Escape') {
        setSelectedAdmin(null); // Go back to list
      } else if (e.key === 'Backspace') {
        removePin();
      } else if (e.key === 'Enter') {
        handleSubmit();
      } else if (/^[0-9]$/.test(e.key)) {
        appendPin(e.key);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, selectedAdmin, loading, pin, appendPin, removePin, handleSubmit]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/80 backdrop-blur-sm p-4">
      <div className="bg-bg-2 border border-border rounded-xl w-full max-w-sm shadow-2xl animate-fade-in-up overflow-hidden flex flex-col max-h-[90vh]">
        
        {/* Header */}
        <div className="flex items-center justify-between p-4 border-b border-border shrink-0">
          <div className="flex items-center gap-2">
            {selectedAdmin ? (
              <button 
                onClick={() => { setSelectedAdmin(null); setPin(''); }}
                className="p-1 hover:bg-bg-3 rounded-md text-text-2 hover:text-text transition-colors"
              >
                <ChevronLeft className="w-5 h-5" />
              </button>
            ) : (
              <ShieldAlert className="w-5 h-5 text-accent" />
            )}
            <h3 className="font-heading font-bold tracking-wide text-accent">
              {selectedAdmin ? 'AUTHORIZE SWITCH' : 'ADMIN QUICK-SWITCH'}
            </h3>
          </div>
          <button onClick={onClose} className="text-text-3 hover:text-text p-1">
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Body */}
        <div className="p-6 overflow-y-auto custom-scrollbar">
          {!selectedAdmin ? (
            // --- STEP 1: ADMIN SELECTION ---
            <div className="animate-fade-in">
              <p className="text-xs text-text-2 text-center mb-4">
                Select an available Admin to temporarily take over this station.
              </p>
              
              {loading ? (
                <div className="flex justify-center items-center py-8">
                  <div className="w-6 h-6 border-2 border-accent border-t-transparent rounded-full animate-spin"></div>
                </div>
              ) : admins.length === 0 ? (
                <div className="text-center py-8 text-text-3 text-sm">
                  No active admins found with an Access PIN.
                </div>
              ) : (
                <div className="space-y-2">
                  {admins.map(admin => (
                    <button
                      key={admin.id}
                      onClick={() => setSelectedAdmin(admin)}
                      className="w-full text-left p-3 rounded-lg border border-border bg-bg-3 hover:bg-bg hover:border-accent/50 transition-all flex items-center gap-3 group"
                    >
                      <UserCircle2 className="w-8 h-8 text-text-3 group-hover:text-accent transition-colors" />
                      <div>
                        <div className="font-semibold text-text">{admin.fullName}</div>
                        <div className="text-[10px] text-text-2 uppercase tracking-wider">{admin.type}</div>
                      </div>
                    </button>
                  ))}
                </div>
              )}
            </div>
          ) : (
            // --- STEP 2: PIN ENTRY ---
            <div className="animate-fade-in">
              <p className="text-xs text-text-2 text-center mb-2">
                Enter PIN for <span className="font-semibold text-text">{selectedAdmin.fullName}</span>
              </p>
              
              <div className="flex justify-center mb-8 mt-4">
                <div className="flex gap-3">
                  {Array.from({ length: selectedAdmin.pinLength }).map((_, i) => (
                    <div 
                      key={i} 
                      className={`w-4 h-4 rounded-full border-2 transition-all ${
                        i < pin.length ? 'bg-accent border-accent shadow-[0_0_10px_rgba(220,38,38,0.5)]' : 'bg-bg-3 border-border'
                      }`} 
                    />
                  ))}
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3 mb-2">
                {[1, 2, 3, 4, 5, 6, 7, 8, 9].map(num => (
                  <button
                    key={num}
                    type="button"
                    onClick={() => appendPin(num.toString())}
                    className="aspect-square bg-bg-3 hover:bg-bg border border-border hover:border-accent/50 rounded-lg text-xl font-mono text-text transition-all active:scale-95"
                  >
                    {num}
                  </button>
                ))}
                <button
                  type="button"
                  onClick={() => setPin('')}
                  className="aspect-square bg-bg-3 hover:bg-neon-red/10 border border-border hover:border-neon-red/50 rounded-lg text-xs font-bold text-neon-red transition-all active:scale-95"
                >
                  CLEAR
                </button>
                <button
                  type="button"
                  onClick={() => appendPin('0')}
                  className="aspect-square bg-bg-3 hover:bg-bg border border-border hover:border-accent/50 rounded-lg text-xl font-mono text-text transition-all active:scale-95"
                >
                  0
                </button>
                <button
                  type="button"
                  onClick={removePin}
                  className="aspect-square bg-bg-3 hover:bg-bg border border-border hover:border-accent/50 rounded-lg flex items-center justify-center text-text transition-all active:scale-95"
                >
                  <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2M3 12l6.414 6.414a2 2 0 001.414.586H19a2 2 0 002-2V7a2 2 0 00-2-2h-8.172a2 2 0 00-1.414.586L3 12z" />
                  </svg>
                </button>
                <button
                  type="button"
                  onClick={() => handleSubmit()}
                  className="col-span-3 mt-2 bg-accent hover:bg-accent/90 border border-accent/50 rounded-lg text-sm font-bold text-white transition-all active:scale-95 py-3 shadow-[0_0_10px_rgba(220,38,38,0.3)] uppercase tracking-widest"
                >
                  Confirm Switch
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
