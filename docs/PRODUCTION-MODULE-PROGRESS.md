# Production Module — Wave Progress Tracker

> Companion to `MAIN-KITCHEN-PRODUCTION-PLAN.md` (the full spec) and `docs/PROJECT-STATUS.md` (whole-app state).
> This file tracks the **build-in-waves** progress of the Main Kitchen Production Module so a fresh chat can pick up exactly where we left off. Update the status box after each wave.

## Locked decisions (apply to every wave)
- **New `modules/production` bounded context** (8 projects, mirrors the `intelligence` module; wired into the host + EF migration pipeline). NOT folded into Operations.
- **Base INTEGER units** for all quantities — ingredients stored in a fine base unit (flour = grams, not kg) so the existing `int QuantityOnHand` / `BranchInventoryManager.AdjustStockAsync(int)` chokepoint and the test suite stay intact. **Money and percentages are decimals; quantities are never decimal.** Reuse `AppProduct.Unit` (UoM label) + `AppProduct.ShelfLifeDays` (expiry).
- **Deterministic, no AI/ML.** All suggestions/costs/shortages are fixed-formula arithmetic + rule logic.
- **All stock mutations go through `AdjustStockAsync`** (consume/produce/waste/dispatch). Never mutate `QuantityOnHand` directly. No `AppStockAlerts`. No child-entity repositories. No LINQ in app services.
- **Roles:** new `KitchenManager` (role) + `kitchen.demo` user; Main Kitchen is an `AppBranch` with `BranchType = MainKitchen`, `ManagerUserId = kitchen.demo` (scoping via the existing manager mechanism).
- After each wave: generate the EF migration, full build, run the test suite green, then stop.

## Status box (update me)
| Wave | Title | Status |
|---|---|---|
| 1 | Foundation — classification + module scaffold + role + Main Kitchen seed | ✅ DONE |
| 2 | Production Formulas + planned-cost calculator | ✅ DONE |
| 3 | Branch production requests + production planning | ✅ DONE |
| 4 | Production orders + Cook screen + inventory integration | ✅ DONE |
| 5 | Dispatch (batch/expiry-aware transfers) + raw-material POs | ✅ DONE |
| 6 | Kitchen waste + dashboard + analytics + production decision rules | ✅ DONE |
| 7 | Full demo seed scenario + tests + docs | ✅ DONE |

Tests baseline: 108 passing after Wave 7 (71 domain + 10 application + 27 EFCore integration). `patisserie_shop.TestBase` is a support assembly and reports no discoverable tests.

---

## Wave 1 — Foundation ✅
**Delivered:** `BranchTypes` (SalesBranch/MainKitchen) on `AppBranch`; `ProductTypes` (FinishedGood/RawMaterial/Packaging/SemiFinished) + `IsSellable`/`IsPurchasable`/`IsProducible` on `AppProduct` (reusing `Unit`+`ShelfLifeDays`); classification UI on Products/Branches; POS + Sales filtered to `IsSellable`. New `modules/production` module fully scaffolded + host-wired. `ProductionPermissions` group. `KitchenManager` role + `kitchen.demo` (password `Kitchen@2026`) + Main Kitchen branch. Migration `AddBranchProductClassification` (backfill: existing branches → SalesBranch; products → FinishedGood, sellable+purchasable, not producible).
**Notes:** Main Kitchen branch + producible-marking use a deferred-safe seeder pattern → may need a **second migrator pass** on a fresh DB. Decision/PO display text is now Arabic; tests assert structural facts, not wording.

## Wave 2 — Formulas + Costing ✅
**Delivered:** `AppProductionFormula` (root) + `AppProductionFormulaItem` (owned child) — FinishedProductId, FormulaName, Version, OutputQuantity, ExpectedWastePercent, LaborCostPerBatch, OverheadCostPerBatch, EstimatedProductionMinutes, IsActive, IsDefault, Notes; items = IngredientProductId, Quantity(int base units), LossPercent, SortOrder. `ProductionFormulaManager` (one default per finished product; finished good must be IsProducible; ingredients must be Raw/Packaging/SemiFinished). Pure `ProductionCostCalculator` (`required = ceil(plannedOut/formulaOut × qty × (1+loss%))`; `total = ingredients + labor×batches + overhead×batches`; unit = total/output; zero-cost flag). `IProductionFormulaRepository`. `IProductionFormulaAppService` (CRUD + `CalculatePlannedCostAsync` + a non-persisting `PreviewPlannedCostAsync` for the live editor + producible/ingredient lookups). Page `/production/formulas` (list + editor + live cost preview). New "Production" sidebar group. Migration `AddProductionFormulas` (+ items). 16 new tests.
**FROZEN contract for later waves:** the entity field names above, `IProductionFormulaRepository`, and `ProductionCostResult`/`PlannedCostDto` shapes.

---

## Wave 3 — Branch Requests + Planning ✅
- `AppBranchProductionRequest` (+ items): BranchManager creates/submits a request for finished goods (needed-by date, priority); KitchenManager approves / adjusts (reason required if approved qty ≠ requested) / rejects. Statuses Draft→Submitted→Approved→…→Fulfilled/Rejected/Cancelled.
- `AppProductionPlan` (+ lines): converts approved requests + deterministic forecast (reuse the existing velocity/weekday forecast) + current kitchen finished stock into `SuggestedProductionQty = max(0, requested + forecastBuffer − kitchenStock)`; KitchenManager can override (difference shown). Confirming a plan creates production orders (Wave 4).
- Pages: BranchManager "Request from Kitchen" (`/production/my-requests` or similar); KitchenManager "Branch Requests" + "Production Plan".
- Grant BranchManager the `Production.BranchRequests.*` (own-branch) permissions; scope to their branches.

**Implemented so far:** request and plan aggregates + owned child items/lines; request manager and plan manager; custom repositories for filtered list pages and deterministic plan suggestions; app services + DTOs + Mapperly mappings; BranchManager gets `Production.BranchRequests.Default`; KitchenManager keeps full Production permissions. Added `/production/my-requests`, `/production/branch-requests`, and `/production/plans`. Plan suggestions combine approved branch demand due by date, weekday-indexed velocity forecast, current Main Kitchen finished stock, and default-formula planned cost where available. Wave 4 now extends plan confirmation so confirmed plans create production orders.

**Migration:** `20260625200708_AddProductionRequestsAndPlans`.

## Wave 4 — Orders + Cook Screen + Inventory Integration ✅
- `AppProductionOrder` (+ ingredient snapshot + branch allocations). Ingredient availability check (`required` from formula vs Main Kitchen on-hand → shortage). Touch **Cook screen** (`/production/cook`): Start Cook → consume ingredients via `AdjustStockAsync` (new movement type `ProductionConsumption`) + snapshot cost; Complete Cook → enter accepted/rejected, create finished-goods **stock batch with expiry** (`ProductionDate + ShelfLifeDays`) + actual unit cost via `AdjustStockAsync` (`ProductionOutput`). Statuses Draft→ReadyToCook/WaitingForIngredients→InProduction→Completed/Cancelled.

**Implemented so far:** `AppProductionOrder` aggregate + owned ingredient snapshots and branch allocations; `ProductionOrderManager`; custom order repository/list read model; app service/DTOs for list, detail, plan-to-order creation, availability refresh, start, complete, and cancel. Confirming a production plan now creates orders from positive planned lines. Inventory integration uses `BranchInventoryManager.AdjustStockAsync`: Start Cook consumes ingredient stock with `ProductionConsumption`; Complete Cook adds finished goods with `ProductionOutput` and passes the finished batch expiry into the stock-batch ledger. Added `/production/cook` and sidebar menu entry. Added Wave 4 production error/localization labels. Added aggregate lifecycle tests.

**Migration:** `20260625204621_AddProductionOrders`.

**Verification:** `dotnet build .\patisserie_shop.slnx --no-restore` passed. `dotnet test .\patisserie_shop.slnx --no-build` passed (68 domain, 10 application, 24 EF Core). EF generated with a tool/runtime patch warning (`10.0.1` tools vs `10.0.2` runtime), but the migration was created successfully.

## Wave 5 — Dispatch + Raw-Material POs ✅
- Dispatch board (`/production/dispatch`): allocate completed batches to branches, create **stock transfers** Main Kitchen → branch, update request fulfilled qty. **Make transfers batch/expiry-aware** (today TransferIn resets expiry — must carry the source batch's expiry across).
- Ingredient shortage → "Create Draft Ingredient PO" (existing PO flow); KitchenManager can create draft + receive raw-material POs at Main Kitchen.

**Implemented so far:** stock transfer completion is now batch/expiry-aware. `StockBatchManager` can return the FEFO consumed-batch split; `BranchInventoryManager.AdjustStockDetailedAsync` exposes that split while preserving the old `AdjustStockAsync` contract; `StockTransferAppService.CompleteAsync` replays transferred lots into the destination branch with their original expiry dates and falls back to normal shelf-life only for untracked ledger drift. Added `/production/dispatch` board and sidebar entry. Dispatch is now production-aware through `ProductionDispatchAppService`: KitchenManager chooses a completed order, destination branch, and quantity; the service creates, submits, approves, ships, and completes the Main Kitchen → branch stock transfer, then applies fulfilled quantity to the oldest matching branch production request lines. Granted KitchenManager the missing transfer approve/complete permissions so `kitchen.demo` can run the full dispatch action.

**Completed scope:** dispatch now moves stock through completed stock transfers and updates branch-request fulfillment totals; ingredient shortages on the Cook screen can generate draft raw-material purchase orders grouped by each ingredient's default supplier. The draft PO stays reviewable in the existing Purchase Orders flow before submit/approve/receive.

**Verification:** `dotnet build .\src\src.sln` passed (existing warnings only). `dotnet test .\test\patisserie_shop.Domain.Tests\patisserie_shop.Domain.Tests.csproj --no-build` passed (68/68). `dotnet test .\test\patisserie_shop.Application.Tests\patisserie_shop.Application.Tests.csproj --no-build` passed (10/10). `dotnet test .\test\patisserie_shop.EntityFrameworkCore.Tests\patisserie_shop.EntityFrameworkCore.Tests.csproj --filter StockBatchFefoTests` passed (5/5). Full EF Core integration suite passed (26/26). Later dispatch/PO slices: `dotnet build .\modules\production\src\Production.Application\Production.Application.csproj` passed; `dotnet build .\src\patisserie_shop.Blazor\patisserie_shop.Blazor.csproj` passed; `dotnet run` from `src\patisserie_shop.DbMigrator` completed successfully after permission updates; `dotnet test .\test\patisserie_shop.EntityFrameworkCore.Tests\patisserie_shop.EntityFrameworkCore.Tests.csproj --filter StockBatchFefoTests` passed (5/5); `dotnet test .\test\patisserie_shop.Application.Tests\patisserie_shop.Application.Tests.csproj` passed (10/10).

## Wave 6 — Waste + Dashboard + Analytics + Decisions ✅
- `AppProductionWaste` is now a CreationAudited cost ledger with fixed waste types (`RejectedOutput`, `ExpiredFinishedGood`, `IngredientSpoilage`, `ManualWriteOff`) and fixed reasons (`Burned`, `UnderBaked`, `OverBaked`, `ShapeDamaged`, `Dropped`, `Contaminated`, `IngredientSpoilage`, `PackagingDamage`, `ExpiredBeforeDispatch`, `TestBatch`, `Other`). Migration `20260626100601_AddProductionWaste` creates `ProductionWastes` with kitchen/date, order, product, and type indexes.
- Completing a production order now records rejected output as costed production waste. Manual/expired kitchen write-offs are available from `/production/waste`; they validate on-hand stock first, then reduce inventory through `BranchInventoryManager.AdjustStockAsync(..., StockMovementTypes.ProductionWaste)` and keep the immutable stock movement trail.
- New application services and pages:
  - `/production/dashboard`: Arabic operational dashboard with KPI strip, action queue, 7-day output chart, and product focus table.
  - `/production/analytics`: Arabic analytics for yield %, waste %, fulfillment %, planned vs actual cost variance, average unit cost, daily output, product performance, and waste reason mix.
  - `/production/waste`: Arabic waste ledger with filters, 30-day waste trend, top waste products, and manual write-off modal sourced from branch inventory.
- Production decision alerts now use the existing decision log/bell pipeline. Added deterministic decision types: `IngredientShortage`, `ProductionShortageRisk`, `HighKitchenWaste`, `ProductionCostVariance`, `LateProductionRisk`, `UnfulfilledBranchRequest`. They are raised idempotently once per day through a seeded inactive sentinel rule (`IntelligenceConstants.ProductionOperationsRuleId`) so the rule engine remains deterministic and no AI/ML is introduced.
- Sidebar now includes Production Dashboard, Waste, and Analytics. KitchenManager receives these through the existing full Production permission reflection.

**Verification:** `dotnet build .\modules\production\src\Production.Application\Production.Application.csproj` passed. `dotnet build .\src\patisserie_shop.Blazor\patisserie_shop.Blazor.csproj` passed. `dotnet ef migrations add AddProductionWaste ...` created migration `20260626100601_AddProductionWaste` (EF tools 10.0.1 vs runtime 10.0.2 warning only). `dotnet run` from `src\patisserie_shop.DbMigrator` completed successfully and applied the migration/seed. `dotnet build .\src\src.sln` passed (existing warnings only). Tests passed: Domain 71/71, Application 10/10, EF Core 26/26.

## Wave 7 — Seed + Tests + Docs ✅
- Seed raw materials/packaging + kitchen inventory + formulas + a runnable demo scenario (Branch A requests → butter shortage → draft PO → receive → cook → batch → dispatch → receive). Full test pass. Update PROJECT-STATUS.md, architecture/database/demo-script docs, and the thesis "deterministic production" note.

**Implemented so far:** added an idempotent Arabic production demo seed (`ProductionDemoSeedContributor`) that creates readable raw-material and packaging products, Main Kitchen ingredient stock and seed batches, default Arabic formulas for core finished goods, approved branch production requests, and one confirmed starter production plan with ready-to-cook orders. The seed is tagged with `[DEMO-PRODUCTION-AR]` so rerunning the migrator does not duplicate the demo scenario. Also hardened branch request numbers to include milliseconds (`REQ-yyyyMMdd-HHmmssfff`) so fast request creation does not collide on the unique request-number index.

**Completed scope:** added a second idempotent Arabic shortage lane tagged `[DEMO-PRODUCTION-AR-SHORTAGE]`: Riverside requests 340 بان أو شوكولا, the seeded production order deliberately waits on a clean butter shortage, and the Cook screen can create the draft ingredient PO from the shortage. Updated production number generation to millisecond precision (`PLAN-yyyyMMdd-HHmmssfff`, `PROD-yyyyMMdd-HHmmssfff-###`) so fast seeded plans/orders do not collide. Production localization now uses clear Arabic values throughout the Production resource file. Production waste stock movements now drain expired batches first, matching the waste-write-off behavior.

**Docs:** updated `docs/demo-script.md` with an Arabic shortage → draft PO → receive → refresh → cook → complete → dispatch walkthrough and the butter calculation; updated `docs/PROJECT-STATUS.md`, `docs/architecture.md`, and `docs/database.md` for the completed Production module and deterministic production note.

**Verification:** `dotnet build .\src\src.sln` passed (existing Mapperly mapping warnings only). Tests passed: Domain 71/71, Application 10/10, EF Core 27/27. `dotnet run` from `src\patisserie_shop.DbMigrator` completed successfully. Read-only DB verification found `ShortageSeedCount=1` and `LatestStatus=WaitingForIngredients` for `[DEMO-PRODUCTION-AR-SHORTAGE]`.

---

## New stock movement types introduced across the module
`ProductionConsumption` (W4), `ProductionOutput` (W4), `ProductionWaste` (W6). Wave 5 dispatch currently reuses existing `TransferOut`/`TransferIn` movements through the stock-transfer lifecycle; only add `KitchenDispatch`/`ProductionReturn` later if a separate non-transfer dispatch flow becomes necessary.

## How to resume in a new chat
1. Attach this file + `docs/PROJECT-STATUS.md` (and `MAIN-KITCHEN-PRODUCTION-PLAN.md` for full detail).
2. Say e.g. "continue the Production Module — do Wave 3". The Status box says what's done; the frozen contracts above keep new code consistent with what's built.
