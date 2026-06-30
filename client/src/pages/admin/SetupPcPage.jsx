import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Monitor, CheckCircle, MonitorSmartphone, MapPin, Loader2 } from 'lucide-react';
import { useToast } from '../../components/ui/Toast';
import api from '../../config/api';

export default function SetupPcPage() {
  const navigate = useNavigate();
  const toast = useToast();
  
  const [branches, setBranches] = useState([]);
  const [selectedBranchId, setSelectedBranchId] = useState('');
  
  const [pcs, setPcs] = useState([]);
  const [selectedPcId, setSelectedPcId] = useState('');
  const [monitorHz, setMonitorHz] = useState('');
  
  const [isLoading, setIsLoading] = useState(false);
  const [isSaving, setIsSaving] = useState(false);

  // Define branch-specific monitor refresh rates based on the official website
  const BRANCH_MONITORS = {
    adajan: ['240'],
    citylight: ['144', '240'],
    katargam: ['165', '240', '360'],
    varachha: ['240', '400', '4K']
  };
  
  const selectedBranchName = branches.find(b => b.id === selectedBranchId)?.name?.toLowerCase().replace(/\s+/g, '') || '';
  const availableHz = BRANCH_MONITORS[selectedBranchName] || ['60', '75', '120', '144', '165', '240', '360', '400', '500', '4K', 'Standard', 'VIP'];


  // Fetch branches on mount
  useEffect(() => {
    const fetchBranches = async () => {
      setIsLoading(true);
      try {
        const res = await api.get('/public/branches');
        if (res.data.success) {
          setBranches(res.data.data);
        }
      } catch (err) {
        toast.error('Failed to load branches');
      } finally {
        setIsLoading(false);
      }
    };
    fetchBranches();
  }, []);

  // Fetch PCs when branch is selected
  useEffect(() => {
    if (!selectedBranchId) {
      setPcs([]);
      setSelectedPcId('');
      return;
    }

    const fetchPcs = async () => {
      setIsLoading(true);
      try {
        const res = await api.get(`/public/branches/${selectedBranchId}/pcs`);
        if (res.data.success) {
          setPcs(res.data.data);
        }
      } catch (err) {
        toast.error('Failed to load PCs');
      } finally {
        setIsLoading(false);
      }
    };
    fetchPcs();
  }, [selectedBranchId]);

  const handleSave = async (e) => {
    e.preventDefault();
    if (!selectedPcId) return;

    setIsSaving(true);
    try {
      // Save Hz to the backend
      if (monitorHz) {
        await api.post(`/public/pcs/${selectedPcId}/hz`, { monitorHz });
      }
      
      localStorage.setItem('dedicatedPcId', selectedPcId);
      toast.success('PC Setup completed! Redirecting...');
      
      // Redirect to root, which will now catch the localStorage value
      // and redirect to the PC overlay.
      setTimeout(() => {
        window.location.href = '/';
      }, 1500);
    } catch (err) {
      toast.error('Failed to save PC configuration');
      setIsSaving(false);
    }
  };

  return (
    <div className="min-h-screen bg-bg flex items-center justify-center p-6 relative overflow-hidden">
      <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] bg-accent/20 rounded-full blur-[120px] pointer-events-none" />

      <div className="relative z-10 w-full max-w-md">
        <div className="card bg-bg-2/80 backdrop-blur-xl border-border/60 shadow-xl shadow-black/50 p-8 hover:border-accent transition-colors duration-300">
          <div className="text-center mb-8">
            <div className="w-16 h-16 bg-accent/10 border border-accent/30 rounded-full flex items-center justify-center mx-auto mb-4 shadow-[0_0_15px_rgba(220,38,38,0.2)]">
              <MonitorSmartphone className="w-8 h-8 text-accent" />
            </div>
            <h1 className="font-heading text-3xl font-bold text-text tracking-wide uppercase">Setup Dedicated PC</h1>
            <p className="text-text-2 font-body mt-2 text-sm">Configure this device to always launch the PC Overlay.</p>
          </div>

          <form onSubmit={handleSave} className="space-y-6">
            <div>
              <label className="block text-sm font-medium text-text-2 mb-2 font-body tracking-wide uppercase">Select Branch</label>
              <div className="relative">
                <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                  <MapPin className="h-5 w-5 text-text-3" />
                </div>
                <select
                  required
                  value={selectedBranchId}
                  onChange={(e) => setSelectedBranchId(e.target.value)}
                  className="input w-full pl-10 focus:border-accent focus:ring-accent/30 appearance-none bg-bg-3"
                  disabled={isLoading}
                >
                  <option value="" disabled>Choose a branch...</option>
                  {branches.map((b) => (
                    <option key={b.id} value={b.id}>{b.name}</option>
                  ))}
                </select>
              </div>
            </div>

            <div>
              <label className="block text-sm font-medium text-text-2 mb-2 font-body tracking-wide uppercase">Select PC</label>
              <div className="relative">
                <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                  <Monitor className="h-5 w-5 text-text-3" />
                </div>
                <select
                  required
                  value={selectedPcId}
                  onChange={(e) => setSelectedPcId(e.target.value)}
                  className="input w-full pl-10 focus:border-accent focus:ring-accent/30 appearance-none bg-bg-3"
                  disabled={!selectedBranchId || isLoading}
                >
                  <option value="" disabled>Choose a PC...</option>
                  {pcs.map((p) => (
                    <option key={p.id} value={p.id}>{p.name}</option>
                  ))}
                </select>
              </div>
              <p className="text-xs text-text-3 mt-2">Select the exact PC hardware ID.</p>
            </div>

            <div>
              <label className="block text-sm font-medium text-text-2 mb-2 font-body tracking-wide uppercase">Monitor Refresh Rate (Hz)</label>
              <div className="relative">
                <select
                  required
                  value={monitorHz}
                  onChange={(e) => setMonitorHz(e.target.value)}
                  className="input w-full focus:border-accent focus:ring-accent/30 appearance-none bg-bg-3"
                  disabled={isLoading}
                >
                  <option value="" disabled>Select Refresh Rate...</option>
                  {availableHz.map(hz => (
                    <option key={hz} value={hz}>{hz}{hz !== '4K' && hz !== 'Standard' && hz !== 'VIP' ? ' Hz' : ''}</option>
                  ))}
                </select>
              </div>
              <p className="text-xs text-text-3 mt-2">Required for accurate automatic billing mapping.</p>
            </div>

            <button
              type="submit"
              disabled={!selectedPcId || isSaving}
              className="w-full bg-accent hover:bg-accent-dark text-white font-semibold py-3 px-4 rounded-sm transition-all duration-200 flex items-center justify-center gap-2 shadow-[0_0_15px_rgba(220,38,38,0.3)] disabled:opacity-50 disabled:cursor-not-allowed border border-accent/50"
            >
              {isSaving ? <Loader2 className="w-5 h-5 animate-spin" /> : <CheckCircle className="w-5 h-5" />}
              <span className="font-heading text-lg tracking-wider uppercase font-bold">Save Configuration</span>
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
