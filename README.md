# AppleEsports Pro — Gaming Café ERP System

> Enterprise Gaming Café Management Platform  
> Centralized Multi-Branch Real-Time Operations System

## System Overview

This is **NOT** simple billing software. This is an enterprise-grade:

- **Branch Management System**
- **Live Session Engine**
- **Operator Workflow System**
- **Real-Time PC Control System**
- **Reservation Engine**
- **Gaming Café POS**
- **Operator Accountability System**

## Tech Stack

| Layer      | Technology                      |
| ---------- | ------------------------------- |
| Frontend   | React + Vite + TailwindCSS v3  |
| Backend    | Node.js + Express.js           |
| Database   | PostgreSQL 16 (Docker)         |
| Auth       | JWT + bcrypt                    |
| Real-Time  | Socket.IO                      |
| DevOps     | Docker Compose                 |

## Quick Start

### Prerequisites
- Node.js 18+
- Docker Desktop

### 1. Start Database
```bash
docker-compose up -d
```
PostgreSQL runs on `localhost:5432`  
pgAdmin available at `http://localhost:5050`

### 2. Start Backend
```bash
cd server
npm install
npm run dev
```

### 3. Start Frontend
```bash
cd client
npm install
npm run dev
```

## Architecture

```
                    CENTRAL SERVER
            (PostgreSQL + Express APIs + Socket.IO)

                         │
        ┌────────────────┼────────────────┐
        │                │                │

        ▼                ▼                ▼

   Branch A         Branch B         Branch C

        │                │                │

   Operators        Operators        Operators
   PCs              PCs              PCs
   User Panels      User Panels      User Panels
```

## Role Hierarchy

| Role        | Access Level              |
| ----------- | ------------------------- |
| Super Admin | Full system control       |
| Operator    | Branch operational access |
| User Panel  | Lightweight gaming panel  |

## Project Structure

```
├── server/                 # Backend
│   └── src/
│       ├── config/         # Database, environment, socket config
│       ├── controllers/    # Request handlers
│       ├── database/       # Schema, seeds, migrations
│       ├── middleware/      # Auth, RBAC, audit, branch isolation
│       ├── routes/         # API route definitions
│       ├── services/       # Business logic layer
│       ├── sockets/        # Socket.IO event handlers
│       └── utils/          # Logger, constants, helpers
├── client/                 # Frontend
│   └── src/
│       ├── api/            # API service layer
│       ├── components/     # Reusable components
│       ├── config/         # API, socket, constants config
│       ├── contexts/       # Auth, Socket, Branch contexts
│       ├── hooks/          # Custom React hooks
│       ├── layouts/        # App shell, sidebar
│       └── pages/          # Dashboard pages (future)
├── docs/                   # SOP documentation
├── docker-compose.yml      # PostgreSQL + pgAdmin
└── README.md
```

## Documentation

All operational logic is defined in `/docs/MASTER_SOP.md` — the single source of truth for this system.
