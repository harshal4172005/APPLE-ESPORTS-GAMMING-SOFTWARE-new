# Phase 3 — the gaming-PC app (User EXE)

Appendix to `REBUILD_PLAN.md`. Drafted 16 August 2026. *Nothing built yet, no branch cut yet.*

Revision 2 — merges the implementation plan drafted in the side chat with a line-by-line
verification pass against the real code. Where the two disagree, §2 says so and why. Every file and
line reference below was opened and read on 16 August 2026, not inferred.

Read this, add to it, cut what you don't want. Then §3 gets decided and `phase3-user-exe` is cut.

---

## 0. Ground rules inherited from `REBUILD_PLAN.md`

- *Additive only.* Server and schema changes never rename, drop or retype an existing column.
  This is the rule that decides whether "just remove Phase 3" actually works — rolling back code
  does not roll back a database.
- *One phase, one branch.* `phase3-user-exe`. Removing Phase 3 is deleting a branch.
- *Rehearse locally.* The same docker-compose stack runs on the dev machine — API `5016`,
  Postgres `5433`, gated dashboard `8081`. Oracle is where a change goes after that.
- *Money is proven by running, never by reading.* Every money bug in this project was caught by
  driving the real path with realistic inputs. Three of them read as correct.

---

## 1. What is actually on a gaming PC today

The important discovery of the verification pass: **there are two half-finished gaming-PC clients in
this repository, and neither one works on its own.**

### Client A — `AppleEsportsErp.ClientAgent` (WPF, .NET 8)

Tracked on `main`, `phase2-exe` and `phase1-frozen`; in the solution; published by
`installer/build-branch-installer.ps1`.

| Has | Where |
|---|---|
| A real Windows lock — low-level keyboard hook blocking Alt+Tab, Alt+F4, Win, Ctrl+Esc, plus `DisableTaskMgr` in `HKCU` | `Services/SystemLockService.cs:80-131` |
| LAN-primary / Cloud-fallback SignalR with a health loop and failover threshold | `Services/DualConnectionService.cs:35-215` |
| Remote unlock / lock / force-shutdown handling | `Services/SessionControlService.cs:20-69` |
| A countdown timer that auto-locks at zero and reports it | `Views/LockScreen.xaml.cs:124-153` |
| Close blocked outright — `Closing += (s, e) => e.Cancel = true;` | `Views/LockScreen.xaml.cs:49` |
| Single-instance mutex | `App.xaml.cs` |

*And the server side of it is real too.* Starting a session at the counter genuinely sends an
unlock to `agent:{pcId}` — `SessionService.cs:370` on start, `:652` on end, `:753` on resume, via
`HubNotificationService.SendUnlockCommandToAgentAsync`. Hub auth accepts the agent's
`?access_token=` because `Program.cs:151-161` wires query-string tokens for `/hubs`, and
`GenerateAgentToken` mints a 365-day token carrying `branchId` and role `Agent`.

| Does not have | Consequence |
|---|---|
| Any local persistence at all | `_remainingSeconds` is an int in memory. A reboot loses the session outright. |
| Any launch trigger | The installer copies `AppleEsportsAgent.exe` to disk (`AppleEsportsBranch.iss:83`) and *never starts it* — no Start-menu icon, no desktop icon, no `Run` key, no scheduled task. `[Icons]` and `[Run]` name only `AppleEsports.exe`. |
| Any real configuration | It ships with `"MachineToken": "PASTE_YOUR_TOKEN_HERE"` and `"PcId": "00000000-…"`, and nothing in the installer writes them. |
| The right default role | `AssignedRole` ships empty, and `App.xaml.cs` routes empty → `DashboardWindow`, not `LockScreen`. Even if launched, it would open a dashboard. |
| Provisioning | It never calls `/api/agent/provision`. Its identity is hand-pasted per machine. |

### Client B — `desktop-client` (WinForms + WebView2), on `phase2-exe`

| Has | Does not have |
|---|---|
| Installed, launched, configured per machine by the installer | Any Windows-level lock — no keyboard hook, no Task Manager policy |
| Machine fingerprint + real provisioning against `/api/agent/provision` | Any LAN/Cloud failover |
| PIN-gated escape, no title bar, no close button, taskbar hidden for `user` role | Any local persistence |
| An update loop that refuses to update mid-session | The right URL — `MainForm.Connect()` opens the server root, i.e. *the operator dashboard*, whatever the role |

### The customer UI — already built, in React

`/pc-overlay/:pcId` (`client/src/App.jsx:97`, public route) is a complete customer application:
`PcLockScreen.jsx` already does selection → walk-in → member login → time selection with live
pricing from `/public/pcs/{pcId}/plans`; the panel does session info, food ordering, extension
requests, call operator, live bill, and the wallet-runs-out rules. `PcOverlayHub.cs` serves it.

*So the honest summary:* the lock exists but never runs; the shell that runs has no lock; the
customer UI exists in a third place and neither Windows client points at it. Phase 3 is mostly
*joining three working halves*, not writing three new things.

---

## 2. Corrections to the incoming plan

The side-chat plan is sound and most of it is adopted verbatim below. Six claims changed under
verification.

| Claim | Verdict |
|---|---|
| `Ctrl+Shift+Alt+U` unlocks to desktop with no PIN | *Confirmed.* `LockScreen.xaml.cs:55-65` — `DisableLock()` then `Application.Current.Shutdown()`, no check of any kind. |
| It is "a live hole in the currently-deployed lock screen", ship the fix independently and urgently | *Fix is right; urgency is wrong.* Nothing launches that exe (§1), and with its shipped config it would open a dashboard rather than the lock screen. It is a real bug in code that ships but does not run. Fix it as Phase 3's first commit — it does not need to jump the queue as an emergency, and framing it as one on a system that trades real money spends credibility that will be needed later. |
| `IsAgentOnline` "keeps showing whatever state it was last in, forever" | *Understated — it is worse.* `IsAgentOnline = true` is written in exactly one place, `AgentController.cs:240`, the REST endpoint `POST /api/agent/heartbeat` — and *nothing in the repository calls it.* The agent beats to the hub method `AgentHeartbeat`, which logs one line and returns (`Hubs.cs`). So the flag is never set true at all: every PC shows the red "Agent Offline" dot permanently. An offline sweep alone fixes nothing — *the online path has to be wired first.* |
| Fix the dashboard by branching on `state === 'AwaitingSetup'` | *Necessary but insufficient.* `PcStatusDto` (`DTOs/PcStatus/PcStatusDto.cs`) carries no `MachineId`, `IsProvisioned` or `ProvisionedAt` at all, so the dashboard cannot see provisioning. And the live PC rows predate provisioning: `AddPcAgentColumns` added the columns with no state backfill, so real PCs sit at `Idle` with `MachineId = null` and would never match `AwaitingSetup`. Add `IsProvisioned` to the DTO (additive) and key the badge on it. |
| §11 — build a full-screen idle panel with Walk-in/Member, a session bar, food, and so on in WPF | *Do not rebuild it.* All of it exists and works in React at `/pc-overlay/:pcId`. A second copy means two places where pricing, wallet rules and the walk-in flow can drift apart, and drift in that direction costs money. Host the existing UI in WebView2 instead — see §3, Path C. |
| Require an exact IP at provisioning; never re-negotiate it | *Adopted with a caveat.* Identity is `MachineId` + machine token; the IP is a label so staff can find a box. On DHCP that label goes stale on its own after a router reboot, and "never re-negotiate" then means a permanently wrong record. Either the four shops get DHCP reservations per PC (an ops job, before rollout) or the agent refreshes the label on each heartbeat. Recommend reservations. |

Everything else — the SQLite schema, the local-vs-server state separation, snapshot-before-UI,
`Microsoft.Data.Sqlite` over EF, fail-loud migrations, ProgramData placement, uninstall exclusion,
the offline sweep service, process cleanup on session end — is adopted as written.

---

## 3. The shell decision — decide before branching

*Path A — build on `ClientAgent` (WPF).* Keeps the real lock and the failover; must gain
fingerprint provisioning, the proven kiosk details from `desktop-client`, and a launch trigger.

*Path B — build on `desktop-client` (WinForms).* Keeps what is actually installed and
provisioning; must gain the whole Windows lock and the failover, and it couples gaming-PC logic
into the shell the operator counter also runs.

*Path C — `ClientAgent` owns the machine, WebView2 owns the screen. ← recommended.*

The WPF process keeps what only a native process can do: the keyboard hook, the Task Manager
policy, the watchdog, the SQLite file, process cleanup at session end, and the LAN/Cloud failover
it already has. Everything the customer looks at is the React overlay already built, loaded in a
WebView2 control inside that window. `ClientAgent` already references WebView2.

Why this and not A or B: it is the only one that does not rebuild a working UI, and it puts the
line in the right place — native code for what Windows will not let a web page do, web code for
everything else, one copy of the pricing and wallet rules. `desktop-client` stays exactly what it is
today, the operator counter's shell, and Phase 3 does not touch it.

The rest of this document says "the Phase 3 app" and holds whichever path you pick.

---

## 4. First commit — PIN-gate the escape hatch

`LockScreen.xaml.cs:55-65` today:

```csharp
if (e.Key == Key.U &&
    Keyboard.Modifiers.HasFlag(ModifierKeys.Control) &&
    Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) &&
    Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
{
    _systemLock.DisableLock();
    Application.Current.Shutdown();
}
```

Anyone who knows the combination is out to the desktop. Put a PIN prompt between the key and
`DisableLock()`, checked against `pc_identity.admin_pin_hash` (§6). One PIN-checking path, reused by
every other escape in the app — the close affordance in §5, the setup dialog, the quit shortcut.
Never a second door with its own guard.

While in there, two things the same hook gets wrong: `VK_DELETE` is declared at
`SystemLockService.cs:25` and never used, and Ctrl+Shift+Esc is not blocked by the hook at all — the
Task Manager defence is entirely the `HKCU` registry policy, which is silently skipped when the
process lacks the rights (`SystemLockService.cs:127-130` swallows the failure). If that write fails,
the machine is unlocked and nothing says so. It must be visible.

---

## 5. The two modes, and the local UI flow

The lock is not "fullscreen forever". The app changes shape when a session starts and changes back
when it ends. Today it does neither: `UnlockPc()` calls `Hide()` (`LockScreen.xaml.cs:82`) and the
customer gets a bare Windows desktop with no app on screen at all.

| Mode | Screen | Escape |
|---|---|---|
| *Locked* (`AVAILABLE`) — no session, or time gone | Full screen, topmost, no OS chrome. The React idle panel: Walk-in and Member entry points, already built in `PcLockScreen.jsx`. | Admin PIN only |
| *Playing* (`ACTIVE`) | The desktop and games. The React overlay narrows to the right-hand panel — time remaining, name, connection status, food, extend, call, bill. Minimisable to a bubble. | Session end, or admin PIN |

Rules that come with it:

- *A close (X) affordance may be visible, but it never closes the app.* It routes through the same
  PIN gate as §4 — the same interception the existing `Closing` handler already performs for Alt+F4.
- *The panel minimises; it does not dismiss.* The customer can get it out of the way. They cannot
  make it go away.
- *Session end does two things, not one.* Processes started during the session are terminated —
  record PIDs at unlock, kill at lock; this is new state, in no current file — and the app returns to
  the full-screen idle panel. It must not exit, and a failure to kill a process must not take the
  app down with it. Today `LockPc()` stops a timer and re-shows a window; nothing else is tracked.
- *Connection status is already written* — `UpdateConnectionStatus` at `LockScreen.xaml.cs:103-122`
  renders LAN green / Cloud orange / disconnected red. Keep it, surface it in both modes.

---

## 6. The SQLite file

One file per PC, `Microsoft.Data.Sqlite`, not EF Core: three small tables, mostly singleton rows,
written from a SignalR callback on the hot path. EF's `DbContext` and migration machinery are not
earning their keep at that scale, and raw ADO.NET keeps startup fast and schema versioning explicit.

```sql
CREATE TABLE schema_version (
    version     INTEGER NOT NULL,
    applied_at  TEXT NOT NULL
);

CREATE TABLE pc_identity (
    machine_id        TEXT PRIMARY KEY,   -- stable hardware fingerprint
    pc_id             TEXT,               -- server Pc.Id once provisioned, else NULL
    pc_number         TEXT,               -- e.g. "PC-1"
    branch_id         TEXT,
    operator_lan_url  TEXT,               -- the counter this PC talks to
    configured_ip     TEXT,               -- this PC's own LAN IP, sent at provisioning
    machine_token     TEXT,               -- secret issued at provisioning
    admin_pin_hash    TEXT,
    is_configured     INTEGER NOT NULL DEFAULT 0,
    provisioned_at    TEXT
);

CREATE TABLE session_snapshot (
    id                     INTEGER PRIMARY KEY CHECK (id = 1),   -- singleton
    local_state            TEXT NOT NULL,   -- AVAILABLE | STARTING | ACTIVE | STOPPING | COMPLETED | INTERRUPTED_LOCAL
    server_session_id      TEXT,
    started_at             TEXT,
    expires_at             TEXT,
    pending_reconciliation INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE disconnected_action_queue (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    event_type    TEXT NOT NULL,
    event_data    TEXT NOT NULL,   -- JSON
    occurred_at   TEXT NOT NULL,
    sent_at       TEXT,
    attempt_count INTEGER NOT NULL DEFAULT 0
);
```

SQLite rather than a JSON file because a power cut mid-write corrupts a text file, and a power cut
is the exact case this exists for. `disconnected_action_queue` mirrors the shape of the server's
`SyncOutboxEntry`, scaled down from branch→server to PC→counter.

*`local_state` is deliberately not a copy of the server's `SessionState`.* That enum — `Active`,
`Reserved`, `AwaitingBilling`, `Completed`, `Expired`, `Interrupted` — is the authoritative record of
billing state. The PC only needs to know what to draw right now. Keeping them separate means
reconciliation is an explicit, visible step instead of an assumed 1:1 mapping, and a future server
state does not force a client schema change.

*Versioning.* Ordered migrations in code, run in one transaction at startup before anything else
opens the file, additive only, same rule as the server. On failure: *fail loud and stop.* Never
recreate the file — recreating destroys an unresolved `session_snapshot`, which is the exact
crash-recovery data this whole file exists to protect.

*Placement.* `%ProgramData%\Apple Esports\agent\` — mirrors the Phase 2 convention, and
`Program Files` is not writable. Excluded from uninstall-delete, so an update or repair never wipes
`pc_identity` or an in-flight session.

---

## 7. Session state and reconciliation

- *Write before you draw.* The snapshot is written before or as the UI changes, never after. If
  the process dies mid-transition, the disk holds the last intended state.
- *On startup, read disk before opening any connection.* If `local_state = ACTIVE`, restore the
  Playing UI and *recompute the remaining time from `expires_at`* — never start a fresh timer from
  a duration. This is the whole reason a reboot mid-session is survivable.
- *Losing the connection does not change the local state.* Queue the event, keep showing what is
  on screen. A disconnected PC does not get to decide that a customer's time is over.
- *On reconnect:*
  - agrees with the counter → clear `pending_reconciliation`, done;
  - counter says `Interrupted` → keep the current UI and *wait for an explicit command*. The
    server's own doc comment is explicit that an interrupted session never resumes on its own,
    because an operator has to decide whether the customer came back;
  - any other disagreement → log a conflict for the counter to show. Never silently resolved on the
    PC.

---

## 8. Kiosk hardening

*Already proven*, between the two clients: the keyboard hook, `DisableTaskMgr`, the close block,
PIN-gated escape.

*Gaps:*
- The unauthenticated escape hatch — §4.
- Ctrl+Shift+Esc is not hook-blocked; the registry policy is the only defence and it fails silently.
- Ctrl+Alt+Del cannot be intercepted from user land at all. It needs a local Group Policy change
  delivered as an *installer step*, not application code.
- No explorer-shell replacement or taskbar suppression.
- *A crash in the hook leaves an unlocked desktop.* Needs a watchdog — a small supervisor that
  relaunches the app if it exits unexpectedly. Note the existing single-instance mutex in
  `App.xaml.cs` has to be accounted for so the watchdog does not race the app it just restarted.

---

## 9. Identity, exact IP, and "not configured"

Each gaming PC connects to its branch counter over a known LAN address. What exists already:
`PcState.AwaitingSetup` with a doc comment stating exactly the failure it prevents; `Pc.IpAddress`,
`MachineId`, `MachineToken`, `ProvisionedAt`, `IsProvisioned => MachineId != null`; and
`POST /api/agent/provision`, which refuses a second machine claiming a taken seat, hands back the
existing token on a reinstall, and flips `AwaitingSetup → Idle`.

*To build:*

1. *The Phase 3 app actually provisions.* Today `ClientAgent` has a token pasted into a JSON file
   by hand. Setup asks for the seat and the admin PIN, collects and validates the IP, calls
   `/api/agent/provision`, and persists what comes back into `pc_identity`.
2. *IP required for a Phase 3 claim.* `AgentProvisionDto.IpAddress` is nullable today; reject
   blank on new claims. Additive — callers already sending an IP are unaffected. Pair it with DHCP
   reservations per PC as an ops prerequisite (§2), or the label rots.
3. *`IsProvisioned` onto `PcStatusDto`* — additive, and without it the dashboard cannot tell a
   configured PC from an unconfigured one at all.
4. *The dashboard fix.* `AdminPcCard` (`PcStatusPage.jsx:38-59`) branches on
   Active/AwaitingBilling/Reserved/Idle/UnderMaintenance and nothing else. An unconfigured PC falls
   through every branch, takes the default idle border, shows no badge, and reads **"Ready for
   Session"** (~line 136) — precisely the "seat a customer at a machine that cannot be unlocked"
   failure the enum's own comment warns about. Give it its own treatment: a `NOT CONFIGURED` badge,
   the offline/orange tokens already in the file, and body copy like "No machine set up for this
   station". Key it on `isProvisioned`, not only on `state === 'AwaitingSetup'`.
5. *The client refuses too.* With `is_configured = 0` the app shows "Not configured — contact
   admin" and does not attempt any session or lock UI. Both ends independently refuse to pretend.

---

## 10. "Powered off" must read as powered off

Three separate gaps, in order:

1. *Wire the online path.* The agent reports to the hub; the hub throws it away. `AgentConnected`
   and `AgentHeartbeat` in `PcStatusHub` must persist `IsAgentOnline = true`, `ConnectionMode` and
   `LastAgentHeartbeat` — the columns already exist, so this is code only. (Alternative: have the
   agent also call the REST endpoint. Persisting where the agent already reports is simpler and
   removes the second path entirely.)
2. *Add the offline sweep.* A new `BackgroundService` — `AgentOfflineSweepService`, same pattern as
   `SessionHeartbeatService` — polling roughly every 15s for PCs with `IsAgentOnline = true` and
   `LastAgentHeartbeat` older than 2–3 missed beats (~30–45s at the agent's 10s health interval).
   Set `IsAgentOnline = false`, `ConnectionMode = "None"`, and broadcast `AgentStatusChanged` on the
   same `branch:{branchId}` / `admin:all` groups `AgentConnected` already uses, so the grid moves
   without a refresh. Purely additive: one service registration, no schema change.
   *A stale `true` is worse than a `false`* — it tells an operator a machine is fine while a
   customer stares at a dead screen.
3. *Boot auto-connect.* Register the app to start at Windows logon — `HKCU\…\Run` or a Task
   Scheduler "at logon" task — as an installer step. Power on → logon → app starts → reads
   `pc_identity` → LAN, then Cloud → hub reports in → the grid flips to online, with nobody touching
   anything. Nothing does this today for either client.

Then the label can finally distinguish three states that look identical now: *Not Configured* /
*PC Off* / *Online (LAN|Cloud)*.

---

## 11. Connection, and what happens when it drops

LAN first, cloud only as a fallback — `DualConnectionService` already implements the failover, the
health loop and the threshold. What is missing is what it means for money, which is §15's questions
3 and 4, not an assumption made here.

The one thing that is not open: *a disconnected PC never invents a billing decision.* It may keep
showing a session it already knows about; it may not start one, extend one, or bill one.

---

## 12. Updates reaching the gaming PCs

The Updates dashboard already counts how many of a branch's PCs are up to date, and the Phase 2
shell already refuses to update mid-session. Missing: the branch handing an approved update down to
its own machines over the shop LAN, and each machine reporting its version back. Sequenced last —
an update mechanism is the one piece of software that can break every branch at once, and it needs
the rest to be stable before it is worth having.

---

## 13. The overlay hub is anonymous

`PcOverlayHub` is deliberately unauthenticated — written before machine tokens existed. Anything
that can reach it can join any PC's group, place a food order billed to that PC's live session, raise
walk-in requests and decline them. Machine tokens now exist and the hub pipeline already accepts
them by query string. Require one, and let a PC act only as itself.

---

## 14. Installer

- Ships the Phase 3 app for the `gaming` install type — the component already exists
  (`AppleEsportsBranch.iss:83`), it just installs a program nothing runs.
- *Registers boot auto-launch* (§10.3). This is the step that makes the whole thing real.
- Writes a per-machine config with a real identity instead of `PASTE_YOUR_TOKEN_HERE`, the way
  `WriteClientConfig` already does for the counter shell.
- SQLite in `%ProgramData%\Apple Esports\agent\`, excluded from uninstall-delete.
- Group Policy step for Ctrl+Alt+Del (§8).
- Repeatable and boring: 35 machines at Citylight in an evening, one seat number typed per machine.

Traps already paid for once, in Phase 2: services hold their own DLLs open; `localhost` resolves to
IPv6 first on a machine running Docker or WSL; a service's working directory is `System32`; setup
must not grant itself read-only access to a file it needs to rewrite.

---

## 15. Decisions I need from you

Recommendations given, so "yes to all" is a valid answer and you overwrite what you disagree with.

1. *Which shell* — Recommend Path C: WPF owns the machine, WebView2 hosts the React UI already
   built. §3.
2. *How hard is the lock* — Recommend: keyboard hook + topmost + watchdog + Task Manager policy.
   *Not* replacing the Windows shell: if the app then fails to start, the machine needs safe mode,
   and that is 106 machines of risk to close one escape route.
3. *Counter unreachable mid-session* — Recommend: keep playing on the machine's own timer to the
   end time it already knows, queue everything, show a small reconnecting indicator. No extensions,
   no new sessions offline.
4. *Pay-as-you-go with no end time, offline* — Recommend: keep running and reconcile when the
   counter is back. The alternative caps it at a fixed offline limit. Genuinely your call: it is
   your money either way during a long outage.
5. *Time-up* — Recommend: warnings at 10, 5 and 1 minutes in the panel, then a full-screen
   60-second "save your game" countdown, then Locked. Note the current agent flashes the timer red
   under 5 minutes and cuts at zero with no countdown.
6. *Between customers* — Recommend: V1 kills session processes and clears the WebView profile.
   Game-account wiping and disk-restore-on-reboot are a separate job, not Phase 3.
7. *Windows account* — Recommend: one auto-login *standard* (non-admin) local account. A
   customer who is a local admin defeats everything in §8.
8. *Admin PIN* — Recommend: one per branch, set at install, changeable from the dashboard later.
   Separate from any operator login.
9. *Static IPs or DHCP reservations* — Recommend: reservations per PC on each shop's router,
   done before rollout. Otherwise §9's required IP goes stale on its own.
10. *Customer-started sessions* — the walk-in and member-login flows already exist and work.
    Recommend: keep them; the operator still approves.
11. *Counter switched off* — Recommend: Locked, "Counter offline — please see the desk", no
    session can start.

---

## 16. Milestones

Each verified against the local stack (`5016` / `5433`) before the next begins. Never against a
branch's live server.

1. Branch `phase3-user-exe`; shell decision made (§3). *First commit: PIN-gate the escape hatch*
   (§4).
2. SQLite scaffold, migration runner, `pc_identity`.
3. Provisioning: seat claim, admin PIN, required IP — including the rejected path when the IP is
   missing.
4. The app opens the customer screen at all, and *reports in* — hub persistence (§10.1) plus the
   boot-launch path. Small, and it makes everything after it observable.
5. Session snapshot and crash recovery — proven by killing the process mid-session and by pulling
   the plug, twice: once with the counter reachable at boot, once without.
6. The two modes (§5) — idle panel full-screen on a fresh start; an unlock command producing the
   minimisable, non-closable panel instead of a bare desktop; session end killing a test background
   process and returning to the idle panel without the app exiting.
7. Kiosk hardening — watchdog, Ctrl+Shift+Esc, the visible failure when the registry write is
   refused.
8. Disconnected queue and reconciliation — proven by pulling the LAN cable mid-session, acting from
   the counter, reconnecting, and confirming reconciliation rather than a silent overwrite.
9. Offline sweep + the three-way dashboard label (§9.4, §10) — proven by powering a provisioned
   agent off and watching the grid flip within the sweep interval with no admin action, then
   powering it back on and watching it return via boot-launch alone.
10. Machine tokens on the overlay hub (§13).
11. Installer wiring — ProgramData, uninstall exclusion, boot auto-launch, GPO step.
12. Updates down to the gaming PCs (§12).
13. *One real PC, one branch, one full trading day, watched.* Then the rest.

---

## 17. Explicitly not in Phase 3

- No Postgres, no multi-branch data, no business logic, no pricing or billing computation on a
  gaming PC.
- No Phase 4 bidirectional command bus; no outbox-grade retry engine — the disconnected queue is
  small and short-lived by design.
- No non-additive server schema changes.
- No full OS-level hardening rollout bundled into the app milestones — Ctrl+Alt+Del and GPO work is
  an ops track.
- *No automatic re-provisioning or IP renegotiation* if a PC's address changes. That stays a
  manual admin action through the existing PC edit path (`PcsController.Update`,
  `pc.IpAddress = dto.IpAddress`).
- *No OS-level sandboxing.* §5's cleanup is PID tracking and terminate, not a sandbox around
  whatever the customer launches.
- No game launchers, disk restore, anti-cheat, or remote screen viewing.
- *The identity gap stays open* — an operator created at Head Office still cannot log in at a
  branch. Not this phase.

---

## 18. How it will be proven

Not "the step ran". The real machine doing the real thing:

- A customer at a Locked PC, with a keyboard and mouse, actively trying, cannot reach the desktop
  without the PIN — and killing the process from another machine brings it straight back.
- The plug pulled mid-session, twice: counter reachable at boot, and counter not.
- The LAN unplugged mid-session — the game continues, the counter sees it go offline, the queue
  drains on return.
- A session running out with a customer sitting there: warnings, countdown, lock.
- A food order placed from the PC appears at the counter and on the bill, in rupees that match.
- A PC powered off shows as off, a PC never set up shows as not configured, and the two do not look
  alike.
- Install on a clean machine twice, plus a repair install on a machine that already holds a seat.
