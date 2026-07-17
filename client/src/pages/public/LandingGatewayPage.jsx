import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { User, MonitorStop, ShieldAlert, Shield } from 'lucide-react';
import { authAPI } from '../../api/auth.api';

export default function LandingGatewayPage() {
  const navigate = useNavigate();
  const [setupStatus, setSetupStatus] = useState(null);

  useEffect(() => {
    // Hidden escape hatch to clear dedicated PC from this browser
    const params = new URLSearchParams(window.location.search);
    if (params.get('clear') === 'true') {
      localStorage.removeItem('dedicatedPcId');
      window.location.href = '/';
      return;
    }

    // Check if PC is already dedicated to a specific client
    const pcId = localStorage.getItem('dedicatedPcId');
    if (pcId) {
      navigate(`/pc-overlay/${pcId}`, { replace: true });
    }

    // Fetch setup status
    authAPI.checkSetup().then(res => {
      setSetupStatus(res.data.data);
    }).catch(err => console.error("Failed to fetch setup status", err));
  }, [navigate]);

  const cards = [
    {
      id: 'user',
      title: 'USER',
      description: 'Play as a guest or member.',
      icon: <User className="w-12 h-12 text-accent mb-4 group-hover:scale-110 transition-transform" />,
      onClick: () => {
        const pcId = localStorage.getItem('dedicatedPcId');
        if (pcId) {
          navigate(`/pc-overlay/${pcId}`);
        } else {
          navigate('/setup-pc');
        }
      },
    },
    {
      id: 'operator',
      title: 'OPERATOR',
      description: 'Manage sessions, billing and branch operations.',
      icon: <MonitorStop className="w-12 h-12 text-accent mb-4 group-hover:scale-110 transition-transform" />,
      onClick: () => {
        navigate('/login/operator');
      },
    },
    {
      id: 'admin',
      title: 'ADMIN',
      description: 'Multi-branch management portal.',
      icon: <Shield className="w-12 h-12 text-accent mb-4 group-hover:scale-110 transition-transform" />,
      onClick: () => {
        navigate('/login/admin');
      },
    },
    {
      id: 'superadmin',
      title: 'SUPER ADMIN',
      description: 'Manage branches, analytics and global operations.',
      icon: <ShieldAlert className="w-12 h-12 text-accent mb-4 group-hover:scale-110 transition-transform" />,
      onClick: () => {
        if (setupStatus?.needsSuperAdminSetup) navigate('/setup/superadmin');
        else navigate('/login/superadmin');
      },
    }
  ];

  return (
    <div className="min-h-screen bg-bg flex items-center justify-center p-6 overflow-hidden relative">
      {/* Background glow effects - matching LoginPage */}
      <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] bg-accent/20 rounded-full blur-[120px] pointer-events-none" />

      <div className="relative z-10 max-w-6xl w-full">
        <div className="text-center mb-16">
          <motion.img 
            initial={{ scale: 0.9 }}
            animate={{ scale: 1 }}
            transition={{ duration: 0.5 }}
            src="/logo.png" 
            alt="Apple Esports" 
            className="h-24 w-auto mx-auto mb-6 drop-shadow-[0_0_15px_rgba(220,38,38,0.5)]"
          />
          <motion.h1 
            initial={{ opacity: 0, y: -20 }}
            animate={{ opacity: 1, y: 0 }}
            className="font-heading text-4xl md:text-6xl font-bold tracking-wide text-text mb-2"
          >
            APPLE ESPORTS
          </motion.h1>
          <motion.p 
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ delay: 0.2 }}
            className="text-accent text-sm font-mono tracking-[0.2em] uppercase mb-8"
          >
            Select your portal to continue
          </motion.p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
          {cards.map((card, index) => (
            <motion.div
              key={card.id}
              initial={{ opacity: 0, y: 30 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.3 + index * 0.1 }}
              whileHover={{ scale: 1.03, translateY: -5 }}
              whileTap={{ scale: 0.95 }}
              onClick={card.onClick}
              className="card group relative flex flex-col items-center text-center p-8 bg-bg-2/80 backdrop-blur-xl border-border/60 shadow-xl shadow-black/50 hover:border-accent hover:shadow-[0_0_20px_rgba(220,38,38,0.15)] cursor-pointer transition-all duration-300"
            >
              {card.icon}
              <h2 className="font-heading text-xl font-bold text-text mb-3 tracking-wider">{card.title}</h2>
              <p className="text-text-2 text-sm leading-relaxed">{card.description}</p>
            </motion.div>
          ))}
        </div>
      </div>
    </div>
  );
}
