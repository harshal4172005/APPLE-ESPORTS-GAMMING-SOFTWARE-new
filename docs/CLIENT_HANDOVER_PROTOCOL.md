# Apple Esports ERP - Client Handover Protocol

This document outlines the exact procedure for taking a messy, internally tested build of the Apple Esports ERP and converting it into a 100% pristine, production-ready system for a new client.

## The Strategy: "The PostgreSQL Fresh Start"

Because the system is designed to run on the Oracle Cloud Free Tier, it requires a **PostgreSQL** database. 
Instead of running risky SQL "Delete" scripts on your local testing database (which can leave behind orphaned data), we use the database migration itself as the ultimate cleanup tool.

When you spin up the PostgreSQL database for the client, it starts completely empty. 
To ensure the client can actually log in, we use an **Entity Framework Data Seeder** to automatically inject your core configuration (PCs, Admin Logins, Menu Items) while perfectly ignoring all fake testing sessions and bills.

---

## Handover Checklist

### Step 1: Prepare the Core Configuration Data
Before migrating, you must ensure your C# `DataSeeder.cs` (or `ModelBuilder` extensions) contains the correct, clean starting values for this specific client:
- [ ] Define the `Branch` (Name, Address, Tax Rates).
- [ ] Define the `PricingProfiles` (e.g., PS5 Rate, 240Hz Rate).
- [ ] Define the `PCs` (e.g., PC-01 through PC-30) and bind them to the Branch.
- [ ] Define the `Super Admin` and `Operator` accounts with secure default passwords.

### Step 2: Switch the Database Engine to PostgreSQL
1. Open the `.NET 8` Backend API.
2. In `appsettings.json` (or `appsettings.Production.json`), change the connection string from your local SQL Server to the Oracle Cloud PostgreSQL endpoint.
3. In `Program.cs` (or `Startup.cs`), ensure Entity Framework Core is using `UseNpgsql()` instead of `UseSqlServer()`.

### Step 3: Run the Database Migration
Execute the Entity Framework migration command against the new PostgreSQL database:
```bash
dotnet ef database update
```
**What happens here:**
- EF Core creates a brand new, perfectly empty set of tables in PostgreSQL.
- The Data Seeder automatically runs and injects your Branches, PCs, and Admin logins.
- All testing data (Sessions, Orders, Logs) from your old local SQL Server is completely left behind. The client's first bill will be exactly `#1`.

### Step 4: Finalize the Frontend Executable
1. Update `client/src/config/api.js` (or `.env`) so the React app points to the public Oracle Cloud API (e.g., `https://api.appleesports.com`).
2. Run the build command for the Operator Dashboard:
   ```bash
   npm run build
   ```
3. Run the Electron packager for the PC Overlay `.exe`.

### Step 5: On-Site Delivery
- Install the packaged `.exe` on the client's gaming PCs.
- Provide the client with their `Super Admin` credentials (which were safely injected via the seeder in Step 1).
- The client logs in and sees a perfectly clean dashboard, ready for their first customer.
