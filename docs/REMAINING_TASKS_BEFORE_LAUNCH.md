# Remaining Tasks Before Launch (EXE Creation)

This checklist tracks all pending development and configuration tasks that must be completed BEFORE we compile the final .exe and hand the software over to the client.

## 1. UI & Bug Fixes
- [ ] **Jetty Credit Reconciliation**: The backend Docker container has been updated for credit clearances, but the frontend ReportsPage.jsx UI needs to be aligned to correctly display settled credits.

## 2. Database Migration (The Clean Wipe)
- [ ] **Switch to PostgreSQL**: Update the Entity Framework Core provider in the .NET 8 backend from SQL Server to PostgreSQL.
- [ ] **Create Data Seeder**: Write a C# script to automatically inject the Admin Passwords, Branch details, and PC profiles into the new empty database.

## 3. Network & Configuration
- [ ] **Dynamic IP Setup**: Remove hardcoded localhost:5016 references in the React/Electron frontend. Ensure the .exe can point to the Local Server's IP address (e.g., 192.168.1.100) via an .env file or config.
- [ ] **Dynamic Email Links**: Replace hardcoded `http://localhost:5173` in backend email templates (Welcome Email, Password Reset) with a dynamic `FRONTEND_URL` environment variable for when the app is deployed to the Oracle server.
- [ ] **Cloudflare Tunnel Setup**: Install and configure the free Cloudflare Tunnel on the Counter PC so the Super Admin can view the dashboard from home.

## 4. Distributed Sync Engine (MVP)
- [ ] **Local Sync Worker**: Build a .NET Background Service that pushes local sessions/orders up to the Oracle Cloud Server.
- [ ] **Oracle Cloud Receiver**: Set up the Oracle Free Tier server to receive and store the synced data.

## 5. Final Packaging
- [ ] **React Build**: Run 
pm run build on the frontend.
- [ ] **Electron Packager**: Package the PC Overlay application into a silent .exe that launches on Windows startup.