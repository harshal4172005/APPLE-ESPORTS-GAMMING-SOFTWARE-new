▼

## RECENT UPDATES & BUG FIXES (June 2026)

### 1. Food Order Placement Fix
- **Issue**: PC overlay orders successfully saved the `FoodOrder` but omitted `FoodOrderItem` entries due to a missing object mapper in the backend logic.
- **Fix**: The `PlaceFoodOrder` method in `PcOverlayHub.cs` was updated to accurately map incoming `FoodItemPayload` properties to their `FoodOrderItem` entity equivalents (`InventoryId`, `ItemName`, `Quantity`, `UnitPrice`, `TotalPrice`).

### 2. Session Timer Sticking Fix
- **Issue**: The countdown timer on the overlay screen sometimes failed to start if the UI mounted prior to a successful session data fetch.
- **Fix**: Re-evaluated the dependency array of the real-time countdown `useEffect` hook in `OverlaySocketContext.jsx` to depend strictly on `sessionData.sessionId` and `sessionData.sessionStatus === 'active'` instead of a generic truthy check on the object.

### 3. Operator Call Notification UI
- **Issue**: The "Call Operator" feature resulted in a minor, easily missed toast notification rather than an actionable operator alert.
- **Fix**: Upgraded the `OperatorCall` to use the primary `AnimatePresence` big modal UI card in `GlobalNotificationListener.jsx`. Added an explicit "Acknowledge" button to clear the alert. Implemented `speechSynthesis` to provide verbal warnings to operators.

### 4. Dynamic Time Extension & Walk-in Billing
- **Issue**: The operator dashboard calculated time extensions and walk-in bill estimates using a hardcoded base rate of `₹100/hr`, ignoring variable PC monitor rates (e.g., PS5 or 240Hz PCs).
- **Fix**: Adjusted `GlobalNotificationListener.jsx` to perform API calls fetching the exact `ratePerHour` applied to the specific `pcId` (from `/api/public/session/pc/${pcId}` or `/api/public/pcs/${pcId}`) and multiplying dynamically for correct expected billing amounts in backend `SessionExtendDto`.

## RECENT UPDATES & BUG FIXES (July 2026)

### 1. Shift & Billing Reconciliation Dashboard Refactor
- **Issue**: The end-of-day shift reports displayed incorrect calculation totals (e.g., random 26k amounts), confusing terminology ("Final discrepancy"), poor grammar ("1 transaction done"), missing credit totals, and lacked a unified overall summary.
- **Fix**: Rewrote the shift summary calculation logic in the frontend to correctly aggregate "Cash", "Online", "Wallet", and "Credit" totals. Renamed "Final discrepancy" to "Total Cash Difference" for clarity. Added pluralization to transaction counts and ensured a clean, structured breakdown of end-of-day totals.

### 2. Operator Permission Sidebar Mapping
- **Issue**: Newly added dashboards (like Credit Clearances and Menu Editor) were missing from the Super Admin's Operator Access Settings panel, and existing labels didn't match the actual sidebar strings.
- **Fix**: Added missing permission keys (`credits`, `menu_editor`, `pc_status`) into the `PERMISSION_KEYS` array in `SettingsPage.jsx` and renamed their labels to be an exact 1:1 match with the sidebar for intuitive administration.

### 3. Real-Time Permission Synchronization
- **Issue**: When a Super Admin checked or unchecked a permission for an Operator, the Operator had to manually refresh the page or log out to see the changes.
- **Fix**: Injected `IHubContext<NotificationHub>` into the `OperatorsController.cs` `.NET 8` backend to broadcast a `PermissionsUpdated` SignalR event specifically targeted at `user:{op.Id}` whenever a PUT request updates their access. The `SocketContext.jsx` client now listens for this event and triggers a silent `fetchCurrentUser` refresh, instantly hiding or showing sidebar modules on the operator's active screen.

### 4. Operator Dashboard State Synchronization (Cache Busting)
- **Issue**: When operators approved walk-in requests, the dashboard's `fetchPcs` request was aggressively cached by browsers, causing the PC to momentarily revert to the "Idle" state on the UI until manually refreshed.
- **Fix**: Added a cache-busting timestamp (`_t: Date.now()`) to the `/pcs` GET request in `SessionsPage.jsx` to bypass browser caching and ensure the UI immediately reflects the fresh database state.

### 2. Global Refresh Event & Dedicated PC Identification
- **Issue**: Needed a centralized way to refresh PC sessions and handle dedicated PC identification clearing on the overlay.
- **Fix**: Implemented a global refresh event for PC sessions and added a URL parameter to correctly clear dedicated PC identification (Commit by Meet).

---

## COMPREHENSIVE END-TO-END SYSTEM WORKFLOWS (BY ROLE)

The system supports three distinct operational roles. Below is the complete start-to-end lifecycle and feature access for each.

### 1. SUPER ADMIN WORKFLOW

**Goal**: Full system oversight, multi-branch management, global revenue tracking, and configuration.

**A. Authentication & Entry**
1. **Login**: The Super Admin navigates to the global login gateway (`/login/super-admin`).
2. **Authentication**: Enters master credentials. The system authenticates against global permissions.
3. **Dashboard Access**: Redirects to the Global Dashboard (not tied to any specific branch).

**B. Global Management & Monitoring**
1. **Cross-Branch Oversight**: Can view aggregate revenue, active sessions, and PC statuses across all branches (e.g., Adajan, Citylight).
2. **Operator Management**: Creates, edits, or revokes operator accounts and assigns them to specific branches.
3. **Inventory & Menu**: Manages the global food and beverage menu, updating prices and stock that reflect in all branches.
4. **System Configuration**: Edits global rules (base PC rates, monitor Hz pricing multipliers, tax configurations).

**C. Auditing & Reporting**
1. **Shift Logs**: Reviews End-of-Day (EOD) and shift reports submitted by operators.
2. **Audit Trails**: Tracks overriding actions (e.g., operator canceling a reservation or applying a manual discount).
3. **Revenue Analytics**: Generates date-range filtered reports for gross gaming, food sales, and tax liabilities.

---

### 2. OPERATOR WORKFLOW

**Goal**: Manage a specific branch's day-to-day operations, including PC sessions, billing, walk-ins, and shift cash handling.

**A. Authentication & Shift Start**
1. **Branch Selection**: Navigates to `/login/operator` and selects their assigned branch (secured by backend data isolation).
2. **Login**: Enters personal PIN/Password.
3. **Shift Initialization**: The system logs the exact time, operator ID, and cash float. The shift becomes "ACTIVE".

**B. Day-to-Day Operations**
1. **Walk-in Handling**:
   - Customer arrives; Operator checks the **PC Status Grid** for available (gray) computers.
   - Operator clicks an available PC, inputs Customer Name and Duration (or Pay-As-You-Go), and hits **Start Session**.
   - PC status turns **Active (Green)**, and the system sends a SignalR command to unlock the user's PC.
2. **Reservation Management**:
   - Operator creates future bookings. PC turns **Reserved (Purple)**.
   - If a walk-in tries to take a reserved PC, the system pops up a warning preventing the session unless manually overridden.
   - When the customer arrives, the operator converts the reservation to an Active Session.
3. **Remote PC Control**: Operator can Reboot, Shutdown, or Lock any PC remotely from the dashboard.

**C. Requests & Food Orders**
1. **Overlay Notifications**: Listens (via `GlobalNotificationListener.jsx`) for incoming SignalR events.
2. **Time Extensions**: A popup alerts the operator that PC-X wants 30 more minutes. Operator clicks **Approve**; the system dynamically calculates the new cost based on the PC's monitor Hz rate and extends the session.
3. **Food Orders**: Operator receives order tickets in the Kanban board. Moves tickets from "Pending" -> "Preparing" -> "Delivered". Charges are added to the active session's bill.
4. **Call Operator**: A modal popup appears with a text-to-speech voice alert if a user needs physical assistance at their desk.

**D. Checkout & Shift End**
1. **Billing**: Customer finishes. Operator opens the bill, reviews gaming + food charges, applies discounts if applicable, logs the payment method (Cash/UPI), and closes the bill. The PC auto-locks and returns to Idle.
2. **EOD / Shift End**: Operator closes their shift, reconciles physical cash with the system's expected cash, and submits the shift report to the Super Admin.

---

### 3. USER (GAMER) WORKFLOW

**Goal**: Seamless gaming experience with in-seat ordering and session management, powered by a lightweight PC overlay.

**A. Session Start**
1. **Idle State**: PC is locked by the Client Overlay, showing the branch logo and a "Please see the front desk" message.
2. **Activation**: Upon Operator starting the session, the backend broadcasts a SignalR `SessionUpdated` event.
3. **Unlock**: The PC overlay unlocks the Windows environment, allowing the user to launch games.

**B. Active Session Experience**
1. **Overlay Access**: The lightweight widget (designed for minimal RAM/CPU usage) sits on the screen.
2. **Real-time Tracker**: Shows exact remaining time syncing dynamically with the server.
3. **Self-Service Actions**:
   - **Order Food**: Opens a digital menu. User adds items to the cart and places the order. It sends a payload directly to the operator's Kanban board.
   - **Extend Time**: User selects +30 mins or +1 hr. The request pauses locally until the Operator approves it.
   - **Call Operator**: Sends an urgent ping to the front desk.
   - **View Bill**: User can check their current running total (Gaming + Food) at any time.

**C. Session End**
1. **Warning**: At 5 minutes remaining, the overlay pulses to warn the user.
2. **Auto-Lock**: When time expires (or if the operator ends the session manually), the PC Overlay instantly re-locks the Windows environment.
3. **Logout**: User leaves the desk, proceeds to the counter to pay the final bill.

---

Reservation Flow — Simplified Production Workflow

```text id="z14p3j"
┌──────────────────────────┐
│ Customer wants booking  │
└────────────┬─────────────┘
             │
             ▼
┌──────────────────────────┐
│ Operator creates         │
│ reservation for PC-05    │
│ (1:00 PM)                │
└────────────┬─────────────┘
             │
             ▼
┌────────────────────────────────────┐
│ SYSTEM STATE = RESERVED            │
│                                    │
│ PC-05 Reserved for Rahul           │
│ Starts in 10 min                   │
└────────────┬───────────────────────┘
             │
             ▼
 ┌──────────────────────────────────┐
 │ REFLECTED EVERYWHERE             │
 └──────────────────────────────────┘

   Billing Counter
   ┌─────────────────────┐
   │ PC-05               │
   │ Reserved for Rahul  │
   │ Starts in 10 min    │
   └─────────────────────┘

   Session Dashboard
   ┌─────────────────────┐
   │ Upcoming Session    │
   │ Rahul → PC-05       │
   │ 1:00 PM             │
   └─────────────────────┘

   PC Status Dashboard
   ┌─────────────────────┐
   │ PC-05 = PURPLE      │
   │ Reserved State      │
   └─────────────────────┘
```

---

Operator Clicks Reserved PC

```text id="8w17ol"
┌─────────────────────────────┐
│ Operator clicks PC-05       │
└────────────┬────────────────┘
             │
             ▼
┌──────────────────────────────────────┐
│ POPUP APPEARS                        │
│                                      │
│ ⚠ RESERVED PC                        │
│                                      │
│ PC-05 reserved for Rahul             │
│ Starts at 1:00 PM                    │
│                                      │
│ [Cancel] [Override] [Start Session]  │
└──────────────────────────────────────┘
```

---

When Reservation Time Starts

```text id="84sqzv"
1:00 PM reached
        │
        ▼
┌─────────────────────────────┐
│ Operator clicks             │
│ "Start Reserved Session"    │
└────────────┬────────────────┘
             │
             ▼
┌─────────────────────────────┐
│ RESERVED → ACTIVE SESSION   │
│                             │
│ Timer starts                │
│ Billing starts              │
│ PC becomes BUSY             │
└─────────────────────────────┘
```

---

If Customer Never Arrives

```text id="jl3xgc"
┌─────────────────────────────┐
│ Grace Period Ends           │
│ (Example: 15 min)           │
└────────────┬────────────────┘
             │
             ▼
┌──────────────────────────────────┐
│ RESERVATION EXPIRED              │
│                                  │
│ [Release PC]                     │
│ [Extend Reservation]             │
└──────────────────────────────────┘
```

---

Final System States

| State            | Meaning                        | Color  |
| ---------------- | ------------------------------ | ------ |
| Idle             | Available                      | Gray   |
| Busy             | Active session running         | Green  |
| Reserved         | Future booking exists          | Purple |
| Awaiting Billing | Session ended, payment pending | Orange |
| Offline          | System disconnected            | Red    |

---

Core Production Logic

```text id="ev1uq6"
Reservation is NOT just a dashboard feature.

Reservation = Central System State

So every module reflects:
- reservation info
- countdown
- protection logic
- popup warnings
- session transition
```

---

Final Recommended UX

```text id="9u3vzo"
Visual Reserved State
          +
Popup Protection
          +
Permission Override
          +
Auto Expiry
          +
Real-Time Sync
```

# = Production Grade Reservation System



























FULL SYSTEM ARCHITECTURE OF STARTING POINT 

COMPLETE PRODUCTION WORKFLOW

Gaming Café Management System

(Based on Your Full Prototype + Chat Flow + Operational Logic)

Your diagram is actually correct architecturally.

You are building:

CENTRALIZED MULTI-BRANCH OPERATION SYSTEM

NOT:

simple billing software
simple cyber café app
simple timer software

This is:

branch management system
live session engine
operator workflow system
real-time PC control system
reservation engine
gaming café POS
operator accountability system

---

1. COMPLETE SYSTEM ARCHITECTURE

MAIN STRUCTURE

```text
                    CENTRAL SERVER
            (Main Database + APIs + Sync)

                         │
        ┌────────────────┼────────────────┐
        │                │                │ 
        ▼                ▼                ▼ 	     

   Branch A         Branch B         Branch C
   (Adajan)         (Citylight)     (Katargam)

        │                │                │

   Operators        Operators        Operators
   PCs              PCs              PCs
   User Panels      User Panels      User Panels
```

---

2. THE 3 MAIN ACCESS LAYERS

Your understanding is CORRECT.

The app has 3 access layers:

| Layer       | Purpose                       |
| ----------- | ----------------------------- |
| Super Admin | Controls entire system        |
| Operator    | Runs branch operations        |
| User Panel  | Lightweight gaming-side panel |

---

3. LOGIN FLOW (FULL PRODUCTION WORKFLOW)

APPLICATION START

When software opens:

```text
┌─────────────────────────┐
│    GAMING SOFTWARE      │
│                         │
│ [ Super Admin Login ]   │
│                         │
│ [ Operator Login ]      │
│                         │
│ [ User Panel ]          │
└─────────────────────────┘
```

This is your MASTER ENTRY SCREEN.

---

4. SUPER ADMIN LOGIN FLOW

STEP 1

Super admin clicks:

```text
[ Super Admin Login ]
```

---

STEP 2

Login screen appears:

```text
Email
Password

[ Login ]
```

---

STEP 3

Backend verifies:

password
device
permissions
account status

---

STEP 4

If valid:

```text
Redirect → Main Admin Dashboard
```

---

SUPER ADMIN CAN ACCESS

| Module       | Access |
| ------------ | ------ |
| All Branches | YES    |
| Operators    | YES    |
| Reports      | YES    |
| Revenue      | YES    |
| Settings     | YES    |
| Reservations | YES    |
| PC Controls  | YES    |
| Shift Logs   | YES    |
| Audit Logs   | YES    |

---

SUPER ADMIN LIMITATIONS

Even super admin should have protections.

## SHOULD NOT:

delete transaction history directly
modify closed invoices
remove audit logs
access without authentication
override without reason logging

---

IMPORTANT

Super admin session should persist.

Meaning:

If logged in once:

```text
Next app open →
direct dashboard access
```

UNTIL:

logout
timeout
password reset
forced signout

---

5. OPERATOR LOGIN FLOW

This is your MOST IMPORTANT workflow.

---

STEP 1

Operator clicks:

```text
[ Operator Login ]
```

---

STEP 2 — BRANCH SELECTION

```text
Select Branch

[ Adajan ]
[ Citylight ]
[ Katargam ]
[ Vesu ]
```

---

CRITICAL SECURITY RULE

Operator CANNOT see other branch data.

Meaning:

If operator belongs to:

```text
Adajan
```

Then:

```text
Citylight ❌
Katargam ❌
Vesu ❌
```

are blocked.

---

HOW THIS WORKS

Backend stores:

```text
Operator → Assigned Branch
```

Example:

```text
Operator Rahul
Assigned Branch = Adajan
```

So backend ALWAYS filters:

```sql
WHERE branch_id = operator_branch
```

---

STEP 3 — OPERATOR SELECTION

Now branch-specific operator board appears:

Example:

```text
ADAJAN OPERATORS

[ Rahul ]
[ Meet ]
```

---

STEP 4 — QUICK LOGIN

Operator selects own profile.

Then enters:

```text
PIN / Password
```

NOT full heavy login every time.

This is BEST for shift workflow.

---

STEP 5 — SHIFT START

System logs:

```text
Operator: Rahul
Branch: Adajan
Login Time: 10:00 AM
Device: Counter-PC-01
```

NOW:

```text
SHIFT = ACTIVE
```

---

STEP 6 — DASHBOARD ACCESS

Operator redirected to:

```text
Operator Dashboard
```

---

6. OPERATOR DASHBOARD MODULES

WHAT OPERATOR CAN ACCESS

| Module          | Access |
| --------------- | ------ |
| Billing Counter | YES    |
| Sessions        | YES    |
| Reservations    | YES    |
| Food Orders     | YES    |
| PC Status       | YES    |
| User Requests   | YES    |
| Cash Register   | YES    |
| Shift Reports   | YES    |

---

OPERATOR CANNOT ACCESS

| Restricted Item   | Why        |
| ----------------- | ---------- |
| Other Branch Data | Security   |
| Global Revenue    | Admin only |
| System Settings   | Dangerous  |
| User Management   | Admin only |
| Audit Logs        | Admin only |
| Database Controls | Dangerous  |

---

7. USER PANEL FLOW

Users DO NOT LOGIN.

This is VERY IMPORTANT.

Users are TEMPORARY SESSION USERS.

---

USER PANEL WORKFLOW

When session starts:

```text
PC-03 → ACTIVE SESSION
```

Then automatically:

```text
User Side Panel Opens
```

---

USER SIDE PANEL

LIGHTWEIGHT overlay.

NOT heavy software.

---

USER PANEL FEATURES

| Feature                | Allowed |
| ---------------------- | ------- |
| View Remaining Time    | YES     |
| Order Food             | YES     |
| Request Time Extension | YES     |
| Call Operator          | YES     |
| View Current Bill      | YES     |

---

USER PANEL MUST NOT ALLOW

| Feature               | Reason    |
| --------------------- | --------- |
| Close Session         | Dangerous |
| Access Other PCs      | Security  |
| View Admin Data       | Security  |
| Open Windows Controls | Dangerous |
| Modify Billing        | Dangerous |

---

USER PANEL PERFORMANCE RULE

VERY IMPORTANT.

Because gaming PCs need performance.

So:

USER PANEL MUST:

consume low RAM
consume low CPU
run lightweight
stay minimized
avoid animations
avoid background heavy tasks

---

8. SESSION WORKFLOW

USER ARRIVES

Operator:

```text
Checks available PCs
```

---

OPERATOR SELECTS PC

Example:

```text
PC-03 = AVAILABLE
```

---

START SESSION

Operator fills:

| Field     | Example  |
| --------- | -------- |
| User Name | Rahul    |
| Duration  | 2 Hours  |
| Package   | Premium  |
| Notes     | Optional |

---

BACKEND ACTIONS

Backend changes:

```text
PC-03 → ACTIVE
```

Creates:

```text
Session Record
```

Starts:

```text
Timer
Billing
User Panel
```

---

9. RESERVATION WORKFLOW

This is centralized.

VERY IMPORTANT.

---

WHEN RESERVATION CREATED

Backend stores:

```text
PC-05
Reserved for Rahul
1:00 PM
```

---

ALL DASHBOARDS REFLECT

| Dashboard       | Reflection |
| --------------- | ---------- |
| Billing Counter | YES        |
| PC Status       | YES        |
| Sessions        | YES        |
| Reservations    | YES        |

---

RESERVED PC PROTECTION

If operator clicks reserved PC:

POPUP:

```text
⚠ RESERVED PC

PC-05 reserved for Rahul
Starts in 10 minutes

[ Cancel ]
[ Override ]
[ Start Reserved Session ]
```

---

OVERRIDE RULE

Override should require:

permission
reason logging
audit tracking

---

10. SHIFT HANDOVER WORKFLOW

VERY IMPORTANT.

---

OPERATOR 1

```text
10 AM → 8 PM
```

works entire shift.

---

LOGOUT

At shift end:

```text
Operator Logout
```

System records:

```text
Logout Time
Shift Summary
Revenue
Actions
```

---

# OPERATOR 2 LOGIN

OPERATOR 2 LOGIN
8:30 PM
```

New operator logs in.

---

IMPORTANT

Dashboard state remains SAME.

Because:

DATA IS CENTRALIZED

NOT tied to operator.

---

11. PAYMENT WORKFLOW

You explained this VERY correctly.

Gaming and food MUST remain separate.

---

BILL STRUCTURE

```text
Gaming Charges = ₹100
Food Charges   = ₹20

Total          = ₹120
```

---

PAYMENT SPLIT

```text
Cash  = ₹20
UPI   = ₹100
```

System records BOTH.

---

WHY IMPORTANT

For:

accounting
audits
food revenue
gaming revenue
GST/tax logic later

---

12. CENTRALIZED LIVE STATE ENGINE

THIS IS THE CORE.

Every dashboard syncs from SAME backend state.

---

EXAMPLE

```text
PC-03 ACTIVE
```

Then EVERYWHERE:

* billing counter
* reservations
* operator dashboard
* session dashboard
* PC map

shows:

```text
ACTIVE
```

---

13. SECURITY MODEL

MUST HAVE

| Security         | Required |
| ---------------- | -------- |
| Password Hashing | YES      |
| Session Tokens   | YES      |
| Role Validation  | YES      |
| Audit Logs       | YES      |
| API Validation   | YES      |
| Branch Isolation | YES      |
| Device Tracking  | YES      |
| Forced Logout    | YES      |

---

14. PRODUCTION RULES

NEVER TRUST FRONTEND

ALL permissions checked in backend.

---

EXAMPLE

Even if operator hacks UI:

Backend still blocks:

```text
Access denied
```

---

15. FINAL ROLE HIERARCHY

```text
SUPER ADMIN
    │
    ├── Branch Control
    ├── Operators
    ├── Reports
    ├── Revenue
    └── System Settings

OPERATOR
    │
    ├── Sessions
    ├── Billing
    ├── Reservations
    ├── Food Orders
    ├── User Requests
    └── Shift Operations

USER PANEL
    │
    ├── Food Orders
    ├── Time Requests
    ├── Remaining Time
    └── Call Operator
```

---

16. YOUR CURRENT ARCHITECTURE STATUS

Your current idea is actually VERY GOOD.

You already understood:

branch isolation
centralized state
role hierarchy
shift continuity
lightweight user panels
reservation synchronization
popup safety UX
operational workflows

Which means:

You are already thinking like a production systems architect now — not just a frontend developer.

The next step after this is:

PROPER ENGINEERING PHASE

Meaning:

1. Final workflow freeze
2. Database design
3. API architecture
4. Role permission matrix
5. PC state engine
6. Session lifecycle engine
7. Frontend module design
8. Electron desktop packaging
9. LAN synchronization
10. Deployment architecture

BILLING COUNTER DASHBOARD — SOP & OPERATIONAL WORKFLOW
Gaming Café Management System

1. PURPOSE OF BILLING COUNTER
The Billing Counter is the main operational dashboard used inside the gaming café.
This dashboard is responsible for:
Starting gaming sessions
Managing live PC sessions
Handling food & beverage billing
Managing split payments
Viewing reservation status
Completing customer checkout
Sending billing data to:
Cash Register
End of Day Reports
Dashboard Analytics
Operator Logs
This is the most frequently used dashboard in the entire system.

2. WHO CAN ACCESS THIS DASHBOARD
Role
Access
Super Admin
Full Access
Operator
Operational Access
User
NO ACCESS


3. BILLING COUNTER OBJECTIVE
The Billing Counter is designed to ensure:
Fast operator workflow
Smooth customer handling
Accurate gaming billing
Accurate food billing
Correct cash tracking
Correct online payment tracking
Proper split-payment handling
Real-time PC synchronization
Reservation protection
Safe operational flow

4. BILLING COUNTER MODULES
The Billing Counter contains the following sections:

A. PC STATION SELECTOR
Purpose:
Displays all PCs of the branch
Shows real-time PC state
Allows operator to select a PC

PC STATES
State
Meaning
Idle
Available for session
Active
Session currently running
Reserved
Reserved for customer
Awaiting Bill
Session stopped, billing pending
Offline
System/PC unavailable


VISUAL STATUS COLORS
Color
Meaning
Green
Active Session
Purple
Reserved
Orange
Awaiting Billing
Gray
Idle
Red
Offline/Error


5. SESSION INFORMATION STRIP
When operator selects a PC:
The dashboard displays:
Field
Example
PC Name
PC-03
Customer Name
Rahul
Session Duration
2h 15m
Session Charge
₹135


6. SESSION WORKFLOW

STEP 1 — CUSTOMER ARRIVES
Customer requests:
gaming duration
preferred PC
reservation confirmation (if booked)

STEP 2 — OPERATOR CHECKS PC STATUS
Operator verifies:
available PCs
reserved PCs
active sessions
billing pending PCs

STEP 3 — SESSION START
Operator fills:
Field
Example
Customer Name
Rahul
Duration
2 Hours
Gaming Type
Standard
Notes
Optional


STEP 4 — SYSTEM ACTIONS
Backend automatically:
Creates session record
Assigns user to PC
Starts session timer
Starts billing engine
Opens user-side panel
Synchronizes all dashboards

7. USER SIDE PANEL CONNECTION
When session starts:
The customer automatically receives a lightweight side panel.

USER PANEL FEATURES
Feature
Allowed
View Remaining Time
YES
Order Food
YES
Request Time Extension
YES
Call Operator
YES


USER PANEL RESTRICTIONS
Users CANNOT:
close session
access billing
access admin data
access other PCs
modify payments

8. FOOD & BEVERAGE BILLING
Operators can add:
drinks
snacks
food combos
beverages
during an active session.

9. GAMING + FOOD BILL SEPARATION
Gaming and food revenue MUST remain separated.

EXAMPLE
Category
Amount
Gaming Charges
₹100
Food Charges
₹40

Total:
₹140

10. CURRENT BILL SECTION
The Current Bill panel displays:
Item
Purpose
Date
Billing tracking
Time
Session tracking
Session Charges
Gaming revenue
Food Charges
Food revenue
Reservation Status
Reserved customer visibility
Payment Breakdown
Cash + Online split
Final Total
Final payable amount


11. DATE & TIME DISPLAY
The Billing Counter MUST display:
current date
current time
billing timestamp
This helps:
operator tracking
shift monitoring
transaction auditing
dispute handling

12. RESERVATION VISIBILITY
If a PC is reserved:
Billing Counter MUST show:
Field
Example
Reserved For
Rahul
Reservation Time
1:00 PM
Remaining Time
10 Minutes


13. RESERVED PC PROTECTION
If operator clicks a reserved PC:
System displays popup:
⚠ RESERVED PC
PC-05 reserved for Rahul
Starts at 1:00 PM
Options:
Cancel
Start Reserved Session

14. RESERVATION RESTRICTIONS
Operators CANNOT:
remove reservation
override reservation
edit reservation timing
Only Super Admin can control reservation overrides.

15. SESSION STOP / BILL FREEZE STATE
When operator stops a gaming session:
PC enters:
AWAITING BILL state.

SYSTEM ACTIONS
During this state:
timer freezes
session amount locks
billing becomes editable
food ordering stops
new sessions blocked

16. PAYMENT SYSTEM
The Billing Counter supports:
Payment Type
Supported
Cash
YES
UPI / Online
YES
Split Payment
YES
Member Wallet
YES (Requires PC Approval)


17. SPLIT PAYMENT SYSTEM
This is a critical production feature.
Customers may pay using:
partial cash
partial online payment

EXAMPLE
Total Bill = ₹500
Method
Amount
Cash
₹100
Online
₹400


18. SPLIT PAYMENT VALIDATION
System MUST validate:
Cash + Online = Total Bill
If mismatch occurs:
payment blocked
operator warned
bill cannot complete

19. SPLIT PAYMENT IMPACT
Split payments directly affect:
Dashboard
Impact
Cash Register
Cash tracking
End of Day
Revenue split
Cash Desk
Physical cash count
Dashboard Analytics
Revenue reporting
Operator Logs
Audit tracking


20. CASH PAYMENT FLOW
Operator enters:
cash received amount
System automatically:
calculates change
detects shortage
detects exact payment

21. ONLINE PAYMENT FLOW
Operator confirms:
UPI completed
online payment received
System records:
payment amount
payment method
payment timestamp

21A. MEMBER WALLET PAYMENT FLOW
This flow is used exclusively for logged-in Members with wallet balances.
- Operator selects the Member Wallet option in the Billing Counter.
- Operator clicks "Request PC Approval".
- System sends a SignalR request to the physical PC.
- PC overlays a Wallet Approval modal on top of the lock screen (or active session).
- Member clicks "Approve" directly on the PC.
- Backend automatically processes payment, closes the session, and alerts the Operator that billing is complete.
If the member walks away or clicks "Decline", the Operator receives a rejection alert and must ask for Cash or UPI instead.

22. ADMIN-ONLY DISCOUNT SYSTEM
Discount/Coupon system is hidden from operators.
This section is ONLY visible inside:
SUPER ADMIN LOGIN

23. WHO CAN APPLY DISCOUNTS
Role
Permission
Super Admin
YES
Operator
NO
User
NO


24. WHY DISCOUNTS ARE RESTRICTED
To prevent:
operator misuse
unauthorized discounts
hidden revenue loss
billing manipulation
fake discounts

25. ADMIN DISCOUNT FLOW
Super Admin can:
select PC
apply coupon
apply flat discount
apply percentage discount

EXAMPLE
Type
Value
Flat Discount
₹50
Percentage Discount
10%


26. DISCOUNT AUDIT LOGGING
Every discount MUST store:
Field
Example
Admin Name
Harshal
Discount Type
Flat
Discount Amount
₹50
Reason
VIP Customer
Date & Time
7:30 PM


27. MEMBER LINK REMOVAL
The “Link Member” section is removed from the Billing Counter.
Reason:
reduce operator complexity
faster billing flow
cleaner operational UI
avoid unnecessary billing interruptions
Member management will remain inside:
Members Dashboard
Admin Controls

28. BILL COMPLETION FLOW
When operator clicks:
COMPLETE BILL
System performs:
validates payment
validates split payment totals
closes session
stores transaction
updates cash register
updates EOD reports
updates dashboard analytics
clears PC state
resets billing counter

29. SESSION RESET AFTER BILLING
PC state changes:
ACTIVE → IDLE
OR
AWAITING BILL → IDLE

30. REAL-TIME SYNCHRONIZATION
Billing Counter synchronizes with:
Dashboard
Sync Type
PC Status
Live
Cash Register
Live
End of Day
Live
Dashboard Analytics
Live
Reservation Dashboard
Live


31. SECURITY RULES
Billing Counter MUST enforce:
branch isolation
operator access control
backend payment validation
reservation protection
session locking
transaction logging
audit tracking

32. OPERATOR LIMITATIONS
Operators CANNOT:
apply discounts
delete bills
modify completed bills
override reservations
access admin controls
access other branch data

33. FINAL PRODUCTION PRINCIPLE
The Billing Counter is NOT only a billing screen.
It is:
real-time session engine
operational control center
synchronized payment system
reservation-aware dashboard
revenue control system
gaming café operational core
This dashboard controls the live operational flow of the gaming café.
BILLING COUNTER — UPDATED PAYMENT & CHANGE RETURN SOP
NEW SECTION TO ADD

20A. CASH RECEIVED & CHANGE RETURN SYSTEM
This section is used when customer pays using cash.

PURPOSE
To help operators:
avoid cash calculation mistakes
quickly calculate return amount
reduce billing confusion
maintain accurate drawer cash

CASH PAYMENT FLOW
When operator selects:
Cash Payment
System displays:
Field
Purpose
Final Bill Amount
Actual payable amount
Cash Received
Money given by customer
Return Change
Money to return


EXAMPLE
Customer Bill:
₹280
Customer Gives:
₹500
System Automatically Calculates:
Return Change = ₹220


SYSTEM VALIDATION
System MUST:
calculate automatically
prevent negative values
prevent underpayment
prevent billing completion if payment insufficient

UNDERPAYMENT EXAMPLE
Bill:
₹280
Cash Received:
₹200
System shows:
⚠ INSUFFICIENT CASH RECEIVED
Remaining:
₹80
Bill cannot complete.

EXACT PAYMENT DETECTION
If:
Bill:
₹280
Cash Received:
₹280
System shows:
Exact Payment Received

No change required.

CHANGE RETURN DISPLAY RULE
Return amount should:
appear large and visible
use highlighted UI
remain visible until bill completes
This reduces operator mistakes during rush hours.

SPLIT PAYMENT + CHANGE RULE
If split payment exists:
Example:
Bill:
₹500
Online:
₹300
Cash Received:
₹500
Remaining Cash Needed:
₹200
System Calculates:
Return Change = ₹300


IMPORTANT RULE
Change calculation should ALWAYS happen ONLY on:
remaining cash amount
not total bill amount

BILL COMPLETION RULE
When bill completes:
System stores:
Field
Example
Final Bill
₹280
Cash Received
₹500
Change Returned
₹220
Actual Cash Added
₹280


IMPORTANT PRINCIPLE
Cash drawer should increase ONLY by:
Final Cash Collected
NOT:
temporary received amount.

IMPACT ON CASH REGISTER
Billing Counter sends ONLY:
Actual Cash Collected

to:
Cash Register
EOD
Revenue Reports
NOT the raw received amount.


CASH REGISTER DASHBOARD — FINAL REFINED PRODUCTION SOP
Gaming Café Management System

1. PURPOSE OF CASH REGISTER
The Cash Register dashboard is responsible ONLY for:
PHYSICAL CASH DRAWER MANAGEMENT
This dashboard tracks:
opening cash
cash received from customers
drawer balance
shift cash verification
cash mismatch detection
This is NOT:
a revenue analytics dashboard
a financial management panel
an expense management system

2. WHO CAN ACCESS THIS DASHBOARD
Role
Access
Super Admin
Full Access
Operator
Operational Access
User
NO ACCESS


3. PRIMARY OBJECTIVE
The Cash Register exists to ensure:
clean cash handling
no drawer mismatch
operator accountability
safe shift handover
accurate physical cash tracking

4. WHAT CASH REGISTER TRACKS
The Cash Register tracks ONLY:
Item
Included
Opening Balance
YES
Cash Transactions
YES
Split Payment Cash Portion
YES
Expected Drawer Cash
YES
Shift Cash Verification
YES
Cash Mismatch Detection
YES


5. WHAT CASH REGISTER DOES NOT HANDLE
The following are NOT part of operator cash control:
Feature
Allowed?
Cash Removal
NO
Petty Cash Usage
NO
Tea Expense
NO
Owner Withdrawal
NO
Manual Drawer Editing
NO
Manual Cash Addition
NO


IMPORTANT PRINCIPLE
OPERATOR = CASH HANDLER
NOT CASH CONTROLLER
Meaning:
operator can receive cash
operator can verify cash
operator can count cash
BUT:
operator CANNOT use business cash

6. OPENING BALANCE SYSTEM

PURPOSE
At the beginning of every shift:
Operator enters the physical cash available inside the drawer.
This becomes:
OPENING BALANCE

7. OPENING BALANCE WORKFLOW

STEP 1 — OPERATOR LOGIN
Operator logs into:
assigned branch
assigned shift

STEP 2 — OPEN CASH REGISTER
Operator opens:
Cash Register Dashboard

STEP 3 — ENTER OPENING CASH
Operator enters:
Field
Example
Opening Balance
₹5000


STEP 4 — SYSTEM STORES ENTRY
System records:
Field
Example
Operator Name
Rahul
Branch
Adajan
Opening Balance
₹5000
Date
12/07/2026
Time
10:00 AM


8. WHY OPENING BALANCE IS IMPORTANT
Without opening balance:
drawer calculation breaks
shift accountability breaks
mismatch verification becomes impossible

9. CASH TRANSACTION LOG
This section records ONLY transactions containing CASH.

10. TRANSACTION LOG MUST STORE
Field
Required
Bill ID
YES
PC Number
YES
Cash Amount
YES
Gaming Amount
YES
Food Amount
YES
Operator Name
YES
Branch
YES
Date
YES
Time
YES


11. DATE & TIME TRACKING
Every transaction MUST contain:
exact date
exact timestamp

WHY IMPORTANT?
Because:
multiple shifts exist
multiple operators exist
disputes require proof
audits require traceability
EOD depends on timestamps

12. SPLIT PAYMENT SUPPORT
The system supports:
Payment Type
Example
Cash Only
₹500
Split Payment
₹100 cash + ₹400 online


IMPORTANT RULE
ONLY THE CASH PORTION enters Cash Register calculations.

EXAMPLE
Bill Total:
₹500
Customer Pays:
₹100 cash
₹400 online
Then:
Section
Amount
Cash Register
₹100
Online Analytics
₹400


13. EXPECTED CASH IN DRAWER NOW
This is the MOST IMPORTANT section.

PURPOSE
Shows:
HOW MUCH PHYSICAL CASH SHOULD CURRENTLY EXIST INSIDE THE DRAWER

14. CASH DRAWER FORMULA
FINAL SAFE FORMULA
Expected Drawer Cash

=

Opening Balance
+ Cash Transactions
+ Split Payment Cash Portion


15. WHAT AFFECTS DRAWER CASH
Action
Affects Drawer?
Cash Payment
YES
Split Payment Cash Part
YES
UPI Payment
NO
Online Payment
NO
Card Payment
NO


16. LIVE CASH DRAWER TRACKING
Cash Register updates LIVE whenever:
cash bill completes
split-payment cash portion received

17. SHIFT ACCOUNTABILITY
Cash Register remains linked to:
operator login
branch
active shift

18. SHIFT CLOSING VERIFICATION
At logout:
Operator MUST count physical cash inside drawer.

SYSTEM DISPLAYS
Field
Example
Opening Balance
₹5000
Cash Sales
₹2300
Expected Drawer Cash
₹7300
Physical Cash Counted
₹7280
Difference
-₹20


19. CASH MISMATCH DETECTION
System MUST detect:
Type
Example
Excess Cash
+₹50
Short Cash
-₹20


20. MISMATCH ALERT SYSTEM
If mismatch occurs:
Popup appears:
⚠ CASH MISMATCH DETECTED
Expected Drawer Cash:
₹7300
Actual Drawer Cash:
₹7280
Difference:
-₹20
Reason Required Before Logout

21. SHIFT CLOSURE RULES
Operator CANNOT close shift without:
entering physical cash count
verifying drawer amount
entering mismatch reason (if needed)

22. ONLINE PAYMENT VISIBILITY
Online/UPI payments may appear ONLY as:
small informational reference
transaction awareness

IMPORTANT
Online payments:
DO NOT affect drawer cash
DO NOT affect cash calculations
DO NOT enter cash reconciliation

23. CASH REGISTER SUMMARY CARDS
Dashboard should display:
Card
Purpose
Opening Balance
Starting shift cash
Cash Sales
Physical cash received
Split Payment Cash
Mixed-payment cash
Expected Drawer Cash
Live drawer amount


24. LIVE SYNCHRONIZATION
Cash Register synchronizes with:
Dashboard
Sync
Billing Counter
YES
End of Day
YES
Dashboard Analytics
YES
Operator Logs
YES


25. SECURITY RULES
Cash Register MUST enforce:
branch isolation
operator tracking
immutable transaction logs
audit logging
secure reconciliation

26. OPERATOR LIMITATIONS
Operators CANNOT:
remove cash
use business cash
edit transactions
modify drawer balance
delete bills
manipulate shift records

27. SUPER ADMIN RESPONSIBILITY
Super Admin handles:
branch cash collection
EOD approval
financial audits
mismatch review
branch-level verification

28. FINAL PRODUCTION PRINCIPLE
The Cash Register is NOT a payment analytics dashboard.
It is:
physical drawer management system
operator accountability engine
shift cash verification system
cash reconciliation dashboard
This dashboard protects the physical cash integrity of the gaming café.
CASH REGISTER — UPDATED CASH ENTRY LOGIC
NEW SECTION TO ADD

15A. CASH RECEIVED VS ACTUAL CASH COLLECTED
This is a critical accounting rule.

PURPOSE
The Cash Register must track ONLY:
ACTUAL BUSINESS CASH RETAINED
NOT temporary cash handed by customer.

EXAMPLE
Bill Amount:
₹280
Customer Gives:
₹500
Operator Returns:
₹220

IMPORTANT RULE
Cash Register MUST store:
Actual Cash Collected = ₹280

NOT:
₹500
Because:
₹220 was immediately returned.

WHY THIS IS IMPORTANT
If system stores ₹500:
drawer balance becomes incorrect
reconciliation breaks
EOD becomes inaccurate
mismatch detection fails

CORRECT CASH FLOW
Item
Amount
Customer Gives
₹500
Change Returned
₹220
Actual Cash Retained
₹280

ONLY:
₹280 enters drawer calculations.

UPDATED CASH DRAWER FORMULA
Expected Drawer Cash

=

Opening Balance
+ Actual Cash Collected
+ Split Payment Cash Portion


IMPORTANT PRINCIPLE
Cash Register should always track:
NET CASH RETAINED BY BUSINESS
NOT:
temporary payment handling.

SPLIT PAYMENT EXAMPLE
Bill:
₹500
Online:
₹300
Customer Gives Cash:
₹500
Required Cash:
₹200
Return Change:
₹300

CASH REGISTER STORES
Actual Cash Collected = ₹200

ONLY.

LIVE SYNCHRONIZATION RULE
Billing Counter sends:
Field
Sent to Cash Register?
Cash Received
NO
Change Returned
NO
Actual Cash Collected
YES


FINAL PRODUCTION PRINCIPLE
Cash Register must reflect ONLY:
actual retained business cash
verified drawer cash
net physical cash flow
This ensures:
correct reconciliation
correct EOD
accurate shift verification
proper cash integrity


END OF DAY (EOD) DASHBOARD — FINAL PRODUCTION SOP
Gaming Café Management System

1. PURPOSE OF END OF DAY DASHBOARD
The End of Day (EOD) Dashboard is responsible for:
daily branch closing summary
final revenue reconciliation
operator shift verification
cash verification
transaction auditing
branch performance tracking
daily operational closure
This dashboard acts as the:
DAILY OPERATIONAL & FINANCIAL CLOSURE SYSTEM

2. WHO CAN ACCESS THIS DASHBOARD
Role
Access
Super Admin
Full Access
Operator
Limited Operational Access
User
NO ACCESS


3. PRIMARY OBJECTIVE
The EOD Dashboard exists to ensure:
accurate daily closure
transaction traceability
clean financial reporting
operator accountability
branch reconciliation
operational auditing

4. WHAT EOD TRACKS
The End of Day Dashboard tracks:
Item
Included
Gaming Revenue
YES
Food Revenue
YES
Cash Revenue
YES
Online Revenue
YES
Split Payments
YES
Completed Bills
YES
Active Sessions
YES
Pending Bills
YES
Reservation Logs
YES
Operator Logs
YES
Cash Verification
YES
Shift Summary
YES
Date & Time Logs
YES


5. EOD DASHBOARD SECTIONS
The EOD dashboard contains:
Section
Purpose
Revenue Summary
Daily totals
Payment Breakdown
Cash vs Online
Transaction Logs
Complete billing history
Operator Activity
Shift monitoring
Cash Verification
Drawer reconciliation
Session Summary
Gaming usage tracking
Reservation Summary
Reserved PC analytics
Alerts & Exceptions
Operational issues


6. DATE & TIME TRACKING
This is a CRITICAL production requirement.
EVERY record inside EOD MUST contain:
exact date
exact timestamp

WHY IMPORTANT?
Because:
multiple shifts exist
multiple operators exist
audits require traceability
disputes require proof
historical analysis depends on timestamps

7. REVENUE SUMMARY SECTION
This section displays:
Revenue Type
Example
Gaming Revenue
₹12,000
Food Revenue
₹4,500
Total Revenue
₹16,500


IMPORTANT RULE
Gaming and food revenue MUST remain separated.

8. PAYMENT BREAKDOWN SECTION
This section displays:
Payment Type
Amount
Cash Revenue
₹6,000
Online Revenue
₹10,500
Split Payments
₹2,000


IMPORTANT
Split payment totals should reflect:
cash portion
online portion
accurately.

9. CASH VERIFICATION SECTION
This section synchronizes directly with:
Cash Register
Shift Closing

SYSTEM DISPLAYS
Field
Example
Opening Balance
₹5000
Cash Collected
₹6000
Expected Drawer Cash
₹11,000
Physical Cash Counted
₹10,980
Difference
-₹20


10. CASH MISMATCH DETECTION
System MUST detect:
Type
Example
Excess Cash
+₹50
Short Cash
-₹20


11. MISMATCH ALERT SYSTEM
If mismatch exists:
EOD should display:
⚠ CASH MISMATCH DETECTED
Expected Drawer:
₹11,000
Actual Drawer:
₹10,980
Difference:
-₹20
Reason Submitted:
“Cash handling mistake during rush hour”

12. TRANSACTION LOG SECTION
This is one of the MOST IMPORTANT sections.

PURPOSE
Stores complete billing history for the day.

EACH TRANSACTION MUST STORE
Field
Required
Bill ID
YES
PC Number
YES
Customer Name
Optional
Gaming Amount
YES
Food Amount
YES
Total Amount
YES
Cash Amount
YES
Online Amount
YES
Payment Type
YES
Change Returned
YES
Actual Cash Collected
YES
Operator Name
YES
Branch
YES
Date
YES
Time
YES


13. CHANGE RETURN TRACKING
This is VERY IMPORTANT.

EXAMPLE
Bill:
₹280
Customer Gives:
₹500
Change Returned:
₹220
Actual Cash Collected:
₹280

IMPORTANT RULE
EOD MUST track:
ACTUAL BUSINESS CASH RETAINED
NOT temporary cash received.

14. SESSION SUMMARY SECTION
This section displays:
Metric
Example
Total Sessions
58
Active Sessions
3
Completed Sessions
55
Pending Bills
2
Average Session Time
2h 10m


15. PC USAGE ANALYTICS
EOD should track:
Metric
Example
Most Used PC
PC-05
Least Used PC
PC-11
Total PC Hours
102 Hours


16. FOOD SALES SUMMARY
This section tracks:
Metric
Example
Total Food Orders
85
Top Selling Item
Cold Coffee
Food Revenue
₹4500


17. RESERVATION SUMMARY
Tracks:
Metric
Example
Total Reservations
12
Completed Reservations
10
Missed Reservations
2


18. OPERATOR ACTIVITY SECTION
Tracks operator operational activity.

EACH OPERATOR LOG MUST STORE
Field
Example
Operator Name
Rahul
Login Time
10:00 AM
Logout Time
8:00 PM
Bills Processed
42
Sessions Handled
38
Cash Verified
YES


19. ALERTS & EXCEPTION SECTION
This section displays operational problems.

EXAMPLES
Alert
Example
Cash Mismatch
YES
Pending Bills
YES
Active Sessions at Closing
YES
System Errors
YES
Reservation Conflict
YES


20. ACTIVE SESSION PROTECTION
EOD cannot fully close if:
active gaming sessions exist
pending bills exist
unless:
Super Admin force closes

21. FORCE CLOSE RESTRICTION
Only Super Admin can:
force close EOD
override pending sessions
override billing locks

22. EOD FINALIZATION FLOW

STEP 1 — VERIFY CASH
Operator verifies:
physical drawer cash
transaction totals

STEP 2 — VERIFY ACTIVE SESSIONS
System checks:
active PCs
pending bills
reservations

STEP 3 — SYSTEM GENERATES EOD REPORT
System generates:
revenue summary
payment summary
transaction summary
operator summary
mismatch logs

STEP 4 — FINALIZE DAY
System:
locks day records
stores immutable logs
prepares next operational cycle

23. EOD IMMUTABLE LOGGING
After EOD closure:
Transactions become:
READ ONLY
Operators CANNOT:
edit bills
modify revenue
change payment values
alter timestamps

24. LIVE SYNCHRONIZATION
EOD synchronizes with:
Dashboard
Sync
Billing Counter
YES
Cash Register
YES
Dashboard Analytics
YES
Reservation System
YES
Operator Logs
YES


25. SECURITY RULES
EOD MUST enforce:
immutable transaction history
operator accountability
branch isolation
audit-safe logging
timestamp validation
reconciliation protection

26. OPERATOR LIMITATIONS
Operators CANNOT:
modify finalized EOD
delete transactions
alter cash totals
override mismatches
force close active sessions

27. SUPER ADMIN ACCESS
Super Admin CAN:
audit all branches
export EOD reports
monitor revenue
inspect mismatches
review operator activity
override closure restrictions

28. RECOMMENDED EXTRA FEATURES
These are highly recommended for production systems:
Feature
Recommended
PDF Export
YES
CSV Export
YES
Daily Backup
YES
Auto EOD Snapshot
YES
Revenue Graphs
YES
Hourly Revenue Analytics
YES
Peak Usage Tracking
YES


29. FINAL PRODUCTION PRINCIPLE
The End of Day Dashboard is NOT only a summary screen.
It is:
operational closure engine
financial reconciliation system
audit tracking system
branch verification system
immutable historical record system
This dashboard represents the final operational truth of the gaming café for that business day.

PC STATUS DASHBOARD — FINAL REFINED PRODUCTION SOP
Gaming Café Management System

1. PURPOSE OF PC STATUS DASHBOARD
The PC Status Dashboard is a:
REAL-TIME BRANCH MONITORING & CONTROL SYSTEM
This dashboard is responsible for:
monitoring all PCs
tracking live PC states
monitoring branch activity
viewing active sessions
viewing reservations
detecting offline PCs
monitoring operational flow
This dashboard gives the Super Admin a complete real-time operational overview of the gaming café.

2. WHO CAN ACCESS THIS DASHBOARD
Role
Access
Super Admin
Full Access
Operator
NO ACCESS
User
NO ACCESS


IMPORTANT PRINCIPLE
This dashboard is:
SUPER ADMIN ONLY
Because this dashboard exposes:
complete branch visibility
operational monitoring
live business activity
centralized system tracking
Operators should NOT have access to this level of monitoring.

3. PRIMARY OBJECTIVE
The PC Status Dashboard exists to:
monitor all PCs centrally
track live branch activity
detect operational issues
monitor active gaming load
monitor reservations
monitor pending billing states
maintain operational visibility

4. WHAT PC STATUS DASHBOARD TRACKS
Item
Included
Live PC Status
YES
Active Sessions
YES
Reserved PCs
YES
Awaiting Bills
YES
Offline PCs
YES
Branch Mapping
YES
Session Timers
YES
Operator Handling
YES
Customer Assignment
YES
Last Activity Time
YES
Date & Time Logs
YES


5. DASHBOARD STRUCTURE
The dashboard contains:
Section
Purpose
Branch Overview
Multi-branch monitoring
PC Grid Layout
Real-time PC visualization
Active Session Monitor
Live gaming tracking
Reservation Monitor
Reserved PC tracking
Awaiting Bill Monitor
Pending billing tracking
Offline Monitor
Disconnected PC tracking
Session Details Panel
Selected PC information
Alerts & Exceptions
Operational issue detection


6. MULTI-BRANCH MONITORING
Super Admin can:
switch branches
monitor all branches
compare live branch activity

EXAMPLE
Branch
Active PCs
Adajan
18
Citylight
11
Katargam
9


7. LIVE PC GRID SYSTEM
The dashboard visually displays:
every PC
current live status
real-time operational state

PC STATES
State
Meaning
Idle
Available for gaming
Active
Gaming session running
Reserved
Reserved for customer
Awaiting Bill
Session stopped, billing pending
Offline
PC unavailable/disconnected


8. VISUAL STATUS COLORS
Color
Meaning
Green
Active
Purple
Reserved
Orange
Awaiting Bill
Gray
Idle
Red
Offline


9. ACTIVE SESSION MONITOR
Tracks:
all live gaming sessions
active PCs
running session durations
assigned operators

EACH ACTIVE SESSION MUST DISPLAY
Field
Example
PC Number
PC-05
Customer Name
Rahul
Session Start Time
2:00 PM
Live Session Duration
1h 25m
Current Bill
₹180
Operator
Meet
Branch
Adajan


10. LIVE SESSION TIMER
Session timers must:
update in real-time
synchronize with billing engine
synchronize across all dashboards

IMPORTANT
All dashboards must show:
SAME SESSION STATE
No dashboard should display conflicting information.

11. RESERVATION MONITOR
Tracks:
reserved PCs
reservation timings
upcoming reservations
missed reservations

EACH RESERVATION MUST DISPLAY
Field
Example
PC Number
PC-03
Reserved For
Rahul
Reservation Start
5:00 PM
Remaining Time
12 Minutes
Branch
Adajan


12. RESERVATION PROTECTION
Reserved PCs should:
remain visually protected
prevent accidental reassignment
display warning indicators

13. AWAITING BILL MONITOR
Tracks PCs where:
gaming stopped
billing pending
session not finalized

PURPOSE
Helps Super Admin detect:
operator delays
unclosed bills
pending customer payments

EACH AWAITING BILL ENTRY MUST DISPLAY
Field
Example
PC Number
PC-09
Session Duration
2h 40m
Pending Amount
₹320
Operator
Rahul
Waiting Since
8 Minutes


14. OFFLINE PC MONITOR
Tracks:
disconnected PCs
inactive systems
unavailable machines

EACH OFFLINE ENTRY MUST DISPLAY
Field
Example
PC Number
PC-12
Status
Offline
Last Seen
5:10 PM
Branch
Citylight


15. LAST ACTIVITY TRACKING
Every PC should maintain:
Field
Purpose
Last Active Time
Session tracking
Last Session End
Usage tracking
Last Operator Interaction
Audit tracking


16. LIVE SYNCHRONIZATION ENGINE
PC Status Dashboard synchronizes LIVE with:
Dashboard
Sync
Billing Counter
YES
Reservation Dashboard
YES
EOD Dashboard
YES
Session Engine
YES
Operator Logs
YES


17. REAL-TIME UPDATE REQUIREMENT
Dashboard should update:
automatically
without manual refresh
in real-time

RECOMMENDED TECHNOLOGY
Recommended:
WebSockets
Socket.IO
Event-driven synchronization

18. ALERTS & EXCEPTION SECTION
Tracks operational problems.

EXAMPLES
Alert
Example
PC Offline
YES
Long Pending Bill
YES
Reservation Conflict
YES
Idle Reserved PC
YES
Session Timeout
YES


19. LONG SESSION DETECTION
System should detect:
unusually long sessions
forgotten sessions
possible operator negligence

EXAMPLE
⚠ LONG SESSION ALERT
PC-07 active for:
9h 15m
Operator:
Rahul

20. OCCUPANCY MONITORING
Admin should see:
Metric
Example
Total PCs
40
Active PCs
31
Occupancy Rate
77%


21. OCCUPANCY ANALYTICS
Helps admin monitor:
peak hours
business load
branch performance
idle capacity

22. REMOTE ADMIN ACTIONS (OPTIONAL)
Super Admin MAY be allowed to:
force stop session
lock PC
send operator alert

IMPORTANT
These actions should require:
confirmation popup
audit logging

23. AUDIT LOGGING
Every critical admin action MUST store:
Field
Example
Admin Name
Harshal
Action
Force Stop Session
PC Number
PC-07
Date
12/07/2026
Time
8:40 PM


24. DATE & TIME TRACKING
Every PC event MUST contain:
exact date
exact timestamp

EVENTS TO TRACK
Event
Logged
Session Start
YES
Session End
YES
Reservation Start
YES
Reservation End
YES
Offline Detection
YES


25. SECURITY RULES
PC Status Dashboard MUST enforce:
Super Admin-only access
branch security
immutable operational logs
real-time synchronization
secure monitoring

26. WHY OPERATORS SHOULD NOT ACCESS THIS
Because operators should NOT:
monitor all branches
monitor all operators
access centralized visibility
access system-wide operational control

27. RECOMMENDED EXTRA FEATURES
Recommended production features:
Feature
Recommended
Search PC
YES
Filter by State
YES
Filter by Branch
YES
Occupancy Graph
YES
Notification System
YES
Auto Refresh
YES


28. FINAL PRODUCTION PRINCIPLE
The PC Status Dashboard is NOT only a PC list.
It is:
real-time monitoring engine
operational visibility system
centralized branch monitor
session tracking engine
synchronization control dashboard
This dashboard gives the Super Admin complete operational visibility of the gaming café ecosystem.

MEMBERS DASHBOARD — FINAL REFINED PRODUCTION SOP
Gaming Café Management System

1. PURPOSE OF MEMBERS DASHBOARD
The Members Dashboard is responsible for:
managing registered members
maintaining loyalty points
maintaining prepaid wallet balances
tracking gaming spending
tracking food spending
tracking member payment activity
maintaining member transaction history
managing rewards & benefits
This dashboard acts as the:
CUSTOMER MEMBERSHIP, WALLET & LOYALTY MANAGEMENT SYSTEM

2. WHO CAN ACCESS THIS DASHBOARD
Role
Access
Super Admin
Full Access
Operator
Operational Access
User
NO ACCESS


3. PRIMARY OBJECTIVE
The Members Dashboard exists to:
improve customer retention
simplify recurring customer billing
maintain prepaid wallet system
reward loyal customers
track member activity
maintain secure transaction records

4. WHAT MEMBERS DASHBOARD TRACKS
Item
Included
Member Information
YES
Loyalty Points
YES
Wallet Balance
YES
Gaming Spending
YES
Food Spending
YES
Wallet Transactions
YES
Payment Methods
YES
Session History
YES
Reward Activity
YES
Date & Time Logs
YES


5. MEMBER PROFILE INFORMATION
Each member profile should contain:
Field
Required
Member ID
YES
Full Name
YES
Mobile Number
YES
Email
Optional
Join Date
YES
Current Points
YES
Wallet Balance
YES
Member Status
YES


6. MEMBER STATUS TYPES
Status
Meaning
Active
Normal member
VIP
Premium member
Suspended
Temporarily blocked
Inactive
No recent activity


7. MEMBER CREATION WORKFLOW

STEP 1 — OPERATOR CREATES MEMBER
Operator enters:
Field
Example
Name
Rahul
Mobile Number
9876543210


STEP 2 — SYSTEM GENERATES
System creates:
Member ID
Wallet Profile
Loyalty Profile
Transaction History

STEP 3 — MEMBER ACTIVATED
Member becomes available for:
wallet recharge
loyalty tracking
gaming sessions
food billing

8. MEMBER SEARCH SYSTEM
Members should be searchable using:
Search Type
Supported
Mobile Number
YES
Member ID
YES
Name
YES


9. MEMBER WALLET SYSTEM
Members may maintain:
PREPAID WALLET BALANCE

PURPOSE
Allows:
faster billing
prepaid gaming
prepaid food purchases
customer retention
smoother repeat usage

10. WALLET BALANCE MANAGEMENT
This is a core operational feature.

OPERATORS CAN:
Action
Allowed
Add Wallet Balance
YES
Remove Wallet Balance
YES
Use Wallet Balance
YES
View Wallet Balance
YES


SUPER ADMIN CAN:
Action
Allowed
Add Wallet Balance
YES
Remove Wallet Balance
YES
Correct Wallet Balance
YES
Audit Wallet Activity
YES


IMPORTANT PRINCIPLE
Operators are allowed to:
recharge member wallets
deduct wallet balance
because operators directly handle customer transactions.

11. WALLET RECHARGE PAYMENT TRACKING
This is a VERY IMPORTANT production feature.
Whenever wallet balance is added:
System MUST track:
how customer paid
how much customer paid
payment method used

SUPPORTED PAYMENT METHODS
Method
Supported
Cash
YES
Online / UPI
YES
Split Payment
YES


12. WALLET RECHARGE FLOW

EXAMPLE
Customer wants:
₹1000 wallet recharge
Customer pays:
₹500 cash
₹500 online

SYSTEM MUST STORE
Field
Example
Recharge Amount
₹1000
Cash Amount
₹500
Online Amount
₹500
Payment Type
Split
Operator
Rahul
Date
12/07/2026
Time
5:20 PM


IMPORTANT
This recharge transaction MUST synchronize with:
Billing System
Cash Register
EOD Reports
Wallet History

13. WALLET DEDUCTION SYSTEM
Wallet balance may reduce because of:
gaming billing
food billing
reward redemption

IMPORTANT RULE
Gaming and food billing MUST remain separated.

EXAMPLE
Category
Amount
Gaming Deduction
₹400
Food Deduction
₹150


WHY IMPORTANT?
Because:
gaming analytics differ
food analytics differ
reward structures differ
business reporting differs

14. WALLET TRANSACTION LOGGING
Every wallet action MUST store:
Field
Example
Member Name
Rahul
Action
Wallet Recharge
Amount
₹1000
Cash Amount
₹500
Online Amount
₹500
Payment Type
Split
Operator
Meet
Date
12/07/2026
Time
5:20 PM


15. LOYALTY POINT SYSTEM
The system supports:
MEMBER REWARD POINTS
based on:
gaming spending
food spending

16. POINT TRACKING SEPARATION
Points MUST track separately for:
Category
Separate Tracking
Gaming Points
YES
Food Points
YES


WHY IMPORTANT?
Because:
food promotions differ
gaming promotions differ
VIP rules may differ later
reward campaigns may differ

17. POINT MODIFICATION SYSTEM

SUPER ADMIN CAN:
Action
Allowed
Add Points
YES
Remove Points
YES
Correct Points
YES


OPERATORS CAN:
Action
Allowed
View Points
YES
Redeem Points
YES
Modify Points Manually
NO


IMPORTANT
Operators should NOT manually manipulate loyalty points.
This prevents:
reward abuse
fake rewards
unauthorized loyalty manipulation

18. POINT REDEMPTION SYSTEM
Members may redeem points for:
gaming discounts
free gaming hours
food discounts

19. REWARD LOGGING
Every reward redemption MUST store:
Field
Example
Member Name
Rahul
Reward Type
Gaming Discount
Points Used
250
Value Given
₹50
Operator
Meet
Date
12/07/2026
Time
7:10 PM


20. MEMBER TRANSACTION HISTORY
Each member profile should display:
Item
Included
Gaming Bills
YES
Food Bills
YES
Wallet Recharges
YES
Wallet Deductions
YES
Point Activity
YES
Session History
YES


21. MEMBER SESSION HISTORY
Tracks:
visited PCs
gaming duration
spending history
branch usage

EACH SESSION ENTRY MUST DISPLAY
Field
Example
Date
12/07/2026
Branch
Adajan
PC Used
PC-07
Session Duration
2h 20m
Gaming Spend
₹350
Food Spend
₹120


22. DATE & TIME TRACKING
Every member activity MUST contain:
exact date
exact timestamp

EVENTS TO TRACK
Event
Logged
Member Creation
YES
Wallet Recharge
YES
Wallet Usage
YES
Wallet Deduction
YES
Point Redemption
YES
Session Usage
YES


23. MEMBER PAYMENT TRACEABILITY
This is a CRITICAL production feature.
System MUST always trace:
Action
Trace Required
Wallet Recharge
YES
Wallet Deduction
YES
Gaming Payment
YES
Food Payment
YES
Split Payment
YES


PURPOSE
This prevents:
missing money
operator confusion
reconciliation mismatch
wallet fraud
transaction disputes

24. LIVE SYNCHRONIZATION
Members Dashboard synchronizes with:
Dashboard
Sync
Billing Counter
YES
Cash Register
YES
EOD Dashboard
YES
Reward Engine
YES
Transaction Logs
YES


25. MEMBER ANALYTICS
Dashboard should track:
Metric
Example
Total Members
580
Active Members
320
VIP Members
24
Most Active Member
Rahul
Total Wallet Balance
₹85,000


26. MEMBER SECURITY RULES
Members Dashboard MUST enforce:
secure wallet tracking
immutable transaction logs
audit-safe payment history
secure reward tracking
role-based access

27. OPERATOR LIMITATIONS
Operators CANNOT:
manually modify loyalty points
delete members
alter old wallet logs
manipulate payment history

28. SUPER ADMIN ACCESS
Super Admin CAN:
audit all members
inspect wallet history
inspect recharge history
modify balances
monitor loyalty activity
review payment traceability

29. RECOMMENDED EXTRA FEATURES
Recommended production features:
Feature
Recommended
QR Member Lookup
YES
OTP Verification
YES
Reward Notifications
YES
Birthday Rewards
YES
Referral System
YES
Auto VIP Upgrade
YES
Recharge Offers
YES


30. FINAL PRODUCTION PRINCIPLE
The Members Dashboard is NOT only a customer list.
It is:
wallet management system
loyalty engine
customer retention platform
payment traceability system
gaming & food behavior analytics system
This dashboard manages the long-term customer ecosystem of the gaming café.

MAIN DASHBOARD — FINAL PRODUCTION SOP
Gaming Café Management System

1. PURPOSE OF MAIN DASHBOARD
The Main Dashboard is the:
CENTRAL OPERATIONAL OVERVIEW SYSTEM
This dashboard provides:
live business overview
real-time operational visibility
branch activity monitoring
revenue tracking
session monitoring
transaction visibility
reservation monitoring
This dashboard acts as the:
CONTROL CENTER OF DAILY OPERATIONS

2. WHO CAN ACCESS THIS DASHBOARD
Role
Access
Super Admin
Full Access
Operator
Operational Access
User
NO ACCESS


3. PRIMARY OBJECTIVE
The Dashboard exists to:
provide quick operational visibility
reduce operator navigation time
monitor live business activity
track ongoing sessions
monitor revenue flow
monitor branch performance
quickly identify operational issues

4. WHAT DASHBOARD TRACKS
Item
Included
Active Sessions
YES
Available PCs
YES
Reserved PCs
YES
Awaiting Bills
YES
Daily Revenue
YES
Gaming Revenue
YES
Food Revenue
YES
Recent Transactions
YES
Operator Activity
YES
Date & Time Logs
YES


5. DASHBOARD STRUCTURE
The Main Dashboard contains:
Section
Purpose
Live Status Cards
Quick operational overview
Revenue Summary
Business monitoring
Session Overview
Live gaming monitoring
Recent Transactions
Transaction visibility
Reservation Overview
Reservation tracking
Alerts & Exceptions
Operational issue detection
Activity Summary
Operator activity monitoring


6. LIVE STATUS CARDS
Dashboard should display:
Card
Purpose
Active PCs
Running sessions
Idle PCs
Available systems
Reserved PCs
Upcoming reservations
Awaiting Bills
Pending billing
Offline PCs
Unavailable systems


7. LIVE SESSION OVERVIEW
This section displays:
active sessions
session durations
live gaming load
operator handling

EACH ACTIVE SESSION SHOULD DISPLAY
Field
Example
PC Number
PC-05
Customer Name
Rahul
Session Duration
2h 10m
Current Bill
₹180
Operator
Meet


8. REVENUE SUMMARY SECTION
This section displays:
Revenue Type
Example
Gaming Revenue
₹12,000
Food Revenue
₹4,500
Total Revenue
₹16,500


IMPORTANT RULE
Gaming and food revenue MUST remain separated.

9. PAYMENT SUMMARY SECTION
Tracks:
cash revenue
online revenue
split-payment totals

IMPORTANT
Split-payment analytics should properly separate:
cash portion
online portion

10. RECENT TRANSACTION SECTION
This is one of the MOST IMPORTANT dashboard sections.

PURPOSE
Allows:
quick transaction visibility
dispute verification
operational tracking
historical transaction lookup

EACH TRANSACTION ENTRY MUST DISPLAY
Field
Example
Bill ID
BILL-1021
PC Number
PC-03
Gaming Amount
₹220
Food Amount
₹80
Total Amount
₹300
Payment Type
Split
Operator
Rahul
Date
12/07/2026
Time
7:10 PM


11. DATE & TIME TRACKING
Every transaction MUST contain:
exact date
exact timestamp

WHY IMPORTANT?
Because:
multiple shifts exist
audits require timestamps
disputes require proof
historical tracking depends on timing

12. TRANSACTION HISTORY CALENDAR SYSTEM
This is a critical production feature.

PURPOSE
Allows:
operator
Super Admin
to:
access old transactions
filter by specific date
quickly retrieve historical records

CALENDAR FILTER WORKFLOW

STEP 1 — OPEN RECENT TRANSACTION SECTION
Operator/Super Admin opens:
Recent Transactions Panel

STEP 2 — SELECT DATE FROM CALENDAR
User selects:
specific day
previous date
custom historical date
using:
CALENDAR PICKER

STEP 3 — SYSTEM FILTERS TRANSACTIONS
System displays ALL transactions for selected date.

EXAMPLE
Selected Date:
12/07/2026
System Displays:
all bills
all sessions
all payments
all operators
all timestamps
for that date only.

13. CALENDAR FILTER REQUIREMENTS
The calendar system MUST support:
Feature
Required
Single Date Selection
YES
Previous Date Lookup
YES
Current Day View
YES
Fast Filtering
YES
Auto Refresh
YES


14. TRANSACTION SEARCH SYSTEM
Highly recommended production feature.

USERS SHOULD BE ABLE TO SEARCH BY
Search Type
Supported
Bill ID
YES
PC Number
YES
Operator Name
YES
Customer Name
YES
Payment Type
YES


15. TRANSACTION FILTER SYSTEM
Recommended production feature.

FILTERS SHOULD INCLUDE
Filter
Supported
Cash Payments
YES
Online Payments
YES
Split Payments
YES
Gaming Revenue
YES
Food Revenue
YES


16. TRANSACTION DETAIL VIEW
When transaction clicked:
System should display:
complete bill breakdown
payment details
operator details
timestamps
change returned
actual cash collected

17. ALERTS & EXCEPTION SECTION
Tracks operational issues.

EXAMPLES
Alert
Example
Pending Bills
YES
Offline PCs
YES
Reservation Conflict
YES
Cash Mismatch
YES
Long Active Session
YES


18. OPERATOR ACTIVITY SUMMARY
Tracks:
currently logged-in operators
shift activity
bills processed

EACH OPERATOR ENTRY SHOULD DISPLAY
Field
Example
Operator Name
Rahul
Shift Start
10:00 AM
Bills Processed
42
Active Sessions Handled
18


19. RESERVATION OVERVIEW
Tracks:
reserved PCs
upcoming reservations
reservation timings

EACH RESERVATION SHOULD DISPLAY
Field
Example
PC Number
PC-08
Reserved For
Rahul
Start Time
8:00 PM
Remaining Time
15 Minutes


20. LIVE SYNCHRONIZATION
Dashboard synchronizes LIVE with:
Dashboard
Sync
Billing Counter
YES
Cash Register
YES
EOD Dashboard
YES
Reservation System
YES
Members Dashboard
YES


21. REAL-TIME UPDATE REQUIREMENT
Dashboard must:
update automatically
synchronize instantly
avoid manual refresh

RECOMMENDED TECHNOLOGY
Recommended:
WebSockets
Socket.IO
Event-driven synchronization

22. DATE-WISE ANALYTICS (RECOMMENDED)
Highly recommended production feature.

ADMIN SHOULD BE ABLE TO VIEW
Metric
Example
Daily Revenue
YES
Previous Day Revenue
YES
Weekly Revenue
YES
Peak Hours
YES


23. QUICK ACTION SHORTCUTS (RECOMMENDED)
Dashboard may include quick shortcuts:
Action
Recommended
Open Billing Counter
YES
Open Reservations
YES
Open EOD
YES
Open Members
YES


24. SECURITY RULES
Dashboard MUST enforce:
branch isolation
role-based access
immutable transaction visibility
secure audit tracking
safe synchronization

25. OPERATOR LIMITATIONS
Operators CANNOT:
modify old transactions
delete transaction history
alter payment history
modify historical logs

26. SUPER ADMIN ACCESS
Super Admin CAN:
monitor all branches
inspect all transactions
access historical analytics
review operational history
audit payment activity

27. RECOMMENDED EXTRA FEATURES
Recommended production features:
Feature
Recommended
Revenue Graphs
YES
Hourly Analytics
YES
Occupancy Analytics
YES
Transaction Export
YES
PDF Reports
YES
CSV Export
YES
Notification System
YES


28. FINAL PRODUCTION PRINCIPLE
The Main Dashboard is NOT only a homepage screen.
It is:
operational overview system
business monitoring engine
transaction visibility platform
live synchronization dashboard
historical tracking system
This dashboard provides the real-time operational heartbeat of the gaming café.

SESSIONS DASHBOARD — FINAL PRODUCTION SOP
Gaming Café Management System

1. PURPOSE OF SESSIONS DASHBOARD
The Sessions Dashboard is the:
LIVE SESSION MANAGEMENT SYSTEM
This dashboard is responsible for:
monitoring all running sessions
managing active gaming sessions
tracking session timers
handling session lifecycle
managing reservation conflicts
tracking pending billing states
monitoring operator activity
This dashboard acts as the:
REAL-TIME SESSION CONTROL CENTER

2. WHO CAN ACCESS THIS DASHBOARD
Role
Access
Super Admin
Full Access
Operator
Operational Access
User
NO ACCESS


3. PRIMARY OBJECTIVE
The Sessions Dashboard exists to:
manage active gaming sessions
monitor real-time session activity
reduce operator confusion
prevent reservation conflicts
track pending bills
maintain synchronized session states

4. WHAT SESSIONS DASHBOARD TRACKS
Item
Included
Active Sessions
YES
Reserved Sessions
YES
Awaiting Bills
YES
Session Timers
YES
Assigned PCs
YES
Customer Assignment
YES
Operator Handling
YES
Billing Status
YES
Date & Time Logs
YES


5. DASHBOARD STRUCTURE
The Sessions Dashboard contains:
Section
Purpose
Active Sessions List
Running session monitoring
Reservation Monitor
Reserved session tracking
Awaiting Bill Section
Billing pending sessions
Session Detail Panel
Detailed selected session info
Alerts & Exceptions
Session issue detection
Live Session Controls
Session management actions


6. ACTIVE SESSION MONITOR
This is the core section.
Displays:
all currently running sessions
active PCs
live timers
current billing status

EACH ACTIVE SESSION MUST DISPLAY
Field
Example
PC Number
PC-05
Customer Name
Rahul
Session Start Time
2:00 PM
Live Duration
1h 20m
Current Bill
₹180
Operator
Meet
Branch
Adajan
Session Status
Active


7. LIVE SESSION TIMER
Session timers must:
update in real-time
synchronize with billing engine
synchronize across all dashboards

IMPORTANT PRINCIPLE
All dashboards must always display:
SAME SESSION STATE
No dashboard should show conflicting session data.

8. SESSION STATES
State
Meaning
Active
Session running
Reserved
Reserved for customer
Awaiting Bill
Session stopped, billing pending
Completed
Session fully closed
Expired
Reserved session missed


9. RESERVATION MONITOR
This is a CRITICAL production feature.
Tracks:
upcoming reservations
reserved PCs
reservation timings
reservation conflicts

EACH RESERVATION MUST DISPLAY
Field
Example
PC Number
PC-03
Reserved For
Rahul
Reservation Start Time
5:00 PM
Remaining Time
12 Minutes
Branch
Adajan


10. RESERVATION PROTECTION SYSTEM
Reserved PCs MUST remain protected.
Operators should NOT accidentally:
start another session
override reserved PCs
assign wrong customers

11. RESERVED SESSION POPUP SYSTEM
If operator tries to access a reserved PC:
System MUST display popup:
⚠ RESERVED PC
PC-03 reserved for Rahul
Reservation starts at 5:00 PM

AVAILABLE OPTIONS
Action
Allowed
Cancel
YES
Start Reserved Session
YES


IMPORTANT
Operators CANNOT:
override reservation
remove reservation
bypass reservation protection
Only Super Admin can override reservation conflicts.

12. UPCOMING RESERVATION ALERTS
The system should automatically warn operators when:
reservation time is near
active session overlaps reservation

EXAMPLE
⚠ UPCOMING RESERVATION ALERT
PC-05 reserved in:
15 minutes
Current session still active.

13. RESERVATION CONFLICT DETECTION
System MUST detect:
Conflict Type
Example
Active Session Overlap
YES
Wrong Customer Assignment
YES
Late Session Closure
YES


14. CONFLICT WARNING SYSTEM
When conflict occurs:
System should:
show warning popup
notify operator
notify Super Admin (optional)

15. AWAITING BILL SECTION
Tracks sessions where:
gaming stopped
billing pending
session not finalized

PURPOSE
Prevents:
forgotten bills
delayed checkout
unclosed sessions

EACH AWAITING BILL ENTRY MUST DISPLAY
Field
Example
PC Number
PC-09
Customer Name
Rahul
Session Duration
2h 40m
Pending Amount
₹320
Waiting Since
8 Minutes
Operator
Meet


16. SESSION COMPLETION FLOW
When session stops:
System performs:
timer freeze
billing lock
session state update
awaiting bill activation

17. SESSION FINALIZATION FLOW
When billing completes:
System:
closes session
clears PC assignment
resets session state
synchronizes dashboards

18. SESSION HISTORY TRACKING
Every session MUST store:
Field
Required
Session ID
YES
PC Number
YES
Customer Name
Optional
Start Time
YES
End Time
YES
Total Duration
YES
Gaming Amount
YES
Food Amount
YES
Operator Name
YES
Date
YES
Time
YES


19. DATE & TIME TRACKING
Every session activity MUST contain:
exact date
exact timestamp

EVENTS TO TRACK
Event
Logged
Session Start
YES
Session Stop
YES
Reservation Start
YES
Reservation Completion
YES
Awaiting Bill Activation
YES
Session Completion
YES


20. LONG SESSION DETECTION
System should detect:
unusually long sessions
forgotten sessions
inactive sessions

EXAMPLE
⚠ LONG SESSION ALERT
PC-07 active for:
9h 10m
Operator:
Rahul

21. LIVE SESSION CONTROLS
Operators MAY be allowed to:
stop session
extend session
transfer session (optional)

IMPORTANT
Critical actions should require:
confirmation popup
audit logging

22. SESSION EXTENSION SYSTEM
Operators may extend:
gaming duration
reserved session timing
ONLY if:
no reservation conflict exists

23. SESSION TRANSFER SYSTEM (OPTIONAL)
Recommended production feature.
Allows:
moving customer to another PC
maintaining billing continuity
avoiding restart

IMPORTANT
Transfer should:
move timer
move billing state
update session logs
synchronize all dashboards

24. LIVE SYNCHRONIZATION
Sessions Dashboard synchronizes LIVE with:
Dashboard
Sync
Billing Counter
YES
Reservation Dashboard
YES
EOD Dashboard
YES
PC Status Dashboard
YES
Operator Logs
YES


25. REAL-TIME UPDATE REQUIREMENT
Sessions Dashboard must:
update automatically
synchronize instantly
avoid manual refresh

RECOMMENDED TECHNOLOGY
Recommended:
WebSockets
Socket.IO
Event-driven synchronization

26. ALERTS & EXCEPTION SECTION
Tracks operational session issues.

EXAMPLES
Alert
Example
Reservation Conflict
YES
Long Session
YES
Pending Bill
YES
Missed Reservation
YES
Session Timeout
YES


27. SECURITY RULES
Sessions Dashboard MUST enforce:
branch isolation
synchronized session states
secure reservation handling
immutable session history
audit-safe tracking

28. OPERATOR LIMITATIONS
Operators CANNOT:
override reservations
delete session history
modify completed sessions
alter timestamps

29. SUPER ADMIN ACCESS
Super Admin CAN:
monitor all sessions
inspect reservation conflicts
audit session history
override operational conflicts
monitor branch activity

30. RECOMMENDED EXTRA FEATURES
Recommended production features:
Feature
Recommended
Session Search
YES
Filter by Status
YES
Session Export
YES
Live Occupancy Analytics
YES
Session Notifications
YES
Auto Session Warning
YES


31. FINAL PRODUCTION PRINCIPLE
The Sessions Dashboard is NOT only a running session list.
It is:
live session management engine
reservation-aware control system
operational monitoring platform
synchronized gaming session tracker
This dashboard controls the real-time gaming activity of the café ecosystem.

CASH DESK DASHBOARD — FINAL REFINED PRODUCTION SOP
Gaming Café Management System

1. PURPOSE OF CASH DESK DASHBOARD
The Cash Desk Dashboard is the:
CENTRAL PAYMENT & MONEY FLOW MONITORING SYSTEM
This dashboard is responsible for:
monitoring all payment activity
tracking payment methods
monitoring split payments
tracking wallet payments
monitoring operator payment handling
maintaining payment traceability
handling end-shift denomination verification

IMPORTANT PRINCIPLE
CASH DESK ≠ CASH REGISTER
These are DIFFERENT systems.

CASH REGISTER
Tracks:
physical drawer balance
opening balance
expected drawer cash
shift cash verification

CASH DESK
Tracks:
all payment activity
payment methods
split payments
wallet transactions
payment traceability
denomination verification

2. WHO CAN ACCESS THIS DASHBOARD
Role
Access
Super Admin
Full Access
Operator
Operational Access
User
NO ACCESS


3. PRIMARY OBJECTIVE
The Cash Desk exists to:
monitor all payment flow
maintain payment traceability
reduce operator mistakes
track split payments
verify shift-end cash denominations
support reconciliation accuracy

4. WHAT CASH DESK TRACKS
Item
Included
Cash Payments
YES
Online Payments
YES
Split Payments
YES
Wallet Transactions
YES
Change Returned
YES
Payment Logs
YES
Operator Payment Activity
YES
Denomination Count
YES
Date & Time Logs
YES


5. DASHBOARD STRUCTURE
The Cash Desk Dashboard contains:
Section
Purpose
Live Payment Feed
Real-time transaction visibility
Payment Summary
Payment analytics
Split Payment Monitor
Mixed-payment tracking
Wallet Transaction Monitor
Wallet activity tracking
Denomination Counter
Shift-end cash verification
Payment History
Historical payment lookup
Alerts & Exceptions
Payment issue tracking


6. LIVE PAYMENT FEED
Displays:
latest completed transactions
live payment activity
payment method details

EACH PAYMENT ENTRY MUST DISPLAY
Field
Example
Bill ID
BILL-1021
PC Number
PC-03
Gaming Amount
₹220
Food Amount
₹80
Total Bill
₹300
Cash Amount
₹100
Online Amount
₹200
Payment Type
Split
Change Returned
₹0
Operator
Rahul
Date
12/07/2026
Time
7:10 PM


7. PAYMENT TYPE SUPPORT
Supported payment methods:
Payment Type
Supported
Cash
YES
Online / UPI
YES
Split Payment
YES
Wallet Payment
YES


8. SPLIT PAYMENT MONITOR
Tracks:
mixed payment handling
cash + online combinations
split-payment accuracy

EXAMPLE
Bill:
₹500
Customer Pays:
₹200 cash
₹300 online
Cash Desk displays:
Type
Amount
Cash
₹200
Online
₹300


IMPORTANT
Split payments MUST remain properly separated.

9. CHANGE RETURN TRACKING
Tracks:
cash received
change returned
retained business cash

EXAMPLE
Bill:
₹280
Customer Gives:
₹500
Change Returned:
₹220
Actual Cash Retained:
₹280

IMPORTANT RULE
Cash Desk tracks:
COMPLETE PAYMENT FLOW

10. WALLET TRANSACTION MONITOR
Tracks:
wallet recharge
wallet deduction
wallet payment usage

EACH WALLET ENTRY MUST DISPLAY
Field
Example
Member Name
Rahul
Wallet Action
Recharge
Recharge Amount
₹1000
Payment Type
Split
Cash Amount
₹500
Online Amount
₹500
Operator
Meet
Date
12/07/2026
Time
5:20 PM


11. GAMING & FOOD PAYMENT SEPARATION
Gaming and food billing MUST remain separated.

EXAMPLE
Category
Amount
Gaming Revenue
₹400
Food Revenue
₹150


WHY IMPORTANT?
Because:
reporting differs
analytics differ
loyalty systems differ
promotions may differ

12. PAYMENT SUMMARY SECTION
Displays:
Metric
Example
Total Payments
₹25,000
Cash Payments
₹8,000
Online Payments
₹14,000
Wallet Payments
₹3,000


13. DENOMINATION COUNTER SYSTEM
This is a VERY IMPORTANT production feature.

PURPOSE
At shift closing:
Operator counts:
physical notes
physical cash denominations
and enters denomination totals into the system.

IMPORTANT PRINCIPLE
Operator DOES NOT enter denomination count for every transaction.
Operator performs denomination counting:
ONLY AT SHIFT END

14. DENOMINATION ENTRY FLOW

STEP 1 — SHIFT END
Operator opens:
Denomination Counter Section

STEP 2 — COUNT PHYSICAL NOTES
Operator counts:
₹500 notes
₹200 notes
₹100 notes
₹50 notes
₹20 notes
₹10 notes

STEP 3 — ENTER NOTE COUNTS
Example:
Denomination
Count
₹500
4
₹200
1
₹100
3
₹50
2


SYSTEM AUTOMATICALLY CALCULATES
Calculation
Result
₹500 × 4
₹2000
₹200 × 1
₹200
₹100 × 3
₹300
₹50 × 2
₹100

Final Total:
₹2600

15. DENOMINATION VERIFICATION
System compares:
Item
Value
Expected Drawer Cash
₹2600
Counted Denomination Total
₹2600


IF MATCHES
System displays:
✅ CASH VERIFIED

IF MISMATCH OCCURS
System displays:
⚠ CASH MISMATCH DETECTED
Expected:
₹2600
Counted:
₹2550
Difference:
-₹50

16. WHY DENOMINATION SYSTEM IS IMPORTANT
This helps:
reduce counting mistakes
verify physical cash accurately
simplify shift closing
improve reconciliation
reduce operator disputes

17. DENOMINATION LOGGING
Every denomination verification MUST store:
Field
Example
Operator Name
Rahul
Branch
Adajan
Total Counted Cash
₹2600
Difference
₹0
Date
12/07/2026
Time
10:05 PM


18. PAYMENT HISTORY SYSTEM
Allows:
historical payment lookup
dispute resolution
transaction verification

19. DATE FILTER & CALENDAR SYSTEM
Allows:
operators
Super Admin
to:
access old payment records
filter by date
verify historical payments
using:
CALENDAR PICKER

20. PAYMENT SEARCH SYSTEM
Users should search by:
Search Type
Supported
Bill ID
YES
Member Name
YES
Operator Name
YES
PC Number
YES
Payment Type
YES


21. ALERTS & EXCEPTION SECTION
Tracks payment issues.

EXAMPLES
Alert
Example
Split Payment Error
YES
Payment Mismatch
YES
Wallet Failure
YES
Duplicate Transaction
YES
Denomination Mismatch
YES


22. LIVE SYNCHRONIZATION
Cash Desk synchronizes LIVE with:
Dashboard
Sync
Billing Counter
YES
Cash Register
YES
Members Dashboard
YES
EOD Dashboard
YES
Transaction Logs
YES


23. REAL-TIME UPDATE REQUIREMENT
Cash Desk must:
update automatically
synchronize instantly
avoid manual refresh

RECOMMENDED TECHNOLOGY
Recommended:
WebSockets
Socket.IO
Event-driven synchronization

24. SECURITY RULES
Cash Desk MUST enforce:
immutable payment logs
secure denomination tracking
role-based access
audit-safe transaction history
synchronized payment records

25. OPERATOR LIMITATIONS
Operators CANNOT:
modify completed payments
delete payment history
alter old transactions
manipulate denomination history

26. SUPER ADMIN ACCESS
Super Admin CAN:
inspect all payments
audit denomination reports
inspect operator activity
review split payments
inspect wallet transactions

27. RECOMMENDED EXTRA FEATURES
Recommended production features:
Feature
Recommended
PDF Reports
YES
CSV Export
YES
Revenue Graphs
YES
Denomination Export
YES
Payment Notifications
YES


28. FINAL PRODUCTION PRINCIPLE
The Cash Desk Dashboard is NOT only a payment list.
It is:
centralized payment monitoring system
denomination verification engine
split-payment control system
payment traceability platform
shift-end reconciliation support system
This dashboard controls the complete payment visibility and denomination verification flow of the gaming café ecosystem.

FOOD ORDERS DASHBOARD — FINAL PRODUCTION SOP
Gaming Café Management System

1. PURPOSE OF FOOD ORDERS DASHBOARD
The Food Orders Dashboard is the:
LIVE FOOD ORDER MANAGEMENT SYSTEM
This dashboard is responsible for:
managing customer food orders
tracking live food requests
monitoring order preparation & delivery
tracking food billing
handling food payment methods
synchronizing food charges with billing system
maintaining food order history
This dashboard acts as the:
CENTRAL FOOD OPERATIONS PANEL

2. WHO CAN ACCESS THIS DASHBOARD
Role
Access
Super Admin
Full Access
Operator
Operational Access
User
NO ACCESS


3. PRIMARY OBJECTIVE
The Food Orders Dashboard exists to:
manage live food ordering
reduce delivery confusion
synchronize food billing
track payment methods
maintain order traceability
improve café operations

4. WHAT FOOD ORDERS DASHBOARD TRACKS
Item
Included
Food Orders
YES
Order Status
YES
PC Assignment
YES
Payment Methods
YES
Split Payments
YES
Food Billing
YES
Delivery Status
YES
Operator Handling
YES
Date & Time Logs
YES


5. DASHBOARD STRUCTURE
The Food Orders Dashboard contains:
Section
Purpose
Live Orders Feed
Real-time order visibility
Order Queue
Pending food orders
Delivery Monitor
Delivery tracking
Payment Monitor
Food payment tracking
Completed Orders
Historical food records
Alerts & Exceptions
Food issue tracking


6. LIVE FOOD ORDER FLOW

STEP 1 — CUSTOMER PLACES ORDER
Customer places:
food order
drink order
snack order
from:
gaming PC
operator counter
member account

STEP 2 — ORDER REFLECTS IN DASHBOARD
Food order instantly appears inside:
LIVE FOOD ORDER FEED

STEP 3 — OPERATOR ACCEPTS ORDER
Operator:
views order
confirms order
prepares/delivers order

STEP 4 — BILLING SYNCHRONIZATION
Food amount automatically synchronizes with:
Billing Counter
Sessions Dashboard
Cash Desk
EOD Reports

IMPORTANT PRINCIPLE
Food billing should automatically attach to:
CURRENT SESSION BILL
if session exists.

7. LIVE ORDER FEED
Displays:
newly placed orders
active food requests
pending deliveries

EACH ORDER MUST DISPLAY
Field
Example
Order ID
FOOD-102
PC Number
PC-05
Customer Name
Rahul
Ordered Items
Burger + Cold Coffee
Total Amount
₹280
Payment Type
Split
Operator
Meet
Order Time
7:10 PM
Order Status
Preparing


8. ORDER STATUS SYSTEM
Each order should maintain:
Status
Meaning
Pending
Order received
Preparing
Operator processing order
Ready
Order prepared
Delivered
Delivered to customer
Completed
Billing completed
Cancelled
Order cancelled


9. DELIVERY MONITOR SYSTEM
Tracks:
undelivered orders
delayed orders
completed deliveries

PURPOSE
Helps:
avoid missed deliveries
reduce customer complaints
improve operational speed

10. FOOD PAYMENT SYSTEM
Customer may choose:
Payment Type
Supported
Cash
YES
Online / UPI
YES
Split Payment
YES
Wallet Payment
YES


11. SPLIT PAYMENT SUPPORT
This is a CRITICAL production feature.

EXAMPLE
Food Bill:
₹300
Customer Pays:
₹100 cash
₹200 online
System stores:
Type
Amount
Cash
₹100
Online
₹200


IMPORTANT
Split payments MUST remain properly separated.

12. FOOD BILLING SYNCHRONIZATION
Food orders MUST synchronize automatically with:
Dashboard
Sync
Billing Counter
YES
Sessions Dashboard
YES
Cash Desk
YES
Members Dashboard
YES
EOD Dashboard
YES


IMPORTANT RULE
Gaming billing and food billing MUST remain separated.

EXAMPLE
Category
Amount
Gaming Bill
₹400
Food Bill
₹200


WHY IMPORTANT?
Because:
analytics differ
taxation may differ
reporting differs
loyalty systems differ

13. FOOD ORDER HISTORY
Tracks:
completed food orders
cancelled orders
payment history
delivery history

EACH HISTORY ENTRY MUST DISPLAY
Field
Example
Order ID
FOOD-102
Customer
Rahul
Items
Burger + Coffee
Total Amount
₹280
Payment Type
Split
Order Status
Completed
Date
12/07/2026
Time
7:45 PM


14. DATE & TIME TRACKING
Every food activity MUST contain:
exact date
exact timestamp

EVENTS TO TRACK
Event
Logged
Order Created
YES
Order Accepted
YES
Order Delivered
YES
Payment Completed
YES
Order Cancelled
YES


15. FOOD ORDER ALERT SYSTEM
Tracks:
delayed orders
undelivered food
pending payments
cancelled orders

EXAMPLES
Alert
Example
Long Pending Order
YES
Delivery Delay
YES
Payment Pending
YES
Cancelled Order
YES


16. FOOD ORDER TIMER SYSTEM
Recommended production feature.
Tracks:
order waiting time
delivery speed
preparation duration

EXAMPLE
⚠ ORDER DELAY ALERT
Order:
FOOD-102
Waiting Time:
22 Minutes

17. FOOD ORDER SEARCH SYSTEM
Users should search by:
Search Type
Supported
Order ID
YES
PC Number
YES
Customer Name
YES
Operator Name
YES
Payment Type
YES


18. DATE FILTER & CALENDAR SYSTEM
Allows:
operators
Super Admin
to:
access old food orders
filter by specific dates
verify historical orders
using:
CALENDAR PICKER

19. MEMBER FOOD BILL SYNCHRONIZATION
If member account linked:
Food orders should:
reflect in member history
reflect in loyalty system
reflect in wallet deductions

20. FOOD CANCELLATION RULES
Operators MAY cancel order ONLY if:
preparation not started
payment not finalized

IMPORTANT
Cancelled orders MUST remain logged.
Never fully delete order history.

21. LIVE SYNCHRONIZATION
Food Orders Dashboard synchronizes LIVE with:
Dashboard
Sync
Billing Counter
YES
Sessions Dashboard
YES
Cash Desk
YES
Members Dashboard
YES
EOD Dashboard
YES


22. REAL-TIME UPDATE REQUIREMENT
Food dashboard must:
update automatically
synchronize instantly
avoid manual refresh

RECOMMENDED TECHNOLOGY
Recommended:
WebSockets
Socket.IO
Event-driven synchronization

23. SECURITY RULES
Food Orders Dashboard MUST enforce:
immutable food history
payment traceability
secure billing synchronization
audit-safe food logs
role-based permissions

24. OPERATOR LIMITATIONS
Operators CANNOT:
delete completed food orders
modify finalized payments
alter historical records
manipulate food payment logs

25. SUPER ADMIN ACCESS
Super Admin CAN:
audit food orders
inspect food payments
review operator handling
inspect delivery delays
monitor food revenue

26. RECOMMENDED EXTRA FEATURES
Recommended production features:
Feature
Recommended
Kitchen Display Mode
YES
Sound Notification
YES
Food Preparation Timer
YES
Food Revenue Graphs
YES
Popular Food Analytics
YES
Daily Food Summary
YES


27. FINAL PRODUCTION PRINCIPLE
The Food Orders Dashboard is NOT only a food list.
It is:
live food management system
synchronized billing engine
payment traceability platform
delivery monitoring system
operational food control dashboard
This dashboard controls the complete food ordering and billing workflow of the gaming café ecosystem.

MENU EDITOR DASHBOARD — FINAL PRODUCTION SOP
Gaming Café Management System

1. PURPOSE OF MENU EDITOR DASHBOARD
The Menu Editor Dashboard is the:
FOOD MENU & INVENTORY MANAGEMENT SYSTEM
This dashboard is responsible for:
managing food menu items
managing food pricing
tracking inventory stock
monitoring stock refill activity
tracking inventory usage
detecting low-stock items
maintaining food availability status
This dashboard acts as the:
CENTRAL FOOD INVENTORY CONTROL SYSTEM

2. WHO CAN ACCESS THIS DASHBOARD
Role
Access
Super Admin
Full Access
Operator
Operational Access
User
NO ACCESS


3. PRIMARY OBJECTIVE
The Menu Editor exists to:
maintain food inventory
prevent out-of-stock situations
simplify food management
synchronize food availability
manage pricing updates
track refill activity
maintain operational continuity

4. WHAT MENU EDITOR TRACKS
Item
Included
Menu Items
YES
Food Prices
YES
Current Stock
YES
Stock Refill Logs
YES
Low Stock Alerts
YES
Food Availability
YES
Inventory Activity
YES
Operator Actions
YES
Date & Time Logs
YES


5. DASHBOARD STRUCTURE
The Menu Editor Dashboard contains:
Section
Purpose
Menu Management
Food item control
Inventory Monitor
Live stock tracking
Stock Refill System
Inventory refill management
Low Stock Alerts
Out-of-stock prevention
Availability Control
Food visibility control
Inventory History
Historical inventory logs
Alerts & Exceptions
Inventory issue tracking


6. MENU MANAGEMENT SYSTEM
Operators can:
add menu items
edit food names
update food prices
manage item availability

EACH MENU ITEM MUST CONTAIN
Field
Example
Item Name
Cold Coffee
Category
Beverage
Price
₹120
Current Stock
18
Status
Available


7. FOOD CATEGORY SYSTEM
Recommended categories:
Category
Example
Beverages
Coffee, Cold Drinks
Snacks
Fries, Chips
Meals
Burger, Pizza
Desserts
Brownie


8. PRICE MANAGEMENT SYSTEM
Operators may:
update menu pricing
change promotional pricing
correct item pricing

IMPORTANT
Every price modification MUST be logged.

PRICE CHANGE LOG MUST STORE
Field
Example
Item Name
Cold Coffee
Old Price
₹100
New Price
₹120
Modified By
Rahul
Date
12/07/2026
Time
5:20 PM


9. INVENTORY STOCK SYSTEM
This is a CRITICAL production feature.
Tracks:
current stock quantity
live stock usage
stock refill activity
stock depletion

EACH INVENTORY ITEM MUST DISPLAY
Field
Example
Item Name
Cold Coffee
Current Stock
18
Minimum Stock Limit
5
Last Refilled By
Rahul
Last Refill Date
12/07/2026
Last Refill Time
4:10 PM


10. LIVE STOCK REDUCTION SYSTEM
When customer places food order:
System MUST automatically:
reduce inventory stock
synchronize across dashboards
update stock count live

EXAMPLE
Current Stock:
Cold Coffee → 18
Customer Orders:
2 Cold Coffee
System Updates:
Cold Coffee → 16

11. STOCK REFILL SYSTEM
Operators can:
refill inventory
update stock quantity
restock food items

REFILL FLOW

STEP 1 — OPERATOR SELECTS ITEM
Example:
Cold Coffee

STEP 2 — ENTER REFILL QUANTITY
Example:
+20 stock

STEP 3 — SYSTEM UPDATES STOCK
Old Stock:
18
New Stock:
38

12. STOCK REFILL LOGGING
Every refill MUST store:
Field
Example
Item Name
Cold Coffee
Added Quantity
20
Final Stock
38
Refilled By
Rahul
Date
12/07/2026
Time
4:10 PM


13. LOW STOCK ALERT SYSTEM
This is a VERY IMPORTANT production feature.

PURPOSE
System should automatically detect:
low inventory
near out-of-stock items
before stock finishes.

EXAMPLE
⚠ LOW STOCK ALERT
Cold Coffee stock is low.
Remaining:
4 items

14. MINIMUM STOCK LIMIT SYSTEM
Each item should maintain:
Field
Example
Minimum Limit
5


IF STOCK GOES BELOW LIMIT
System should:
generate alert
notify operator
notify Super Admin

15. OUT OF STOCK SYSTEM
If stock becomes:
0

then item status becomes:
OUT OF STOCK

IMPORTANT
Out-of-stock items should:
disappear from customer ordering panel
OR
display as unavailable

16. FOOD AVAILABILITY CONTROL
Operators may:
temporarily disable items
hide unavailable food
reactivate items later

EXAMPLE
Status
Meaning
Available
Order allowed
Out of Stock
Ordering blocked
Disabled
Temporarily hidden


17. INVENTORY HISTORY SYSTEM
Tracks:
stock refill history
stock usage history
inventory changes
availability changes

EACH HISTORY ENTRY MUST DISPLAY
Field
Example
Item Name
Fries
Action
Stock Refilled
Quantity
+15
Operator
Rahul
Date
12/07/2026
Time
6:20 PM


18. DATE & TIME TRACKING
Every inventory action MUST contain:
exact date
exact timestamp

EVENTS TO TRACK
Event
Logged
Item Added
YES
Price Changed
YES
Stock Refilled
YES
Stock Reduced
YES
Item Disabled
YES
Out Of Stock
YES


19. FOOD ORDER SYNCHRONIZATION
Menu Editor synchronizes LIVE with:
Dashboard
Sync
Food Orders Dashboard
YES
Billing Counter
YES
Sessions Dashboard
YES
EOD Dashboard
YES


IMPORTANT
Food stock should update:
IN REAL-TIME

20. INVENTORY SEARCH SYSTEM
Operators should search by:
Search Type
Supported
Item Name
YES
Category
YES
Availability Status
YES


21. DATE FILTER & CALENDAR SYSTEM
Allows:
operators
Super Admin
to:
inspect old refill history
inspect stock changes
verify inventory actions
using:
CALENDAR PICKER

22. ALERTS & EXCEPTION SECTION
Tracks inventory issues.

EXAMPLES
Alert
Example
Low Stock
YES
Out Of Stock
YES
Negative Inventory
YES
Refill Missing
YES
Price Modification
YES


23. INVENTORY ANALYTICS (RECOMMENDED)
Highly recommended production feature.

TRACKS
Metric
Example
Most Sold Item
Cold Coffee
Least Sold Item
Brownie
Fastest Consumed Stock
Fries
Total Inventory Usage
YES


24. WASTAGE TRACKING (HIGHLY RECOMMENDED)
Very important production feature.

PURPOSE
Tracks:
expired items
damaged stock
wasted inventory

EXAMPLE
Field
Example
Item Name
Cold Coffee
Wastage Quantity
2
Reason
Expired
Operator
Rahul


WHY IMPORTANT?
Without wastage tracking:
inventory becomes inaccurate
stock mismatch occurs
food cost analysis becomes incorrect

25. SECURITY RULES
Menu Editor Dashboard MUST enforce:
secure inventory logs
immutable stock history
audit-safe price changes
synchronized inventory tracking
role-based access

26. OPERATOR LIMITATIONS
Operators CANNOT:
delete inventory history
alter old stock logs
manipulate finalized food orders
modify historical inventory records

27. SUPER ADMIN ACCESS
Super Admin CAN:
audit inventory activity
inspect refill history
review wastage logs
inspect pricing history
monitor food operations

28. REAL-TIME UPDATE REQUIREMENT
Menu Editor must:
update automatically
synchronize instantly
avoid manual refresh

RECOMMENDED TECHNOLOGY
Recommended:
WebSockets
Socket.IO
Event-driven synchronization

29. RECOMMENDED EXTRA FEATURES
Recommended production features:
Feature
Recommended
Item Images
YES
Barcode Support
YES
Stock Export
YES
Inventory Reports
YES
Consumption Analytics
YES
Auto Low Stock Notification
YES


30. FINAL PRODUCTION PRINCIPLE
The Menu Editor Dashboard is NOT only a food menu editor.
It is:
inventory management system
food stock control engine
refill tracking platform
food availability management system
operational inventory monitoring dashboard
This dashboard controls the complete food inventory ecosystem of the gaming café.

SETTINGS DASHBOARD — FINAL REFINED PRODUCTION SOP
Gaming Café Management System

1. PURPOSE OF SETTINGS DASHBOARD
The Settings Dashboard is the:
CENTRAL ADMINISTRATIVE CONTROL PANEL
This dashboard is responsible for:
managing system configuration
controlling operator access
managing permissions
controlling dashboard visibility
managing branch operations
maintaining security controls
governing the entire gaming café ecosystem
This dashboard acts as the:
MASTER ADMIN PANEL OF THE ENTIRE SYSTEM

2. WHO CAN ACCESS THIS DASHBOARD
Role
Access
Super Admin
Full Access
Operator
NO ACCESS
User
NO ACCESS


IMPORTANT PRINCIPLE
SETTINGS DASHBOARD IS SUPER ADMIN ONLY
Operators should NEVER access:
administrative controls
permission management
security configuration
operator management
access control systems

3. PRIMARY OBJECTIVE
The Settings Dashboard exists to:
control the complete system
manage operator permissions
protect sensitive dashboards
maintain operational security
govern user access
maintain centralized authority

4. WHAT SETTINGS DASHBOARD CONTROLS
Item
Included
Operator Management
YES
Dashboard Access Control
YES
Permission Management
YES
Branch Configuration
YES
Security Settings
YES
System Configuration
YES
Session Policies
YES
Audit Logs
YES
Date & Time Logs
YES


5. DASHBOARD STRUCTURE
The Settings Dashboard contains:
Section
Purpose
Operator Management
Add/remove operators
Dashboard Permission Control
Dashboard access management
Access Monitoring
Live operator tracking
Branch Settings
Branch configuration
Security Settings
System protection
System Configuration
Operational settings
Audit Logs
Administrative activity tracking


6. OPERATOR MANAGEMENT SYSTEM
This is one of the MOST IMPORTANT sections.

PURPOSE
Allows Super Admin to:
create operators
suspend operators
disable operators
remove operators
control dashboard permissions

7. OPERATOR CREATION FLOW

STEP 1 — SUPER ADMIN OPENS OPERATOR MANAGEMENT
Admin accesses:
Operator Management Section

STEP 2 — ADD OPERATOR
Super Admin enters:
Field
Example
Full Name
Rahul
Mobile Number
9876543210
Username
rahul01
Password
********
Assigned Branch
Adajan


STEP 3 — ASSIGN DASHBOARD ACCESS
Super Admin selects:
which dashboard operator can access.

EXAMPLE
Dashboard
Access
Billing Counter
YES
Sessions Dashboard
YES
Food Orders
YES
Cash Register
YES
Members Dashboard
YES
Cash Desk
YES
EOD Dashboard
NO
Settings Dashboard
NO
PC Status Dashboard
NO


IMPORTANT PRINCIPLE
SUPER ADMIN CAN GIVE OR REMOVE ACCESS
FOR INDIVIDUAL DASHBOARD SECTIONS

8. DASHBOARD ACCESS CONTROL SYSTEM
This is a CRITICAL production feature.

PURPOSE
Allows Super Admin to:
enable dashboard access
disable dashboard access
restrict sensitive sections
customize operator permissions

EXAMPLE
Operator Rahul may access:
Billing Counter
Sessions Dashboard
Food Orders
BUT cannot access:
PC Status
Settings Dashboard
EOD Dashboard

9. LIVE ACCESS MODIFICATION
Super Admin can:
MODIFY ACCESS IN REAL-TIME
without requiring:
system restart
logout
manual refresh

EXAMPLE
If Super Admin disables:
Cash Register access
Then operator immediately loses:
dashboard visibility
navigation access
operational control

SYSTEM ACTIONS
System automatically:
removes dashboard access
closes restricted dashboard
blocks future access attempts
synchronizes permissions live

10. OPERATOR REMOVAL SYSTEM
Super Admin can:
deactivate operator
suspend operator
permanently remove operator

IMPORTANT
Removed operators should:
instantly lose system access
be forcefully logged out
lose dashboard visibility

11. LIVE ACCESS REVOCATION SYSTEM
This is another CRITICAL feature.

PURPOSE
Super Admin can directly:
TERMINATE OPERATOR ACCESS LIVE

EXAMPLE
If operator:
misuses system
behaves suspiciously
violates policies
Super Admin can:
instantly revoke access
terminate active session
block future login

SYSTEM ACTIONS
When access revoked:
System:
force logs out operator
closes active sessions
blocks dashboard access
stores audit logs

12. OPERATOR STATUS TYPES
Status
Meaning
Active
Access allowed
Suspended
Temporarily blocked
Disabled
Login blocked
Logged Out
No active session


13. ROLE-BASED ACCESS CONTROL (RBAC)
This is a VERY IMPORTANT production feature.

PURPOSE
Ensures:
operators only access allowed dashboards
sensitive data remains protected
operational security maintained

14. ACTIVE OPERATOR MONITORING
Tracks:
logged-in operators
branch assignments
current dashboard access
active operational sessions

EACH OPERATOR ENTRY MUST DISPLAY
Field
Example
Operator Name
Rahul
Branch
Adajan
Login Time
10:00 AM
Current Dashboard
Billing Counter
Status
Active


15. SESSION CONTROL SYSTEM
Super Admin may:
terminate operator session
force logout operator
block simultaneous login
revoke dashboard access

IMPORTANT
Operators should NOT:
share credentials
login from multiple systems
bypass access restrictions

16. BRANCH CONFIGURATION SYSTEM
Allows Super Admin to manage:
branch names
branch operational timings
branch activation status

EXAMPLE
Setting
Example
Branch Name
Adajan
Opening Time
10:00 AM
Closing Time
2:00 AM
Status
Active


17. SYSTEM CONFIGURATION SETTINGS
Super Admin can configure:
Setting
Purpose
Session Pricing
Gaming pricing
Reservation Rules
Reservation control
Loyalty Rules
Reward configuration
Wallet Rules
Recharge configuration
Tax Settings
Billing calculations


18. SECURITY SETTINGS
Controls:
password rules
session timeout
login protection
authentication policies

RECOMMENDED SECURITY FEATURES
Feature
Recommended
Auto Logout
YES
Failed Login Lock
YES
Password Reset Control
YES
Device Session Tracking
YES


19. AUDIT LOG SYSTEM
This is one of the MOST IMPORTANT sections.

PURPOSE
Tracks every critical admin action.

EVERY ADMIN ACTION MUST STORE
Field
Example
Admin Name
Harshal
Action
Access Revoked
Target Operator
Rahul
Dashboard
Cash Register
Date
12/07/2026
Time
8:40 PM


20. DATE & TIME TRACKING
Every administrative activity MUST contain:
exact date
exact timestamp

EVENTS TO TRACK
Event
Logged
Operator Created
YES
Operator Removed
YES
Dashboard Access Granted
YES
Dashboard Access Revoked
YES
Password Reset
YES
Session Terminated
YES


21. ALERTS & EXCEPTION SYSTEM
Tracks:
suspicious activity
failed logins
unauthorized access attempts
repeated access denial

EXAMPLES
Alert
Example
Unauthorized Dashboard Access
YES
Multiple Login Attempt
YES
Access Revoked
YES
Suspicious Activity
YES


22. LIVE SYNCHRONIZATION
Settings Dashboard synchronizes LIVE with:
Dashboard
Sync
Billing Counter
YES
Sessions Dashboard
YES
Members Dashboard
YES
Cash Register
YES
EOD Dashboard
YES


23. REAL-TIME UPDATE REQUIREMENT
Settings Dashboard must:
update instantly
synchronize live
enforce access changes immediately

RECOMMENDED TECHNOLOGY
Recommended:
WebSockets
Socket.IO
JWT Authentication
Role-Based Access Control (RBAC)

24. BACKUP & RECOVERY SYSTEM (HIGHLY RECOMMENDED)
Very important production feature.

SHOULD SUPPORT
Feature
Recommended
Automatic Backups
YES
Database Export
YES
Restore System
YES
Recovery Logs
YES


25. SYSTEM MAINTENANCE MODE (RECOMMENDED)
Super Admin may enable:
MAINTENANCE MODE
during:
updates
maintenance
emergency fixes

EFFECT
Operators cannot:
access operational dashboards
process billing
modify records
until maintenance ends.

26. SECURITY RULES
Settings Dashboard MUST enforce:
Super Admin-only access
secure authentication
immutable audit logs
strict permission control
live access enforcement

27. OPERATOR LIMITATIONS
Operators CANNOT:
access Settings Dashboard
modify permissions
create operators
remove operators
access audit logs
bypass dashboard restrictions

28. SUPER ADMIN RESPONSIBILITIES
Super Admin controls:
system governance
operator lifecycle
dashboard permissions
security enforcement
centralized authority

29. RECOMMENDED EXTRA FEATURES
Recommended production features:
Feature
Recommended
Dark Mode
YES
Theme Settings
YES
System Notifications
YES
Database Backup Export
YES
Login Device Tracking
YES
Permission Templates
YES


30. FINAL PRODUCTION PRINCIPLE
The Settings Dashboard is NOT only a settings page.
It is:
centralized administrative control center
dashboard permission management system
operator governance engine
security enforcement platform
operational authority system
This dashboard controls the complete administrative backbone and permission architecture of the gaming café ecosystem.


# GAMING CAFÉ MANAGEMENT SYSTEM

# MASTER PRODUCTION SOP (STANDARD OPERATING PROCEDURE)

## Enterprise Gaming Café ERP & Operations Platform

---

# DOCUMENT PURPOSE

This document defines the:

* complete operational workflow
* dashboard logic
* role hierarchy
* billing architecture
* reservation engine
* payment system
* operator workflow
* inventory system
* security controls
* synchronization logic
* production standards

for the Gaming Café Management System.

This SOP acts as the:

# MASTER REFERENCE DOCUMENT

for:

* development
* operations
* UI/UX
* backend architecture
* database design
* deployment
* future scaling

---

# 1. SYSTEM OVERVIEW

The Gaming Café Management System is a:

# CENTRALIZED MULTI-BRANCH REAL-TIME OPERATIONS PLATFORM

The system is designed to manage:

* gaming sessions
* billing operations
* reservations
* food ordering
* inventory management
* cash handling
* operator workflows
* member management
* live PC tracking
* real-time synchronization

across one or multiple gaming café branches.

---

# 2. SYSTEM OBJECTIVES

The primary objectives are:

| Objective                   | Purpose                             |
| --------------------------- | ----------------------------------- |
| Fast Operations             | Reduce operator delay               |
| Accurate Billing            | Prevent payment mistakes            |
| Real-Time Synchronization   | Maintain live dashboard consistency |
| Reservation Protection      | Prevent booking conflicts           |
| Cash Accountability         | Prevent drawer mismatch             |
| Operator Governance         | Maintain operational control        |
| Audit Tracking              | Ensure traceability                 |
| Branch Isolation            | Secure multi-branch operations      |
| Lightweight User Experience | Maintain gaming performance         |

---

# 3. SYSTEM ARCHITECTURE

```text
                    CENTRAL SERVER
            (Main Database + APIs + Sync)

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

---

# 4. CORE SYSTEM COMPONENTS

| Component               | Purpose                  |
| ----------------------- | ------------------------ |
| Central Database        | Stores all system data   |
| Real-Time Socket Engine | Synchronization          |
| Billing Engine          | Revenue handling         |
| Session Engine          | Gaming session lifecycle |
| Reservation Engine      | Future bookings          |
| Inventory Engine        | Food stock tracking      |
| Wallet Engine           | Member balance system    |
| Audit Engine            | Activity tracking        |
| Permission Engine       | Access control           |

---

# 5. ROLE HIERARCHY

The system contains 3 main access layers.

---

## 5.1 SUPER ADMIN

Highest authority level.

### Access Includes:

* all branches
* all dashboards
* all reports
* all operators
* all settings
* all reservations
* audit logs
* permissions

### Responsibilities:

* system governance
* operator management
* dashboard permissions
* revenue oversight
* security enforcement

---

## 5.2 OPERATOR

Branch-level operational role.

### Access Includes:

* billing counter
* sessions
* reservations
* food orders
* cash register
* members
* cash desk

### Restrictions:

* no settings access
* no audit logs
* no cross-branch visibility
* no admin permissions

---

## 5.3 USER PANEL

Lightweight gaming-side interface.

### Features:

* remaining time
* food ordering
* call operator
* session extension request
* current bill viewing

### Restrictions:

* cannot close sessions
* cannot access billing
* cannot access admin systems
* cannot access other PCs

---

# 6. LOGIN SYSTEM

---

# 6.1 MASTER ENTRY SCREEN

```text
[ Super Admin Login ]
[ Operator Login ]
[ User Panel ]
```

---

# 6.2 SUPER ADMIN LOGIN FLOW

### Workflow:

1. Enter email/password
2. Backend validates:

   * credentials
   * permissions
   * device
   * account status
3. Redirect to Admin Dashboard

---

# 6.3 OPERATOR LOGIN FLOW

### Workflow:

1. Select branch
2. Select operator profile
3. Enter PIN/password
4. System starts shift
5. Redirect to Operator Dashboard

---

# 6.4 BRANCH ISOLATION

Operators can ONLY access:

# THEIR ASSIGNED BRANCH

Cross-branch visibility is blocked.

---

# 7. SESSION ENGINE

The Session Engine controls:

* gaming timers
* session billing
* PC states
* user panel lifecycle
* billing synchronization

---

# 7.1 SESSION STATES

| State            | Meaning                        |
| ---------------- | ------------------------------ |
| Idle             | Available                      |
| Active           | Session running                |
| Reserved         | Future booking                 |
| Awaiting Billing | Session ended, payment pending |
| Offline          | PC unavailable                 |

---

# 7.2 SESSION START FLOW

Operator:

* selects PC
* enters customer details
* selects duration/package

System:

* creates session
* starts timer
* starts billing
* activates user panel
* updates all dashboards

---

# 7.3 SESSION END FLOW

When session stops:

* timer freezes
* amount locks
* PC enters Awaiting Billing
* billing becomes editable
* food ordering stops

---

# 8. RESERVATION SYSTEM

Reservations are:

# CENTRALIZED SYSTEM STATES

---

# 8.1 RESERVATION FLOW

1. Operator creates reservation
2. System marks PC as Reserved
3. Reservation reflects everywhere

---

# 8.2 RESERVATION REFLECTION

| Dashboard          | Reflection |
| ------------------ | ---------- |
| Billing Counter    | YES        |
| Sessions Dashboard | YES        |
| PC Status          | YES        |
| Reservations Panel | YES        |

---

# 8.3 RESERVED PC PROTECTION

Clicking reserved PC shows:

```text
⚠ RESERVED PC
Reserved for Rahul
Starts in 10 minutes

[ Cancel ]
[ Start Reserved Session ]
```

---

# 8.4 RESERVATION EXPIRY

If customer never arrives:

* grace period applies
* reservation expires automatically
* PC becomes available

---

# 9. BILLING COUNTER DASHBOARD

This is the:

# PRIMARY OPERATIONAL DASHBOARD

---

# 9.1 BILLING COUNTER RESPONSIBILITIES

* session management
* live billing
* food billing
* split payment handling
* change return calculation
* reservation visibility
* transaction synchronization

---

# 9.2 BILLING COUNTER MODULES

| Module             | Purpose                |
| ------------------ | ---------------------- |
| PC Selector        | Station control        |
| Session Strip      | Live session data      |
| Current Bill       | Billing summary        |
| Payment Section    | Payment processing     |
| Food Ordering      | Add food items         |
| Reservation Alerts | Reservation protection |

---

# 9.3 PAYMENT METHODS

Supported:

* cash
* online/UPI
* split payment
* wallet payment

---

# 9.4 SPLIT PAYMENT LOGIC

Example:

| Method | Amount |
| ------ | ------ |
| Cash   | ₹100   |
| Online | ₹400   |

Validation:

# Cash + Online MUST equal total bill

---

# 9.5 CHANGE RETURN SYSTEM

Example:

| Bill           | ₹280 |
| -------------- | ---- |
| Customer Gives | ₹500 |
| Return Change  | ₹220 |

System automatically calculates:

* change
* shortages
* exact payment

---

# 9.6 DISCOUNT SYSTEM

Discounts are:

# SUPER ADMIN ONLY

Operators cannot:

* apply discounts
* view coupon engine

---

# 10. CASH REGISTER DASHBOARD

This dashboard manages:

# PHYSICAL CASH DRAWER CONTROL

---

# 10.1 CASH REGISTER RESPONSIBILITIES

* opening balance
* cash tracking
* drawer calculation
* shift verification
* mismatch detection

---

# 10.2 OPENING BALANCE FLOW

Operator enters:

* starting drawer cash

System stores:

* operator
* branch
* timestamp
* amount

---

# 10.3 EXPECTED DRAWER CASH FORMULA

```text
Opening Balance
+ Cash Sales
+ Split Payment Cash Portion
```

---

# 10.4 SHIFT CLOSING VERIFICATION

Operator counts:

* physical drawer cash

System compares:

* expected cash
* actual counted cash

Mismatch requires:

# REASON ENTRY

---

# 11. CASH DESK DASHBOARD

The Cash Desk is:

# PAYMENT FLOW MONITORING SYSTEM

Tracks:

* payment methods
* split payments
* denomination verification
* wallet usage
* payment traceability

---

# 11.1 DENOMINATION COUNTER

At shift end:
Operator enters:

* ₹500 notes
* ₹200 notes
* ₹100 notes
  etc.

System verifies:

* counted total
* expected total

---

# 12. FOOD ORDERS DASHBOARD

Handles:

* live food orders
* preparation workflow
* delivery tracking
* food payment synchronization

---

# 12.1 FOOD ORDER FLOW

1. Customer places order
2. Order appears in dashboard
3. Operator accepts order
4. Food delivered
5. Billing synchronized

---

# 12.2 FOOD BILLING RULE

Gaming billing and food billing:

# MUST REMAIN SEPARATED

---

# 13. MENU EDITOR DASHBOARD

Acts as:

# FOOD INVENTORY MANAGEMENT SYSTEM

---

# 13.1 FEATURES

* menu item control
* stock tracking
* refill logging
* low stock alerts
* availability management

---

# 13.2 LOW STOCK ALERTS

If stock falls below minimum:

* operator alerted
* Super Admin alerted

---

# 14. MEMBERS DASHBOARD

Handles:

* member registration
* wallet balances
* loyalty points
* gaming & food history

---

# 14.1 WALLET SYSTEM

Operators can:

* add wallet balance
* remove balance
* recharge accounts

Every wallet action MUST store:

* payment type
* operator
* date/time
* amount

---

# 14.2 GAMING & FOOD SEPARATION

Member history stores:

* gaming spending
* food spending

separately.

---

# 15. DASHBOARD PANEL

Central operational overview.

Displays:

* revenue summary
* active sessions
* cash summary
* recent transactions
* reservation statistics

---

# 15.1 CALENDAR FILTER SYSTEM

Operators and Admin can:

* select previous dates
* inspect old transactions
* access historical reports

---

# 16. SESSIONS DASHBOARD

Displays:

* all active sessions
* reservations
* awaiting billing PCs
* live timers

---

# 16.1 SESSION ALERTS

Includes:

* reservation popups
* billing pending alerts
* session expiry warnings

---

# 17. PC STATUS DASHBOARD

Super Admin-only dashboard.

Displays:

* all PC states
* live usage
* reservation mapping
* live revenue state

---

# 18. END OF DAY DASHBOARD

Handles:

* final revenue reports
* cash verification
* operator shift closure
* payment analytics

---

# 18.1 EOD REPORT CONTENTS

| Item                | Included |
| ------------------- | -------- |
| Gaming Revenue      | YES      |
| Food Revenue        | YES      |
| Cash Revenue        | YES      |
| Online Revenue      | YES      |
| Split Payments      | YES      |
| Wallet Transactions | YES      |
| Operator Logs       | YES      |
| Timestamp Logs      | YES      |

---

# 19. SETTINGS DASHBOARD

This is:

# SUPER ADMIN CONTROL CENTER

---

# 19.1 SETTINGS RESPONSIBILITIES

* operator creation/removal
* dashboard permissions
* security rules
* branch settings
* access revocation

---

# 19.2 DASHBOARD PERMISSION CONTROL

Super Admin can:

* grant dashboard access
* revoke dashboard access
* control feature visibility

per operator.

---

# 20. REAL-TIME SYNCHRONIZATION

Entire system operates using:

# CENTRALIZED LIVE STATE ENGINE

---

# 20.1 LIVE SYNCHRONIZATION TARGETS

| Dashboard       | Sync |
| --------------- | ---- |
| Billing Counter | YES  |
| Cash Register   | YES  |
| Sessions        | YES  |
| Reservations    | YES  |
| Food Orders     | YES  |
| EOD             | YES  |

---

# 20.2 RECOMMENDED TECHNOLOGY

* WebSockets
* Socket.IO
* Event-driven architecture

---

# 21. SECURITY MODEL

---

# 21.1 REQUIRED SECURITY FEATURES

| Security Feature | Required |
| ---------------- | -------- |
| Password Hashing | YES      |
| JWT Tokens       | YES      |
| Role Validation  | YES      |
| Audit Logs       | YES      |
| API Validation   | YES      |
| Device Tracking  | YES      |
| Branch Isolation | YES      |
| Forced Logout    | YES      |

---

# 21.2 CORE SECURITY PRINCIPLE

# NEVER TRUST FRONTEND

All permissions MUST be validated in backend.

---

# 22. AUDIT LOGGING SYSTEM

Every critical action MUST store:

| Field  | Example          |
| ------ | ---------------- |
| User   | Rahul            |
| Action | Discount Applied |
| Branch | Adajan           |
| Date   | 12/07/2026       |
| Time   | 7:40 PM          |

---

# 23. DATABASE ARCHITECTURE

Recommended core tables:

* users
* operators
* branches
* sessions
* reservations
* bills
* bill_items
* food_orders
* inventory
* members
* wallet_transactions
* audit_logs
* pc_states
* cash_register
* shifts

---

# 24. API ARCHITECTURE

Recommended architecture:

```text
Frontend
   ↓
REST APIs
   ↓
Business Logic Layer
   ↓
Database
```

Recommended:

* JWT authentication
* role middleware
* branch middleware
* audit middleware

---

# 25. FRONTEND ARCHITECTURE

Recommended:

* Electron.js desktop app
* React frontend
* Socket.IO live sync

---

# 26. USER PANEL PERFORMANCE RULES

User panel MUST:

* consume low RAM
* consume low CPU
* remain lightweight
* avoid heavy animations

because:

# GAMING PERFORMANCE IS PRIORITY

---

# 27. OFFLINE & BACKUP STRATEGY

Highly recommended:

* local caching
* automatic sync recovery
* daily backups
* offline transaction queue

---

# 28. PRODUCTION RULES

---

# NEVER ALLOW

* direct database edits
* deleted transaction history
* hidden cash modifications
* reservation bypass without logs
* dashboard permission bypass

---

# ALWAYS REQUIRE

* audit logging
* timestamp tracking
* role validation
* backend verification
* synchronization consistency

---

# 29. FUTURE SCALABILITY FEATURES

Recommended future modules:

| Feature                    | Priority  |
| -------------------------- | --------- |
| GST Engine                 | Future    |
| Tournament System          | Future    |
| Steam Launcher Integration | Advanced  |
| Hardware Control Layer     | Advanced  |
| Mobile App                 | Future    |
| LAN Discovery              | Advanced  |
| Auto Backup Cloud Sync     | Important |

---

# 30. FINAL SYSTEM PRINCIPLE

This system is NOT:

* simple billing software
* simple timer software
* basic cyber café app

This system is:

* enterprise gaming café ERP
* real-time operations engine
* multi-branch management platform
* centralized session ecosystem
* audit-safe financial system
* operational governance platform

This SOP defines the complete production architecture and operational standards of the Gaming Café Management System.
