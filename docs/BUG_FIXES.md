# Bug Fixes Log

Running log of bugs identified in the app. Save the screenshot into `docs/images/` and reference it in the entry.

---

## 🔴 TOP PRIORITY

### Bug: Head Office commands get silently reverted by Operator .exe heartbeat (no downstream command path)

- **Priority:** 🔴 High
- **Date identified:** 2026-08-14
- **Description:** Root-cause bug behind the Sessions/Billing/Finance sync failures above. Whenever Head Office changes something (e.g. stopping a PC session remotely), the change is applied on the server — but it never reliably reaches the Operator .exe. Repro: put one PC in "Under Maintenance" and another in "Pay As You Go" — both work fine on server and Operator .exe. Then stop that PC's session from the server: the session ends on the server, but the "stop" never reaches the Operator .exe, because the Operator .exe keeps sending its own current-state status upstream on a continuous heartbeat. Since there's no versioning/authority check, the server accepts that stale heartbeat as the latest truth and reverts the session back to "started" — so the session ends up stuck in "started" on both sides again.
  - **Likely cause:** Sync architecture (see `docs/DISTRIBUTED_SYNC_MVP.md`) is built for local→cloud data push (sessions, orders, bills going upstream via `SyncQueue`), but there's no equivalent downstream command queue with acknowledgment for Head Office → Operator .exe. A remote command (stop/maintenance/etc.) needs to be durable and pulled+ack'd by the operator, not just a server-side row update that a later heartbeat can silently overwrite.
  - **Suggested fix direction:** (1) Add a version number / `last_modified_by` + timestamp to session/PC-state rows and reject heartbeat updates older than the server's current version. (2) Add a real downstream command queue mirroring the existing `SyncQueue` pattern, so Head Office actions are commands the Operator .exe must fetch and ack. (3) Stop treating operator heartbeats as authoritative state changes — they should only report status, never override a command that originated elsewhere.
- **Screenshot:** ![<description>](./images/<file-name>.png)
- **Status:** Open
- **Fix notes:** _(fill in once resolved)_

---

## 🔴 TOP PRIORITY

### Bug: Sessions / Billing Counter / Finance not syncing between Operator .exe and Head Office (server)

- **Priority:** 🔴 High
- **Date identified:** 2026-08-14
- **Description:** Session, Billing Counter, and Finance data are completely out of sync between the Operator .exe and the server. Example: started a session and closed it at ₹10 — this did not reflect in the Finance section on the server at all. Instead, the server showed it as a "Walk-in" entry and logged ₹10 as a Credit, instead of a completed billed session. Everything on the Operator .exe side was correct; the mismatch is entirely on the server/Head Office side. This affects three core modules at once (Sessions, Billing Counter, Finance), so treating as top priority.
- **Screenshot:** ![<description>](./images/<file-name>.png)
- **Status:** Open
- **Fix notes:** _(fill in once resolved)_

---

## Example (delete or keep as reference)

### Bug: Session total shows negative balance after refund

- **Date identified:** 2026-08-14
- **Description:** When a refund is issued mid-session, the session total on the PC grid drops below zero instead of stopping at zero.
- **Screenshot:** ![Negative session balance](./images/bug-negative-balance.png)
- **Status:** Open
- **Fix notes:** _(fill in once resolved)_

---

### Bug: Member sync emails link to localhost

- **Date identified:** 2026-08-14
- **Description:** Created a member via the Operator .exe and added a balance of 5000. All email delivery works correctly (member creation mail and top-up mail both send), but the links inside those emails point to a `localhost` URL, which isn't accessible to the recipient. Not a big bug, but needs the mail template's base URL swapped for the real deployed/public URL.
- **Screenshot:** ![<description>](./images/<file-name>.png)
- **Status:** Open
- **Fix notes:** _(fill in once resolved)_

---

### Bug: Member top-ups and Head Office-created members not syncing between Operator .exe and server

- **Date identified:** 2026-08-14
- **Description:** Two-way sync issue between Operator .exe and Head Office (server). (1) When a balance top-up is done through the Operator .exe, the new balance reflects in the Operator .exe but not on Head Office/server — and it doesn't flow upstream into EOD or any other report. (2) When a member is created by Head Office or Super Admin on the server, it doesn't reflect back in the Operator .exe — no transaction shows up, and it's missing from reports on that side either. Screenshot shows the mismatch: Operator .exe (left) lists only "meeeettt" (1 total), while Head Office/server (right) lists both "harsh dave" and "meeeettt" (2 total) — the server-created member and its top-up never made it down to the operator side.
- **Screenshot:** ![Operator .exe vs Head Office member list mismatch](./images/bug-member-sync.png)
- **Status:** Open
- **Fix notes:** _(fill in once resolved)_

---

### Bug: Menu Editor items not syncing between Operator .exe and Head Office

- **Date identified:** 2026-08-14
- **Description:** Same sync issue as member data, but for the Menu Editor. Items already present on Head Office (server) are not getting reflected down to the Operator .exe — and changes don't sync upstream either. Menu Editor sync is totally broken in both directions.
- **Screenshot:** ![<description>](./images/<file-name>.png)
- **Status:** Open
- **Fix notes:** _(fill in once resolved)_

---

### Bug: Credits log not syncing between Operator .exe and Head Office

- **Date identified:** 2026-08-14
- **Description:** Same sync issue, now on the Credits log. Credits updated on the Operator .exe aren't syncing at all — not going upstream to Head Office/server, and not coming back downstream either.
- **Screenshot:** ![<description>](./images/<file-name>.png)
- **Status:** Open
- **Fix notes:** _(fill in once resolved)_

---

## New Bug

### Bug: Logout option not working properly in member user screen

- **Date identified:** 2026-08-19
- **Description:** From the member session's user screen, the logout option doesn't work properly — a member is unable to log out of their own session from their user screen.
- **Screenshot:** ![<description>](./images/<file-name>.png)
- **Status:** Open
- **Fix notes:** _(fill in once resolved)_

---

### Bug: <short title>

- **Date identified:** <YYYY-MM-DD>
- **Description:** <what's wrong / what you observed>
- **Screenshot:** ![<description>](./images/<file-name>.png)
- **Status:** Open
- **Fix notes:** _(fill in once resolved)_

---
