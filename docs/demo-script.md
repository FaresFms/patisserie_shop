# Defense Demo Script (~9 min core, ~18 min full)

A scripted walkthrough of the live demo for the thesis defense. Every route, button label,
and expected behavior below is verified against the code (`@page` directives +
`Menus/patisserie_shopMenuContributor.cs`). Steps 0–1 happen **before** the defense starts;
steps 2–10 are the live demo. Steps 7b–7g are the Phase-5/Production standout features — include them
when time allows (see the timing table at the end).

**Demo users:** `admin` (full access) and `manager.demo` (BranchManager of *Main Street
Boutique*), plus `kitchen.demo` for the Main Kitchen production path. Passwords are defined in
`src/patisserie_shop.DbMigrator/IdentityDataSeedContributor.cs` (and ABP's standard
identity seed for `admin`).

---

## Step 0 — Pre-demo setup (do this ~30 minutes before)

1. **Lower the scanner intervals** in `src/patisserie_shop.Blazor/appsettings.json` so the
   background intelligence is visibly alive during the demo. The repo already ships
   demo-friendly **2-minute** values for all four periodic scanners (the outcome scanner
   stays at 6 h — its 48 h window can't be rushed):

   ```json
   "BackgroundJobs": {
     "DeadStockScanIntervalMinutes": 2,
     "TransferSuggestionScanIntervalMinutes": 2,
     "VelocityScanIntervalMinutes": 2,
     "DecisionOutcomeScanIntervalMinutes": 360,
     "ExpiryScanIntervalMinutes": 2
   }
   ```

   The **velocity scanner** recomputes sales velocity, ABC class, the **weekday demand
   indices** *and* runs the StockoutRisk forecast sweep; the **expiry scanner** raises both
   `ExpiryAlert` (expiring-soon) and `WasteWriteOff` (already-expired) decisions. Several
   standout features below are driven by these scans — Product Velocity "Next 7 days", the
   reorder calendar's predicted stockouts, and the expiry→write-off→waste flow — so the
   prerequisite is the same for all of them: **keep `BackgroundJobs:*IntervalMinutes` low
   and wait for the first scan**.

   Note: ABP periodic workers fire their **first tick one full period after startup** —
   with 2-minute intervals, the first scanner decisions and the Product Velocity / reorder
   calendar data appear ~2 minutes after the app starts. Start the app early; set any
   interval to `0` to fall back to the worker's built-in (1440 / 360) default.

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
  in 1–2 days — deliberately inside the expiry rule's window). Each **retail** branch also
  gets one batch slice that is **already 2 days past expiry** with quantity remaining, so
  the `ExpiredStock` rule has waste to flag out of the box (for Step 8b below).
- **An 8-rule starter rule bank covering all 7 rule types**: Global Low Stock (5),
  Critical Low Stock (2, priority 10), Global Excess Stock (100), Global Dead Stock
  (30 days), Global Transfer Suggestion (10), Days of Cover Risk (4 days), Expiring Soon
  (3 days), and Expired Stock Write-Off (no threshold — "expired" is absolute). Five come
  from `IntelligenceDataSeedContributor`; the last three (DaysOfCover / ExpiringSoon /
  ExpiredStock) from `PatisserieDataSeedContributor.SeedDemoRulesAsync`.
- Roles `admin` and `BranchManager`, users `admin` and `manager.demo`.
- **Main Kitchen production seed**: user `kitchen.demo`, branch `Main Kitchen`, Arabic raw
  materials/packaging, Arabic default formulas, approved branch production requests, ready
  cook orders, and one deliberate shortage scenario tagged `[DEMO-PRODUCTION-AR-SHORTAGE]`:
  *مقهى ضفة النهر* requests **340 بان أو شوكولا**, and the Cook screen shows a clear
  **زبدة فرنسية غير مملحة** shortage while the other ingredients are available.

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

## Step 6 — Product Velocity: ABC, weekday forecast & days of cover (1.5 min)

1. Go to `/intelligence/product-velocity` (Intelligence menu).
2. Show per product × branch: **ABC class chip** (A = top 80% of 30-day revenue,
   B = next 15%, C = tail), **avg daily sales 7d / 30d**, units & revenue (30 d), current
   stock, a **"Next 7 days" forecast** column, and **days of cover**.
3. Hover the **"Next 7 days"** value to reveal the per-day tooltip (e.g. *"Fri 4.1 ·
   Sat 5.8 · Sun 2.3 · …"*). This is the weekday-indexed forecast: the scanner computes a
   demand multiplier per weekday (busy Saturday > quiet Monday) and the engine **walks the
   next 7 days weekday-by-weekday** rather than assuming a flat average. Products with no
   weekday pattern fall back to a flat 30-day average (and say so in their reasoning).
4. The **days of cover** is the day the same forecast walk depletes current stock (flat
   `stock ÷ avgDaily30` only when there's no weekday pattern).
5. Talking point: this read model (`AppProductVelocity`) is recomputed by the velocity
   scanner and is exactly what the ROP formula, the DaysOfCover rule, and the reorder
   calendar consume — the numbers the manager sees are the numbers the engine uses.

> If the page is empty: the velocity scanner hasn't ticked yet (first tick = one full
> interval after startup) — confirm `BackgroundJobs:VelocityScanIntervalMinutes` is 2 and
> the app has been up that long.

## Step 7 — Stock batches and the ExpiryAlert (1 min)

1. Go to `/inventory/stock-batches`. Default sort is soonest-expiring first; show the
   **expiry countdown chips** (red "Expired Nd ago" / "expires today", amber "Nd left").
2. The seeder planted a batch expiring in 1–2 days; the **"Expiring Soon (3 days)"** rule
   plus the 2-minute expiry scanner means `/intelligence/decision-log` already shows
   **ExpiryAlert** decisions referencing those batches.
3. Talking point: FEFO — consumption drains the earliest-expiring batch first, receipts of
   perishables auto-create a batch from the product's shelf life; the batch ledger is
   best-effort by design and never blocks a sale.

> If no ExpiryAlert exists yet: the expiry scanner also waits one full interval after
> startup — show the batch grid and the rule instead, then return after a few minutes.

## Step 7b — Waste: ExpiredStock → WasteWriteOff → write-off → Waste Analytics (1.5 min)

1. The same expiry scanner runs a **second pass**: any live batch already **past** its
   expiry date raises a **`WasteWriteOff`** decision (the seeder gave each retail branch a
   2-days-expired slice, so one is waiting on `/intelligence/decision-log` after the first
   scan). The card quantifies the expired units, the oldest batch, and the **estimated
   waste cost** (`expired qty × cost price`).
2. Show that `WasteWriteOff` has **no autopilot** — even if you set the "Expired Stock
   Write-Off" rule to CreateDraft on `/intelligence/inventory-rules`, the decision stays
   Pending. Destroying stock is always a human action.
3. Click **Executed** on the WasteWriteOff card. The snackbar reports a `WriteOff −N`
   adjustment (no draft document — `ExecutedActionType` is `StockAdjustment`). Behind it:
   `BranchInventoryManager.AdjustStockAsync` records an immutable `WriteOff`
   `AppStockMovement` and the FEFO hook drains the **expired batches first**.
4. Go to `/inventory/waste-analytics`: the total waste cost, units written off,
   waste-to-sales ratio, weekly trend, and per-branch / top-wasted-product breakdowns now
   reflect that write-off — all derived from `WriteOff` movements.

> If no WasteWriteOff appears: confirm the **"Expired Stock Write-Off"** rule is Active and
> the expiry scanner has ticked once; on a freshly reset DB the expired slice exists, but
> the scanner still needs its first interval. If Execute reports nothing written off, the
> expired stock was already sold/transferred since the scan — pick another flagged product.

## Step 7c — Reorder calendar (1 min)

1. Go to `/intelligence/reorder-calendar` (Intelligence menu). It's a forward month grid
   merging three deterministic forward signals:
   - **predicted stockouts** (red) — from the *same* weekday-indexed forecast walk as
     Product Velocity, placed on the day stock is forecast to hit zero;
   - **expiring batches** (amber) — live batches on their expiry day;
   - **expected deliveries** (green) — open POs on their `ExpectedDeliveryDate`.
2. Talking point: it's a read-model, not a stored plan — every marker is recomputed from
   velocity rows, the batch ledger, and open POs, so it always matches the live data the
   rest of the system acts on. Use the month/branch controls to pan.

> If the stockout layer is empty: it needs the velocity scan (Step 0 prerequisite — low
> `VelocityScanIntervalMinutes`, app up at least one interval). Delivery markers only show
> for POs that have an `ExpectedDeliveryDate` inside the visible month.

## Step 7d — Supplier scorecards & measured lead time (1 min)

1. Go to `/inventory/suppliers`. Each supplier row carries a **scorecard**: on-time rate,
   fill rate, average delay, a **measured lead time** (computed from received-PO history),
   and a **grade A–D** (graded by the *worse* of on-time / fill: A needs ≥95% on-time and
   ≥98% fill; N/A when there's no delivery history yet).
2. Talking point — the learning loop on procurement: once a supplier has **≥ 3 received
   orders with delivery dates**, the *measured* lead time **overrides the configured
   `LeadTimeDays` in the reorder-point formula**. Re-open an auto-created PO's notes
   (Step 4) — when measured data exists the note reads *"… (measured 4.2d lead from
   6 orders)"* instead of *"(configured 3d lead)"*. The system observes how long the
   supplier actually takes and tightens its own reorder timing — no ML, just measured
   history beating a static config.

> If every grade is N/A: there are no `Received`/`PartialReceived` POs with delivery dates
> in the window yet (a freshly reset DB seeds none) — receive a PO with an actual delivery
> date first, or note that the column populates as real receiving history accumulates.

## Step 7e — Branch health league table (45 s)

1. Back on `/` (admin dashboard), scroll to the **Branch Health league table**. Each
   branch carries a **0–100 score** and **grade A–D**, ranked best-first.
2. Expand the breakdown bars: the score is a deterministic sum of six weighted components —
   **StockHealth (30), StockoutSeverity (20), PendingLoad (15), WasteRatio (15),
   ExpiryRisk (10), Responsiveness (10)** — each with a plain-English detail string
   (e.g. *"2 item(s) out of stock (−8)"*, *"3% waste-to-sales over 30 days"*).
3. Talking point: same explainability rule as everywhere else — the composite is pure
   arithmetic over data the dashboards already show, so any branch's grade can be
   re-derived by hand.

## Step 7f — PO consolidation (1 min)

1. On `/intelligence/decision-log`, with several **pending reorder decisions**
   (LowStockAlert / ReorderSuggestion / StockoutRisk) present, click **Consolidate
   reorders** in the toolbar; confirm in the dialog.
2. The snackbar reports how many **draft POs** were created and how many decisions were
   folded in. Open `/operations/purchase-orders`: instead of one PO per alert, there's
   **one draft PO per (supplier × branch)**, with duplicate products merged to a single
   line at the max computed quantity, and every contributing decision flipped to
   **Executed** and linked to its PO.
3. Talking point: real purchasing batches lines onto one order per supplier; line
   quantities use the *same* reorder-point math as a one-off Execute, so the numbers match,
   and the results are still **drafts a human approves**. Decisions whose product has no
   default supplier are skipped and counted, never fatal to the run.

> If "nothing to consolidate": there are no pending reorder-type decisions in the current
> branch filter — trigger a couple of LowStock breaches first (Step 3), and make sure their
> products have a default supplier.

## Step 7g — الإنتاج في المطبخ الرئيسي: نقص زبدة → شراء → طبخ → صرف (3 min)

هذا الجزء استخدمه مع المستخدم `kitchen.demo` حتى تكون الصفحة عربية ومفهومة من منظور مدير
المطبخ.

1. سجّل الدخول باسم `kitchen.demo` وافتح `/production/dashboard`. اعرض البطاقات السريعة:
   أوامر تنتظر خامات، جاهز للطبخ، إنتاج اليوم، والهدر. الفكرة: هذه ليست صفحة تسويق؛ هي
   قائمة تشغيل يومية للمطبخ.
2. افتح `/production/cook` وابحث عن الطلب التجريبي الذي يحتوي في الملاحظات على
   `[DEMO-PRODUCTION-AR-SHORTAGE]` أو المنتج **بان أو شوكولا**. افتح الأمر. جدول
   **توفر الخامات** يجب أن يوضح أن **زبدة فرنسية غير مملحة** ناقصة، بينما الدقيق
   والشوكولا والبيض والخميرة والملح متوفرة.
3. اضغط **إنشاء طلب شراء خامات**. تظهر رسالة عربية تقول إن النظام أنشأ مسودة طلب شراء
   بإجمالي النقص. هذا الطلب لا يعتمد نفسه؛ يبقى مسودة حتى يراجعه الإنسان.
4. افتح `/operations/purchase-orders`. ابحث عن طلب الشراء الجديد، وافتح التفاصيل. يجب أن
   ترى بند الزبدة بكمية النقص تقريبًا **1261 غرام** وملاحظة عربية تربطه بأمر الطبخ.
   اضغط بالترتيب: **إرسال** ثم **اعتماد** ثم **استلام**، واستلم الكمية كاملة.
5. ارجع إلى `/production/cook`، افتح نفس أمر الطبخ، واضغط **تحديث توفر الخامات**. الحالة
   تتحول إلى **جاهز للطبخ**. اضغط **بدء الطبخ**؛ هنا تُسحب الخامات من مخزون المطبخ
   بحركة `ProductionConsumption`.
6. اضغط **إكمال الطبخ** وأدخل:
   `الناتج الفعلي = 340`، `الكمية المقبولة = 340`، `الكمية المرفوضة = 0`.
   عند الحفظ يدخل المنتج النهائي إلى مخزون المطبخ بحركة `ProductionOutput` مع تاريخ
   صلاحية دفعة.
7. افتح `/production/dispatch`. اختر الأمر المكتمل، ثم اختر فرع **مقهى ضفة النهر**،
   واصرف **340**. زر الصرف ينشئ التحويل ويشحنه ويكمله مباشرة، لذلك يرتفع مخزون الفرع
   وتُغلق كمية طلب الفرع من نفس العملية.
8. افتح `/production/analytics` أو `/production/dashboard` لإغلاق القصة: الكمية المنتجة،
   نسبة التلبية، ومتوسط التكلفة تظهر من نفس السجلات، ولا يوجد أي حساب احتمالي أو AI/ML.

نقطة الدفاع: كل رقم هنا قابل للإعادة يدويًا. كمية الزبدة = `ceil(340 / 40 × 2200 × 1.03)`
أي حوالي 19261 غرام، والمتوفر في البذرة 18000 غرام، لذلك يظهر نقص 1261 غرام. نفس الرقم
هو الذي يتحول إلى بند طلب الشراء.

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
- **Learns from its own history (still deterministically)** — a weekday-indexed demand
  forecast walks each weekday rather than a flat average; supplier scorecards feed the
  *measured* lead time back into the reorder-point math; a 0–100 branch-health score ranks
  branches; and the reorder calendar projects stockouts, expiries, and deliveries forward.
  All of it is plain arithmetic over the operational history — no model, no training.
- **Auditable** — immutable stock-movement and decision ledgers (including `WriteOff`
  waste adjustments); nothing in the history can be edited, only appended.

## Expected timing

A full run touches every Phase-5 feature; for a tight slot, the **core** path is steps
1–6 + 10 (~9 min), and steps 7b–7f are the standout add-ons to fit as time allows.

| Step | Time |
|---|---|
| 1–2. Seed recap + dashboards | 1.5 min |
| 3. Live LowStock breach + bell | 2 min |
| 4. Execute → draft PO with ROP notes | 1.5 min |
| 5. Autopilot CreateDraft | 1.5 min |
| 6. Product Velocity / ABC / weekday forecast | 1.5 min |
| 7. Stock batches + ExpiryAlert | 1 min |
| 7b. Expiry → WasteWriteOff → write-off → Waste Analytics | 1.5 min |
| 7c. Reorder calendar | 1 min |
| 7d. Supplier scorecards + measured lead time | 1 min |
| 7e. Branch health league table | 0.75 min |
| 7f. PO consolidation | 1 min |
| 7g. Main Kitchen production shortage path | 3 min |
| 8. Effectiveness + tuning hints | 1 min |
| 9. Sales Analytics | 0.75 min |
| 10. Closing | 1 min |
| **Total** | **~21 min full** · **~9 min core** (steps 1–6 + 10) |
