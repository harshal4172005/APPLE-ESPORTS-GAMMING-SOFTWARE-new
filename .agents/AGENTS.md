# Project-Scoped Rules: Apple Esports ERP

## Real-Time Updates & Caching
When implementing real-time updates (e.g. SignalR websockets) combined with REST API fallback/polling:
- Always ensure that GET requests fetched in real-time callbacks bypass browser cache.
- The project's global Axios instance (`client/src/config/api.js`) has been configured with `Cache-Control: no-cache` headers to prevent stale data when re-fetching data upon receiving SignalR events (like `PcStatusChanged`).
- Do NOT rely on the browser to automatically re-fetch the latest data if no-cache headers are missing; you must enforce no-cache or use cache-busting mechanisms if doing raw fetch/axios GETs outside the global `api` instance.

## Transparent Multi-Agent Debate (SOP §21)
All code changes and feature requests, no matter how small or simple, MUST go through a planning phase. Every implementation plan (`implementation_plan.md`) MUST include a visible debate section before execution to ensure absolute transparency for the non-technical user.
1. The user must NOT be asked to solve technical problems.
2. Every single task must generate an `implementation_plan.md` artifact containing a section titled **"The Developer vs The Critic"**.
3. In this section, you must write out a script-like debate:
   - **👨‍💻 The Developer:** Proposes the change (even for minor tweaks).
   - **🕵️‍♂️ The Critic:** Ruthlessly interrogates the Developer's plan (focusing on edge cases, database locks, cache desyncs, and failure points).
   - **👨‍💻 The Developer:** Responds and adjusts the plan based on the Critic's pushback.
4. This debate MUST be visible inside the implementation plan artifact so the user can read the transparent process before clicking "Proceed" on EVERY task.

## Live Synchronization (SOP §20)
All dashboards (Operator, Admin, SuperAdmin) require live synchronization. Any changes triggered by Member Overlays (e.g., Member Login, Session Start, Walkin Requests, Member Checkout) must emit the appropriate SignalR events to the respective target groups (`branch:{branchId}` and/or `admin:all`) so that the frontends can immediately refetch their state using the cache-disabled API layer.
