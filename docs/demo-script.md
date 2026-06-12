# Defense Demo Script (~10 minutes)

A scripted walkthrough of the live demo for the thesis defense. Every route, button label,
and expected behavior below is verified against the code. Steps 0–1 happen **before** the
defense starts; steps 2–10 are the live demo.

**Demo users:** `admin` (full access) and `manager.demo` (BranchManager of *Main Street
Boutique*). Passwords are defined in
`src/patisserie_shop.DbMigrator/IdentityDataSeedContributor.cs` (and ABP's standard
identity seed for `admin`).

---

## Step 0 — Pre-demo setup (do this ~30 minutes before)

1. **Lower the scanner intervals** in `src/patisserie_shop.Blazor/appsettings.json` so the
   background intelligence is visibly alive during the demo. The repo defaults already use
   5 minutes for three scanners; for the demo also lower the velocity scanner:

   ```json
   "BackgroundJobs": {
     "DeadStockScanIntervalMinutes": 5,
     "TransferSuggestionScanIntervalMinutes": 5,
     "VelocityScanIntervalMinutes": 5,
     "DecisionOutcomeScanIntervalMinutes": 360,
     "ExpiryScanIntervalMinutes": 5
   }
   ```

   Note: ABP periodic workers fire their **first tick one full period after startup** —
   with 5-minute intervals, the first scanner decisions and the Product Velocity data
   appear ~5 minutes after the app starts. Start the app early.

2. **Reset the database** for a clean, deterministic state:

   ```powershell
   # optional clean slate — drops everything
   psql -U postgres -c "DROP DATABASE IF EXISTS patisserie;"
   dotnet run --project src/patisserie_shop.DbMigrator
   ```

3. **Start the app** and leave it running:

   ```powershell
   dotnet run --project src/patisserie_shop.Blazor
   ```

4. **Open two browser windows** side by side, both logged in as `admin`, at
   <https://localhost:44387>. Window A is the "operator", window B just sits on any page
   showing the top-bar **notification bell**.

5. **Pick the demo product** while waiting: open `/inventory/branch-inventory`, choose
   branch *Main Street Boutique*, and find a product with on-hand around 6–10 units and a
   default supplier (e.g. one of the viennoiserie items — VN-xxx SKUs). Use the row's
   **Adjust Stock** action to set it to exactly **6** if needed. Write down a second such
   product for the autopilot step.

---

## Step 1 — What the migrator seeded (talk over the dashboards, ~30 s)

Running `patisserie_shop.DbMigrator` applied all migrations and seeded:

- **23 products** in 6 categories (SKUs like `VN-001 Classic Butter Croissant`,
  `CT-001 Classic Strawberry Tart`), suppliers with **lead times**, and **3 branches**:
  *Central Kitchen & Warehouse*, *Main Street Boutique*, *Riverside Café*.
- **~90 days of deterministic historical sales** (`HIST-…` invoice numbers, weekend
  uplift, slight upward trend) at the two retail branches — this is what powers velocity,
  ABC, and the sales charts.
- **Expiry-dated stock batches** for perishable products (the oldest seeded batch expires
  in 1–2 days — deliberately inside the expiry rule's window).
- **A 7-rule starter rule bank**: Global Low Stock (5), Critical Low Stock (2, priority 10),
  Global Excess Stock (100), Global Dead Stock (30 days), Global Transfer Suggestion (10),
  Days of Cover Risk (4 days), Expiring Soon (3 days).
- Roles `admin` and `BranchManager`, users `admin` and `manager.demo`.

---

## Step 2 — Dashboards tour (1 min)

1. Window A, as `admin`, go to `/` — the **Admin dashboard**: KPI cards (Total Products,
   Pending Decisions, Low Stock, Today's Sales, PO Pipeline), 7-day sales area chart,
   top-5 sellers, recent decisions, branch performance cards.
2. Briefly open `/inventory/dashboard` — stock-health donut, movement breakdown, critical
   stock table.
3. Talking point: every number here is derived from the immutable `AppStockMovement` and
   `AppDecisionLog` ledgers — nothing is hand-maintained.

> If charts are empty: the DbMigrator sales-history seeder skips itself when `HIST-`
> invoices already exist — verify the migrator console printed the seed log lines.

## Step 3 — A sale breaches a LowStock rule, live (2 min)

The seeded rule: **"Global Low Stock Alert"** — LowStock, threshold **5**, global scope.

1. Window B: park it anywhere (e.g. `/`) and point at the **bell** in the top bar.
2. Window A: go to `/operations/sales`, click **New Sale**, branch *Main Street Boutique*,
   add the demo product with **quantity 2** (6 on hand → 4 after), save.
3. Narrate the pipeline while it happens (it is synchronous, in-process):
   sale → `BranchInventoryManager.AdjustStockAsync` → `StockChangedEto` →
   `DecisionMakerService` → 4 < 5 → `AppDecisionLog` → `DecisionMadeEto` → bell.
4. Window B: the bell badge increments and a toast appears ("New … decision …") —
   without any refresh.

> If no decision appears: check `/intelligence/decision-log` for an **already-Pending
> LowStockAlert for that product+branch** — the engine deduplicates standing problems
> (dismiss the old one and sell again), and confirm the rule is still Active on
> `/intelligence/inventory-rules`.

## Step 4 — Execute → draft PO with the ROP math (1.5 min)

1. Go to `/intelligence/decision-log`. Show the new **Pending** `LowStockAlert` card: the
   rule that fired, the **reasoning sentence**, stock at evaluation, and the three buttons
   **Acknowledge / Dismiss / Executed**.
2. Click **Executed**. Snackbar: *"Draft purchase order PO-… created"*. The card flips to
   status **Executed** and now shows a **link to the created purchase order**.
3. Follow the link to `/operations/purchase-orders`, open the PO detail. Read the
   **Notes** aloud — the system wrote its own justification, e.g.:
   *"Auto-created from decision …. ROP: 4.2/day × (3d lead + 7d cover) + 9 safety = 51
   target − 4 on hand → order 47."*
4. Talking point: quantity = reorder-point formula using the **supplier's lead time** and
   the **30-day sales velocity**; with no velocity it falls back to a min/max refill — and
   it says so in the note.

> If Execute fails with a business error: the product has **no default supplier**
> (`DecisionProductHasNoDefaultSupplier`) — pick a product that has one, or set it on
> `/inventory/products`.

## Step 5 — Autopilot: flip the rule to CreateDraft (1.5 min)

1. Go to `/intelligence/inventory-rules`, edit **"Global Low Stock Alert"**, set
   **Action Mode** to **CreateDraft**, save. Point at the action-mode chip changing.
2. Window A: record another sale that takes the **second** demo product below 5 at
   *Main Street Boutique*.
3. Go to `/intelligence/decision-log`: the new decision arrives **already Executed**, with
   the draft-PO link attached — no human click. `DecisionAutopilotHandler` reacted to the
   same `DecisionMadeEto` the bell uses.
4. Talking point: three-position autopilot dial per rule — `SuggestOnly` (default),
   `CreateDraft`, `AutoSubmit` (also submits the PO for approval). The human approval gate
   on the PO itself always remains.
5. Flip the rule back to **SuggestOnly**.

> If the decision stays Pending: confirm the *rule that actually fired* is the one you
> edited — at quantity ≤ 2 the higher-priority **Critical Low Stock Alert** (still
> SuggestOnly) wins instead; check the rule name on the decision card.

## Step 6 — Product Velocity: ABC and days of cover (1 min)

1. Go to `/intelligence/product-velocity` (Intelligence menu).
2. Show per product × branch: **ABC class chip** (A = top 80% of 30-day revenue,
   B = next 15%, C = tail), **avg daily sales 7d / 30d**, units & revenue (30 d), current
   stock, and **days of cover** = stock ÷ 30-day daily average.
3. Talking point: this read model (`AppProductVelocity`) is recomputed by the velocity
   scanner and is exactly what the ROP formula and the DaysOfCover rule consume —
   the same numbers the manager sees are the numbers the engine uses.

> If the page is empty: the velocity scanner hasn't ticked yet (first tick = one full
> interval after startup) — confirm `BackgroundJobs:VelocityScanIntervalMinutes` is 5 and
> the app has been up that long.

## Step 7 — Stock batches and the ExpiryAlert (1 min)

1. Go to `/inventory/stock-batches`. Default sort is soonest-expiring first; show the
   **expiry countdown chips** (red "Expired Nd ago" / "expires today", amber "Nd left").
2. The seeder planted a batch expiring in 1–2 days; the **"Expiring Soon (3 days)"** rule
   plus the 5-minute expiry scanner means `/intelligence/decision-log` already shows
   **ExpiryAlert** decisions referencing those batches.
3. Talking point: FEFO — consumption drains the earliest-expiring batch first, receipts of
   perishables auto-create a batch from the product's shelf life; the batch ledger is
   best-effort by design and never blocks a sale.

> If no ExpiryAlert exists yet: the expiry scanner also waits one full interval after
> startup — show the batch grid and the rule instead, then return after a few minutes.

## Step 8 — Rule effectiveness and tuning hints (1 min)

1. Back on `/intelligence/inventory-rules`, point at the **Effectiveness** column:
   a compact stats line per rule — *"N alerts · X% exec · Y% dismissed"*.
2. Explain the 48-hour feedback loop: `DecisionOutcomeScannerService` re-checks every
   decision ~48 h after creation and records **Resolved / Unresolved / StockedOut**;
   those outcomes feed the deterministic tuning hints:
   - dismissed alerts followed by stockouts → "alerts were warranted" warning;
   - ≥ 70% dismissed over ≥ 10 decisions → "noisy — consider relaxing the threshold";
   - ≥ 80% of executed decisions resolved → "effective" badge.
3. Honest caveat to state up front: on a freshly reset database the outcome counters are
   empty (the 48 h window hasn't passed), so hints appear on a database that has lived for
   a couple of days — the stats line itself (counts, exec/dismiss %) is live immediately.

## Step 9 — Sales Analytics (45 s)

1. Go to `/operations/sales-analytics`. Flip the range selector (7 / 14 / 30 / 60 days) and
   show the revenue trend with its weekend rhythm and growth — the 90-day seeded history
   makes the charts meaningful.
2. Mention `manager.demo`: logging in as the branch manager shows the same system scoped
   to one branch (branch-scoped sales, a branch dashboard with its own alerts), driven by
   permissions, not hidden buttons.

## Step 10 — Closing talking points (1 min)

- **Explainable by design** — no AI/ML. Every decision names its rule, carries a
  human-readable reasoning string and a stock snapshot, and every auto-created document
  contains the formula that produced its quantity. The committee can re-derive any number
  by hand.
- **Human-in-the-loop, with a dial** — decisions default to suggestions; autopilot is
  opt-in *per rule* and never bypasses the PO/transfer approval workflow.
- **Self-evaluating** — the system grades its own past decisions 48 h later and turns the
  results into threshold-tuning evidence, closing the loop: *detect → decide → act →
  measure → tune*.
- **Auditable** — immutable stock-movement and decision ledgers; nothing in the history
  can be edited, only appended.

## Expected timing

| Step | Time |
|---|---|
| 1–2. Seed recap + dashboards | 1.5 min |
| 3. Live LowStock breach + bell | 2 min |
| 4. Execute → draft PO with ROP notes | 1.5 min |
| 5. Autopilot CreateDraft | 1.5 min |
| 6. Product Velocity / ABC | 1 min |
| 7. Stock batches + ExpiryAlert | 1 min |
| 8. Effectiveness + tuning hints | 1 min |
| 9. Sales Analytics | 0.75 min |
| 10. Closing | 1 min |
| **Total** | **~11 min** (cut step 9 if tight) |
