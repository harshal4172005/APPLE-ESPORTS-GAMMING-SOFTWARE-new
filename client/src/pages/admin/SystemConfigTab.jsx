import React, { useState, useEffect } from 'react';
import { getSystemConfigs, saveSystemConfig, getWalletTopUpRules, saveWalletTopUpRules } from '../../api/settings.api';
import { useAuth } from '../../contexts/AuthContext';
import { useToast } from '../../components/ui/Toast';
import { Save, Wallet } from 'lucide-react';

function WalletTopUpSettingsCard() {
  const toast = useToast();
  const [rules, setRules] = useState({ minGamingTopUp: 500, defaultBonusPercent: 10 });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    getWalletTopUpRules()
      .then(res => res?.data && setRules(res.data))
      .catch(() => toast.error('Failed to load wallet top-up settings'))
      .finally(() => setLoading(false));
  }, []);

  const handleSave = async (e) => {
    e.preventDefault();
    const formData = new FormData(e.target);
    const payload = {
      minGamingTopUp: Number(formData.get('minGamingTopUp')),
      defaultBonusPercent: Number(formData.get('defaultBonusPercent')),
    };
    setSaving(true);
    try {
      await saveWalletTopUpRules(payload);
      setRules(payload);
      toast.success('Wallet top-up settings saved');
    } catch (err) {
      toast.error(err.response?.data?.error || 'Failed to save wallet top-up settings');
    } finally {
      setSaving(false);
    }
  };

  if (loading) return null;

  return (
    <form onSubmit={handleSave} className="bg-bg-2 p-5 rounded-lg border border-border">
      <h3 className="text-sm font-semibold mb-1 text-accent flex items-center gap-2">
        <Wallet size={14} /> Wallet Top-Up Settings
      </h3>
      <p className="text-xs text-text-2 mb-4">Controls every Gaming wallet top-up across all branches — the minimum amount allowed and the default bonus % applied automatically.</p>
      <div className="grid grid-cols-2 gap-4">
        <div className="form-group">
          <label>Minimum Gaming Top-Up (₹)</label>
          <input type="number" min="1" name="minGamingTopUp" defaultValue={rules.minGamingTopUp} className="form-control" />
        </div>
        <div className="form-group">
          <label>Default Bonus (%)</label>
          <input type="number" min="0" step="0.1" name="defaultBonusPercent" defaultValue={rules.defaultBonusPercent} className="form-control" />
        </div>
      </div>
      <div className="flex justify-end mt-4">
        <button type="submit" disabled={saving} className="btn-primary flex items-center gap-2 shadow-lg shadow-accent/25 disabled:opacity-50">
          <Save size={14} /> {saving ? 'SAVING...' : 'SAVE WALLET SETTINGS'}
        </button>
      </div>
    </form>
  );
}

export default function SystemConfigTab() {
  const [configs, setConfigs] = useState({});
  const [loading, setLoading] = useState(false);
  const toast = useToast();
  const { hasDashboardAccess } = useAuth();

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
      toast.error('Failed to load system configurations');
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async (e) => {
    e.preventDefault();
    const formData = new FormData(e.target);

    const data = {
      reservation: {
        gracePeriodMinutes: Number(formData.get('gracePeriodMinutes')),
        maxAdvanceDays: Number(formData.get('maxAdvanceDays'))
      },
      loyalty: {
        pointsPerRupee: Number(formData.get('pointsPerRupee')),
        minRedemption: Number(formData.get('minRedemption'))
      },
      emailNotifications: {
        receivers: formData.get('emailReceivers'),
        sender: formData.get('emailSender'),
        appPassword: formData.get('emailAppPassword')
      }
    };

    try {
      await saveSystemConfig({
        configKey: 'global_system_rules',
        configValue: data,
        description: 'Global system rules for reservations and loyalty'
      });
      toast.success('System configuration saved successfully');
    } catch (err) {
      toast.error('Failed to save configurations');
    }
  };

  if (loading) return <div className="text-center py-10 text-text-2">Loading configs...</div>;

  const currentRules = configs['global_system_rules'] || {
    reservation: { gracePeriodMinutes: 15, maxAdvanceDays: 7 },
    loyalty: { pointsPerRupee: 0.1, minRedemption: 100 },
    emailNotifications: {
      receivers: '',
      sender: '',
      appPassword: ''
    }
  };

  return (
    <div className="tab-pane fade-in space-y-6">
      <div className="pane-header">
        <div>
          <h2>System Configuration</h2>
          <p className="text-text-2 text-xs mt-1">Configure reservation rules and global loyalty settings. Pricing lives entirely in Settings → Pricing Profiles.</p>
        </div>
      </div>

      <form onSubmit={handleSave} className="space-y-6">
        <div className="bg-bg-2 p-5 rounded-lg border border-border">
          <h3 className="text-sm font-semibold mb-4 text-accent">Reservation Rules</h3>
          <div className="grid grid-cols-2 gap-4">
            <div className="form-group">
              <label>Grace Period (Minutes before auto-cancel)</label>
              <input type="number" name="gracePeriodMinutes" defaultValue={currentRules.reservation.gracePeriodMinutes} className="form-control" />
            </div>
            <div className="form-group">
              <label>Max Advance Booking (Days)</label>
              <input type="number" name="maxAdvanceDays" defaultValue={currentRules.reservation.maxAdvanceDays} className="form-control" />
            </div>
          </div>
        </div>

        <div className="bg-bg-2 p-5 rounded-lg border border-border">
          <h3 className="text-sm font-semibold mb-4 text-accent">Loyalty & Wallet Rules</h3>
          <div className="grid grid-cols-2 gap-4">
            <div className="form-group">
              <label>Points Per ₹ Spent</label>
              <input type="number" step="0.01" name="pointsPerRupee" defaultValue={currentRules.loyalty.pointsPerRupee} className="form-control" />
            </div>
            <div className="form-group">
              <label>Minimum Points for Redemption</label>
              <input type="number" name="minRedemption" defaultValue={currentRules.loyalty?.minRedemption} className="form-control" />
            </div>
          </div>
        </div>

        <div className="bg-bg-2 p-5 rounded-lg border border-border">
          <h3 className="text-sm font-semibold mb-1 text-accent">Email Verifier & Notifications</h3>
          <p className="text-xs text-text-2 mb-4">Set up email sender credentials and receivers to get alerts when members or operators join/leave.</p>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
            <div className="form-group">
              <label>Sender Mail ID (Super Admin)</label>
              <input type="email" name="emailSender" defaultValue={currentRules.emailNotifications?.sender} className="form-control" placeholder="superadmin@gmail.com" />
            </div>
            <div className="form-group">
              <label>App Password (16-letter Gmail App Password)</label>
              <input type="password" name="emailAppPassword" defaultValue={currentRules.emailNotifications?.appPassword} className="form-control" placeholder="abcd efgh ijkl mnop" />
            </div>
            <div className="form-group md:col-span-2 mt-2">
              <label>Notification Receiver Email(s) (Comma separated)</label>
              <input type="text" name="emailReceivers" defaultValue={currentRules.emailNotifications?.receivers} className="form-control" placeholder="receiver1@gmail.com, receiver2@gmail.com" />
            </div>
          </div>
        </div>

        <div className="flex justify-end">
          <button type="submit" className="btn-primary flex items-center gap-2 shadow-lg shadow-accent/25">
            <Save size={14} /> SAVE CONFIGURATIONS
          </button>
        </div>
      </form>

      {hasDashboardAccess('wallet_settings') && <WalletTopUpSettingsCard />}
    </div>
  );
}
