import { useState, useEffect, useCallback, useRef } from 'react';
import {
  Users, Plus, Search, Wallet, Phone, Mail, Clock,
  X, Banknote, CreditCard, AlertTriangle,
  UserPlus, Edit2, Check, ArrowUpRight, ArrowDownRight,
  Gamepad2, Coffee, RefreshCw, Trash2, Receipt, Eye, EyeOff,
  KeyRound, User as UserIcon, ShieldCheck, ShieldAlert, Gift, SlidersHorizontal, Download,
} from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import { useBranch } from '../../contexts/BranchContext';
import { useToast } from '../../components/ui/Toast';
import {
  getMembers, getMemberById, registerMember, updateMember,
  getWalletHistory, topUpWallet, adminEditMemberValues, deleteMember,
} from '../../api/members.api';
import { getWalletTopUpRules } from '../../api/settings.api';

const TOPUP_PRESETS = [200, 500, 1000, 2000, 5000];

function ActionBadge({ action }) {
  const map = {
    TopUp:     { label: 'Top-Up',   cls: 'text-neon-blue   bg-neon-blue/10   border-neon-blue/20' },
    Deduction: { label: 'Deducted', cls: 'text-neon-red    bg-neon-red/10    border-neon-red/20' },
    Bonus:     { label: 'Bonus',    cls: 'text-neon-orange bg-neon-orange/10 border-neon-orange/20' },
    Refund:    { label: 'Refund',   cls: 'text-neon-purple bg-neon-purple/10 border-neon-purple/20' },
  };
  const { label, cls } = map[action] ?? { label: action, cls: 'text-text-2 bg-bg-3 border-border' };
  return (
    <span className={`inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-bold border uppercase tracking-wider ${cls}`}>
      {label}
    </span>
  );
}

function StatusBadge({ status }) {
  const active = status === 0 || status === 'Active';
  return (
    <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider border ${
      active
        ? 'bg-neon-blue/10 border-neon-blue/30 text-neon-blue'
        : 'bg-bg-3 border-border text-text-3'
    }`}>
      <span className={`w-1.5 h-1.5 rounded-full ${active ? 'bg-neon-blue' : 'bg-text-3'}`} />
      {active ? 'Active' : 'Inactive'}
    </span>
  );
}

function relTime(dateStr) {
  if (!dateStr) return '—';
  const diff = Date.now() - new Date(dateStr).getTime();
  const days = Math.floor(diff / 86400000);
  if (days === 0) return 'Today';
  if (days === 1) return 'Yesterday';
  if (days < 30) return `${days}d ago`;
  const months = Math.floor(days / 30);
  if (months < 12) return `${months}mo ago`;
  return `${Math.floor(months / 12)}y ago`;
}


// ═══════════════════════════════════════════════════════════════════════════════
// RegisterDrawer — handles both Create and Edit
// ═══════════════════════════════════════════════════════════════════════════════
function RegisterDrawer({ open, onClose, onSuccess, editMember }) {
  const toast = useToast();
  const isEdit = !!editMember;
  const [fields, setFields] = useState({ fullName: '', mobileNumber: '', email: '', username: '', password: '' });
  const [enableLogin, setEnableLogin] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (open) {
      const hasCredentials = !!(editMember?.username);
      setFields({
        fullName: editMember?.fullName ?? '',
        mobileNumber: editMember?.mobileNumber ?? '',
        email: editMember?.email ?? '',
        username: editMember?.username ?? '',
        password: '',
      });
      setEnableLogin(hasCredentials);
      setShowPassword(false);
      setError(null);
    }
  }, [open, editMember]);

  const set = (k, v) => setFields(p => ({ ...p, [k]: v }));

  const handleSubmit = async (e) => {
    e?.preventDefault();
    setError(null);

    // Validate credentials section
    if (enableLogin) {
      const username = fields.username.trim();
      if (!username) {
        setError('Username is required when Login Access is enabled.');
        return;
      }
      if (username.length < 3) {
        setError('Username must be at least 3 characters.');
        return;
      }
    }
    setLoading(true);
    try {
      const dto = {
        fullName: fields.fullName.trim(),
        mobileNumber: fields.mobileNumber.trim(),
        ...(fields.email.trim() && { email: fields.email.trim() }),
        // Only include credentials if the toggle is ON
        ...(enableLogin && fields.username.trim() && { username: fields.username.trim() }),
        ...(enableLogin && fields.password && { password: fields.password }),
        // Signal backend to clear credentials if disabled during edit
        ...(isEdit && !enableLogin && { disableLogin: true }),
      };
      if (isEdit) {
        const updated = await updateMember(editMember.id, dto);
        toast.success('Member updated');
        onSuccess(updated);  // pass fresh member back
      } else {
        const created = await registerMember(dto);
        toast.success('Member registered');
        onSuccess(created);
      }
      onClose();
    } catch (err) {
      setError(err.response?.data?.error || err.response?.data?.message || 'Failed to save member');
    } finally {
      setLoading(false);
    }
  };

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex">
      <div className="flex-1 bg-black/60 backdrop-blur-sm" onClick={onClose} />
      <div className="w-full max-w-sm bg-bg-2 border-l border-border flex flex-col shadow-2xl">
        <div className="p-4 border-b border-border bg-bg-3 flex items-center justify-between shrink-0">
          <div className="flex items-center gap-2">
            <UserPlus className="w-5 h-5 text-accent" />
            <h2 className="font-heading font-bold text-text uppercase tracking-wider">
              {isEdit ? 'Edit Member' : 'Register Member'}
            </h2>
          </div>
          <button onClick={onClose} className="p-1 text-text-3 hover:text-text rounded transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto p-5 space-y-4">
          {error && (
            <div className="flex items-start gap-2 p-3 bg-neon-red/10 border border-neon-red/20 rounded-lg text-neon-red text-sm">
              <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5" />
              <p>{error}</p>
            </div>
          )}
          <div>
            <label className="block text-[10px] text-text-3 uppercase tracking-widest font-bold mb-1.5">Full Name *</label>
            <input
              required type="text"
              value={fields.fullName}
              onChange={e => set('fullName', e.target.value)}
              placeholder="e.g. Rahul Sharma"
              className="w-full bg-bg-3 border border-border text-text rounded-lg px-3 py-2.5 text-sm focus:border-accent focus:ring-1 focus:ring-accent transition-all placeholder:text-text-3"
            />
          </div>
          <div>
            <label className="block text-[10px] text-text-3 uppercase tracking-widest font-bold mb-1.5">Mobile Number * <span className="normal-case font-normal">(10-digit)</span></label>
            <input
              required type="tel" pattern="\d{10}" maxLength={10}
              value={fields.mobileNumber}
              onChange={e => set('mobileNumber', e.target.value.replace(/\D/g, '').slice(0, 10))}
              placeholder="9876543210"
              className="w-full bg-bg-3 border border-border text-text rounded-lg px-3 py-2.5 text-sm font-mono tracking-wider focus:border-accent focus:ring-1 focus:ring-accent transition-all placeholder:text-text-3"
            />
          </div>
          <div className="form-group">
            <label className="block text-[10px] text-text-3 uppercase tracking-widest font-bold mb-1.5">Email *</label>
            <div className="relative">
              <input
                type="email"
                required
                value={fields.email}
                onChange={e => set('email', e.target.value)}
                placeholder="rahul@example.com"
                className="w-full bg-bg-3 border border-border text-text rounded-lg px-3 py-2.5 text-sm focus:border-accent focus:ring-1 focus:ring-accent transition-all placeholder:text-text-3"
              />
            </div>
          </div>

          {/* ── Login credentials toggle ── */}
          <div className="border border-border rounded-xl overflow-hidden">
            {/* Toggle row */}
            <button
              type="button"
              onClick={() => setEnableLogin(p => !p)}
              className={`w-full flex items-center justify-between px-4 py-3 transition-colors ${
                enableLogin ? 'bg-neon-blue/10' : 'bg-bg-3 hover:bg-bg-2'
              }`}
            >
              <div className="flex items-center gap-2.5">
                <KeyRound className={`w-4 h-4 ${enableLogin ? 'text-neon-blue' : 'text-text-3'}`} />
                <div className="text-left">
                  <p className={`text-sm font-bold ${enableLogin ? 'text-neon-blue' : 'text-text-2'}`}>
                    Setup Login Access
                  </p>
                  <p className="text-[10px] text-text-3 mt-0.5">
                    Member can log in and use wallet for payments
                  </p>
                </div>
              </div>
              {/* Toggle switch */}
              <div className={`w-10 h-5 rounded-full border-2 relative transition-colors shrink-0 ${
                enableLogin ? 'bg-neon-blue border-neon-blue' : 'bg-bg-2 border-border'
              }`}>
                <div className={`absolute top-0.5 w-3 h-3 rounded-full bg-white shadow transition-transform ${
                  enableLogin ? 'translate-x-[18px]' : 'translate-x-0.5'
                }`} />
              </div>
            </button>

            {/* Credential fields — only visible when toggle is ON */}
            {enableLogin && (
              <div className="px-4 pb-4 pt-3 space-y-3 border-t border-border bg-bg-3/50">
                <div>
                  <label className="block text-[10px] text-text-3 uppercase tracking-widest font-bold mb-1.5">
                    Username *
                  </label>
                  <div className="relative">
                    <UserIcon className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-text-3" />
                    <input
                      type="text"
                      autoComplete="off"
                      value={fields.username}
                      onChange={e => set('username', e.target.value.replace(/\s/g, '').toLowerCase())}
                      placeholder="e.g. rahul123"
                      className="w-full bg-bg-3 border border-border text-text rounded-lg pl-9 pr-3 py-2.5 text-sm font-mono focus:border-neon-blue focus:ring-1 focus:ring-neon-blue transition-all placeholder:text-text-3"
                    />
                  </div>
                </div>
              </div>
            )}
          </div>
        </form>

        <div className="p-4 border-t border-border bg-bg-3 shrink-0 flex gap-3">
          <button
            type="button" onClick={onClose}
            className="flex-1 py-2.5 rounded-lg border border-border text-text-2 text-sm font-bold uppercase tracking-wider hover:bg-bg-2 transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={handleSubmit} disabled={loading}
            className="flex-[2] py-2.5 rounded-lg bg-accent/10 border border-accent/50 text-accent text-sm font-bold uppercase tracking-wider flex items-center justify-center gap-2 hover:bg-accent/20 transition-colors disabled:opacity-50"
          >
            {loading
              ? <div className="w-4 h-4 rounded-full border-2 border-current border-t-transparent animate-spin" />
              : <><Check className="w-4 h-4" />{isEdit ? 'Save Changes' : 'Register'}</>
            }
          </button>
        </div>
      </div>
    </div>
  );
}

// ═══════════════════════════════════════════════════════════════════════════════
// TopUpModal
// ═══════════════════════════════════════════════════════════════════════════════
const BONUS_PRESETS = [10, 15, 20, 25, 30];

function TopUpModal({ member, isSuperAdmin, onClose, onSuccess }) {
  const toast = useToast();
  const [amount, setAmount] = useState('');
  const [paymentType, setPaymentType] = useState('Cash');
  const [splitCash, setSplitCash] = useState('');
  const [splitOnline, setSplitOnline] = useState('');
  const [walletType, setWalletType] = useState(member?.tempTarget || 'Gaming');
  const [topUpRules, setTopUpRules] = useState({ minGamingTopUp: 500, defaultBonusPercent: 10 });
  const [bonusPercent, setBonusPercent] = useState(10);
  const [isCustomBonus, setIsCustomBonus] = useState(false);
  const [customBonusInput, setCustomBonusInput] = useState('');
  const [reason, setReason] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    getWalletTopUpRules()
      .then(res => {
        if (res?.data) {
          setTopUpRules(res.data);
          setBonusPercent(res.data.defaultBonusPercent);
        }
      })
      .catch(() => {}); // fall back to the 500/10 defaults already in state
  }, []);

  const numAmount = parseFloat(amount) || 0;
  const numSplitCash = parseFloat(splitCash) || 0;
  const numSplitOnline = parseFloat(splitOnline) || 0;
  const splitRemaining = numAmount - numSplitCash - numSplitOnline;
  const minAmount = walletType === 'Gaming' ? topUpRules.minGamingTopUp : 10;
  const isValid = numAmount >= minAmount && (paymentType !== 'Split' || Math.abs(splitRemaining) < 0.01);
  const effectiveBonusPercent = walletType === 'Gaming' ? bonusPercent : 0;

  const handleTopUp = async () => {
    if (!isValid) return;
    setError(null);
    setLoading(true);
    try {
      await topUpWallet(member.id, {
        targetWallet: walletType,
        amount: numAmount,
        paymentType,
        ...(paymentType === 'Split' && { cashAmount: numSplitCash, onlineAmount: numSplitOnline }),
        ...(isSuperAdmin && walletType === 'Gaming' && bonusPercent !== topUpRules.defaultBonusPercent && { bonusPercentOverride: bonusPercent }),
        reason: reason.trim() || 'Manual top-up',
      });
      toast.success(`₹${numAmount} added to ${member.fullName}'s ${walletType} wallet`);
      onSuccess();
      onClose();
    } catch (err) {
      setError(err.response?.data?.error || err.response?.data?.message || 'Top-up failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm">
      <div className="w-full max-w-sm bg-bg-2 border border-border rounded-xl shadow-2xl overflow-hidden">
        <div className="p-4 border-b border-border bg-bg-3 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Wallet className="w-5 h-5 text-neon-blue" />
            <div>
              <h2 className="font-heading font-bold text-text uppercase tracking-wider text-sm leading-none">Top-Up Wallet</h2>
              <p className="text-[10px] text-text-3 mt-0.5 font-mono">
                {member.fullName} · G: ₹{parseFloat(member.gamingBalance || 0).toFixed(0)} · F: ₹{parseFloat(member.foodBalance || 0).toFixed(0)}
              </p>
            </div>
          </div>
          <button onClick={onClose} className="p-1 text-text-3 hover:text-text rounded transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-5 space-y-4">
          {error && (
            <div className="flex items-start gap-2 p-3 bg-neon-red/10 border border-neon-red/20 rounded-lg text-neon-red text-sm">
              <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5" /><p>{error}</p>
            </div>
          )}

          <div>
            <label className="block text-[10px] text-text-3 uppercase tracking-widest font-bold mb-2">Amount (₹) · Min ₹{minAmount}</label>
            <div className="relative">
              <span className="absolute left-3 top-1/2 -translate-y-1/2 text-text-3 font-bold">₹</span>
              <input
                type="number" min={minAmount}
                value={amount}
                onChange={e => setAmount(e.target.value)}
                placeholder="0"
                className="w-full bg-bg-3 border border-border text-text rounded-lg pl-7 pr-3 py-2.5 font-mono text-xl focus:border-neon-blue focus:ring-1 focus:ring-neon-blue transition-all placeholder:text-text-3"
              />
            </div>
            {numAmount > 0 && numAmount < minAmount && (
              <p className="text-[11px] text-neon-red mt-1.5">Minimum {walletType} top-up is ₹{minAmount}</p>
            )}

            <div className="flex gap-2 mt-3">
              <label className="flex items-center gap-1.5 text-xs text-text-2 cursor-pointer">
                <input type="radio" name="walletType" value="Gaming" checked={walletType === 'Gaming'} onChange={() => setWalletType('Gaming')} className="text-neon-blue bg-bg-3 border-border focus:ring-neon-blue" />
                Gaming Wallet
              </label>
              <label className="flex items-center gap-1.5 text-xs text-text-2 cursor-pointer">
                <input type="radio" name="walletType" value="Food" checked={walletType === 'Food'} onChange={() => setWalletType('Food')} className="text-neon-orange bg-bg-3 border-border focus:ring-neon-orange" />
                Food Wallet
              </label>
            </div>

            <div className="flex gap-1.5 mt-3 flex-wrap">
              {TOPUP_PRESETS.map(p => (
                <button
                  key={p}
                  onClick={() => setAmount(String(p))}
                  className={`px-2.5 py-1 rounded border text-[11px] font-mono font-bold transition-colors ${
                    Number(amount) === p
                      ? 'bg-neon-blue/20 border-neon-blue text-neon-blue'
                      : 'bg-bg-3 border-border text-text-2 hover:border-neon-blue/40'
                  }`}
                >
                  ₹{p}
                </button>
              ))}
            </div>
          </div>

          <div>
            <label className="block text-[10px] text-text-3 uppercase tracking-widest font-bold mb-2">Payment Method</label>
            <div className="grid grid-cols-3 gap-2">
              {[
                { val: 'Cash',   Icon: Banknote,   active: 'bg-neon-blue/15 border-neon-blue text-neon-blue' },
                { val: 'Online', Icon: CreditCard, active: 'bg-neon-purple/15 border-neon-purple text-neon-purple' },
                { val: 'Split',  Icon: ArrowUpRight, active: 'bg-neon-orange/15 border-neon-orange text-neon-orange' },
              ].map(({ val, Icon, active }) => (
                <button
                  key={val}
                  onClick={() => setPaymentType(val)}
                  className={`py-2.5 rounded-lg border text-sm font-bold uppercase tracking-wider flex items-center justify-center gap-1.5 transition-colors ${
                    paymentType === val ? active : 'bg-bg-3 border-border text-text-2 hover:border-border'
                  }`}
                >
                  <Icon className="w-4 h-4" />{val}
                </button>
              ))}
            </div>

            {paymentType === 'Split' && (
              <div className="grid grid-cols-2 gap-2 mt-3">
                <div>
                  <label className="block text-[10px] text-text-3 uppercase tracking-widest font-bold mb-1.5">Cash (₹)</label>
                  <input
                    type="number" min="0"
                    value={splitCash}
                    onChange={e => setSplitCash(e.target.value)}
                    placeholder="0"
                    className="w-full bg-bg-3 border border-border text-text rounded-lg px-3 py-2 font-mono text-sm focus:border-neon-blue focus:ring-1 focus:ring-neon-blue transition-all placeholder:text-text-3"
                  />
                </div>
                <div>
                  <label className="block text-[10px] text-text-3 uppercase tracking-widest font-bold mb-1.5">Online (₹)</label>
                  <input
                    type="number" min="0"
                    value={splitOnline}
                    onChange={e => setSplitOnline(e.target.value)}
                    placeholder="0"
                    className="w-full bg-bg-3 border border-border text-text rounded-lg px-3 py-2 font-mono text-sm focus:border-neon-purple focus:ring-1 focus:ring-neon-purple transition-all placeholder:text-text-3"
                  />
                </div>
                {Math.abs(splitRemaining) >= 0.01 && (
                  <p className="col-span-2 text-[11px] text-neon-orange font-mono">
                    {splitRemaining > 0 ? `₹${splitRemaining.toFixed(0)} remaining` : `₹${Math.abs(splitRemaining).toFixed(0)} over the total`}
                  </p>
                )}
              </div>
            )}
          </div>

          {/* Bonus % override — SuperAdmin only, Gaming wallet only */}
          {isSuperAdmin && walletType === 'Gaming' && (
            <div>
              <div className="flex items-center justify-between mb-2">
                <span className="text-[10px] text-text-3 font-bold uppercase tracking-widest flex items-center gap-1">
                  <Gift className="w-3 h-3" /> Bonus Boost
                  <span className="text-accent/60 ml-1 normal-case">(Admin Only)</span>
                </span>
                {bonusPercent !== topUpRules.defaultBonusPercent && (
                  <span className="text-neon-orange text-[10px] font-bold font-mono">{bonusPercent}% applied</span>
                )}
              </div>
              <div className="flex flex-wrap gap-1.5">
                {BONUS_PRESETS.map(pct => (
                  <button
                    key={pct}
                    onClick={() => { setIsCustomBonus(false); setBonusPercent(pct); }}
                    className={`px-3 py-1 rounded border text-[11px] font-bold uppercase tracking-wider transition-all ${
                      !isCustomBonus && bonusPercent === pct
                        ? 'bg-neon-orange/20 border-neon-orange text-neon-orange'
                        : 'bg-bg-3 border-border text-text-3 hover:border-neon-orange/50 hover:text-neon-orange/80'
                    }`}
                  >
                    {pct}% {pct === topUpRules.defaultBonusPercent ? '(Default)' : ''}
                  </button>
                ))}
                <button
                  onClick={() => { setIsCustomBonus(true); if (customBonusInput) setBonusPercent(parseFloat(customBonusInput) || 0); }}
                  className={`px-3 py-1 rounded border text-[11px] font-bold uppercase tracking-wider transition-all ${
                    isCustomBonus
                      ? 'bg-neon-orange/20 border-neon-orange text-neon-orange'
                      : 'bg-bg-3 border-border text-text-3 hover:border-neon-orange/50 hover:text-neon-orange/80'
                  }`}
                >
                  Custom
                </button>
              </div>
              {isCustomBonus && (
                <div className="relative mt-2">
                  <span className="absolute left-3 top-1/2 -translate-y-1/2 text-text-3 font-bold">%</span>
                  <input
                    type="number" min="0" step="0.1" autoFocus
                    value={customBonusInput}
                    onChange={e => { setCustomBonusInput(e.target.value); setBonusPercent(parseFloat(e.target.value) || 0); }}
                    placeholder="e.g. 12.5"
                    className="w-full bg-bg-3 border border-border text-text rounded-lg pl-7 pr-3 py-2 font-mono text-sm focus:border-neon-orange focus:ring-1 focus:ring-neon-orange transition-all placeholder:text-text-3"
                  />
                </div>
              )}
            </div>
          )}

          <div>
            <label className="block text-[10px] text-text-3 uppercase tracking-widest font-bold mb-2">Reason <span className="normal-case font-normal">(optional)</span></label>
            <input
              type="text"
              value={reason}
              onChange={e => setReason(e.target.value)}
              placeholder="e.g. Monthly gaming pack"
              className="w-full bg-bg-3 border border-border text-text rounded-lg px-3 py-2.5 text-sm focus:border-neon-blue focus:ring-1 focus:ring-neon-blue transition-all placeholder:text-text-3"
            />
          </div>

          {numAmount > 0 && (
            <div className="bg-neon-blue/5 border border-neon-blue/20 rounded-lg p-3 space-y-1.5">
              {walletType === 'Gaming' && (
                <div className="flex justify-between items-center text-xs text-text-2">
                  <span>Top-Up ₹{numAmount} + Bonus ({effectiveBonusPercent}%) ₹{(numAmount * effectiveBonusPercent / 100).toFixed(0)}</span>
                  <span className="font-mono font-bold text-neon-orange">= ₹{(numAmount * (1 + effectiveBonusPercent / 100)).toFixed(0)} credited</span>
                </div>
              )}
              <div className="flex justify-between items-center">
                <span className="text-sm text-text-2">New {walletType} Balance</span>
                <span className="font-mono font-bold text-neon-blue text-lg">
                  ₹{((walletType === 'Food' ? parseFloat(member.foodBalance || 0) : parseFloat(member.gamingBalance || 0)) + numAmount * (1 + effectiveBonusPercent / 100)).toFixed(0)}
                </span>
              </div>
            </div>
          )}
        </div>

        <div className="p-4 border-t border-border bg-bg-3 flex gap-3">
          <button
            onClick={onClose}
            className="flex-1 py-2.5 rounded-lg border border-border text-text-2 text-sm font-bold uppercase tracking-wider hover:bg-bg-2 transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={handleTopUp}
            disabled={!isValid || loading}
            className={`flex-[2] py-2.5 rounded-lg border text-sm font-bold uppercase tracking-wider flex items-center justify-center gap-2 transition-all ${
              isValid && !loading
                ? 'bg-neon-blue/10 border-neon-blue/50 text-neon-blue hover:bg-neon-blue/20'
                : 'bg-bg-2 border-border text-text-3 cursor-not-allowed'
            }`}
          >
            {loading
              ? <div className="w-4 h-4 rounded-full border-2 border-current border-t-transparent animate-spin" />
              : <><ArrowUpRight className="w-4 h-4" /> Add ₹{numAmount || '–'}</>
            }
          </button>
        </div>
      </div>
    </div>
  );
}

// ═══════════════════════════════════════════════════════════════════════════════
// AdminEditValuesModal — direct override of a member's wallet balances/lifetime stats.
// Gated behind the "member_value_edit" permission (Super Admin always has it; an Admin only
// if explicitly granted via Settings → Admins).
// ═══════════════════════════════════════════════════════════════════════════════
const EDITABLE_FIELDS = [
  { key: 'gamingBalance', label: 'Gaming Wallet Balance (₹)', step: '0.01' },
  { key: 'foodBalance', label: 'Food Wallet Balance (₹)', step: '0.01' },
  { key: 'totalGamingTopUps', label: 'Total Gaming Top-Ups (₹, lifetime)', step: '0.01' },
  { key: 'totalGamingBonusEarned', label: 'Total Gaming Bonus Earned (₹, lifetime)', step: '0.01' },
  { key: 'totalGamingSpend', label: 'Total Gaming Spend (₹, lifetime)', step: '0.01' },
  { key: 'totalFoodSpend', label: 'Total Food Spend (₹, lifetime)', step: '0.01' },
  { key: 'gamingPoints', label: 'Gaming Points', step: '1' },
  { key: 'foodPoints', label: 'Food Points', step: '1' },
  { key: 'totalPoints', label: 'Total Points', step: '1' },
];

function AdminEditValuesModal({ member, onClose, onSuccess }) {
  const toast = useToast();
  const [values, setValues] = useState(() =>
    Object.fromEntries(EDITABLE_FIELDS.map(f => [f.key, member[f.key] ?? 0]))
  );
  const [reason, setReason] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const handleChange = (key, val) => setValues(prev => ({ ...prev, [key]: val }));

  const handleSave = async () => {
    setError(null);
    setLoading(true);
    try {
      const payload = {
        gamingBalance: Number(values.gamingBalance),
        foodBalance: Number(values.foodBalance),
        totalGamingTopUps: Number(values.totalGamingTopUps),
        totalGamingBonusEarned: Number(values.totalGamingBonusEarned),
        totalGamingSpend: Number(values.totalGamingSpend),
        totalFoodSpend: Number(values.totalFoodSpend),
        gamingPoints: Math.round(Number(values.gamingPoints)),
        foodPoints: Math.round(Number(values.foodPoints)),
        totalPoints: Math.round(Number(values.totalPoints)),
        reason: reason.trim() || undefined,
      };
      await adminEditMemberValues(member.id, payload);
      toast.success(`${member.fullName}'s profile values updated`);
      onSuccess();
      onClose();
    } catch (err) {
      setError(err.response?.data?.error || err.response?.data?.message || 'Failed to save changes');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm">
      <div className="w-full max-w-lg bg-bg-2 border border-neon-orange/30 rounded-xl shadow-2xl overflow-hidden">
        <div className="p-4 border-b border-border bg-bg-3 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <SlidersHorizontal className="w-5 h-5 text-neon-orange" />
            <div>
              <h2 className="font-heading font-bold text-text uppercase tracking-wider text-sm leading-none">Edit Values (Admin)</h2>
              <p className="text-[10px] text-text-3 mt-0.5 font-mono">{member.fullName} · {member.memberNumber}</p>
            </div>
          </div>
          <button onClick={onClose} className="p-1 text-text-3 hover:text-text rounded transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-5 space-y-4 max-h-[65vh] overflow-y-auto">
          <div className="flex items-start gap-2 p-3 bg-neon-orange/10 border border-neon-orange/20 rounded-lg text-neon-orange text-xs">
            <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5" />
            <p>These numbers directly override the member's profile. Balance changes are logged as a "Correction" transaction in their history so there's a paper trail.</p>
          </div>

          {error && (
            <div className="flex items-start gap-2 p-3 bg-neon-red/10 border border-neon-red/20 rounded-lg text-neon-red text-sm">
              <AlertTriangle className="w-4 h-4 shrink-0 mt-0.5" /><p>{error}</p>
            </div>
          )}

          <div className="grid grid-cols-2 gap-3">
            {EDITABLE_FIELDS.map(f => (
              <div key={f.key} className="form-group">
                <label className="block text-[10px] text-text-3 uppercase tracking-widest font-bold mb-1.5">{f.label}</label>
                <input
                  type="number" step={f.step}
                  value={values[f.key]}
                  onChange={e => handleChange(f.key, e.target.value)}
                  className="w-full bg-bg-3 border border-border text-text rounded-lg px-3 py-2 font-mono text-sm focus:border-neon-orange focus:ring-1 focus:ring-neon-orange transition-all"
                />
              </div>
            ))}
          </div>

          <div>
            <label className="block text-[10px] text-text-3 uppercase tracking-widest font-bold mb-2">Reason <span className="normal-case font-normal">(optional)</span></label>
            <input
              type="text"
              value={reason}
              onChange={e => setReason(e.target.value)}
              placeholder="e.g. Correcting a data entry mistake"
              className="w-full bg-bg-3 border border-border text-text rounded-lg px-3 py-2.5 text-sm focus:border-neon-orange focus:ring-1 focus:ring-neon-orange transition-all placeholder:text-text-3"
            />
          </div>
        </div>

        <div className="p-4 border-t border-border bg-bg-3 flex gap-3">
          <button
            onClick={onClose}
            className="flex-1 py-2.5 rounded-lg border border-border text-text-2 text-sm font-bold uppercase tracking-wider hover:bg-bg-2 transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={handleSave}
            disabled={loading}
            className="flex-[2] py-2.5 rounded-lg border text-sm font-bold uppercase tracking-wider flex items-center justify-center gap-2 transition-all bg-neon-orange/10 border-neon-orange/50 text-neon-orange hover:bg-neon-orange/20 disabled:opacity-50"
          >
            {loading
              ? <div className="w-4 h-4 rounded-full border-2 border-current border-t-transparent animate-spin" />
              : <><SlidersHorizontal className="w-4 h-4" /> Save Changes</>
            }
          </button>
        </div>
      </div>
    </div>
  );
}

// ═══════════════════════════════════════════════════════════════════════════════
// DeleteConfirmModal
// ═══════════════════════════════════════════════════════════════════════════════
function DeleteConfirmModal({ member, onClose, onConfirm }) {
  const [loading, setLoading] = useState(false);
  const toast = useToast();

  const handleDelete = async () => {
    setLoading(true);
    try {
      await deleteMember(member.id);
      toast.success(`${member.fullName} has been removed`);
      onConfirm();
    } catch (err) {
      toast.error(err.response?.data?.error || 'Failed to delete member');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/70 backdrop-blur-sm">
      <div className="w-full max-w-sm bg-bg-2 border border-neon-red/30 rounded-xl shadow-2xl overflow-hidden">
        <div className="p-5 space-y-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-full bg-neon-red/10 border border-neon-red/30 flex items-center justify-center shrink-0">
              <Trash2 className="w-5 h-5 text-neon-red" />
            </div>
            <div>
              <h3 className="font-bold text-text">Delete Member?</h3>
              <p className="text-xs text-text-3 mt-0.5">This will deactivate <span className="text-text-2 font-bold">{member.fullName}</span></p>
            </div>
          </div>
          <p className="text-sm text-text-3">
            The member will be marked as suspended and will no longer appear in active lists. This action can be reversed by an admin.
          </p>
        </div>
        <div className="p-4 border-t border-border bg-bg-3 flex gap-3">
          <button onClick={onClose} className="flex-1 py-2.5 rounded-lg border border-border text-text-2 text-sm font-bold uppercase tracking-wider hover:bg-bg-2 transition-colors">
            Cancel
          </button>
          <button
            onClick={handleDelete} disabled={loading}
            className="flex-[2] py-2.5 rounded-lg bg-neon-red/10 border border-neon-red/50 text-neon-red text-sm font-bold uppercase tracking-wider flex items-center justify-center gap-2 hover:neon-red/20 transition-colors disabled:opacity-50"
          >
            {loading
              ? <div className="w-4 h-4 rounded-full border-2 border-current border-t-transparent animate-spin" />
              : <><Trash2 className="w-4 h-4" /> Delete Member</>
            }
          </button>
        </div>
      </div>
    </div>
  );
}

// ═══════════════════════════════════════════════════════════════════════════════
// MemberDetailPanel — right side
// ═══════════════════════════════════════════════════════════════════════════════
function MemberDetailPanel({ member, onEdit, onTopUp, onEditValues, onRefresh, onDelete }) {
  const { hasDashboardAccess } = useAuth();
  const [txHistory, setTxHistory] = useState(null);
  const [txLoading, setTxLoading] = useState(false);
  useEffect(() => {
    if (!member) return;
    setTxHistory(null);
    setTxLoading(true);
    getWalletHistory(member.id)
      .then(r => setTxHistory(r?.items ?? (Array.isArray(r) ? r : [])))
      .catch(() => setTxHistory([]))
      .finally(() => setTxLoading(false));
  }, [member?.id]);

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="px-4 py-3 border-b border-border flex items-center justify-between shrink-0">
        <div className="flex items-center gap-2">
          <Users className="w-4 h-4 text-neon-green" />
          <h2 className="font-heading font-bold text-text uppercase tracking-wider text-sm">Member Detail Summary</h2>
        </div>
        <div className="flex items-center gap-0.5">
          <button onClick={onRefresh} className="p-1.5 text-text-3 hover:text-neon-blue rounded transition-colors" title="Refresh">
            <RefreshCw className="w-3.5 h-3.5" />
          </button>
          {hasDashboardAccess('member_value_edit') && (
            <button onClick={() => onEditValues(member)} className="p-1.5 text-text-3 hover:text-neon-orange rounded transition-colors" title="Edit wallet values (Admin)">
              <SlidersHorizontal className="w-3.5 h-3.5" />
            </button>
          )}
          <button onClick={() => onEdit(member)} className="p-1.5 text-text-3 hover:text-neon-purple rounded transition-colors" title="Edit member">
            <Edit2 className="w-3.5 h-3.5" />
          </button>
          <button onClick={() => onDelete(member)} className="p-1.5 text-text-3 hover:text-neon-red rounded transition-colors" title="Delete member">
            <Trash2 className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>

      {/* Body */}
      <div className="flex-1 overflow-y-auto">
        {/* Avatar & Identity */}
        <div className="flex flex-col items-center pt-4 pb-3 px-4">
          <div className="w-14 h-14 rounded-full bg-neon-green/15 border-2 border-neon-green/40 flex items-center justify-center text-2xl font-bold text-neon-green mb-2 shadow-[0_0_16px_rgba(34,211,166,0.15)]">
            {member.fullName.charAt(0).toUpperCase()}
          </div>
          <h3 className="text-base font-bold text-text leading-tight">{member.fullName}</h3>
          <p className="text-[11px] text-text-3 font-mono mt-0.5">
            {member.memberNumber}
            {member.username && <span className="text-neon-blue"> · @{member.username}</span>}
          </p>
        </div>

        {/* Wallet Cards */}
        <div className="grid grid-cols-2 gap-2.5 px-4 mb-3">
          <div className="bg-bg-3 border border-border rounded-xl p-3 text-center">
            <p className="text-[9px] text-text-3 uppercase tracking-widest font-bold">Gaming Wallet</p>
            <p className="font-mono font-bold text-2xl text-neon-blue mt-1 drop-shadow-[0_0_10px_rgba(77,166,255,0.4)]">
              ₹{parseFloat(member.gamingBalance || 0).toFixed(0)}
            </p>
            {parseFloat(member.totalGamingBonusEarned || 0) > 0 && (
              <p className="text-[9px] text-text-3 font-mono mt-1">
                ₹{parseFloat(member.totalGamingTopUps || 0).toFixed(0)} topped up
                <span className="text-neon-orange"> + ₹{parseFloat(member.totalGamingBonusEarned || 0).toFixed(0)} bonus</span>
              </p>
            )}
          </div>
          <div className="bg-bg-3 border border-border rounded-xl p-3 text-center">
            <p className="text-[9px] text-text-3 uppercase tracking-widest font-bold">Food Wallet</p>
            <p className="font-mono font-bold text-2xl text-neon-orange mt-1 drop-shadow-[0_0_10px_rgba(255,140,66,0.4)]">
              ₹{parseFloat(member.foodBalance || 0).toFixed(0)}
            </p>
          </div>
        </div>

        {/* Contact row */}
        <div className="mx-4 mb-3 bg-bg-3 border border-border rounded-xl px-3 py-2.5">
          <p className="text-[9px] text-text-3 uppercase tracking-widest font-bold mb-1">Phone Number</p>
          <p className="font-mono text-text font-bold">{member.mobileNumber}</p>
          {member.email ? (
            <p className="text-[11px] text-text-3 mt-0.5">{member.email}</p>
          ) : (
            <p className="text-[11px] text-yellow-500 mt-0.5 flex items-center gap-1">
              <AlertTriangle className="w-3 h-3" /> Missing Email
            </p>
          )}
          <p className="text-[10px] text-text-3 mt-1">
            Joined {relTime(member.joinDate)} · Last visit {relTime(member.lastVisit)}
          </p>
        </div>

        {/* Primary CTA */}
        <div className="px-4 mb-3">
          <button
            onClick={() => { member.tempTarget = 'Gaming'; onTopUp(member); }}
            className="w-full py-3.5 rounded-xl bg-neon-green text-bg font-bold text-sm uppercase tracking-widest flex items-center justify-center gap-2 hover:brightness-110 transition-all shadow-[0_0_20px_rgba(34,211,166,0.2)]"
          >
            <Receipt className="w-4 h-4" /> Process Wallet Top-Up
          </button>
        </div>

        {/* Login credentials — compact */}
        <div className="mx-4 mb-3 bg-bg-3 border border-border rounded-xl px-3 py-2.5">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <KeyRound className="w-3.5 h-3.5 text-text-3" />
              <span className="text-[11px] text-text-2 font-bold uppercase tracking-wider">Login Access</span>
              {member.username
                ? <span className="font-mono text-[11px] text-text-3">· @{member.username}</span>
                : <span className="text-[11px] text-text-3">· Not set</span>
              }
            </div>
            <div className="flex items-center gap-1.5">
              {member.username
                ? <ShieldCheck className="w-3.5 h-3.5 text-neon-blue" />
                : <ShieldAlert className="w-3.5 h-3.5 text-text-3" />
              }
              <button
                onClick={() => onEdit(member)}
                className="text-[10px] text-neon-blue font-bold uppercase tracking-wider hover:underline"
              >
                {member.username ? 'Change' : 'Set Up'}
              </button>
            </div>
          </div>
        </div>

        {/* Transaction History */}
        <div className="px-4 pb-4">
          <p className="text-[9px] text-text-3 uppercase tracking-widest font-bold mb-2">Transaction History</p>
          {txLoading ? (
            <div className="flex justify-center py-6">
              <div className="w-5 h-5 rounded-full border-2 border-neon-green border-t-transparent animate-spin" />
            </div>
          ) : !txHistory || txHistory.length === 0 ? (
            <div className="text-center py-8 text-text-3 text-sm bg-bg-3 border border-border rounded-xl">No transactions yet</div>
          ) : (
            <div className="space-y-1.5">
              {txHistory.slice(0, 25).map(tx => (
                <div key={tx.id} className="flex items-center justify-between bg-bg-3 border border-border rounded-lg px-3 py-2">
                  <div className="flex items-center gap-2.5 min-w-0">
                    <div className={`p-1 rounded ${tx.action === 'Deduction' ? 'bg-neon-red/10' : 'bg-neon-green/10'}`}>
                      {tx.action === 'Deduction'
                        ? <ArrowDownRight className="w-3.5 h-3.5 text-neon-red" />
                        : <ArrowUpRight className="w-3.5 h-3.5 text-neon-green" />
                      }
                    </div>
                    <div className="min-w-0">
                      <ActionBadge action={tx.action} />
                      {tx.reason && <p className="text-[10px] text-text-3 truncate mt-0.5">{tx.reason}</p>}
                    </div>
                  </div>
                  <div className="text-right shrink-0 ml-2">
                    <p className={`font-mono font-bold text-sm ${tx.action === 'Deduction' ? 'text-neon-red' : 'text-neon-green'}`}>
                      {tx.action === 'Deduction' ? '−' : '+'}₹{tx.amount + (tx.bonusAmount || 0)}
                    </p>
                    {tx.bonusAmount > 0 && (
                      <p className="text-[10px] text-neon-orange font-mono">
                        {tx.amount > 0 ? `₹${tx.amount} + ` : ''}₹{tx.bonusAmount} bonus
                      </p>
                    )}
                    <p className="text-[10px] text-text-3 font-mono">
                      {new Date(tx.createdAt).toLocaleDateString('en-IN', { day: '2-digit', month: 'short' })}
                    </p>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// ─── Member row card ──────────────────────────────────────────────────────────
function MemberRow({ member, selected, onClick }) {
  return (
    <button
      onClick={onClick}
      className={`w-full text-left px-3 py-2.5 rounded-lg border transition-all ${
        selected
          ? 'bg-accent/10 border-accent/40 shadow-[0_0_0_1px_rgba(220,38,38,0.12)]'
          : 'bg-bg-3 border-border hover:bg-bg-2'
      }`}
    >
      <div className="flex items-center gap-2.5">
        <div className={`w-8 h-8 rounded-full flex items-center justify-center shrink-0 font-bold text-sm ${
          selected ? 'bg-accent/20 text-accent' : 'bg-bg-2 text-text-2 border border-border'
        }`}>
          {member.fullName.charAt(0).toUpperCase()}
        </div>
        <div className="flex-1 min-w-0">
          <p className="font-bold text-text text-sm truncate leading-tight">{member.fullName}</p>
          <div className="text-[10px] text-text-3 font-mono mt-0.5 flex flex-wrap items-center gap-1">
            <span>{member.memberNumber} · {member.mobileNumber}</span>
            {member.username && <span className="text-neon-blue"> · @{member.username}</span>}
            {member.homeBranchName && (
              <span className="bg-neon-blue/20 text-neon-blue border border-neon-blue/40 rounded px-1.5 py-0.5 ml-auto shrink-0 truncate max-w-[120px] text-[10px] uppercase tracking-wider font-bold">
                HOME: {member.homeBranchName}
              </span>
            )}
          </div>
          <div className="flex items-center gap-2 mt-1">
            <span className="inline-flex items-center gap-0.5 font-mono text-[11px] font-bold text-neon-blue">
              <Gamepad2 className="w-3 h-3" /> ₹{parseFloat(member.gamingBalance || 0).toFixed(0)}
            </span>
            <span className="inline-flex items-center gap-0.5 font-mono text-[11px] font-bold text-neon-orange">
              <Coffee className="w-3 h-3" /> ₹{parseFloat(member.foodBalance || 0).toFixed(0)}
            </span>
          </div>
          {parseFloat(member.totalGamingBonusEarned || 0) > 0 && (
            <p className="text-[9px] text-text-3 font-mono mt-0.5">
              ₹{parseFloat(member.totalGamingTopUps || 0).toFixed(0)} topped up
              <span className="text-neon-orange"> + ₹{parseFloat(member.totalGamingBonusEarned || 0).toFixed(0)} bonus</span>
            </p>
          )}
        </div>
      </div>
    </button>
  );
}

// ═══════════════════════════════════════════════════════════════════════════════
// MembersPage — main export
// ═══════════════════════════════════════════════════════════════════════════════
export default function MembersPage() {
  const { isSuperAdmin, user } = useAuth();
  const { activeBranch } = useBranch();
  const targetBranchId = isSuperAdmin ? activeBranch?.id : user?.branchId;
  const toast = useToast();

  const [members, setMembers] = useState([]);
  const [pagination, setPagination] = useState({ page: 1, totalCount: 0, totalPages: 1 });
  const [search, setSearch] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [downloadingReport, setDownloadingReport] = useState(false);

  const [selectedMember, setSelectedMember] = useState(null);
  const [showRegister, setShowRegister] = useState(false);
  const [editMember, setEditMember] = useState(null);
  const [topUpMember, setTopUpMember] = useState(null);
  const [editValuesMember, setEditValuesMember] = useState(null);
  const [deletingMember, setDeletingMember] = useState(null);

  const searchTimer = useRef(null);

  const fetchMembers = useCallback(async (pg = 1, q = search) => {
    // Global fetch allowed for members
    setIsLoading(true);
    try {
      const res = await getMembers(targetBranchId, q || undefined, pg, 100);
      const items = res?.items ?? (Array.isArray(res) ? res : []);
      // Ensure we don't show suspended members in case backend cache hasn't cleared
      setMembers(items.filter(m => m.status !== 1 && m.status !== 'Suspended'));
      setPagination({
        page: pg,
        totalCount: res?.totalCount ?? 0,
        totalPages: res?.totalPages ?? 1,
      });
    } catch (err) {
      console.error("Failed to fetch members:", err);
      // Do not wipe out the existing members list on network errors
      // so the user doesn't see a sudden blank screen.
    } finally {
      setIsLoading(false);
    }
  }, [isSuperAdmin, targetBranchId, search]);

  useEffect(() => { fetchMembers(1); }, [targetBranchId]);

  const handleSearch = (val) => {
    setSearch(val);
    clearTimeout(searchTimer.current);
    searchTimer.current = setTimeout(() => fetchMembers(1, val), 400);
  };

  const refreshSelected = async () => {
    if (!selectedMember) return;
    try {
      const updated = await getMemberById(selectedMember.id);
      setSelectedMember(updated);
      setMembers(prev => prev.map(m => m.id === updated.id ? updated : m));
    } catch {}
  };

  const csvEscape = (val) => {
    const s = val === null || val === undefined ? '' : String(val);
    return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };

  const handleDownloadReport = async () => {
    setDownloadingReport(true);
    try {
      // includeDeleted=true so Super Admin/Admin also get every past (deleted) member in the report
      const res = await getMembers(targetBranchId, undefined, 1, 100000, true);
      const rows = res?.items ?? (Array.isArray(res) ? res : []);

      const columns = [
        ['Member Number', m => m.memberNumber],
        ['Full Name', m => m.fullName],
        ['Mobile Number', m => m.mobileNumber],
        ['Email', m => m.email],
        ['Username', m => m.username],
        ['Status', m => m.status],
        ['Home Branch', m => m.homeBranchName],
        ['Gaming Balance', m => m.gamingBalance],
        ['Food Balance', m => m.foodBalance],
        ['Total Gaming Top-Ups', m => m.totalGamingTopUps],
        ['Total Gaming Bonus Earned', m => m.totalGamingBonusEarned],
        ['Total Gaming Spend', m => m.totalGamingSpend],
        ['Total Food Spend', m => m.totalFoodSpend],
        ['Gaming Points', m => m.gamingPoints],
        ['Food Points', m => m.foodPoints],
        ['Total Points', m => m.totalPoints],
        ['Join Date', m => m.joinDate ? new Date(m.joinDate).toLocaleString('en-IN') : ''],
        ['Last Visit', m => m.lastVisit ? new Date(m.lastVisit).toLocaleString('en-IN') : ''],
      ];

      const header = columns.map(([label]) => csvEscape(label)).join(',');
      const lines = rows.map(m => columns.map(([, get]) => csvEscape(get(m))).join(','));
      const csv = [header, ...lines].join('\n');

      const blob = new Blob([`﻿${csv}`], { type: 'text/csv;charset=utf-8;' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `members-report-${new Date().toISOString().slice(0, 10)}.csv`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
      toast.success(`Report downloaded (${rows.length} members)`);
    } catch (err) {
      toast.error('Failed to generate report');
    } finally {
      setDownloadingReport(false);
    }
  };

  if (isSuperAdmin && !targetBranchId) {
    return (
      <div className="flex items-center justify-center h-64 text-text-3 text-sm">
        Select a branch to view members
      </div>
    );
  }

  return (
    <div className="flex h-[calc(100vh-4rem)] overflow-hidden gap-3 p-3">
      {/* Left: member list */}
      <div className="flex flex-col w-[52%] min-w-[380px] bg-bg-2 border border-border rounded-xl shadow-lg overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-4 py-3 border-b border-border bg-bg-3/50">
          <div className="flex items-center gap-2">
            <Users className="w-4 h-4 text-accent" />
            <h2 className="font-heading font-bold text-text uppercase tracking-wider text-sm">Registered Members</h2>
          </div>
          <span className="px-2 py-0.5 rounded-full bg-accent/10 border border-accent/30 text-accent text-[10px] font-bold font-mono">
            {pagination.totalCount} total
          </span>
        </div>

        {/* Search + Add */}
        <div className="flex items-center gap-2 px-3 py-2.5 border-b border-border">
          <div className="relative flex-1">
            <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-text-3" />
            <input
              type="text"
              value={search}
              onChange={e => handleSearch(e.target.value)}
              placeholder="Search members by name, phone..."
              className="w-full bg-bg-3 border border-border text-text text-sm rounded-lg pl-8 pr-3 py-2 focus:border-accent focus:ring-1 focus:ring-accent transition-all placeholder:text-text-3"
            />
          </div>
          <button
            onClick={handleDownloadReport}
            disabled={downloadingReport}
            title="Download a full report of every member (including past/deleted members)"
            className="flex items-center gap-1.5 px-3 py-2 rounded-lg bg-bg-3 border border-border text-text-2 text-xs font-bold uppercase tracking-wider hover:border-neon-blue/50 hover:text-neon-blue transition-colors shrink-0 disabled:opacity-50"
          >
            {downloadingReport
              ? <div className="w-3.5 h-3.5 rounded-full border-2 border-current border-t-transparent animate-spin" />
              : <Download className="w-3.5 h-3.5" />
            } Report
          </button>
          <button
            onClick={() => { setEditMember(null); setShowRegister(true); }}
            className="flex items-center gap-1.5 px-3 py-2 rounded-lg bg-accent/10 border border-accent/40 text-accent text-xs font-bold uppercase tracking-wider hover:bg-accent/20 transition-colors shrink-0"
          >
            <Plus className="w-3.5 h-3.5" /> Add Member
          </button>
        </div>

        {/* List */}
        <div className="flex-1 overflow-y-auto px-3 py-2 space-y-1">
          {isLoading ? (
            <div className="flex justify-center py-12">
              <div className="w-6 h-6 rounded-full border-2 border-accent border-t-transparent animate-spin" />
            </div>
          ) : members.length === 0 ? (
            <div className="text-center py-16">
              <Users className="w-12 h-12 text-text-3 mx-auto mb-3 opacity-40" />
              <p className="text-text-2 font-bold">{search ? 'No members found' : 'No members yet'}</p>
              <p className="text-text-3 text-sm mt-1">
                {search ? 'Try a different search' : 'Click Add Member to get started'}
              </p>
            </div>
          ) : (
            members.map(m => (
              <MemberRow
                key={m.id}
                member={m}
                selected={selectedMember?.id === m.id}
                onClick={() => setSelectedMember(m)}
              />
            ))
          )}
        </div>
      </div>

      {/* Right: detail panel */}
      <div className="flex-1 min-w-0 bg-bg-2 border border-border rounded-xl shadow-lg overflow-hidden">
        {selectedMember ? (
          <MemberDetailPanel
            member={selectedMember}
            onEdit={m => { setEditMember(m); setShowRegister(true); }}
            onTopUp={setTopUpMember}
            onEditValues={setEditValuesMember}
            onRefresh={refreshSelected}
            onDelete={setDeletingMember}
          />
        ) : (
          <div className="flex flex-col items-center justify-center h-full text-text-3">
            <Users className="w-10 h-10 mb-2 opacity-30" />
            <p className="font-heading uppercase tracking-widest text-xs">Select a member to view</p>
            <p className="text-[10px] mt-0.5 font-mono text-text-3/50">Click any member on the left</p>
          </div>
        )}
      </div>

      {/* Register / Edit drawer */}
      <RegisterDrawer
        open={showRegister}
        onClose={() => { setShowRegister(false); setEditMember(null); }}
        onSuccess={(savedMember) => {
          fetchMembers(1, search);
          // Immediately update the selected panel with returned data — no extra API call needed
          if (savedMember) {
            setSelectedMember(savedMember);
            setMembers(prev => prev.map(m => m.id === savedMember.id ? savedMember : m));
          }
        }}
        editMember={editMember}
      />

      {/* Top-up modal */}
      {topUpMember && (
        <TopUpModal
          member={topUpMember}
          isSuperAdmin={isSuperAdmin}
          onClose={() => setTopUpMember(null)}
          onSuccess={() => {
            fetchMembers(pagination.page, search);
            refreshSelected();
          }}
        />
      )}

      {/* Admin Edit Values modal */}
      {editValuesMember && (
        <AdminEditValuesModal
          member={editValuesMember}
          onClose={() => setEditValuesMember(null)}
          onSuccess={() => {
            fetchMembers(pagination.page, search);
            refreshSelected();
          }}
        />
      )}

      {/* Delete confirm modal */}
      {deletingMember && (
        <DeleteConfirmModal
          member={deletingMember}
          onClose={() => setDeletingMember(null)}
          onConfirm={() => {
            setMembers(prev => prev.filter(m => m.id !== deletingMember.id));
            setDeletingMember(null);
            setSelectedMember(null);
            fetchMembers(1, search);
          }}
        />
      )}
    </div>
  );
}
