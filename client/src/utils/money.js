// Money is stored/calculated to the paisa internally (e.g. 20-minute session at ₹50/hr = ₹16.67),
// but displayed everywhere as a clean whole rupee figure — no one wants to read ₹16.666666667.
export function formatMoney(amount) {
  const n = Number(amount);
  if (!Number.isFinite(n)) return '0';
  return String(Math.round(n));
}
