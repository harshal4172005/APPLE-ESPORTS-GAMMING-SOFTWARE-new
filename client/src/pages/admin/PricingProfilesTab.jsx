import { useState, useEffect } from 'react';
import { getPricingProfiles, createPricingProfile, updatePricingProfile, deletePricingProfile } from '../../api/settings.api';
import { useToast } from '../../components/ui/Toast';
import { Plus, Edit, Trash2, MapPin } from 'lucide-react';
import { useBranch } from '../../contexts/BranchContext';

export default function PricingProfilesTab() {
  const { branches } = useBranch();
  const toast = useToast();
  
  const [selectedBranchId, setSelectedBranchId] = useState('');
  const [profiles, setProfiles] = useState([]);
  const [loading, setLoading] = useState(false);
  const [drawer, setDrawer] = useState({ isOpen: false, data: null });

  // Initially select first branch if none selected
  useEffect(() => {
    if (branches.length > 0 && !selectedBranchId) {
      setSelectedBranchId(branches[0].id);
    }
  }, [branches, selectedBranchId]);

  useEffect(() => {
    if (selectedBranchId) {
      fetchProfiles();
    }
  }, [selectedBranchId]);

  const fetchProfiles = async () => {
    try {
      setLoading(true);
      const res = await getPricingProfiles(selectedBranchId);
      setProfiles(res.data || []);
    } catch (err) {
      toast.error('Failed to load pricing profiles');
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async (e) => {
    e.preventDefault();
    const formData = new FormData(e.target);
    const payload = {
      name: formData.get('name'),
      baseHourlyRate: Number(formData.get('baseHourlyRate')),
      bufferMinutes: Number(formData.get('bufferMinutes')),
      isActive: formData.get('isActive') === 'on',
      branchId: selectedBranchId,
      refreshRate: formData.get('refreshRate') || null,
      systemSpecs: formData.get('systemSpecs') || null
    };

    try {
      if (drawer.data) {
        await updatePricingProfile(drawer.data.id, payload);
        toast.success('Pricing profile updated successfully');
      } else {
        await createPricingProfile(payload);
        toast.success('Pricing profile created successfully');
      }
      setDrawer({ isOpen: false, data: null });
      fetchProfiles();
    } catch (err) {
      toast.error(err.response?.data?.error || 'Failed to save profile');
    }
  };

  const handleDelete = async (id) => {
    if (window.confirm('Are you sure you want to deactivate this pricing profile? Existing historical bills will not be affected.')) {
      try {
        await deletePricingProfile(id);
        toast.success('Profile deactivated successfully');
        fetchProfiles();
      } catch (err) {
        toast.error(err.response?.data?.error || 'Failed to deactivate profile');
      }
    }
  };

  const renderDrawer = () => (
    <div className={`fixed inset-y-0 right-0 w-96 bg-bg-2 border-l border-border shadow-2xl transform transition-transform duration-300 z-50 ${drawer.isOpen ? 'translate-x-0' : 'translate-x-full'}`}>
      <div className="h-full flex flex-col">
        <div className="p-4 border-b border-border flex items-center justify-between bg-bg-3">
          <h2 className="font-heading font-bold text-accent tracking-wider">{drawer.data ? 'Edit Profile' : 'New Pricing Profile'}</h2>
          <button onClick={() => setDrawer({ isOpen: false, data: null })} className="text-text-3 hover:text-text transition-colors text-2xl leading-none">&times;</button>
        </div>
        <div className="flex-1 overflow-y-auto p-4 custom-scrollbar">
          <form onSubmit={handleSave} className="form-stack text-xs">
            <div className="form-group">
              <label>Profile / Zone Name *</label>
              <input name="name" required defaultValue={drawer.data?.name} className="form-control" placeholder="e.g. VIP ELITE HUB" />
            </div>
            
            <div className="form-group">
              <label>Base Hourly Rate (₹) *</label>
              <input type="number" step="1" name="baseHourlyRate" required defaultValue={drawer.data?.baseHourlyRate} className="form-control" placeholder="e.g. 80" />
            </div>

            <div className="form-group">
              <label>Free Buffer / Grace Period (minutes) *</label>
              <input type="number" step="1" min="0" name="bufferMinutes" required defaultValue={drawer.data?.bufferMinutes ?? 10} className="form-control" placeholder="e.g. 10" />
              <p className="text-[10px] text-text-3 mt-1">Customers who end their session within this many minutes are charged ₹0. Applies live everywhere — session, PC cards, billing counter, member overlay.</p>
            </div>

            <div className="form-group">
              <label>Refresh Rate (Hz)</label>
              <select name="refreshRate" defaultValue={drawer.data?.refreshRate || ''} className="form-control">
                <option value="">-- Standard / Not Specified --</option>
                <option value="60Hz">60Hz</option>
                <option value="144Hz">144Hz</option>
                <option value="240Hz">240Hz</option>
                <option value="360Hz">360Hz</option>
              </select>
            </div>

            <div className="form-group">
              <label>System Specifications</label>
              <input name="systemSpecs" defaultValue={drawer.data?.systemSpecs || ''} className="form-control" placeholder="e.g. RTX 4090 / i9 14900K / 32GB DDR5" />
            </div>

            <div className="mt-4">
              <label className="flex items-center gap-2 cursor-pointer">
                <input type="checkbox" name="isActive" defaultChecked={drawer.data ? drawer.data.isActive : true} className="rounded border-text-3 text-accent focus:ring-accent bg-transparent" />
                <span className="font-medium text-text">Profile Active</span>
              </label>
              <p className="text-[10px] text-text-3 mt-1 ml-6">If inactive, this profile will not be selectable when adding new PCs.</p>
            </div>

            <div className="drawer-footer pt-4 mt-6 border-t border-border">
              <button type="submit" className="btn-primary w-full flex justify-center items-center gap-2">
                SAVE PROFILE
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  );

  return (
    <div className="tab-pane fade-in space-y-4">
      <div className="pane-header flex-col md:flex-row md:items-center gap-4">
        <div>
          <h2>Branch-Wise Pricing Profiles</h2>
          <p className="text-text-2 text-xs mt-1">Manage dynamic PC zones and pricing overlays tailored to each physical branch.</p>
        </div>
        <div className="flex items-center gap-3 w-full md:w-auto">
          <div className="relative flex-1 md:w-48">
            <div className="absolute inset-y-0 left-0 pl-2.5 flex items-center pointer-events-none">
              <MapPin className="h-4 w-4 text-text-3" />
            </div>
            <select
              value={selectedBranchId}
              onChange={(e) => setSelectedBranchId(e.target.value)}
              className="input w-full pl-9 py-1.5 text-xs bg-bg-3 border border-border rounded"
            >
              <option value="" disabled>Select Branch...</option>
              {branches.map(b => (
                <option key={b.id} value={b.id}>{b.name}</option>
              ))}
            </select>
          </div>
          <button 
            className="btn-primary flex items-center gap-2 text-xs py-1.5"
            onClick={() => setDrawer({ isOpen: true, data: null })}
            disabled={!selectedBranchId}
          >
            <Plus size={14} /> NEW PROFILE
          </button>
        </div>
      </div>

      {loading ? (
        <div className="flex flex-col items-center justify-center py-16 space-y-2">
          <div className="w-6 h-6 rounded-full border-2 border-accent border-t-transparent animate-spin" />
        </div>
      ) : profiles.length === 0 ? (
        <div className="text-center py-12 border border-dashed border-border rounded-lg bg-bg-3">
          <p className="text-text-3 text-sm">No pricing profiles defined for this branch.</p>
        </div>
      ) : (
        <div className="table-container overflow-x-auto">
          <table className="data-table text-xs w-full">
            <thead>
              <tr className="border-b border-border">
                <th>Zone / Profile Name</th>
                <th>Hourly Base Rate</th>
                <th>Free Buffer</th>
                <th>Refresh Rate (Hz)</th>
                <th>System Specs</th>
                <th>Status</th>
                <th className="w-16"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border/60">
              {profiles.map(p => (
                <tr key={p.id} className="hover:bg-bg-3/50 transition-colors group">
                  <td className="font-medium text-text">{p.name}</td>
                  <td className="font-mono text-accent">₹{p.baseHourlyRate.toFixed(2)}/hr</td>
                  <td className="text-text-2 font-mono">{p.bufferMinutes ?? 10}m free</td>
                  <td className="text-text-2">{p.refreshRate || '-'}</td>
                  <td className="text-text-2">{p.systemSpecs || '-'}</td>
                  <td>
                    {p.isActive ? (
                      <span className="px-2 py-0.5 rounded text-[10px] font-semibold bg-green-500/20 text-green-400">ACTIVE</span>
                    ) : (
                      <span className="px-2 py-0.5 rounded text-[10px] font-semibold bg-red-500/20 text-red-400">INACTIVE</span>
                    )}
                  </td>
                  <td className="flex justify-end gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
                    <button onClick={() => setDrawer({ isOpen: true, data: p })} className="btn-icon" title="Edit Profile">
                      <Edit size={14} className="text-blue-400" />
                    </button>
                    {p.isActive && (
                      <button onClick={() => handleDelete(p.id)} className="btn-icon" title="Deactivate">
                        <Trash2 size={14} className="text-red-400" />
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {renderDrawer()}
      {drawer.isOpen && (
        <div className="fixed inset-0 bg-bg/60 backdrop-blur-sm z-40" onClick={() => setDrawer({ isOpen: false, data: null })} />
      )}
    </div>
  );
}
