import React from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { UserPlus, UserCheck, ArrowLeft } from 'lucide-react';

export default function UserFlowSelectionPage() {
  const navigate = useNavigate();

  return (
    <div className="min-h-screen bg-bg flex items-center justify-center p-6 relative overflow-hidden">
      {/* Background glow effects - matching LandingGatewayPage */}
      <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] bg-accent/20 rounded-full blur-[120px] pointer-events-none" />

      <div className="relative z-10 max-w-4xl w-full">
        <button 
          onClick={() => navigate('/')}
          className="absolute -top-16 left-0 flex items-center gap-2 text-text-2 hover:text-accent transition-colors group"
        >
          <ArrowLeft className="w-5 h-5 group-hover:-translate-x-1 transition-transform" />
          <span className="font-heading font-semibold text-lg uppercase tracking-wider">Back</span>
        </button>

        <div className="text-center mb-16">
          <motion.img 
            initial={{ scale: 0.9 }}
            animate={{ scale: 1 }}
            transition={{ duration: 0.5 }}
            src="/logo.png" 
            alt="Apple Esports" 
            className="h-20 w-auto mx-auto mb-6 drop-shadow-[0_0_15px_rgba(220,38,38,0.5)]"
          />
          <motion.h1 
            initial={{ opacity: 0, y: -20 }}
            animate={{ opacity: 1, y: 0 }}
            className="font-heading text-4xl md:text-5xl font-bold tracking-wide text-text mb-2"
          >
            CHOOSE USER TYPE
          </motion.h1>
          <motion.p 
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ delay: 0.2 }}
            className="text-accent text-sm font-mono tracking-[0.2em] uppercase mb-8"
          >
            Select how you want to play
          </motion.p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-8 max-w-3xl mx-auto">
          <motion.div
            initial={{ opacity: 0, x: -30 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ delay: 0.1 }}
            whileHover={{ scale: 1.03, translateY: -5 }}
            whileTap={{ scale: 0.95 }}
            onClick={() => navigate('/user/limited')}
            className="card group relative flex flex-col items-center text-center p-10 bg-bg-2/80 backdrop-blur-xl border-border/60 shadow-xl shadow-black/50 hover:border-accent hover:shadow-[0_0_20px_rgba(220,38,38,0.15)] cursor-pointer transition-all duration-300"
          >
            <UserPlus className="w-12 h-12 text-accent mb-6 group-hover:scale-110 transition-transform" />
            <h2 className="font-heading text-2xl font-bold text-text mb-3 tracking-wider">WALK-IN USER</h2>
            <p className="text-text-2 text-sm leading-relaxed max-w-[250px]">Play as a guest. Proceed to the counter for PC assignment and billing.</p>
          </motion.div>

          <motion.div
            initial={{ opacity: 0, x: 30 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ delay: 0.2 }}
            whileHover={{ scale: 1.03, translateY: -5 }}
            whileTap={{ scale: 0.95 }}
            onClick={() => navigate('/user/member-login')}
            className="card group relative flex flex-col items-center text-center p-10 bg-bg-2/80 backdrop-blur-xl border-border/60 shadow-xl shadow-black/50 hover:border-accent hover:shadow-[0_0_20px_rgba(220,38,38,0.15)] cursor-pointer transition-all duration-300"
          >
            <UserCheck className="w-12 h-12 text-accent mb-6 group-hover:scale-110 transition-transform" />
            <h2 className="font-heading text-2xl font-bold text-text mb-3 tracking-wider">MEMBER</h2>
            <p className="text-text-2 text-sm leading-relaxed max-w-[250px]">Log in to your account, manage wallet balance, and start sessions directly.</p>
          </motion.div>
        </div>
      </div>
    </div>
  );
}
