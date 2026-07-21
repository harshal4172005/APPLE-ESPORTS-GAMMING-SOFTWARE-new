# 🎮 Apple Esports — Gaming Café ERP System

<div align="center">

![.NET](https://img.shields.io/badge/.NET_8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![React](https://img.shields.io/badge/React_19-61DAFB?style=for-the-badge&logo=react&logoColor=black)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL_16-336791?style=for-the-badge&logo=postgresql&logoColor=white)
![SignalR](https://img.shields.io/badge/SignalR-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=for-the-badge&logo=docker&logoColor=white)
![TailwindCSS](https://img.shields.io/badge/Tailwind_CSS-38B2AC?style=for-the-badge&logo=tailwind-css&logoColor=white)
![Redis](https://img.shields.io/badge/Redis_7-DC382D?style=for-the-badge&logo=redis&logoColor=white)
![Nginx](https://img.shields.io/badge/Nginx-009639?style=for-the-badge&logo=nginx&logoColor=white)

**A production-grade, full-stack Enterprise Resource Planning system for a multi-branch gaming café chain.**

*Real-time PC management • Automated billing • Digital wallets • Multi-branch operations • Financial reconciliation*

---

[Features](#-key-features) · [Architecture](#-architecture) · [Tech Stack](#️-tech-stack) · [Getting Started](#-getting-started) · [API Docs](#-api-documentation) · [Branches](#-live-branch-network)

---

</div>

## 🎯 Overview

**Apple Esports ERP** is a comprehensive, real-time business management platform built from scratch for a gaming café chain operating **4 branches across Surat, India** — managing **106 gaming PCs**, **8 operators**, member wallets, food orders, cash registers, and end-of-day financial reconciliation — all synchronized in real-time via WebSockets.

### The Problem
Gaming cafés juggle dozens of PCs, track session durations, handle split payments, manage food orders, and reconcile cash — all simultaneously across multiple locations. Manual systems cause billing errors, revenue leakage, and operational chaos.

### The Solution
A unified real-time platform with:
- 🖥️ **Live PC overlays** on every gaming machine showing session countdowns
- 💰 **Automated billing** calculated from actual usage time with split payments
- 👛 **Digital member wallets** (Gaming + Food) for cashless operations
- ⚡ **Real-time dashboards** synced via 8 WebSocket hubs across all operator screens
- 🔒 **Immutable EOD snapshots** for tamper-proof financial auditing
- 🏢 **Multi-branch isolation** — operators see only their branch, SuperAdmin sees everything

---

## 🏢 Live Branch Network

The system manages **4 active branches** with dedicated operators, zones, and pricing:

| Branch | Location | PCs | Zones | Rate Range |
|:---|:---|:---:|:---|:---|
| **Adajan** | Opp. Honey Park, Adajan, Surat | 16 | Pro Combat Desk | ₹60/hr |
| **Citylight** | Citylight, Surat | 35 | Champion Zone, Elite War Zone | ₹50–60/hr |
| **Katargam** | Opp. Gajera School, Katargam, Surat | 32 | Recruit Deck, Veteran Stand, VIP Elite Hub | ₹60–80/hr |
| **Varachha** | Elita Square, Mota Varachha, Surat | 23 | Titan Desk, God-Tier Arena, Sofa Club (PS5) | ₹80–100/hr |

> **Total: 106 Gaming PCs · 4 Branches · 8 Operators · 10 Pricing Zones**

### Hardware Specs Managed

| Zone | Specs |
|:---|:---|
| Recruit / Champion | i5 11th Gen · GTX 1660 Ti · 144–165Hz |
| Pro Combat / Elite / Veteran | i5 13th Gen · RTX 2060–4060 Ti · 240Hz |
| Titan / God-Tier | i7 14th Gen · RTX 5060 Ti · 240–400Hz |
| Sofa Club | PlayStation 5 · 4K OLED |

---

## ✨ Key Features

### 🖥️ Real-Time PC Session Management
- Live session tracking with countdown timers across 106 gaming PCs
- Gaming PC Agent connects via SignalR for remote lock/unlock commands
- Auto-lock on session expiry with audio + voice alerts (10min, 5min, 1min warnings)
- Support for **prepaid** (fixed duration) and **postpaid** (pay-as-you-go) sessions
- Per-zone hourly pricing with configurable profiles

### 🎮 Gaming PC Overlay System
- Full-screen lock screen on idle PCs with walk-in & member login options
- Real-time session HUD: time remaining, live bill accumulation, food orders
- Member self-service: start sessions, extend time, place food orders, call operator
- Wallet approval modal for deductions requiring member password confirmation
- Operator notification system for walk-in session approvals

### 💰 Automated Billing Engine
- Auto-generates bills on session completion (gaming charges + food charges)
- **Split payment** support: Cash, Online (UPI/Card), and Wallet deductions
- Configurable discount system (percentage or fixed amount)
- Bill lifecycle: `Pending → Processing → Completed` with full audit trail
- Optimistic concurrency (PostgreSQL xmin) to prevent race conditions

### 👤 Member Management & Digital Wallet
- Member registration with unique IDs (e.g., `AEI-M-0001`)
- **Dual wallet system**: Gaming Balance + Food Balance
- Wallet top-ups with full transaction history
- Session self-start from gaming PCs using wallet balance
- Loyalty points tracking and home branch assignment

### 📅 Reservation System
- PC-specific time-slot reservations with conflict detection
- Configurable grace periods with auto-expiry for no-shows
- Advance deposit tracking and state management (`Pending → Started → Completed/Expired`)

### 🍔 Food Order Management
- Menu editor with categories, pricing, and inventory tracking
- Orders placed from gaming PC overlay or operator dashboard
- Kitchen display integration via real-time SignalR events
- Auto-added to session bill on completion

### 🏦 Cash Register & Shift Management
- Register open/close lifecycle with opening balance
- **Denomination-level cash counting** (₹2000, ₹500, ₹200, ₹100, ₹50, ₹20, ₹10, ₹5, ₹2, ₹1)
- Petty expense tracking with categorized deductions
- Discrepancy detection: expected vs. actual cash in drawer
- Multi-operator shift support per register

### 📊 End of Day (EOD) Dashboard
- **PC-wise daily earnings** breakdown with revenue per machine
- Revenue summary: Gaming, Food, Discounts, Net Revenue
- Cash lifecycle: Opening → Sales → Expenses → Expected → Counted → Discrepancy
- Payment collection breakdown: Cash, Online, Wallet
- Financial validation checks before allowing finalization
- **Immutable snapshots** — permanently locked, tamper-proof records

### 🏢 Multi-Branch Architecture
- **4-branch data isolation** via JWT claims — operators see only their branch
- SuperAdmin cross-branch visibility, switching, and management
- Branch-scoped SignalR groups for targeted real-time updates
- Per-branch pricing profiles, operators, and system configuration

### ⚡ Real-Time Synchronization
8 dedicated **SignalR WebSocket Hubs**:

| Hub | Purpose |
|:---|:---|
| `SessionHub` | Session state changes (start/stop/extend) |
| `BillingHub` | Bill creation and payment updates |
| `ReservationHub` | Reservation state transitions |
| `PcStatusHub` | PC state changes + Agent lock/unlock commands |
| `PcOverlayHub` | Gaming PC overlay events (session data, wallet approval) |
| `FoodOrderHub` | Food order placement and status updates |
| `CashHub` | Cash register operations |
| `NotificationHub` | Cross-cutting alerts and operator calls |

---

## 🛠️ Tech Stack

| Layer | Technology |
|:---|:---|
| **Frontend** | React 19, Vite 5, TailwindCSS 3, Framer Motion, React Router v7, Axios |
| **Backend** | .NET 8, ASP.NET Core Web API, Entity Framework Core 8, C# |
| **Real-time** | SignalR WebSockets (8 hubs) with auto-negotiation (WS → SSE → Long Polling) |
| **Database** | PostgreSQL 16 with EF Core Code-First Migrations |
| **Auth** | JWT (Access + Refresh tokens), BCrypt hashing, Role-based Authorization |
| **Email** | SMTP (Gmail) with HTML templated emails for password resets |
| **Caching** | Redis 7 (SignalR backplane for horizontal scaling) |
| **Deployment** | Docker Compose (7 services), Nginx reverse proxy, Let's Encrypt SSL |
| **Logging** | Serilog (structured logging to console + file) |
| **API Docs** | Swagger / OpenAPI (Swashbuckle) |

---

## 🏗 Architecture

### System Architecture

```
                        ┌─────────────────────────────────────────────┐
                        │              CENTRAL SERVER                 │
                        │   (.NET 8 API + PostgreSQL + SignalR)       │
                        └──────────────────┬──────────────────────────┘
                                           │
                    ┌──────────┬───────────┼───────────┬──────────┐
                    │          │           │           │          │
                    ▼          ▼           ▼           ▼          ▼
              ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐
              │  Adajan   │ │Citylight │ │ Katargam │ │ Varachha │
              │ 16 PCs   │ │ 35 PCs   │ │ 32 PCs   │ │ 23 PCs   │
              │ 2 Ops    │ │ 2 Ops    │ │ 2 Ops    │ │ 2 Ops    │
              └────┬─────┘ └────┬─────┘ └────┬─────┘ └────┬─────┘
                   │             │             │             │
              ┌────┴────┐  ┌────┴────┐  ┌────┴────┐  ┌────┴────┐
              │Operators │  │Operators │  │Operators │  │Operators │
              │PCs       │  │PCs       │  │PCs       │  │PCs       │
              │Overlays  │  │Overlays  │  │Overlays  │  │Overlays  │
              │Members   │  │Members   │  │Members   │  │Members   │
              └──────────┘  └──────────┘  └──────────┘  └──────────┘
```

### Clean Architecture (Backend)

```
AppleEsportsErp/
├── Domain/              → 27 Entities, Enums, Value Objects
├── Application/         → Interfaces, DTOs, Service Contracts, Constants
├── Infrastructure/      → 19 Services, EF Core DbContext, Migrations
├── Api/                 → 22 Controllers, 8 SignalR Hubs, Middleware
└── ClientAgent/         → Gaming PC Agent (SignalR client for lock/unlock)
```

### Frontend Architecture

```
client/src/
├── contexts/            → Auth, Branch, Socket (global state management)
├── pages/               → 14 feature modules
│   ├── sessions/        → PC session management dashboard
│   ├── billing/         → Bill processing & split payments
│   ├── overlay/         → Gaming PC overlay (lock screen, HUD, wallet)
│   ├── cash/            → Cash register & desk management
│   ├── eod/             → End of day financial dashboard
│   ├── members/         → Member CRUD & wallet operations
│   ├── food/            → Food order management
│   ├── reservations/    → Reservation system
│   ├── dashboard/       → SuperAdmin multi-branch dashboard
│   ├── admin/           → Settings, system config, PC management
│   ├── menu/            → Menu & inventory editor
│   ├── finance/         → Wallet desk & online desk
│   └── public/          → Login, setup, password reset pages
├── components/          → Reusable UI (modals, cards, drawers, toasts)
├── api/                 → API service layer (Axios interceptors)
└── config/              → Environment & API configuration
```

### Deployment Architecture (Docker Compose)

```
┌──────────────────────────────────────────────────────────┐
│                    Docker Compose                        │
│                    (7 Services)                           │
│                                                          │
│  ┌─────────┐  ┌──────────┐  ┌──────────┐  ┌─────────┐  │
│  │  Nginx  │──│ React    │  │ .NET 8   │──│PostgreSQL│  │
│  │ Reverse │  │ Frontend │  │ API +    │  │   16    │  │
│  │ Proxy   │  │ (Vite)   │  │ SignalR  │  │         │  │
│  └─────────┘  └──────────┘  └────┬─────┘  └────┬────┘  │
│                                  │              │       │
│  ┌─────────┐  ┌──────────┐  ┌───┴──────┐  ┌───┴─────┐ │
│  │Certbot  │  │  Redis   │  │ Serilog  │  │DB Backup│ │
│  │SSL Auto │  │Backplane │  │ Logging  │  │  Daily  │ │
│  └─────────┘  └──────────┘  └──────────┘  └─────────┘ │
└──────────────────────────────────────────────────────────┘
```

---

## 🚀 Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/)
- [PostgreSQL 16](https://www.postgresql.org/download/)
- [Docker](https://www.docker.com/) *(optional, for containerized deployment)*

### Option 1: Docker Compose (Recommended)

```bash
# Clone the repository (always use this repo — not a teammate's older fork)
git clone https://github.com/harshal4172005/APPLE-ESPORTS-GAMMING-SOFTWARE-new.git
cd APPLE-ESPORTS-GAMMING-SOFTWARE-new

# Create your environment file and fill in real values (DB password, JWT secrets, SMTP creds)
cp .env.example .env

# Build and start all services — always include --build so you get the CURRENT
# code, not a stale cached image left over from a previous run
docker compose up -d --build

# Access the application
# Frontend:  http://localhost:8081        (served via nginx)
# API:       http://localhost:5016
# Swagger:   http://localhost:5016/swagger
```

> **Getting "old version" or missing-feature behavior after a `git pull`?**
> It's almost always one of these two things:
> 1. You ran `docker compose up -d` without `--build` — Docker reused the old image instead of rebuilding from the new code. Always redeploy with `docker compose up -d --build`.
> 2. You (or whoever set up your clone) are on the wrong remote — double-check `git remote -v` and make sure `origin`/`new-origin` points at `harshal4172005/APPLE-ESPORTS-GAMMING-SOFTWARE-new`, not an older fork.

### Option 2: Local Development

```bash
# 1. Start PostgreSQL and create the database
createdb gamecafe_erp

# 2. Start the Backend
cd AppleEsportsErp/src/AppleEsportsErp.Api
dotnet restore
dotnet run

# 3. Start the Frontend (in a new terminal)
cd client
npm install
npm run dev
```

### First-Time Setup

On first launch, the system auto-seeds (password for every seeded account is **`12345`** — change these before any real/public deployment):
- **SuperAdmin account** — `admin@appleesports.com`
- **4 Branches** — Adajan, Citylight, Katargam, Varachha
- **106 Gaming PCs** — with zone assignments and pricing profiles
- **8 Operators** — 2 per branch with branch-locked access (e.g. `ankur_adajan`)
- **Test Member** — with pre-loaded wallet balance

---

## 📁 Project Structure

```
.
├── AppleEsportsErp/                 # .NET 8 Backend (Clean Architecture)
│   └── src/
│       ├── AppleEsportsErp.Api/              # 22 Controllers, 8 Hubs, Middleware
│       ├── AppleEsportsErp.Application/      # Interfaces, DTOs, Constants
│       ├── AppleEsportsErp.Domain/           # 27 Entities, Enums
│       ├── AppleEsportsErp.Infrastructure/   # 19 Services, DbContext, Migrations
│       └── AppleEsportsErp.ClientAgent/      # Gaming PC SignalR Agent
├── client/                          # React 19 Frontend (Vite + TailwindCSS)
│   └── src/
│       ├── pages/                   # 14 feature modules
│       ├── components/              # Shared UI components
│       ├── contexts/                # Global state (Auth, Branch, Socket)
│       └── api/                     # API service layer
├── nginx/                           # Nginx reverse proxy config
├── docker-compose.yml               # Full stack orchestration (7 services)
└── doc/                             # Project documentation
```

---

## 📖 API Documentation

The API is fully documented with **Swagger/OpenAPI**. After starting the backend:

```
http://localhost:5015/swagger
```

### Key API Endpoints

| Module | Method | Endpoint | Description |
|:---|:---|:---|:---|
| **Auth** | `POST` | `/api/auth/login-admin` | SuperAdmin login |
| **Auth** | `POST` | `/api/auth/login-operator` | Operator login (branch-scoped) |
| **Auth** | `POST` | `/api/auth/forgot-password` | Send password reset email |
| **Auth** | `POST` | `/api/auth/reset-password` | Complete password reset |
| **Sessions** | `POST` | `/api/sessions/start` | Start a PC session |
| **Sessions** | `POST` | `/api/sessions/{id}/stop` | Stop session & generate bill |
| **Billing** | `POST` | `/api/billing/{id}/pay` | Process split payment |
| **Members** | `GET` | `/api/members` | List all members |
| **Members** | `POST` | `/api/members` | Register new member |
| **Wallet** | `POST` | `/api/wallet/topup` | Top-up member wallet |
| **Reservations** | `POST` | `/api/reservations` | Create PC reservation |
| **Food Orders** | `POST` | `/api/food-orders` | Place food order |
| **Cash** | `POST` | `/api/cash-register/open` | Open cash register (shift start) |
| **Cash** | `POST` | `/api/cash-desk/close` | Close shift with cash count |
| **EOD** | `GET` | `/api/eod/preview` | Preview day's financials |
| **EOD** | `POST` | `/api/eod/finalize` | Lock immutable EOD snapshot |
| **PCs** | `GET` | `/api/pc-status` | Get all PC states (live) |
| **Reports** | `GET` | `/api/eod/range-report` | Date range revenue report |

---

## 📊 Project Metrics

| Metric | Count |
|:---|:---:|
| Branches | **4** |
| Total Gaming PCs | **106** |
| Pricing Zones | **10** |
| Domain Entities | **27** |
| Backend Services | **19** |
| API Controllers | **22** |
| SignalR Hubs | **8** |
| Frontend Modules | **14** |
| User Roles | **3** |
| Docker Services | **7** |

---

## 🔐 Security

- **JWT Authentication** — Access + Refresh token rotation with configurable expiry
- **BCrypt** — Password hashing with auto-generated salts
- **Role-based Authorization** — SuperAdmin, Operator, Member policies
- **Branch Isolation** — JWT claims enforce data boundaries
- **Rate Limiting** — Configurable per-window request throttling
- **CORS Whitelisting** — Restricted origin access
- **Email Verification** — Time-limited password reset tokens (1-hour expiry)
- **Optimistic Concurrency** — PostgreSQL xmin prevents financial race conditions
- **Offline Mode** — Emergency JWT tokens for internet-down scenarios

---

## 🤝 Role-Based Access

| Role | Access Level |
|:---|:---|
| **SuperAdmin** | Full access to all 4 branches. System config, EOD finalization, operator management, reporting. |
| **Operator** | Branch-locked. Session management, billing, cash register, food orders for assigned branch only. |
| **Member** | Self-service via PC overlay. Login, wallet payments, session start, food orders, time extensions. |

---

## 📄 License

This project is proprietary software developed for **Apple Esports, Surat, India**.

---

<div align="center">

**Built with ❤️ for Apple Esports**

*Managing 106 PCs across 4 branches in Surat, India*

*© 2026 Apple Esports. All rights reserved.*

</div>
