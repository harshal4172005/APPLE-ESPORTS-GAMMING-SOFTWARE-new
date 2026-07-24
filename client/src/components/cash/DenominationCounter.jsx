import { useState, useMemo } from 'react';
import { Calculator, AlertTriangle, ShieldCheck } from 'lucide-react';
import api from '../../config/api';
import { generateIdempotencyKey } from '../../utils/idempotency';

const DENOMINATIONS = [
  { key: 'notes2000', label: '₹2000', value: 2000, type: 'note' },
  { key: 'notes500', label: '₹500', value: 500, type: 'note' },
  { key: 'notes200', label: '₹200', value: 200, type: 'note' },
  { key: 'notes100', label: '₹100', value: 100, type: 'note' },
  { key: 'notes50', label: '₹50', value: 50, type: 'note' },
  { key: 'notes20', label: '₹20', value: 20, type: 'note' },
  { key: 'notes10', label: '₹10', value: 10, type: 'note' },
  { key: 'coins5', label: '₹5', value: 5, type: 'coin' },
  { key: 'coins2', label: '₹2', value: 2, type: 'coin' },
  { key: 'coins1', label: '₹1', value: 1, type: 'coin' },
];

export default function DenominationCounter({ expectedTotal, onVerified }) {
  const [counts, setCounts] = useState({
    notes2000: '', notes500: '', notes200: '', notes100: '',
    notes50: '', notes20: '', notes10: '',
    coins5: '', coins2: '', coins1: ''
  });
  const [mismatchReason, setMismatchReason] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const handleCountChange = (key, value) => {
    // Only allow positive integers
    const num = value.replace(/[^0-9]/g, '');
    setCounts(prev => ({ ...prev, [key]: num }));
  };

  const countedTotal = useMemo(() => {
    return DENOMINATIONS.reduce((sum, den) => {
      const count = parseInt(counts[den.key] || '0', 10);
      return sum + (count * den.value);
    }, 0);
  }, [counts]);

  const variance = countedTotal - expectedTotal;
  const isExactMatch = variance === 0;

  const handleSubmit = async () => {
    if (!isExactMatch && !mismatchReason.trim()) {
      setError("A reason must be provided for any cash discrepancy.");
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const payload = {
        notes2000: parseInt(counts.notes2000 || '0', 10),
        notes500: parseInt(counts.notes500 || '0', 10),
        notes200: parseInt(counts.notes200 || '0', 10),
        notes100: parseInt(counts.notes100 || '0', 10),
        notes50: parseInt(counts.notes50 || '0', 10),
        notes20: parseInt(counts.notes20 || '0', 10),
        notes10: parseInt(counts.notes10 || '0', 10),
        coins5: parseInt(counts.coins5 || '0', 10),
        coins2: parseInt(counts.coins2 || '0', 10),
        coins1: parseInt(counts.coins1 || '0', 10),
        mismatchReason: isExactMatch ? undefined : mismatchReason.trim()
      };

      await api.post('/cash-desk/denominations', payload, {
        headers: { 'X-Idempotency-Key': generateIdempotencyKey() }
      });
      onVerified();
    } catch (err) {
      setError(err.response?.data?.error || err.response?.data?.message || 'Failed to submit denominations.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex flex-col h-full">
      <div className="bg-bg-3 p-4 rounded-xl border border-border mb-4 flex items-center justify-between shadow-lg shadow-black/20">
        <div>
          <div className="text-[10px] uppercase font-bold text-text-3 tracking-widest mb-1 flex items-center gap-2">
            <ShieldCheck className="w-4 h-4 text-accent" /> Frozen Snapshot Total
          </div>
          <div className="text-3xl font-mono font-bold text-accent">₹{expectedTotal}</div>
        </div>
        <div className="text-right">
          <div className="text-[10px] uppercase font-bold text-text-3 tracking-widest mb-1">
            Physical Count
          </div>
          <div className="text-3xl font-mono font-bold text-text">₹{countedTotal}</div>
        </div>
        <div className="text-right border-l border-border pl-6">
          <div className="text-[10px] uppercase font-bold text-text-3 tracking-widest mb-1">
            Variance
          </div>
          <div className={`text-3xl font-mono font-bold ${
            isExactMatch ? 'text-neon-blue drop-shadow-[0_0_10px_rgba(0,255,255,0.3)]' :
            variance > 0 ? 'text-neon-purple drop-shadow-[0_0_10px_rgba(153,51,255,0.3)]' :
            'text-neon-red drop-shadow-[0_0_10px_rgba(255,51,102,0.3)]'
          }`}>
            {variance > 0 ? '+' : ''}₹{variance}
          </div>
        </div>
      </div>

      {error && (
        <div className="p-3 mb-4 bg-neon-red/10 border border-neon-red/30 rounded-lg text-neon-red text-sm flex items-center gap-2">
          <AlertTriangle className="w-4 h-4 shrink-0" />
          <p>{error}</p>
        </div>
      )}

      <div className="flex-1 overflow-y-auto mb-4 border border-border rounded-xl bg-bg-2 p-4">
        <h3 className="text-xs uppercase font-bold text-text-2 tracking-wider mb-4 border-b border-border pb-2 flex items-center gap-2">
          <Calculator className="w-4 h-4" /> Denomination Quantities
        </h3>
        
        <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
          {DENOMINATIONS.map(den => (
            <div key={den.key} className="bg-bg-3 border border-border rounded-lg p-2 flex items-center gap-3">
              <label className="text-sm font-bold text-text-2 w-12 text-right shrink-0">
                {den.label}
              </label>
              <div className="text-text-3 font-bold text-sm">×</div>
              <input
                type="text"
                inputMode="numeric"
                placeholder="0"
                value={counts[den.key]}
                onChange={e => handleCountChange(den.key, e.target.value)}
                className="w-full bg-bg-2 border border-border rounded text-text font-mono text-center py-1.5 focus:border-accent focus:ring-1 focus:ring-accent outline-none"
              />
            </div>
          ))}
        </div>

        {/* Discrepancy Reason Panel */}
        {!isExactMatch && (
          <div className="mt-6 p-4 bg-neon-red/5 border border-neon-red/20 rounded-xl">
            <label className="flex items-center gap-2 text-neon-red text-xs font-bold uppercase tracking-wider mb-2">
              <AlertTriangle className="w-4 h-4" /> Discrepancy Reason Required
            </label>
            <input
              type="text"
              placeholder="Explain the surplus/shortage..."
              value={mismatchReason}
              onChange={e => setMismatchReason(e.target.value)}
              className="w-full bg-bg-2 border border-neon-red/30 text-text rounded-lg p-3 focus:border-neon-red outline-none text-sm"
            />
          </div>
        )}
      </div>

      <button
        onClick={handleSubmit}
        disabled={loading || (!isExactMatch && !mismatchReason.trim())}
        className="w-full py-4 rounded-xl font-bold uppercase tracking-widest text-sm transition-all btn-primary shadow-lg shadow-accent/20 disabled:opacity-50 disabled:cursor-not-allowed flex justify-center items-center gap-2"
      >
        {loading ? (
          <div className="w-5 h-5 rounded-full border-2 border-current border-t-transparent animate-spin" />
        ) : (
          <>Submit Physical Count</>
        )}
      </button>
    </div>
  );
}
