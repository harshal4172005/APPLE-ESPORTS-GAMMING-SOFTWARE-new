// Mirrors AppleEsportsErp.Application.Constants.MemberWalletRules (backend) so the overlay
// stops/warns at exactly the same balances the server refuses to start a session at.
// Change both together.

// A member session cannot start below this Gaming balance — and is auto-stopped once the
// remaining balance falls under it, so a session can never start only to stop immediately.
export const MIN_GAMING_BALANCE_TO_START = 1;

// Superseded by affordableMinutes() below and no longer used to stop anything. Kept only so the
// mirror with the backend constant stays obvious. Do not stop a session on this: "remaining
// balance is nearly zero" ignores the rounding applied before the wallet is deducted, which is
// how members ended up owing a few rupees for being stopped over running out of money.
export const AUTO_STOP_REMAINING_BALANCE = 1;

// Two reminders, in the order they fire. The first also triggers the member email +
// operator alert on the backend.
export const FIRST_REMINDER_REMAINING_BALANCE = 20;
export const SECOND_REMINDER_REMAINING_BALANCE = 10;

// Minutes of play a remaining balance still buys at the PC's hourly rate.
export function minutesRemainingFor(remainingBalance, ratePerHour) {
  const rate = Number(ratePerHour) || 0;
  const remaining = Number(remainingBalance) || 0;
  if (rate <= 0 || remaining <= 0) return 0;
  return Math.max(0, Math.floor((remaining / rate) * 60));
}

// ── The stopping point ──
//
// Mirrors SessionPricingCalculator.RoundBillTotal and .AffordableMinutes on the server. Both
// sides have to stop a session at the same moment: the overlay stops it while the member's PC is
// running, and the server's monitor is the backstop for when that PC is closed, asleep or off the
// network. If they disagree, whichever fires first decides, and one of them is wrong.
//
// Change these together with the C# versions.

// A bill is rounded to the nearest 10 rupees BEFORE the wallet is deducted, down for a remainder
// of 0-4 and up for 5-9 (standard round-half-up). That rounding is why stopping "when the balance
// is nearly used up" is not enough: a member with Rs 27 stopped at Rs 26 of play is billed Rs 30
// and ends up owing Rs 3, having been stopped for running out of money.
export function roundBillTotal(amount) {
  const value = Number(amount) || 0;
  if (value <= 0) return 0;
  const remainder = value % 10;
  return remainder < 5 ? value - remainder : value + (10 - remainder);
}

// The elapsed minutes at which to stop, so the member is charged no more than they hold.
// safetyRupees leaves room for the stop arriving a moment late.
export function affordableMinutes(ratePerHour, bufferMinutes, balance, safetyRupees = 1) {
  const rate = Number(ratePerHour) || 0;
  const buffer = Number(bufferMinutes) || 0;
  const bal = Number(balance) || 0;

  // Nothing to run out of on a PC that bills nothing.
  if (rate <= 0) return Infinity;
  if (bal <= 0) return buffer;

  // Starts above the balance because rounding can come down by as much as 4 rupees, so play
  // worth Rs 14 is charged Rs 10. The +5 headroom here is deliberately more than that.
  let raw = bal + 5;
  while (raw > 0 && roundBillTotal(raw + safetyRupees) > bal) raw -= 0.5;

  const safetyMinutes = (safetyRupees * 60) / rate;
  const affordable = raw <= 0 ? 0 : (raw * 60) / rate;

  // The free buffer ends in a cliff: the instant it expires the whole elapsed time becomes
  // billable at once, not just the part beyond it. A member who cannot afford that first
  // chargeable moment has to be stopped short of the edge, not on it.
  if (affordable <= buffer) return Math.max(0, buffer - safetyMinutes);

  return affordable;
}
