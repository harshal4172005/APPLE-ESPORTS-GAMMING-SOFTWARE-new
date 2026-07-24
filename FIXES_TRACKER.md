# Fixes & Errors Tracker

How to use this file:
- Every time you find something broken or want something changed, add a new numbered entry under "New Issues" using the template below.
- Keep each entry SHORT and in your own words — you don't need technical language, just describe what you saw and what you expected.
- When you give me this file, I'll go through the entries **one at a time**, ask you questions if anything is unclear, fix it, tell you in plain English what was wrong and what I changed, then move to the next one.
- Once an issue is fixed and confirmed by you, I'll move it down to "Fixed" so this file stays a clean history.

---

## Template (copy this for each new issue)

```
### Issue #__
- Where: (e.g. "Adajan branch, PC 2, Operator dashboard")
- What I did: (steps you took, e.g. "clicked Start > Pay As You Go")
- What happened: (the bug/error you saw)
- What should happen instead:
- Priority: (Urgent / Normal / Whenever)
```

---

## New Issues (not fixed yet)

```
### Issue # 9
- Where: memeber cannot approve the payment there is one error that is "axious is not defined" this is happening on the members pc means members cannot approve it this is the problem !!
- What I did: i have done members billing and selected pc - 2 that is right now members pc and when i do the billing of that pc and then when i go to billing counter and then do the billing of that pc - 2 and i select wallet and then i send the approve link and that request is comming on pc - 2 and that error pops up and billing is not hapening !!
- What happened: members billing is not happening fix this and also one more thing right now i can see "choose user type" and this is pc - 2 when operator send the request it directly goes to pc - 2 that is fine but unfortunatelly i am not logged in as an member then also i am getting the past member payment approval request !!
- What should happen instead: fix all the billing approval and fix this small login related thing check it first understand and then tell me what are you going to do realted this login realted thing 
- Priority: based on you !!!
```

---

```
### Issue # 10
- Where: in the members database and in the members list section !!
- What I did: i have tested 2 things here when i write my past email and phone number that member account i have removed from the members list then also the system is not letting me to add that member in the list !!
- What happened: the system should allow me to add that member in the list because i have deleted that account earlier !!
- What should happen instead:email and phone number should be same but name should not be same, and also add split option in member top up !!
- Priority: based on you !!
```

---

```
### Issue # 11
- Where: members !
- What I did: i will directly tell the issue you need to fix it !!
- What happened: when member is created then minimum top up value should be 500 rs or above not below than that is allowed and one more thing is that we are giving 10% bonus on gamming top up simple as that so listen understand this how this will work assume new member is created of 500 rs then we will give that member 50 rs bonus amount this bonus amount can only be used in gamming only and this offer is only for gamming and food is different there is no offer for the food and one more thing like all the thing is written in a perfect manner like see i have top up 500 rs then i will get 50 rs bonus for the gamming so we need to mention bonus amount seperately and total is calculated is like 550 rs you got the plan or not ! and another thing here is like in my account i have 10 rs left in my wallet ballacne then only i can play for that 10 rs worth of gamming simple and then automatically billing is done and session is closed and then the warning message comes up like that you need to top - up then only you can play further simple like that and then i will go to the operator and then do top up simple as that !! and super admin want to give more bonus in terms of percentage or in terms of amount then super admin can do from there login and then go to that particular member and then super admin can give extra bonus simple as that !!! 
- What should happen instead: under stand from the "what happened" part so if any doubt arrise then clear that first and then make the whole thing !!
- Priority: based on you !
```

---

## Fixed (history log)

### Issue #1 — Maintenance mode throwing errors (2026-07-18)
- Root cause 1: role name mismatch — token stored role as lowercase (`operator`) but 3 endpoints (incl. the Maintenance endpoint) checked for capitalized `Operator`, so the server silently rejected valid operators. Fixed to use the shared role constants everywhere.
- Root cause 2: "Restore PC" set the PC to an `Offline` state that the UI had no screen for (blank dead card). Fixed to restore straight to Idle/Free, and added a Restore button to the Offline card too as a safety net.
- Confirmed working by user across branches.

### Issue #2 — Stop Session / PC card / billing counter bugs, routing/token issues (2026-07-18/19)
- Same token/role bug as Issue #1 affected several endpoints, not just Maintenance — fixed there covered part of this too.
- Pay-As-You-Go / Postpaid sessions couldn't start at all: frontend sent `durationMinutes: null` for 0-duration plans, which crashed the server's JSON parser (400 error) on every branch. Fixed to send `0`.
- Billing Counter showed a fake hardcoded "Rate Mode: STD ₹40" tile — removed.
- Billing Counter's Active Sessions list could show a stale, non-ticking elapsed time after repeated testing — added a 15s→10s auto-refresh safety net so it can't go stale.
- `sessions.GamingType` (package name) was capped at 50 characters in the database; appending the buffer-cancellation message could push realistic package names over that limit, silently failing the entire Stop transaction (safely rolled back, but the PC stayed stuck "Occupied"). Widened the column and added a permanent length guard.
- Removed the redundant "Bill" button from the operator PC card (was just a shortcut to the same Billing Counter page reachable another way) and hid "Extend" on Pay-As-You-Go sessions (nothing to extend — they already bill continuously for real time).
- Confirmed working across Adajan, Katargam, and Citylight.

### Issue #4 — Pricing not linked across session / PC card / billing counter / member overlay (2026-07-18/19)
- Built one shared calculation (`SessionPricingCalculator`) used identically everywhere: final billing on Stop, the live PC-card feed, the Billing Counter's bill panel, and the member overlay — so the same session always shows the same number on every screen.
- Found and fixed the actual root cause of "billing counter shows a different amount than the PC card": the Billing Counter's bill panel was reading the stale bill row from the database (only updated at Stop), while the PC card computed a fresh live number — now the bill panel also computes live while a session is active.
- Found and fixed a real money bug: food ordered mid-session was silently erased from the final bill when the session was stopped (a leftover `session.FoodAmount`, always 0, was overwriting the real `bill.FoodAmount`). Also fixed the same overwrite wiping out any discount already applied to a bill.
- Removed every hardcoded ₹100/hr fallback rate. A PC without a Pricing Profile now hard-blocks session start and PC creation with a clear error, instead of silently guessing a price.
- Removed the dead/duplicate "Default Base Rate", "Tax Percentage", and "Hardware Tier Pricing (Hz)" fields from Settings → System Configuration — pricing lives only in Branch-Wise Pricing Profiles now.
- Pricing Profile edits (rate or buffer) now push live to every open screen (operator dashboard, Billing Counter) instantly via a real-time signal, with a 10s polling safety net everywhere (PC dashboard, Billing Counter, member overlay) in case a signal is ever missed.
- Proven live on 3 different branches (Adajan, Katargam, Citylight) — not branch-specific.

### Issue #6 — No 10-minute free buffer, fixed sessions charged full price regardless of actual time (2026-07-18/19)
- The 10-minute free buffer is now a real, per-branch configurable field (`BufferMinutes`) on each Branch-Wise Pricing Profile, editable by Super Admin, applied live everywhere immediately (proven: changed a live profile's rate/buffer mid-test and a brand-new session immediately used the new numbers).
- ALL sessions — fixed packages and Pay-As-You-Go alike — are now billed for exact real elapsed time (after the free buffer), never a flat package price. Proven: a "1 Hour ₹60" package stopped at 11 real minutes charged ₹11, not ₹60; a 25-minute session on a "10 Min Trial" package charged ₹25 for the overrun, not capped at ₹10.
- A session that ends within the free buffer now charges ₹0 AND auto-releases the PC straight to Idle — operators no longer have to manually process a ₹0 "payment" to free the PC.
- Confirmed the buffer boundary is exact (10 min → free, 11 min → charged) via direct testing.

### Issue #3 — "Time remaining" voice alert repeating (2026-07-19)
- Root cause: the alert fired on an exact match (`remainingTime === 600` etc). My earlier 10s background refresh (added for Issue #4) periodically re-syncs `remainingTime` from the server — if that resync nudges the value back up even slightly, the local countdown crosses the same threshold a second time and re-fires the alert.
- Fixed: each of the 10-min / 5-min / 1-min alerts now tracks whether it has already fired for the current session (reset only when a new session starts) and can never fire twice, regardless of how many times the value gets resynced.

### Issue #5 — Long decimal values in money displays (2026-07-19)
- Money is still calculated and stored precisely (down to the paisa) for accounting — only the on-screen display was long/messy (e.g. ₹16.666666, or an inconsistent ₹215.1).
- Added one shared rounding helper and applied it everywhere real money gets shown: the operator PC card's live charge, the Admin PC Status page, the member/user panel's live charge and wallet balances, and the Billing Counter (active bills list, bill details panel, payment screen — gaming/food/discount/total/change).
- Every one of those now shows a clean whole-rupee figure (₹17, not ₹16.67 or ₹16.666666667).

### Issue #7 — Food order approval doesn't redirect to Food Orders page (2026-07-19)
- The "New Food Order" popup that appears on the operator panel (wherever they currently are) only dismissed itself on "Acknowledge" — it now also takes the operator straight to the Food Orders page so they can act on it immediately.
- Also fixed: for Super Admin, clicking Acknowledge could land on an empty "Select a Branch" screen instead of the order if no branch was actively selected — it now switches to the order's branch automatically.

### Issue #8 — Member session not auto-ending when wallet balance runs out (2026-07-19)
- Root cause: the code that watches a member's live charge and auto-stops the session once it reaches their wallet balance only ran while the member was looking at the overlay's Home screen. Navigate to Food/Extend/Call/Bill (or minimize the overlay) and it stopped watching entirely — the session then kept running unpaid past their balance.
- Proven live: a real member with a ₹10 wallet balance had a test session sit at ₹20 in live charges (2x over) with nothing stopping it.
- Fixed: moved this check to the part of the app that's always running for the whole overlay session, regardless of which screen is open — it now reliably auto-ends and settles the session the instant the live charge reaches the member's balance, no matter what the customer is looking at.

### Billing Counter — members restricted to Wallet-only payment (2026-07-19)
- When a bill belongs to a member, the payment method grid now shows only "Wallet" (Cash/UPI/Split/Credit are hidden) and defaults straight to it — walk-in bills are unaffected and keep all payment options.