# Smart Multi-Branch Inventory Management System

> Graduation project — a **rule-based inventory decision support system** for a multi-branch patisserie.

The system watches every stock movement across branches and turns raw inventory events into **explainable, auditable decisions**: low-stock alerts, reorder suggestions with reorder-point math, inter-branch transfer suggestions, dead-stock flags, stockout-risk warnings, and expiry alerts. Every decision is produced by a deterministic if/else rules engine — **no AI, no ML, no black boxes** — so each one carries a human-readable reasoning string, the exact rule that fired, and the stock snapshot at evaluation time. Managers stay in the loop (acknowledge / dismiss / execute), rules can optionally run on autopilot (auto-create draft purchase orders and transfers), and a background outcome scanner grades every decision 48 hours later so the rules themselves can be tuned with evidence.

## Feature Highlights

### Inventory module (`modules/inventory`)
- Products (with SKU, cost/sale price, reorder level, optional shelf life), categories, suppliers (with lead time), branches.
- Per-branch inventory with minimum/maximum stock limits and optimistic concurrency.
- **Immutable stock-movement ledger** — every quantity change is recorded as an append-only `AppStockMovement` row (Purchase, Sale, TransferIn, TransferOut, ManualAdjustment).
- **FEFO batch tracking** for perishables (`AppStockBatch`): receipts create expiry-dated batches, consumption drains first-expired-first.

### Operations module (`modules/operations`)
- Purchase orders with a full lifecycle: Draft → Submitted → Approved → PartialReceived → Received (or Cancelled).
- Sales recording with branch-scoped access and stock validation.
- Inter-branch stock transfers: Draft → Pending → Approved → InTransit → Completed (or Cancelled).
- Sales analytics dashboard (7/14/30/60-day ranges) built on ApexCharts.

### Intelligence module (`modules/intelligence`)
- **Rule bank** (`AppInventoryRule`) with 6 rule types: `LowStock`, `ExcessStock`, `DeadStock`, `TransferSuggestion`, `DaysOfCover`, `ExpiringSoon` — each scoped globally, per product, per branch, or per product+branch, with priority-based conflict resolution.
- **Decision log** (`AppDecisionLog`) — an immutable decision ledger with a manager workflow (Pending → Acknowledged / Dismissed / Executed).
- **Execute → action**: one click turns a decision into a draft purchase order (quantity from a reorder-point formula using supplier lead time and 30-day sales velocity) or a draft stock transfer — with the math written into the document notes.
- **Autopilot** per rule (`ActionMode`): `SuggestOnly`, `CreateDraft` (auto-create the corrective draft), or `AutoSubmit` (create and submit it).
- **Background scanners**: dead stock, transfer suggestions, expiry alerts, product velocity + ABC classification + stockout risk, and a decision-outcome scanner that grades every decision (Resolved / Unresolved / StockedOut) 48 hours after it was raised.
- **Rule effectiveness scoring** on the Inventory Rules page, with deterministic threshold-tuning hints ("noisy", "effective", "dismissed alerts were followed by stockouts").
- Real-time **notification bell + toasts** for new decisions on every open page.

## The Intelligence Loop

1. Any stock change (sale, PO receipt, transfer, manual adjustment) goes through one chokepoint — `BranchInventoryManager.AdjustStockAsync` — which updates the branch inventory row and appends an immutable stock movement.
2. The aggregate itself publishes a `StockChangedEto`; the Intelligence module's `StockChangedEventHandler` hands it to `DecisionMakerService`.
3. `DecisionMakerService` loads the active rules matching the product+branch scope (highest priority wins) and evaluates `LowStock`, `ExcessStock`, and `DaysOfCover` in real time; `DeadStock`, `TransferSuggestion`, and `ExpiringSoon` are evaluated by periodic background scanners.
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
   This applies all migrations and seeds demo data: 23 products in 6 categories, 3 branches (Central Kitchen & Warehouse, Main Street Boutique, Riverside Café), ~90 days of deterministic historical sales (`HIST-…` invoices), expiry-dated stock batches for perishables, and a starter rule bank (7 rules covering all 6 rule types).

4. **Run the application:**
   ```bash
   dotnet run --project src/patisserie_shop.Blazor
   ```
   Then open <https://localhost:44387>.

### Demo tip — speeding up the background scanners

The scanner intervals are configured in `src/patisserie_shop.Blazor/appsettings.json` under `BackgroundJobs`:

```json
"BackgroundJobs": {
  "DeadStockScanIntervalMinutes": 5,
  "TransferSuggestionScanIntervalMinutes": 5,
  "VelocityScanIntervalMinutes": 1440,
  "DecisionOutcomeScanIntervalMinutes": 360,
  "ExpiryScanIntervalMinutes": 5
}
```

For a live demo, temporarily lower `VelocityScanIntervalMinutes` (and any other interval) to a few minutes so the Product Velocity page and stockout-risk decisions populate shortly after startup. In production-like use these would be nightly (1440) / 6-hourly (360) jobs.

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
