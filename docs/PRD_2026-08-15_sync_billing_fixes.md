# PRD: Sync, Billing & Finance Fixes — v2.4.10 Follow-up

- **Date:** 2026-08-15
- **Fleet version at time of testing:** 2.4.10 (Citylight, Adajan confirmed on 2.4.10 — this build is the best yet, but the issues below still block a clean launch)
- **Branch tested:** Operator .exe live at Citylight vs. Head Office / Super Admin panel (`140.245.195.222:8081`)

## Summary

Eight issues found while running the Operator .exe and the Head Office/Super Admin panel side by side. Four are independent logic/query bugs local to one feature (split payment, discount, food stock, reservation). Three (#1, #3, #7) all point at the same underlying gap: **there is no durable, acknowledged sync path between Head Office and the Operator .exe**, and the live sync connection itself does not survive over time. #8 is a separate, unrelated auth/session bug found later (2026-08-19).

## Suggested priority order

1. 🔴 **#7** — full sync collapse after ~1-2h uptime (data-visibility risk, most severe)
2. 🔴 **#1 / #3** — Head Office → Operator .exe command sync (shared root cause)
3. 🟠 **#2** — Split payment query missing online leg (revenue reporting accuracy)
4. 🟠 **#4** — Discount logic broken via Super Admin (billing accuracy)
5. 🟠 **#8** — Member logout not working from user screen (member can't end their own session)
6. 🟡 **#6** — Reservation: Head Office panel sends wrong query
7. 🟡 **#5** — Food Orders stock sync (needs an audit pass before fixing — touches stock code, coordinate with whoever is deployed on stock/payments tonight)

---

## 1. Head Office payment not reflected in Operator .exe — bill stuck in "Awaiting Billing"

- **Priority:** 🔴 High
- **Problem:** A payment taken/approved at Head Office does not reflect on the Operator .exe. The bill stays parked in "Awaiting Billing" on the operator side indefinitely, even though Head Office shows it as paid.
- **Impact:** Operator cannot clear a bill Head Office already settled → billing counter shows a permanently stale "awaiting" entry, risk of double-charging the customer or a shift-close mismatch.
- **Likely shared root cause:** There's no downstream command queue with acknowledgment, so a Head-Office-side state change (payment settled) never reliably reaches the operator — the operator's continuous upstream heartbeat can silently overwrite it back to the old state.
- **Acceptance criteria:** When a payment is completed at Head Office (direct or Split) for a bill, the matching bill on the Operator .exe must leave "Awaiting Billing" within the normal sync interval and match the paid amount/method exactly.
- **Screenshot:** ![Sessions grid on Operator .exe](./images/2026-08-15-operator-sessions.png) *(save from the "Apple Esports — Gaming Café ERP — This PC" window screenshot)*

---

## 2. Split payment logic broken — online leg missing from the payment query

- **Priority:** 🟠 High (revenue accuracy)
- **Problem:** Taking a Split payment (part cash, part online) miscalculates / doesn't reconcile, because the finance read/write query only accounts for `Cash`, not other payment methods.
- **Evidence — audit log from a live Split payment on 2026-08-15 05:30 pm at Citylight:**
  ```json
  {
    "action": "payment_process",
    "success": true,
    "targetType": "bill",
    "targetId": "26bfc08f-34a2-4e66-bbeb-779c43963acb",
    "ip": null,
    "Cash": 5,
    "Total": 10,
    "PaymentType": "Split"
  }
  ```
  `PaymentType` is `"Split"` and `Total` is `10`, but only `Cash: 5` is recorded — there is no `Online`/`UPI` field capturing the other ₹5. The split's non-cash leg is invisible to whatever reads this back for Finance.
- **Impact:** Every Split-paid bill under-reports revenue by the non-cash portion in Finance/EOD reconciliation; cash drawer vs. online settlement will never match.
- **Acceptance criteria:** The payment write path persists every leg of a Split payment (Cash, Online/UPI, Wallet, Credit, etc.) with its own amount, and the Finance/reporting query selects all payment-method columns — not just `Cash` — so `sum(legs) == Total` always holds for Split bills.

---

## 3. Maintenance status doesn't sync Head Office → Operator .exe (reverse direction and server-only both work)

- **Priority:** 🔴 High
- **Problem:** Flagging a PC "Under Maintenance" (or restoring it) from the Head Office/server side does not reach the Operator .exe. The reverse direction — operator flags a PC for maintenance — syncs to the server correctly. The server's own UI updates correctly when changed from the server. Only Head-Office-initiated changes fail to reach the operator.
- **Impact:** A PC taken down remotely can still be sold to a walk-in customer at the branch, since the operator's grid never shows it as unavailable.
- **Related:** Independent confirmation of the same downstream-sync gap as #1 — this shows the gap isn't billing-specific, it's *any* Head-Office-initiated PC-state command.
- **Acceptance criteria:** A maintenance flag or restore set from Head Office reaches the Operator .exe's live PC grid without requiring any operator-side action, while the already-working operator→server direction keeps working.

---

## 4. Discount logic broken through Super Admin (works fine from the operator's own session)

- **Priority:** 🟠 High (billing accuracy)
- **Problem:**
  - Discount applied from the session the operator is logged into: billing comes out correct.
  - Discount applied through the official Super Admin: doesn't work — the Discount button doesn't apply any value at all.
  - Where it does attempt to compute a value, the result is wrong.
- **Requirement:** Discount application must behave identically whether triggered by the operator or by Super Admin/Head Office. Wherever discount math is computed, apply one consistent round-off rule to the final billed amount (define and use the same round-half-up logic in every code path — no path should compute a raw, un-rounded value while another rounds).

---

## 5. Food Orders — stock not updating via Super Admin / Head Office Portal

- **Priority:** 🟡 Medium — needs an audit pass, not a blind patch
- **Problem:** Stock changes made through Super Admin/Head Office Portal for food items aren't reflected in the Operator .exe.
- **Action item:** Before fixing, compare the food-credit/stock update path used by Super Admin against the one used by the Operator .exe — there may be two competing implementations rather than one broken sync link.
- **⚠️ Coordination note:** stock and payments files are an active collision zone tonight per the current fleet-deploy work — check with whoever is deployed on stock/payments before editing this path.

---

## 6. Reservation — Head Office / Super Admin panel sends the wrong query

- **Priority:** 🟡 Medium
- **Problem:** Reservation works correctly end-to-end on the Operator .exe. On the Super Admin/Head Office panel it's broken, and the suspicion is a query/schema mismatch on the Head Office side rather than a sync issue.
- **Action item:** Diff the reservation query used by the Operator .exe backend path against the one used by the Super Admin/Head Office backend path to find the discrepancy.

---

## 7. 🔴 Full sync collapse after ~1–2 hours of uptime (most severe)

- **Priority:** 🔴 Highest — looks like data loss to Head Office
- **Problem:** Running the Operator .exe and the server-side Super Admin side by side, after roughly 1–2 hours everything vanishes from the server's view — no active PCs, no billing — while the Operator .exe continues to correctly show active sessions and billing the entire time. Before that window, sync was working correctly. Sync appears to stop outright at some point, not just fall behind.
- **Impact:** Head Office goes completely blind to a live, functioning branch after ~1-2 hrs of normal operation — this is the most severe issue in this batch because it looks like total data loss even though the branch itself is fine.
- **Hypotheses to check first:**
  - Heartbeat/websocket/polling connection timing out and not reconnecting.
  - Auth token (e.g. JWT) with a ~1–2h TTL expiring without a refresh, silently killing the sync session.
  - A background sync worker on the server crashing or exiting after a certain duration/load without restarting.
- **Next step:** Pull server logs for the exact timestamp sync stopped and cross-reference against token TTL / connection-pool / worker-restart config.
- **Screenshots:** two screenshots were captured showing the operator side (correct) vs. the server side (empty) after the collapse — save them into `docs/images/` (e.g. `2026-08-15-sync-collapse-operator.png`, `2026-08-15-sync-collapse-server.png`) and link them here.

---

## 8. Logout option not working properly in member user screen

- **Priority:** 🟠 High
- **Date identified:** 2026-08-19
- **Problem:** From the member session's user screen, the logout option doesn't work properly — a member is unable to log out of their own session from their user screen.
- **Note:** Unrelated to the sync/billing root cause above (#1/#3/#7) — this is an auth/session-handling bug local to the member user screen, not a Head Office ↔ Operator .exe sync gap.
- **Acceptance criteria:** A member clicking Logout on their user screen ends their session immediately and returns them to the login/landing state, every time.

---

## Cross-cutting root cause

Items #1, #3, and #7 all trace back to the same architectural gap: no durable, acknowledged downstream command path from Head Office to the Operator .exe, and no resilience in the live sync connection itself. Fixing the sync layer (versioned state + downstream command queue + reconnect/keepalive handling) likely resolves all three together. #2, #4, #5, and #6 are separate, narrower logic/query bugs local to their own feature and can be fixed independently.
