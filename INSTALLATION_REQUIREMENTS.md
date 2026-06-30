# Apple Esports ERP — Complete Software Architecture & Installation Guide

> **Purpose**: Master reference document for building, packaging, and deploying the Apple Esports ERP software.  
> **Last Updated**: 2026-06-20  
> **Version**: 4.0

---

## TABLE OF CONTENTS

1. [Software Overview](#1-software-overview)
2. [How The System Works (Full Flow)](#2-how-the-system-works-full-flow)
3. [Installation Flow (Single EXE)](#3-installation-flow-single-exe)
4. [Server Mode — Operator PC](#4-server-mode--operator-pc)
5. [Client Mode — Gaming PC](#5-client-mode--gaming-pc)
6. [LAN Communication (Offline Capability)](#6-lan-communication-offline-capability)
7. [AWS Cloud Sync (Phase 2 — Future)](#7-aws-cloud-sync-phase-2--future)
8. [Network Architecture Diagram](#8-network-architecture-diagram)
9. [EXE Packaging Checklist](#9-exe-packaging-checklist)
10. [Decisions To Make Later](#10-decisions-to-make-later)

---

## 1. SOFTWARE OVERVIEW

Apple Esports ERP is a **desktop application** (`.exe`) for managing Esports lounges / gaming cafes. It is a **single EXE installer** that can run in two modes:

### ONE EXE — TWO MODES

```
┌──────────────────────────────────────────────────┐
│                                                  │
│         AppleEsports.exe (Single Installer)      │
│                                                  │
│    On first launch, choose your mode:            │
│                                                  │
│    ┌────────────────┐   ┌────────────────┐       │
│    │  🖥️ SERVER      │   │  🎮 CLIENT      │       │
│    │  MODE          │   │  MODE          │       │
│    │                │   │                │       │
│    │  I am the      │   │  I am a        │       │
│    │  Operator /    │   │  Gaming PC     │       │
│    │  Billing PC    │   │                │       │
│    └────────────────┘   └────────────────┘       │
│                                                  │
└──────────────────────────────────────────────────┘
```

| Mode | Installed On | What It Does |
|---|---|---|
| **Server Mode** | Operator / Billing PC (1 per branch) | Runs the full server, database, and operator dashboard. Controls all gaming PCs. |
| **Client Mode** | Every Gaming PC in the branch | Lightweight mode. Connects to the Operator PC over LAN. Lets customers request sessions, see timers, order food. |

### Key Principle
> **The Operator PC runs in Server Mode — it is the brain of the branch.** All Gaming PCs run in Client Mode and connect to the Operator PC over LAN. No internet is required for day-to-day operations. The software works fully offline within the branch.

---

## 2. HOW THE SYSTEM WORKS (FULL FLOW)

### Scenario: A Customer Walks In

```
Step 1:  Customer enters the gaming lounge
Step 2:  Customer sits at any available Gaming PC (e.g., PC-05)
Step 3:  PC-05 has AppleEsports.exe installed in CLIENT MODE and running
Step 4:  Customer sees the Client interface on screen
Step 5:  Customer enters their name/phone → clicks "Request Session"
Step 6:  On the Operator PC (SERVER MODE), a notification pops up:
         "💬 PC-05: Walk-in customer 'Rahul' is requesting a session"
Step 7:  Operator clicks "Approve" → Session starts on PC-05
Step 8:  Customer's app now shows a live timer counting up
Step 9:  Customer plays GTA 5, Valorant, etc.
Step 10: Customer wants food → Opens food menu in the Client App → Orders a burger
Step 11: Operator sees the food order → Prepares it → Marks as delivered
Step 12: Customer is done playing → Clicks "End Session" or Operator stops it
Step 13: Operator PC calculates the bill:
         - Gaming: 2 hours × ₹60/hr = ₹120
         - Food: 1 Burger = ₹80
         - Total: ₹200
Step 14: Customer pays at the counter → Operator finalizes the bill
Step 15: PC-05 goes back to "Idle" state → Ready for next customer
```

### Scenario: Internet Goes Down

```
Step 1: Internet goes down at the branch
Step 2: NOTHING CHANGES! Everything runs on LAN.
Step 3: Operator PC and all Gaming PCs are connected via LAN switch/router
Step 4: Sessions keep running, billing keeps working, food orders keep flowing
Step 5: Internet comes back
Step 6: The Operator PC's sync service detects internet connectivity
Step 7: All data (sessions, bills, food orders, EOD reports) automatically
        syncs to AWS Cloud in the background
Step 8: Super Admin sitting at home can now see all the updated data
```

### Scenario: Customer Wants To Transfer PC

```
Step 1: Customer is playing on PC-01
Step 2: PC-01 has some issue (mouse broken, monitor flickering, etc.)
Step 3: Operator drags PC-01 → drops on PC-05 (on the Operator Dashboard)
Step 4: Session transfers from PC-01 to PC-05
Step 5: Customer moves to PC-05 and continues playing
Step 6: Transfer is logged: [Transfer: PC-01 → PC-05 at 15:30]
Step 7: This transfer history appears in EOD reports and billing
```

### Scenario: Customer Is A Member

```
Step 1: Member walks in → sits at PC-03
Step 2: Opens the Client App → enters phone number or scans QR
Step 3: App recognizes them as a member → shows their wallet balance
Step 4: Operator approves the session → member rate applied automatically
Step 5: At checkout, amount is deducted from wallet (or member pays cash)
```

---

## 3. INSTALLATION FLOW (Single EXE)

### Step-by-Step for Operator PC (Server Mode)

```
1. Download `AppleEsports.exe` (single file, ~200MB)
2. Double-click → Install wizard opens
3. Select mode: "🖥️ SERVER MODE (Operator / Billing PC)"
4. Click "Install" → Everything installs silently:
   - PostgreSQL database is set up automatically
   - API server is configured
   - Operator Dashboard is bundled
   - Client Mode components are also included (same EXE)
5. Software launches automatically
6. First-time setup wizard appears:
   - Enter branch name (e.g., "Apple Esports — Andheri")
   - Create Super Admin username & password
   - Software detects LAN IP automatically (e.g., 192.168.1.100)
   - Displays: "Your Gaming PCs should connect to: 192.168.1.100"
7. Done! Server is running. Operator Dashboard is ready.
```

### Step-by-Step for Gaming PCs (Client Mode)

```
1. Copy same `AppleEsports.exe` to a USB drive
2. Go to each Gaming PC → plug USB → double-click the installer
3. Select mode: "🎮 CLIENT MODE (Gaming PC)"
4. Click "Install" → Installs only the lightweight client components
   (No database, no API server — just the Client UI)
5. On first launch, setup screen appears:
   - "Enter Operator PC's IP Address: [192.168.1.100]"
   - "Enter this PC's number: [PC-05]"
6. Clicks "Connect" → App connects to the Operator PC's server over LAN
7. Done! App auto-starts with Windows from now on.
```

### Auto-Configuration Tasks (Server Mode)
- [ ] Auto-create database and run all migrations on first launch
- [ ] Auto-detect the Operator PC's LAN IP address
- [ ] Configure API to listen on LAN so Gaming PCs can connect
- [ ] Generate a unique Branch ID
- [ ] Create a Windows desktop shortcut
- [ ] Set the API server to auto-start with Windows (Windows Service)
- [ ] Auto-configure Windows Firewall to allow LAN traffic
- [ ] Display the LAN IP for Gaming PCs on the dashboard

### Auto-Configuration Tasks (Client Mode)
- [ ] Save the Operator PC's IP address locally
- [ ] Auto-connect to the server on every startup
- [ ] Register as a startup app (auto-start with Windows)
- [ ] Create a Windows desktop shortcut

---

## 4. SERVER MODE — OPERATOR PC

### What Gets Activated In Server Mode

| Component | What It Is | Why It's Needed |
|---|---|---|
| **PostgreSQL (Embedded)** | Local database | Stores all sessions, bills, members, food orders, etc. |
| **.NET 8 Runtime** | Application runtime | Runs the backend API server |
| **ASP.NET Core API** | Backend server | Handles all business logic, billing, session management |
| **React Frontend (Built)** | Operator Dashboard UI | Sessions grid, billing, food, EOD, reports, settings |
| **Desktop Shell** | Wraps everything into a desktop app | Native Windows application |

### Operator Dashboard Modules (Already Built)

- **Session Dashboard** — PC Grid, active sessions, drag-and-drop transfer
- **Billing Counter** — Generate bills, apply discounts, process payments
- **Food Orders** — Take orders, assign to PCs or walk-in, track status
- **Members Management** — Add members, manage wallets, view history
- **Cash Register** — Opening balance, petty expenses, denomination counting
- **End of Day (EOD)** — PC-wise billing, revenue summary, finalize day
- **Reports** — Date range reports, billing audit logs, discrepancy tracking
- **Settings** — Branch setup, PC management, pricing profiles, menu items

---

## 5. CLIENT MODE — GAMING PC

### What Gets Activated In Client Mode

| Component | What It Is | Why It's Needed |
|---|---|---|
| **Client UI** | Customer-facing screens | Session request, live timer, food ordering |
| **Desktop Shell** | Wraps UI into a desktop app | Native Windows application |
| **LAN Connector** | Network module | Connects to the Operator PC's API over LAN |

### What Does NOT Run In Client Mode
- ❌ No database — all data lives on the Operator PC
- ❌ No backend server — it only talks to the Operator PC's server
- ❌ Much lighter on resources — doesn't slow down gaming performance

### What The Customer Sees On The Gaming PC

**Before Session (Idle):**
```
┌──────────────────────────────────────────────────┐
│            🎮 APPLE ESPORTS                      │
│                                                  │
│         Welcome to Apple Esports!                │
│                                                  │
│    ┌──────────────────────────────────────┐       │
│    │  Your Name: [___________________]   │       │
│    │  Phone:     [___________________]   │       │
│    │                                     │       │
│    │  [ 🎮 Request Session ]             │       │
│    │                                     │       │
│    │  Already a member? [ Login ]        │       │
│    └──────────────────────────────────────┘       │
│                                                  │
│           This is PC-05 | Zone: Premium          │
│        Connected to Server ✅ | LAN: Active      │
└──────────────────────────────────────────────────┘
```

**During Session (Active):**
```
┌──────────────────────────────────────────────────┐
│            🎮 APPLE ESPORTS                      │
│                                                  │
│    Session Active — PC-05                        │
│    Customer: Rahul | Walk-in                     │
│                                                  │
│    ┌──────────────────────────────────────┐       │
│    │                                     │       │
│    │          ⏱️  1h 23m 45s              │       │
│    │          ₹84.00 running             │       │
│    │                                     │       │
│    └──────────────────────────────────────┘       │
│                                                  │
│    [ 🍔 Order Food ]    [ 📞 Call Operator ]     │
│                                                  │
│    [ ⏹️ End Session ]                             │
│                                                  │
│        Connected to Server ✅ | LAN: Active      │
└──────────────────────────────────────────────────┘
```

### Client Mode Features (To Build)
- [ ] Auto-connect to Operator PC server over LAN on startup
- [ ] Show which PC this is (PC number, zone, pricing)
- [ ] Session request form (name, phone, walk-in/member)
- [ ] Member login (phone + OTP or wallet scan)
- [ ] Live session timer display (counting up with live charge)
- [ ] Food menu browsing and ordering from the gaming PC
- [ ] "Call Operator" button (sends alert to operator dashboard)
- [ ] Session end request
- [ ] Connection status indicator (Connected ✅ / Disconnected ❌)
- [ ] Auto-start with Windows (so the app is always ready when PC boots)
- [ ] Full-screen / kiosk mode (customer can't close the app easily)
- [ ] Minimize to system tray so customer can play games

---

## 6. LAN COMMUNICATION (Offline Capability)

### Why LAN Is The Foundation

| Situation | What Happens |
|---|---|
| Internet is working | Everything works normally. Data syncs to AWS in background. |
| Internet goes down | **Everything still works!** All communication is over LAN. |
| LAN switch/router fails | Gaming PCs lose connection to Operator PC. Fix the network hardware. |

### Communication Flow (Over LAN)
```
Gaming PC (Client Mode)  ←──── LAN ────→  Operator PC (Server Mode)
     │                                         │
     │  "Start session for Rahul on PC-05"     │
     │  ─────────────────────────────────→      │
     │                                         │
     │  "Session approved. Timer started."     │
     │  ←─────────────────────────────────      │
     │                                         │
     │  "Order: 1x Burger for PC-05"           │
     │  ─────────────────────────────────→      │
     │                                         │
     │  "Food order received. Preparing."      │
     │  ←─────────────────────────────────      │
     │                                         │
     │  "End session on PC-05"                 │
     │  ─────────────────────────────────→      │
     │                                         │
     │  "Bill: ₹200. Awaiting payment."        │
     │  ←─────────────────────────────────      │
```

### Real-Time Updates (SignalR over LAN)
We already use **SignalR** for real-time updates. This works perfectly over LAN:
- Operator approves session → Client App instantly shows the timer
- Customer orders food → Operator dashboard instantly shows the order
- Operator stops a session → Client App instantly shows "Session Ended"
- All real-time, all over LAN, zero internet dependency

---

## 7. AWS CLOUD SYNC (Phase 2 — Future)

When we add AWS Cloud later, we just add a **sync layer** on top. The core software doesn't change.

### Sync Behavior
- **Internet available** → Real-time sync every 30 seconds
- **Internet goes down** → All changes queued locally on Operator PC
- **Internet comes back** → Queued data auto-syncs to AWS immediately
- **Conflict resolution** → Local data wins (Operator PC is the source of truth)

### Why AWS?
- **Super Admin remote access** — View all branches from anywhere
- **Data backup** — If Operator PC fails, all data is safe on AWS
- **Multi-branch analytics** — Compare performance across all branches
- **Customer app (future)** — Members check wallet, book slots from phone

---

## 8. NETWORK ARCHITECTURE DIAGRAM

```
┌──────────────────────────────────────────────────────────────┐
│                        BRANCH (LAN)                          │
│                                                              │
│   ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │
│   │ Gaming PC 1 │  │ Gaming PC 2 │  │ Gaming PC 3 │  ...    │
│   │ CLIENT MODE │  │ CLIENT MODE │  │ CLIENT MODE │         │
│   └──────┬──────┘  └──────┬──────┘  └──────┬──────┘         │
│          │                │                │                 │
│          └────────────────┼────────────────┘                 │
│                           │                                  │
│                    ┌──────▼──────┐                            │
│                    │ LAN Switch  │                            │
│                    │  / Router   │                            │
│                    └──────┬──────┘                            │
│                           │                                  │
│                    ┌──────▼──────┐                            │
│                    │ OPERATOR PC │                            │
│                    │ SERVER MODE │                            │
│                    │ (Database + │                            │
│                    │  API +      │                            │
│                    │  Dashboard) │                            │
│                    └──────┬──────┘                            │
│                           │                                  │
└───────────────────────────┼──────────────────────────────────┘
                            │
                       Internet
                    (When Available)
                            │
                     ┌──────▼──────┐
                     │  AWS Cloud   │
                     │  (Phase 2)   │
                     │              │
                     │  • Backup    │
                     │  • Remote    │
                     │    Super     │
                     │    Admin     │
                     │  • Multi-    │
                     │    Branch    │
                     │    Analytics │
                     └─────────────┘
```

---

## 9. EXE PACKAGING CHECKLIST

### Single Installer: `AppleEsports.exe`
- [ ] Use **Inno Setup** or **NSIS** for creating the Windows installer
- [ ] Installer must present a mode selection screen: Server Mode vs Client Mode
- [ ] **Server Mode** installs everything (DB + API + Dashboard + Client components)
- [ ] **Client Mode** installs only the lightweight client UI (no DB, no API)
- [ ] .NET API published as **self-contained** (`dotnet publish -r win-x64 --self-contained`)
- [ ] Embed PostgreSQL (use **EmbeddedPostgres** NuGet or bundle portable PostgreSQL)
- [ ] Alternatively, evaluate **SQLite** for simpler deployments
- [ ] Pre-build React frontend (`npm run build`) and bundle `dist/` folder
- [ ] Wrap in **Electron** or **Tauri** desktop shell for native Windows feel
- [ ] API must listen on `0.0.0.0` (all interfaces) for LAN access
- [ ] Auto-configure Windows Firewall rules during installation
- [ ] Register as startup app (auto-start with Windows)
- [ ] Include an uninstaller that cleanly removes everything

---

## 10. DECISIONS TO MAKE LATER

- [ ] **Embedded database**: PostgreSQL portable vs SQLite?
- [ ] **Installer framework**: Inno Setup vs NSIS vs WiX?
- [ ] **Desktop shell framework**: Electron vs Tauri vs .NET MAUI?
- [ ] **Auto-update mechanism**: Check for updates on startup?
- [ ] **Licensing / activation key system**: How to prevent piracy?
- [ ] **Client Mode UI design**: Exact screens for the customer-facing app
- [ ] **Kiosk mode**: Should Client Mode lock the desktop?
- [ ] **Multi-monitor support**: Some gaming setups have dual monitors
