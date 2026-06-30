import React, { useState, useEffect } from 'react';
import { getSystemConfigs, saveSystemConfig } from '../../api/settings.api';
import { useToast } from '../../components/ui/Toast';
import { ShieldAlert } from 'lucide-react';

export default function SecuritySettingsTab() {
  const [configs, setConfigs] = useState({});
  const [loading, setLoading] = useState(false);
  const toast = useToast();

  useEffect(() => {
    loadConfigs();
  }, []);

  const loadConfigs = async () => {
    setLoading(true);
    try {
      const res = await getSystemConfigs();
      const configMap = {};
      res.data?.forEach(c => {
        configMap[c.configKey] = c.configValue;
      });
      setConfigs(configMap);
    } catch (err) {
      toast.error('Failed to load security settings');
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async (e) => {
    e.preventDefault();
    const formData = new FormData(e.target);
    const data = {
      autoLogoutMinutes: Number(formData.get('autoLogoutMinutes')),
      maxFailedLogins: Number(formData.get('maxFailedLogins')),
      lockoutDurationMinutes: Number(formData.get('lockoutDurationMinutes')),
      trackDeviceSessions: formData.get('trackDeviceSessions') === 'on'
    };

    try {
      await saveSystemConfig({
        configKey: 'global_security_rules',
        configValue: data,
        description: 'Global security rules for system access'
      });
      toast.success('Security settings saved successfully');
    } catch (err) {
      toast.error('Failed to save security settings');
    }
  };

  if (loading) return <div className="text-center py-10 text-text-2">Loading security policies...</div>;

  const currentRules = configs['global_security_rules'] || {
    autoLogoutMinutes: 60,
    maxFailedLogins: 5,
    lockoutDurationMinutes: 15,
    trackDeviceSessions: true
  };

  return (
    <div className="tab-pane fade-in space-y-6">
      <div className="pane-header">
        <div>
          <h2>Security Settings</h2>
          <p className="text-text-2 text-xs mt-1">Configure operator access controls, session timeouts, and intrusion protection.</p>
        </div>
      </div>

      <form onSubmit={handleSave} className="space-y-6">
        <div className="bg-bg-2 p-5 rounded-lg border border-border">
          <h3 className="text-sm font-semibold mb-4 text-accent">Session Management</h3>
          <div className="grid grid-cols-2 gap-4">
            <div className="form-group">
              <label>Auto-Logout Inactive Operators (Minutes)</label>
              <input type="number" name="autoLogoutMinutes" defaultValue={currentRules.autoLogoutMinutes} className="form-control" />
            </div>
            <div className="form-group flex items-end pb-2">
              <label className="flex items-center gap-2 cursor-pointer">
                <input type="checkbox" name="trackDeviceSessions" defaultChecked={currentRules.trackDeviceSessions} className="rounded border-border text-accent focus:ring-accent" />
                <span className="text-sm text-text-2">Track & Restrict Multi-Device Logins</span>
              </label>
            </div>
          </div>
        </div>

        <div className="bg-bg-2 p-5 rounded-lg border border-border">
          <h3 className="text-sm font-semibold mb-4 text-accent">Login Protection</h3>
          <div className="grid grid-cols-2 gap-4">
            <div className="form-group">
              <label>Max Failed Logins Before Lockout</label>
              <input type="number" name="maxFailedLogins" defaultValue={currentRules.maxFailedLogins} className="form-control" />
            </div>
            <div className="form-group">
              <label>Lockout Duration (Minutes)</label>
              <input type="number" name="lockoutDurationMinutes" defaultValue={currentRules.lockoutDurationMinutes} className="form-control" />
            </div>
          </div>
        </div>

        <div className="flex justify-end">
          <button type="submit" className="btn-primary flex items-center gap-2 shadow-lg shadow-accent/25">
            <ShieldAlert size={14} /> ENFORCE SECURITY POLICY
          </button>
        </div>
      </form>
    </div>
  );
}
