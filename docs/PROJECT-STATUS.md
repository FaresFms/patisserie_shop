# Project Status — Smart Multi-Branch Inventory Management System

> **Purpose of this file:** a single source of truth describing what this application *is* and *currently does*, so any planning conversation starts with accurate context. Update it as the project evolves. Last updated: 2026-06-26 (Production Module Wave 7 complete: Arabic seed/demo path + docs).

---

## 1. Identity

- **Name:** Smart Multi-Branch Inventory Management System
- **Type:** Graduation project — a **Rule-Based Inventory Decision Support System** for a multi-branch patisserie (bakery) business.
- **Thesis claim (defend this):** the system is **100% deterministic and explainable — no AI/ML**. Every recommendation can be reproduced and justified with plain arithmetic and if/else rules. This is a deliberate strength, not a limitation.
- **Solution:** `patisserie_shop`

## 2. Current maturity level — honest assessment

**Well past MVP; a feature-rich, demo-ready decision-support platform with a real test suite, CI, and thesis docs.**

It has crossed from "an inventory CRUD app with alerts" into a **self-learning, forward-planning operations system**: it predicts stockouts from weekday-indexed demand, plans consolidated purchasing around *measured* supplier behaviour, manages the full perishable lifecycle including waste, grades its own branches and rules, and runs a touch-screen POS — all deterministic.

What it is **not**: not multi-tenant (deliberately off), not deployed to production, not load-tested at scale (a few in-memory cross-branch aggregations would need windowing for very large catalogues), no payment-gateway/accounting integration.

## 3. Tech stack

| Layer | Choice |
|---|---|
| Framework | ABP Framework v10.1.x |
| Runtime | .NET 10, C# |
| UI | Blazor **Server** (single-node), MudBlazor wrapped as "SoftComponents", **Blazor-ApexCharts 6.1.0** |
| Persistence | PostgreSQL + EF Core 10 |
| Mapping | Mapperly |
| Auth | ABP Identity (multi-tenancy **OFF**) |
| Background work | ABP `AsyncPeriodicBackgroundWorker` (no Hangfire) |
| Real-time | In-process singleton bridge over the Blazor Server circuit (**not** SignalR) |
| Tests | xUnit + Shouldly + NSubstitute; integration tests on in-memory SQLite |
| CI | GitHub Actions (build + test on push/PR) |

## 4. Architecture

Clean/Onion + DDD rich domain model. Modules each have Domain / Domain.Shared / Application / Application.Contracts / EntityFrameworkCore / HttpApi layers.

- **`src/`** — host app (Blazor entry point, DbMigrator, host-level cross-module orchestration services & dashboards).
- **`modules/inventory`** — products, categories, suppliers, branches, branch inventory, immutable stock movements, **stock batches (FEFO/expiry)**.
- **`modules/operations`** — purchase orders, sales, stock transfers, **cashier shifts**.
- **`modules/intelligence`** — inventory rules, decision log, product velocity, the scanners and the decision engine.
- **`modules/production`** — Main Kitchen formulas, branch production requests, production plans, production orders, cook workflow, dispatch, waste, dashboard, analytics, and production decision alerts.
- **`modules/Shared`** — SoftComponents + warm cream/terracotta theme (`glass-theme.css`), the shared `MainLayout`.

### Event-driven core (the thesis centrepiece)
```
Stock changes (sale / purchase / transfer / adjustment / write-off)
  → AppBranchInventory.UpdateStock()  [single chokepoint: BranchInventoryManager.AdjustStockAsync]
  → StockChangedEto (ABP distributed event, in-process)
  → DecisionMakerService evaluates matching active rules
  → AppDecisionLog row created (immutable)
  → DecisionMadeEto → in-process bridge → notification bell + autopilot handler
```
All money/stock mutations funnel through `AdjustStockAsync`, which also drives FEFO batch consumption and writes the immutable `AppStockMovement` ledger.

### Key DDD rules in force
Rich entities (private setters + behaviour methods), aggregate roots own their children (no child repositories), business logic lives in entities/domain-services, **no LINQ in app services** (custom repositories return typed read-models), immutable ledgers (`AppStockMovement`, `AppDecisionLog`) are never updated except their one sanctioned status/outcome transition.

## 5. Roles & permissions

Four project roles (plus ABP's built-ins):

- **Admin** — every permission (granted by reflection, so new permissions are auto-included).
- **BranchManager** — read-only catalogue, branch stock adjust, receive POs, manage sales, create/ship transfers, view+acknowledge decisions, **view cashier shifts & manage/create cashiers for their own branches**. Scoped to branches where `AppBranch.ManagerUserId == them`.
- **Cashier** — POS only (`Operations.Cashier.Default`): sell, manage own shift, void own sales within 60 min, raise low-stock reports. Sees **only** the POS page; lands there on login. Tied to **one branch** via a persistent `AssignedBranchId` user claim; can only operate that branch (server-enforced).
- **KitchenManager** — Main Kitchen production permissions: formulas, branch request approval, production planning, production order start/complete/cancel, dispatch transfer creation, and supporting inventory/PO/transfer read/receive actions.

Branch scoping is enforced server-side everywhere (managers never see other branches' data). Demo users: `admin`, `manager.demo`, `cashier.demo`, `kitchen.demo` (passwords in `IdentityDataSeedContributor.cs`).

## 6. Feature inventory (everything currently built)

### Inventory
- Categories, Suppliers (with **lead time**), Products (with **shelf life**, image, cost/sale price, reorder level), Branches, Branch Inventory, immutable Stock Movements.
- **Stock batches**: FEFO (first-expired-first-out) consumption hooked into every stock change; expiry tracking.
- **Waste Analytics** page: waste cost by branch/product/week, waste-to-sales ratio.
- **Product Story** timeline (`/inventory/product-story`): every movement, decision, and batch for a product×branch merged into one timeline (reached from a Branch Inventory row action).

### Operations
- **Purchase Orders** — full lifecycle (Draft→Submitted→Approved→Partial/Received, Cancel), partial receipts.
- **Sales** — line items, branch-scoped.
- **Stock Transfers** — Draft→Pending→Approved→InTransit→Completed, per-item approval; completion preserves source batch expiry dates when stock is transferred between branches.
- **Sales Analytics** (`/operations/sales-analytics`) — 7/30/90-day revenue trend, branch comparison, category mix, top/slow movers, KPIs.
- **Cashier POS** (`/cashier`) — touch-first: product-tile grid, cart with steppers, on-screen numeric keypads, change-due, park/hold carts, SKU/barcode quick-add, print-friendly on-screen receipt, low-stock "Report to manager" badges (no quantities shown). Cash-only.
- **Cash shifts** — cashier opens with a float, system tracks expected cash, closes by counting → variance. **Cashier Shifts** manager page reconciles drawers.
- **Void / replace** — top-bar slide-in panel of the last hour's sales; void within 60 min (server-enforced, restores stock); manager can void anytime.
- **Add Cashier** (`/operations/add-cashier`) + **Cashier Assignments** — managers create cashiers (full identity + branch) and reassign branches, scoped to their own branches.

### Intelligence
- **7 rule types:** LowStock, ExcessStock, DeadStock, TransferSuggestion, **DaysOfCover**, **ExpiringSoon**, **ExpiredStock**.
- **Decision types:** LowStockAlert, ExcessStockAlert, DeadStockFlag, TransferSuggestion, ReorderSuggestion, **StockoutRisk**, **ExpiryAlert**, **WasteWriteOff**, **StockReport** (cashier-raised).
- **Decision → Action (one click):** Execute creates a draft PO (LowStock/Reorder/StockoutRisk, quantity from the ROP formula) or draft transfer (TransferSuggestion), or performs a write-off (WasteWriteOff, human-only), and links the created document on the decision.
- **Rule ActionMode autopilot:** SuggestOnly / CreateDraft / AutoSubmit — a rule can auto-create (and optionally auto-submit) its corrective document; humans still approve. Write-offs are deliberately never on autopilot.
- **Demand velocity + ABC + weekday forecast:** nightly per product×branch averages, ABC class (80/95 cumulative revenue), **7 weekday demand indices**, and days-of-cover computed by a forward **forecast walk** (`ForecastWalker`). Product Velocity page shows it incl. a "Next 7 days" column.
- **Reorder-point ordering** that uses the supplier's **measured** lead time (from real PO history) once proven, with the formula written into the PO notes.
- **Supplier scorecards** — on-time rate, fill rate, avg delay, measured lead time, A–D grade (on the Suppliers page).
- **Branch health score** — explainable 0–100 composite (6 weighted components) + league table on the Admin dashboard.
- **Reorder calendar** (`/intelligence/reorder-calendar`) — forward month view: predicted stockouts, expiring batches, expected deliveries.
- **PO consolidation** — "Consolidate reorders" groups pending reorder decisions into one draft PO per supplier×branch.
- **Self-evaluation loop:** decision **outcome tracking** (Resolved / Unresolved / StockedOut ~48 h later) + **rule effectiveness** scoring with tuning hints ("70%+ dismissed — relax the threshold").
- **Real-time notification bell** + per-decision Acknowledge/Dismiss/Execute workflow.

### Production / Main Kitchen
- **Production formulas** — deterministic recipe/BOM-lite model for producible finished goods: ingredient snapshots, output quantity, expected loss, labor/overhead, default active formula, planned cost preview.
- **Branch production requests** — BranchManager requests finished goods from the Main Kitchen; KitchenManager approves, adjusts with reason, rejects, or tracks status.
- **Production plans** — KitchenManager turns approved branch demand + weekday demand forecast + current kitchen stock into suggested production quantities; planned quantities can be overridden with a required reason.
- **Production orders + Cook screen** — confirmed plans create production orders; `/production/cook` shows the queue, ingredient availability, Start Cook, Complete Cook, and Cancel. Start consumes ingredients through `BranchInventoryManager.AdjustStockAsync` using `ProductionConsumption`; Complete creates finished stock through `ProductionOutput` with batch expiry. Migration: `20260625204621_AddProductionOrders`.
- **Production dispatch** — `/production/dispatch` lists completed kitchen orders and creates/submits Main Kitchen → branch stock transfers for accepted finished goods. The actual stock move remains in the existing transfer completion lifecycle, now with expiry-preserving batch movement.
- **Raw-material shortage loop** — a waiting kitchen order can create Arabic draft ingredient POs from the exact shortage quantities; receiving the PO updates Main Kitchen stock, then Refresh Availability moves the order to Ready to Cook.
- **Kitchen waste** — `/production/waste` records rejected output and manual/expired write-offs through the same stock chokepoint; `ProductionWaste` movements drain expired batches first.
- **Production dashboard + analytics** — `/production/dashboard` and `/production/analytics` show Arabic KPIs for ready/waiting orders, output, yield, waste, fulfillment, cost variance, product performance, and waste reason mix.
- **Production decision alerts** — deterministic daily checks raise IngredientShortage, ProductionShortageRisk, HighKitchenWaste, ProductionCostVariance, LateProductionRisk, and UnfulfilledBranchRequest decisions through the existing decision log/bell pipeline.
- **Arabic demo seed** — `ProductionDemoSeedContributor` seeds raw materials, packaging, Main Kitchen inventory, Arabic formulas, approved branch requests, starter ready-to-cook orders, and a deliberate Riverside butter-shortage order tagged `[DEMO-PRODUCTION-AR-SHORTAGE]` for the full shortage → PO → receive → cook → dispatch demo.

### Background scanners (ABP periodic workers, intervals in `appsettings.json` `BackgroundJobs:*`)
DeadStock, TransferSuggestion, **Velocity** (averages + weekday indices + ABC + StockoutRisk sweep), **Expiry** (ExpiringSoon + ExpiredStock→WasteWriteOff), **DecisionOutcome**. Demo intervals shipped low (~2–5 min); code defaults are daily/6-hourly.

## 7. Dashboards
- **Admin dashboard** — KPIs, 7-day sales area chart, top sellers, branch performance, **branch health league table** (ApexCharts).
- **Branch Manager dashboard** — branch-scoped KPIs, sales trend, stock, incoming transfers.
- **Inventory dashboard** — stock health donut, movement summary, critical items.

## 8. Quality & docs
- **108 automated tests** passing after Wave 7 (71 domain, 10 application, 27 EF Core): aggregate state machines, the rule scope matcher, the full event chain (stock→decision, with dedup), Decision→Action PO creation + ROP math, FEFO order and transfer expiry preservation, velocity/ABC math, outcome grading, cashier shift/void, production lifecycle/waste checks, and production-waste expired-batch consumption.
- **GitHub Actions CI** (`.github/workflows/ci.yml`).
- Companion docs: `docs/architecture.md` (Mermaid diagrams, formulas), `docs/database.md` (ERD), `docs/demo-script.md` (scripted ~11-min defense walkthrough), `README.md`.

## 9. Deliberate scope decisions / constraints
- **No AI/ML** — intentional, the thesis's core claim.
- **Multi-tenancy OFF.**
- **Rejected features (do not propose):** email digest, PDF receipts (cashier receipt is on-screen/print-friendly only), Excel export, what-if simulator, full manufacturing/ERP recipe complexity. A limited deterministic production formula model now exists because Main Kitchen costing and ingredient consumption require it. (A POS screen was once rejected but later explicitly requested and built as the Cashier page.)
- Real-time is an in-process bridge, correct for single-node Blazor Server — **don't claim SignalR**.

## 10. Demo prerequisites (gotchas worth remembering)
- Background scanners fire one full interval **after** startup — to demo velocity/expiry features quickly, lower `BackgroundJobs:*IntervalMinutes`, start the app, wait for the first scan, then restore.
- Several features need a **DB seeded with 90 days of history** (the seeder creates it) and effectiveness hints need a **≥48 h-old DB** for outcomes.
- **Permission/claim changes require re-login** (principal built at login): after granting the BranchManager the cashier permissions or assigning a cashier's branch, that user must log out/in.
- The dev PostgreSQL password currently sits in `appsettings.json` — rotate before publishing the repo.

## 11. Likely next directions (not yet built)
System Impact scoreboard (quantify prevented stockouts / waste flagged), forecast-accuracy back-testing (MAPE trend), decision confidence/priority weighting, command palette (Ctrl+K), responsive/mobile polish, performance hardening of in-memory cross-branch aggregations. (An LLM "explain my decisions" narration layer is possible but would need to stay narration-only to preserve the deterministic claim.)
