// Turns one audit row into a plain-English sentence, so the Audit Trail screen reads like a
// story of the day instead of a table of codes and JSON.
//
// Deliberately not exhaustive. The action types below cover what actually happens dozens of
// times a day - sessions, payments, wallets, members, logins, remote commands. Anything else
// falls back to a readable title made from its own action code plus its raw details, which is
// less polished but never blank and never wrong.

const money = (v) => (v === undefined || v === null || v === '') ? '?' : `₹${Number(v).toFixed(0)}`;

const REMOTE_COMMAND_LABELS = {
  stop_session: 'stop a session',
  start_session: 'start a session',
  transfer_session: 'move a session to another PC',
  set_pc_state: "change a PC's status",
};

// One function per action code. Each takes the row's own `details` (already-parsed JSON,
// possibly null) and returns the sentence's body - the subject ("X ") is added by the caller,
// since who did it is shown as its own column and repeating the name in every sentence would
// just be noise.
const SUMMARIES = {
  login: () => 'logged in',
  logout: () => 'logged out',
  failed_login: (d) => `tried to log in and failed${d?.reason ? ` (${d.reason})` : ''}`,
  account_locked: (d) => `was locked out${d?.reason ? ` — ${d.reason}` : ''}`,
  password_reset: () => 'reset their password',
  forced_logout: () => 'was signed out by an admin',
  admin_switch_in: (d) => `switched into ${d?.operatorName ?? 'an operator'}'s session`,
  admin_switch_out: () => 'ended an admin override session',

  session_start: (d) => `started a session on ${d?.PcNumber ?? 'a PC'}${d?.DurationMinutes ? ` for ${d.DurationMinutes} min` : ''}${d?.ExpectedAmount ? `, ${money(d.ExpectedAmount)}` : ''}`,
  session_stop: (d) => `stopped the session on ${d?.PcNumber ?? 'a PC'}${d?.TotalAmount !== undefined ? ` — billed ${money(d.TotalAmount)}` : ''}`,
  session_extend: (d) => `extended ${d?.PcNumber ?? 'a PC'} by ${d?.AdditionalMinutes ?? '?'} min (+${money(d?.AdditionalAmount)})`,
  session_transfer: (d) => `moved a session from ${d?.from ?? '?'} to ${d?.to ?? '?'}`,

  reservation_create: (d) => `booked ${d?.PcNumber ?? 'a PC'} for ${d?.CustomerName ?? 'a customer'}`,
  reservation_cancel: (d) => `cancelled a reservation${d?.Reason ? ` (${d.Reason})` : ''}`,
  reservation_override: () => 'overrode a reservation hold to start a walk-in session',
  reservation_expire: (d) => `let a reservation expire${d?.PcNumber ? ` on ${d.PcNumber}` : ''}`,

  bill_create: () => 'opened a bill',
  bill_complete: (d) => `completed bill ${d?.BillNumber ?? ''}`.trim(),
  payment_process: (d) => `took a payment — ${d?.PaymentType ?? 'payment'}, ${money(d?.Total)}`,
  discount_apply: (d) => `applied a ${d?.DiscountType === 'Percentage' ? `${d?.Value}%` : money(d?.Value)} discount${d?.Reason ? ` (${d.Reason})` : ''}`,
  credit_clear: (d) => `cleared ${d?.CustomerName ?? 'a'}'s credit — ${money(d?.Amount)}${d?.PaymentType ? ` (${d.PaymentType})` : ''}`,

  food_order_place: (d) => `placed food order ${d?.OrderNumber ?? ''} — ${d?.ItemCount ?? '?'} item(s), ${money(d?.Total)}`.trim(),
  food_order_status_change: (d) => `marked a food order ${(d?.Status ?? 'updated').toLowerCase()}${d?.Reason ? ` (${d.Reason})` : ''}`,

  cash_opening: () => 'opened the cash drawer',
  cash_verification: () => 'counted the cash drawer',
  cash_mismatch: (d) => `found the drawer ${d?.difference ? `off by ${money(Math.abs(d.difference))}` : 'did not match'}`,
  denomination_count: () => 'counted the drawer\'s notes and coins',

  member_create: (d) => `registered member ${d?.FullName ?? ''} (${d?.MemberNumber ?? '?'})`,
  wallet_recharge: (d) => `topped up Member Amount by ${money(d?.Amount)}${d?.PaymentType ? ` (${d.PaymentType})` : ''}`,
  wallet_deduction: (d) => `deducted ${money(d?.Amount)} from Member Amount${d?.Reason ? ` (${d.Reason})` : ''}`,
  points_redeem: (d) => `redeemed ${d?.Points ?? ''} loyalty points`.trim(),

  operator_create: (d) => `added operator ${d?.FullName ?? ''}`.trim(),
  operator_remove: (d) => `removed operator ${d?.FullName ?? ''}`.trim(),
  operator_suspend: (d) => `suspended operator ${d?.FullName ?? ''}`.trim(),
  access_grant: (d) => `granted ${d?.Dashboard ?? 'a permission'}`,
  access_revoke: (d) => `revoked ${d?.Dashboard ?? 'a permission'}`,

  stock_refill: (d) => `refilled stock on ${d?.ItemName ?? 'an item'}`,
  price_change: (d) => `changed the price of ${d?.ItemName ?? 'an item'}${d?.NewPrice ? ` to ${money(d.NewPrice)}` : ''}`,
  item_disable: (d) => `took ${d?.ItemName ?? 'an item'} off the menu`,
  wastage_log: (d) => `logged wastage on ${d?.ItemName ?? 'an item'}`,

  shift_start: () => 'started a shift',
  shift_end: () => 'ended a shift',
  shift_takeover: (d) => `closed a shift left open by ${d?.previousOperatorName ?? 'someone else'} and counted its drawer`,
  eod_finalize: () => 'finalised End of Day',
  force_close: (d) => `force-closed a shift${d?.reason ? ` (${d.reason})` : ''}`,
  settings_change: (d) => `changed a setting${d?.Key ? `: ${d.Key}` : ''}`,

  // Written by Head Office itself, from the branch's own heartbeat - not by anyone taking an
  // action. If a PC's own "pc_shutdown" row (written at the branch, by whoever pressed the
  // button) has no matching row like this one for the same PC around the same time, that gap
  // is the sync problem itself: the branch said it, and Head Office never heard it.
  pc_powered_off_synced: (d) => d?.poweredOff
    ? `Head Office confirmed ${d?.pcNumber ?? 'a PC'} is shut down (via the branch's heartbeat)`
    : `Head Office confirmed ${d?.pcNumber ?? 'a PC'} powered back on (via the branch's heartbeat)`,

  remote_command_issued: (d) => {
    const label = REMOTE_COMMAND_LABELS[d?.commandType] ?? d?.commandType ?? 'do something';

    // Two different rows share this one action code, and they read differently on purpose:
    // the row written the moment a super admin presses the button ("asked a branch to..."),
    // and the row written later when the branch's own answer comes back ("succeeded" /
    // "failed" — closed by BranchHeartbeatController.LogCommandOutcomeAsync, not by whoever
    // asked). `outcome` is only present on the second kind.
    if (d?.outcome) {
      const verb = d.outcome === 'succeeded' ? 'went through' : 'failed';
      const reason = d?.message ? ` — ${d.message}` : '';
      return `asked a branch, from Head Office, to ${label}, which ${verb}${reason}`;
    }

    const status = d?.branchReporting === false ? ' (branch was offline at the time)' : '';
    return `asked a branch, from Head Office, to ${label}${status}`;
  },
};

// A failure row carries a completely different Details shape than a success row for the same
// action - LogSessionFailureAsync writes only `{ error }`, where a successful session_start
// writes PcNumber/DurationMinutes/ExpectedAmount. Running a failure through SUMMARIES.session_start
// would read the wrong fields and print "started a session on a PC for undefined min", so
// failures get their own map instead of trying to reuse the success one.
const FAILURE_LABELS = {
  session_start: 'start a session',
  session_stop: 'stop a session',
  session_resume: 'resume a session',
  session_extend: 'extend a session',
  session_transfer: 'move a session to another PC',
  food_order_place: 'place a food order',
  food_order_status_change: 'update a food order',
};

const FAILURE_SUMMARIES = {
  ...Object.fromEntries(Object.entries(FAILURE_LABELS).map(([action, label]) => [
    action,
    (d) => `tried to ${label} and it failed${d?.error ? ` — ${d.error}` : ''}`,
  ])),
};

const titleCase = (action) =>
  action.replace(/_/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());

/** Parses the row's Details JSON once, tolerating null/invalid without throwing. */
export function parseDetails(rawDetails) {
  if (!rawDetails) return null;
  try {
    return JSON.parse(rawDetails);
  } catch {
    return null;
  }
}

/**
 * One plain-English sentence body ("started a session on...") for a row. Never throws.
 * `success` picks which map of sentences to read from - see FAILURE_SUMMARIES above for why
 * a failed attempt cannot just reuse the successful one's wording.
 */
export function summarize(action, details, success = true) {
  const table = success === false && FAILURE_SUMMARIES[action] ? FAILURE_SUMMARIES : SUMMARIES;
  const fn = table[action];
  if (fn) {
    try {
      return fn(details ?? {});
    } catch {
      // A summary function reaching for a field this particular row does not have - fall
      // through to the generic form rather than showing nothing.
    }
  }

  if (!details || Object.keys(details).length === 0) return titleCase(action);

  const pairs = Object.entries(details)
    .filter(([, v]) => v !== null && v !== undefined && v !== '')
    .slice(0, 4)
    .map(([k, v]) => `${k}: ${typeof v === 'object' ? JSON.stringify(v) : v}`)
    .join(', ');

  return pairs ? `${titleCase(action)} — ${pairs}` : titleCase(action);
}

// Display-only overrides for action codes whose auto-generated (titleCase) label would still
// read "Wallet" — the `value` stays the real action code the backend expects, only the label
// shown in the dropdown changes.
const ACTION_LABEL_OVERRIDES = {
  wallet_recharge: 'Member Amount Top-Up',
  wallet_deduction: 'Member Amount Deduction',
};

/** For the action filter dropdown - every code this file knows how to describe, readably labelled. */
export const KNOWN_ACTIONS = Object.keys(SUMMARIES).map((value) => ({
  value,
  label: ACTION_LABEL_OVERRIDES[value] ?? titleCase(value),
}));
