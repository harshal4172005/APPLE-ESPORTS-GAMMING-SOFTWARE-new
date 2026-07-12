# Apple Esports - Distributed Sync Architecture (MVP Plan)

## 1. Executive Summary
This document outlines the Minimum Viable Product (MVP) plan for the **Master-Local Distributed Architecture**. This enterprise-grade design ensures that physical gaming cafes remain 100% operational during internet outages, while simultaneously feeding real-time data to a central Cloud Server for remote Super Admin management.

## 2. Core Architecture Topology

The system is divided into two distinct components that talk to each other:

### A. The Local Edge Server (Inside the Cafe)
- **Hardware:** Runs on the main Counter PC.
- **Role:** Acts as the local brain for the branch. All gaming PCs, the Operator Dashboard, and local overlays connect to this server via the local IP address (e.g., `192.168.1.100`).
- **Resilience:** Because all connections are on the Local Area Network (LAN), physical operations (timers, billing, ordering) are completely immune to fiber cuts or Wi-Fi outages.

### B. The Master Cloud Server (Oracle Cloud Free Tier)
- **Hardware:** Oracle Cloud Always Free Tier (ARM A1, 4 Cores, 24GB RAM).
- **Role:** Acts as the central "Watchtower" and global database. Super Admins connect to this server via a public URL (e.g., `admin.appleesports.com`) from anywhere in the world.
- **Database:** Runs **PostgreSQL** (which is fully compatible with ARM processors and completely free).

---

## 3. The Synchronization Engine (How it works)

The magic of this architecture lies in the Background Sync Service running on the Local Server.

### State 1: Internet is ONLINE (Real-Time Mode)
1. A user starts a session on PC-01.
2. The Operator PC sends the start command to the **Local Server**.
3. The Local Server immediately saves the session to the local database.
4. The Local Server's Sync Engine instantly fires a webhook/API call to the **Master Cloud Server**, duplicating the session data in the cloud.
5. *Result:* The Super Admin sitting at home sees the session start in real-time.

### State 2: Internet is OFFLINE (Local Mode)
1. The cafe's internet connection drops completely.
2. A user ends a session and orders food.
3. The Operator PC sends the command to the **Local Server** over the LAN. 
4. The Local Server saves the data and tries to send it to the Master Cloud Server. The connection fails.
5. The Local Server tags the data as `SyncPending = true` and holds it securely in a local offline queue.
6. *Result:* The cafe continues making money with zero interruptions. The Super Admin at home temporarily sees the branch as "Offline".

### State 3: Internet is RESTORED (Re-Sync Mode)
1. The cafe's internet comes back online.
2. The Local Server's Background Worker detects the connection.
3. It grabs all data tagged with `SyncPending = true` (the food orders, the ended sessions, the bills) and bulk-pushes them to the Master Cloud Server.
4. *Result:* The Cloud Server is perfectly updated, and no financial data is ever lost.

---

## 4. Execution Plan (Roadmap to Build)

To upgrade the current codebase to this MVP architecture, we will execute the following phases:

### Phase 1: Database Migration
- Swap the Entity Framework database provider from `SQL Server` to `PostgreSQL`.
- Create the fresh `PostgreSQL` migration and test it locally.
- *Why:* This allows the Cloud Server to run on the powerful 24GB Oracle ARM server for 0 Rs.

### Phase 2: Local Server Sync Agent
- Add a new Background Service (`IHostedService` in .NET) to the Local API.
- Create a `SyncQueue` table in the database to track which rows (Sessions, Orders) haven't been pushed to the cloud yet.
- Write the logic that attempts to push data to the Cloud API and handles failures gracefully.

### Phase 3: Cloud Receiver API
- Deploy the Master Server to Oracle Cloud.
- Build specific API endpoints (e.g., `/api/sync/receive`) on the Cloud Server that are designed strictly to accept incoming data dumps from Local Branch Servers.

### Phase 4: Wiping the Slate Clean
- Run the SQL wipe script to clear all fake test data (Sessions, Bills, Logs) from the local machine.
- Generate the final branch `.exe` files pointing to the Local IP, and deliver the system to the client.

---
*Note: Any additional features or "add-ons" mentioned by the developers will be appended to this document during the execution phase.*
