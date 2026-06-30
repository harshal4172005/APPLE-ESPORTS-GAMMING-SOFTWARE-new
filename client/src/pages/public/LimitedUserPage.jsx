import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { ArrowLeft, MapPin, Loader2, MonitorPlay, Clock } from 'lucide-react';
import api from '../../config/api';
import { useToast } from '../../components/ui/Toast';

export default function LimitedUserPage() {
  const navigate = useNavigate();
  const toast = useToast();

  // step: 'branch' | 'waiting'
  const [step, setStep] = useState('branch');
  const [branches, setBranches] = useState([]);
  const [selectedBranch, setSelectedBranch] = useState(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    api.get('/auth/branches')
      .then(res => { if (res.data.success) setBranches(res.data.data); })
      .catch(() => toast.error('Failed to load branches'))
      .finally(() => setIsLoading(false));
  }, []);

  const handleSelectBranch = (branch) => {
    setSelectedBranch(branch);
    setStep('waiting');
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-bg flex items-center justify-center">
        <Loader2 className="w-10 h-10 text-accent animate-spin" />
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-bg relative overflow-hidden flex items-center justify-center p-6">
      <div className="absolute inset-0 z-0 bg-[radial-gradient(ellipse_at_center,_var(--tw-gradient-stops))] from-accent/10 via-bg to-bg" />

      <button
        onClick={() => step === 'waiting' ? setStep('branch') : navigate('/user/select')}
        className="absolute top-8 left-8 flex items-center gap-2 text-text-2 hover:text-text transition-colors z-20"
      >
        <ArrowLeft className="w-5 h-5" />
        <span className="font-heading font-semibold text-lg uppercase tracking-wider">Back</span>
      </button>

      <div className="relative z-10 max-w-2xl w-full">
        <AnimatePresence mode="wait">

          {/* STEP 1: Branch Selection */}
          {step === 'branch' && (
            <motion.div
              key="branch"
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -20 }}
            >
              <div className="text-center mb-10">
                <h1 className="font-heading text-4xl font-bold text-text mb-3 tracking-wide uppercase">
                  Which Branch?
                </h1>
                <p className="text-text-2 font-body text-lg">Select the Apple Esports location you're at.</p>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-5">
                {branches.map(branch => (
                  <motion.div
                    key={branch.id}
                    whileHover={{ scale: 1.03, translateY: -4 }}
                    whileTap={{ scale: 0.97 }}
                    onClick={() => handleSelectBranch(branch)}
                    className="card group bg-bg-2/80 backdrop-blur-xl border-border/60 shadow-xl shadow-black/50 p-7 hover:border-accent hover:shadow-[0_0_20px_rgba(220,38,38,0.15)] cursor-pointer transition-all duration-300 flex items-center gap-5"
                  >
                    <div className="w-12 h-12 bg-accent/10 rounded-xl flex items-center justify-center border border-accent/30 group-hover:bg-accent/20 transition-colors shrink-0">
                      <MapPin className="w-6 h-6 text-accent" />
                    </div>
                    <div>
                      <h2 className="font-heading text-2xl font-bold text-text tracking-wide">{branch.name}</h2>
                      {branch.address && (
                        <p className="text-text-3 text-sm font-body mt-1">{branch.address}</p>
                      )}
                    </div>
                  </motion.div>
                ))}

                {branches.length === 0 && (
                  <div className="col-span-2 text-center py-10 text-text-3 font-body">
                    No active branches found. Please contact staff.
                  </div>
                )}
              </div>
            </motion.div>
          )}

          {/* STEP 2: Waiting screen */}
          {step === 'waiting' && (
            <motion.div
              key="waiting"
              initial={{ opacity: 0, scale: 0.95 }}
              animate={{ opacity: 1, scale: 1 }}
              exit={{ opacity: 0 }}
              className="card bg-bg-2/80 backdrop-blur-xl border-accent/20 shadow-xl shadow-black/50 p-10 text-center"
            >
              <div className="w-20 h-20 bg-accent/10 border border-accent/30 rounded-full flex items-center justify-center mx-auto mb-6 shadow-[0_0_20px_rgba(220,38,38,0.2)]">
                <MonitorPlay className="w-10 h-10 text-accent" />
              </div>

              <h1 className="font-heading text-3xl font-bold text-text mb-2 tracking-wide uppercase">
                Welcome to Apple Esports
              </h1>

              <div className="inline-flex items-center gap-2 bg-accent/10 border border-accent/30 px-4 py-1.5 rounded-full mb-6">
                <MapPin className="w-4 h-4 text-accent" />
                <span className="font-heading font-semibold text-accent tracking-wider uppercase text-sm">
                  {selectedBranch?.name}
                </span>
              </div>

              <div className="w-16 h-1 bg-accent mx-auto mb-6 rounded-full" />

              <p className="text-2xl text-text-2 font-body leading-relaxed mb-4">
                Please proceed to the <span className="text-text font-semibold">counter</span> to be assigned a gaming station.
              </p>

              <p className="text-text-3 font-body text-sm leading-relaxed">
                Once the operator assigns your PC and starts your session, your gaming overlay will activate automatically on the assigned PC.
              </p>

              <div className="mt-8 flex items-center justify-center gap-2 text-text-3">
                <Clock className="w-4 h-4" />
                <span className="font-body text-sm">Waiting for counter assignment...</span>
                <span className="flex gap-1 ml-1">
                  <span className="w-1.5 h-1.5 bg-text-3 rounded-full animate-bounce" style={{ animationDelay: '0ms' }} />
                  <span className="w-1.5 h-1.5 bg-text-3 rounded-full animate-bounce" style={{ animationDelay: '150ms' }} />
                  <span className="w-1.5 h-1.5 bg-text-3 rounded-full animate-bounce" style={{ animationDelay: '300ms' }} />
                </span>
              </div>
            </motion.div>
          )}

        </AnimatePresence>
      </div>
    </div>
  );
}
