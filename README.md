# Smart Multi-Branch Inventory Management System

> Graduation project — a **rule-based inventory decision support system** for a multi-branch patisserie.

The system watches every stock movement across branches and turns raw inventory events into **explainable, auditable decisions**: low-stock alerts, reorder suggestions with reorder-point math, inter-branch transfer suggestions, dead-stock flags, weekday-aware stockout-risk warnings, expiry alerts, and waste write-offs. Every decision is produced by a deterministic if/else rules engine — **no AI, no ML, no black boxes** — so each one carries a human-readable reasoning string, the exact rule that fired, and the stock snapshot at evaluation time. Managers stay in the loop (acknowledge / dismiss / execute), rules can optionally run on autopilot (auto-create draft purchase orders and transfers), and a background outcome scanner grades every decision 48 hours later so the rules themselves can be tuned with evidence.

The system also **learns from its own operational history**: a weekday-indexed demand forecast walks the next 7 days per weekday rather than a flat average; supplier scorecards measure real on-time/fill performance and feed the *measured* lead time back into the reorder-point math; a 0–100 branch health score ranks branches in a league table; and a reorder calendar projects predicted stockouts, expiring batches, and incoming deliveries across the coming month.

## Feature Highlights

### Inventory module (`modules/inventory`)
- Products (with SKU, cost/sale price, reorder level, optional shelf life), categories, suppliers (with lead time), branches.
- Per-branch inventory with minimum/maximum stock limits and optimistic concurrency.
- **Immutable stock-movement ledger** — every quantity change is recorded as an append-only `AppStockMovement` row (Purchase, Sale, TransferIn, TransferOut, ManualAdjustment, **WriteOff**).
- **FEFO batch tracking** for perishables (`AppStockBatch`): receipts create expiry-dated batches, consumption drains first-expired-first — except waste write-offs, which deliberately drain **expired batches first**.
- **Waste analytics** (`/inventory/waste-analytics`): total waste cost, units written off, waste-to-sales ratio, weekly trend, per-branch and top-wasted-product breakdowns — all derived from `WriteOff` movements.
- **Product story timeline** (`/inventory/product-story`, reached via a row action on Branch Inventory): one chronological feed merging a product's movements, decisions, and batches at a branch.

### Operations module (`modules/operations`)
- Purchase orders with a full lifecycle: Draft → Submitted → Approved → PartialReceived → Received (or Cancelled).
- Sales recording with branch-scoped access and stock validation.
- Inter-branch stock transfers: Draft → Pending → Approved → InTransit → Completed (or Cancelled).
- Sales analytics dashboard (7/14/30/60-day ranges) built on ApexCharts.
- **Supplier scorecards** (shown on `/inventory/suppliers`): on-time rate, fill rate, average delay, and a *measured* lead time computed from received-PO history, graded A–D — the measured lead time feeds back into the reorder-point formula.

### Intelligence module (`modules/intelligence`)
- **Rule bank** (`AppInventoryRule`) with 7 rule types: `LowStock`, `ExcessStock`, `DeadStock`, `TransferSuggestion`, `DaysOfCover`, `ExpiringSoon`, `ExpiredStock` — each scoped globally, per product, per branch, or per product+branch, with priority-based conflict resolution.
- **Decision log** (`AppDecisionLog`) — an immutable decision ledger with a manager workflow (Pending → Acknowledged / Dismissed / Executed). Decision types: `LowStockAlert`, `ExcessStockAlert`, `DeadStockFlag`, `TransferSuggestion`, `ReorderSuggestion`, `StockoutRisk`, `ExpiryAlert`, `WasteWriteOff`.
- **Weekday-indexed demand forecast**: `AppProductVelocity` stores seven per-weekday demand indices; the engine walks the next 7+ days weekday-by-weekday (`ForecastWalker`) to predict depletion, falling back to a flat 30-day average when a product has no weekday pattern. The Product Velocity page shows a "Next 7 days" column.
- **Execute → action**: one click turns a decision into a draft purchase order (quantity from a reorder-point formula using the supplier's lead time and 30-day sales velocity), a draft stock transfer, or — for waste — a `WriteOff` stock adjustment, with the math written into the document notes.
- **Autopilot** per rule (`ActionMode`): `SuggestOnly`, `CreateDraft` (auto-create the corrective draft), or `AutoSubmit` (create and submit it). Waste write-offs are **deliberately never autopilot-eligible** — destroying stock is always a human action.
- **PO consolidation** (`/intelligence/decision-log`, "Consolidate reorders" button): groups pending reorder decisions (`LowStockAlert` / `ReorderSuggestion` / `StockoutRisk`) by supplier × branch into one draft PO each, merging duplicate products at the maximum quantity.
- **Reorder calendar** (`/intelligence/reorder-calendar`): a forward month view of predicted stockouts (from the forecast walk), expiring batches, and expected PO deliveries.
- **Background scanners**: dead stock, transfer suggestions, expiry alerts + expired-stock write-offs, product velocity + ABC classification + weekday indices + stockout risk, and a decision-outcome scanner that grades every decision (Resolved / Unresolved / StockedOut) 48 hours after it was raised.
- **Rule effectiveness scoring** on the Inventory Rules page, with deterministic threshold-tuning hints ("noisy", "effective", "dismissed alerts were followed by stockouts").
- **Branch health score**: a 0–100 composite of six weighted components (stock health, stockout severity, pending load, waste ratio, expiry risk, responsiveness), graded A–D, shown as a league table on the admin dashboard.
- Real-time **notification bell + toasts** for new decisions on every open page.

## The Intelligence Loop

1. Any stock change (sale, PO receipt, transfer, manual adjustment) goes through one chokepoint — `BranchInventoryManager.AdjustStockAsync` — which updates the branch inventory row and appends an immutable stock movement.
2. The aggregate itself publishes a `StockChangedEto`; the Intelligence module's `StockChangedEventHandler` hands it to `DecisionMakerService`.
3. `DecisionMakerService` loads the active rules matching the product+branch scope (highest priority wins) and evaluates `LowStock`, `ExcessStock`, and `DaysOfCover` in real time (the `DaysOfCover` check uses the weekday-indexed forecast walk, falling back to a flat average for products with no weekday pattern); `DeadStock`, `TransferSuggestion`, `ExpiringSoon`, and `ExpiredStock` are evaluated by periodic background scanners.
4. Each triggered rule creates an `AppDecisionLog` row whose constructor publishes a `DecisionMadeEto` — delivered to the notification bell in every open browser window and to the `DecisionAutopilotHandler`, which executes the decision automatically if the rule's `ActionMode` says so.
5. 48 hours later the `DecisionOutcomeScannerService` re-checks the world and records the outcome (Resolved / Unresolved / StockedOut), feeding the per-rule effectiveness statistics and tuning hints.

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | [ABP Framework](https://abp.io) v10.1.1 |
| Runtime | .NET 10 |
| UI | Blazor Server + MudBlazor (wrapped as shared "Soft" components), ApexCharts |
| Database | PostgreSQL (single database, EF Core 10 / Npgsql) |
| Architecture | Modular monolith — 3 DDD modules (inventory, operations, intelligence) + host app, Clean/Onion layering |
| Eventing | ABP distributed event bus (in-process/local by default) |
| Tests | xUnit + Shouldly; EF Core integration tests on in-memory SQLite |
| Mapping | Mapperly (source-generated mappers) |

## How to Run

### Prerequisites
- .NET 10 SDK
- PostgreSQL running locally
- Node v18/v20 (for `abp install-libs`)

### Steps

1. **Configure the connection string.** Both apps read the `Default` connection string (database `patisserie` on `localhost:5432` by default):
   - `src/patisserie_shop.Blazor/appsettings.json`
   - `src/patisserie_shop.DbMigrator/appsettings.json`

2. **Install client-side libraries** (first clone only):
   ```bash
   abp install-libs
   ```

3. **Create and seed the database** — run the migrator first:
   ```bash
   dotnet run --project src/patisserie_shop.DbMigrator
   ```
   This applies all migrations and seeds demo data: 23 products in 6 categories, 3 branches (Central Kitchen & Warehouse, Main Street Boutique, Riverside Café), ~90 days of deterministic historical sales (`HIST-…` invoices), expiry-dated stock batches for perishables (one retail branch gets an already-expired slice for the waste demo), and a starter rule bank (8 rules covering all 7 rule types).

4. **Run the application:**
   ```bash
   dotnet run --project src/patisserie_shop.Blazor
   ```
   Then open <https://localhost:44387>.

### Demo tip — speeding up the background scanners

The scanner intervals are configured in `src/patisserie_shop.Blazor/appsettings.json` under `BackgroundJobs` (the repo ships with demo-friendly low values):

```json
"BackgroundJobs": {
  "DeadStockScanIntervalMinutes": 2,
  "TransferSuggestionScanIntervalMinutes": 2,
  "VelocityScanIntervalMinutes": 2,
  "DecisionOutcomeScanIntervalMinutes": 360,
  "ExpiryScanIntervalMinutes": 2
}
```

The velocity scanner recomputes sales velocity, ABC class, **weekday demand indices**, and the stockout-risk sweep; the expiry scanner raises both `ExpiryAlert` and `WasteWriteOff` decisions. For a live demo the low values above make the Product Velocity page, the reorder calendar, and the forecast/expiry-driven decisions populate shortly after startup (ABP periodic workers fire their first tick one full interval after the app starts). In production-like use these would be nightly (1440) / 6-hourly (360) jobs; set any interval to `0` to fall back to a worker's built-in default.

### Demo credentials

Two users are seeded:

- `admin` — full access (ABP's standard seeded admin).
- `manager.demo` — `BranchManager` role, scoped to the *Main Street Boutique* branch.

Passwords are intentionally not listed here; they are defined in `src/patisserie_shop.DbMigrator/IdentityDataSeedContributor.cs` (and ABP's standard identity seed for `admin`).

### Running the tests

```bash
dotnet test patisserie_shop.slnx
```

Tests run against an in-memory SQLite database — no PostgreSQL needed. The same commands run in CI (`.github/workflows/ci.yml`).

## Documentation

| Document | Contents |
|---|---|
| [docs/architecture.md](docs/architecture.md) | Module/layer architecture, the event-driven core loop, decision lifecycle, scanner schedule, all formulas (reorder point, days of cover, transfer quantity, ABC, outcome grading), and design decisions & trade-offs |
| [docs/database.md](docs/database.md) | ER diagram of the main tables, aggregate ownership, immutability rules |
| [docs/demo-script.md](docs/demo-script.md) | Scripted ~10-minute defense demo with exact navigation, timings, and troubleshooting |
