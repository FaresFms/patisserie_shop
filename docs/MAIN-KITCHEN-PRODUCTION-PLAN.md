# Main Kitchen Production Module Plan

## Purpose

This document is a business-first implementation plan for adding a **Main Kitchen Production Module** to the existing **Smart Multi-Branch Inventory Management System**.

The goal is to make the Main Kitchen a real internal production center that can:

- Receive product requests from sales branches.
- Plan what needs to be produced.
- Check required ingredients.
- Estimate production cost.
- Start and complete cooking/production orders.
- Consume raw materials from kitchen inventory.
- Create finished goods batches with expiry dates.
- Track kitchen waste.
- Dispatch finished goods to branches.
- Report production performance, yield, waste, and fulfillment.

This must remain a **deterministic rule-based system**. No AI, no ML, no probabilistic recommendation logic.

---

## Existing Project Context

The current system already has:

- ABP Framework with Clean/Onion architecture.
- Modules: Inventory, Operations, Intelligence, Shared UI.
- Branches and branch-scoped inventory.
- Products with cost price, sale price, shelf life, reorder level, image.
- Stock batches with FEFO and expiry tracking.
- Immutable stock movements.
- Purchase orders.
- Sales.
- Stock transfers.
- Cashier POS.
- Cashier shifts.
- Rule-based decision engine.
- Demand velocity, weekday forecast, days of cover, stockout risk.
- Waste analytics.
- BranchManager and Cashier roles with server-side branch scoping.

The production module must integrate with the existing system instead of creating a disconnected subsystem.

Critical architectural rule:

> All stock mutations must use the existing stock adjustment / inventory movement flow. Do not directly update stock quantities from production services.

---

## Important Scope Correction

The project previously rejected full Recipes/BOM as a feature. However, production costing and ingredient consumption are impossible without a limited formula model.

Therefore, do **not** build a full recipe system with cooking instructions, nutrition, images, preparation steps, or chef notes.

Build a limited deterministic model called:

> **Production Formula**

The Production Formula exists only to define:

- Finished product output.
- Required ingredients.
- Expected yield.
- Expected waste percentage.
- Labor cost.
- Overhead cost.
- Estimated production time.

This is necessary for ingredient calculation and cost calculation.

---

# 1. Business Concept

## Main Kitchen as Production Center

The Main Kitchen is an internal branch-like location that produces finished goods for sales branches.

Business flow:

```text
Branches sell products
        ↓
Branches request finished goods from Main Kitchen
        ↓
Kitchen Manager reviews requests
        ↓
System checks ingredients and expected cost
        ↓
Kitchen Manager creates production orders
        ↓
Kitchen starts cooking
        ↓
Ingredients are consumed from kitchen inventory
        ↓
Finished goods are produced as stock batches with expiry dates
        ↓
Kitchen dispatches products to branches using stock transfers
        ↓
Branches receive products and sell them
```

---

# 2. New Role: KitchenManager

## Role Name

```text
KitchenManager
```

## Role Purpose

The KitchenManager manages the Main Kitchen production workflow.

They are not a cashier, not a general branch manager, and not an admin.

They are responsible for:

- Reviewing branch demand.
- Planning daily production.
- Checking ingredient availability.
- Executing production orders.
- Recording production results.
- Recording waste.
- Dispatching finished goods to branches.
- Monitoring production performance.

---

## What KitchenManager Can Do

The KitchenManager can:

1. View the Kitchen Dashboard.
2. View submitted branch production requests.
3. Approve branch requests.
4. Reject branch requests with reason.
5. Adjust requested quantities with reason.
6. Generate a production plan.
7. See suggested production quantities.
8. See ingredient requirements.
9. See ingredient shortages.
10. See estimated production cost.
11. Create production orders.
12. Start cooking.
13. Complete cooking.
14. Consume ingredients from Main Kitchen inventory.
15. Record actual produced quantity.
16. Record accepted quantity.
17. Record rejected quantity.
18. Record waste reason.
19. Create finished goods batches.
20. See expiry dates.
21. Dispatch produced goods to branches.
22. Receive raw material purchase orders for Main Kitchen.
23. Create draft ingredient purchase orders when ingredients are missing.
24. View production analytics.
25. View kitchen waste analytics.
26. View formula cost for produced products.

---

## What KitchenManager Must Not Do

The KitchenManager must not:

1. Access Cashier POS.
2. Open or close cashier shifts.
3. Sell products directly.
4. Void branch sales.
5. Manage cashier users unless explicitly granted by Admin.
6. Manage all branches.
7. Change global product sale prices.
8. Manage system-wide inventory rules.
9. Access unrelated branch sales details except aggregated/requested production demand.
10. Approve global admin settings.

---

## Suggested Permissions

Create a new permission group:

```text
Production
```

Suggested permissions:

```text
Production.Dashboard
Production.Formulas.Default
Production.Formulas.Manage
Production.BranchRequests.Default
Production.BranchRequests.Approve
Production.BranchRequests.Reject
Production.Plans.Default
Production.Plans.Manage
Production.Orders.Default
Production.Orders.Create
Production.Orders.Start
Production.Orders.Complete
Production.Orders.Cancel
Production.Ingredients.Default
Production.Ingredients.CheckAvailability
Production.Waste.Default
Production.Waste.WriteOff
Production.Dispatch.Default
Production.Dispatch.CreateTransfer
Production.Dispatch.Ship
Production.Analytics.Default
```

Limited existing permissions to grant to KitchenManager:

```text
Inventory.Products.Default
Inventory.Categories.Default
Inventory.Suppliers.Default
Inventory.BranchInventory.Default
Inventory.StockMovements.Default
Operations.PurchaseOrders.Default
Operations.PurchaseOrders.Receive
Operations.StockTransfers.Default
Operations.StockTransfers.Create
Operations.StockTransfers.Ship
```

Do not grant:

```text
Operations.Cashier.Default
Operations.Sales.Manage
Operations.CashierShifts.Manage
Identity.Admin
Inventory.Products.Manage
Intelligence.Rules.Manage
```

---

# 3. Main Kitchen Modeling

## Branch Type

Add branch type classification:

```text
BranchType
- SalesBranch
- MainKitchen
```

The Main Kitchen should be stored as an existing `AppBranch`, but marked as `MainKitchen`.

Example:

```text
Branch Name: Main Kitchen
Branch Type: MainKitchen
Manager: kitchen.demo
Can Sell: No
Can Produce: Yes
Can Receive Raw Materials: Yes
Can Dispatch Finished Goods: Yes
```

## Business Rules

- A `SalesBranch` can sell products.
- A `SalesBranch` can request finished goods.
- A `SalesBranch` cannot create production orders.
- A `MainKitchen` can produce products.
- A `MainKitchen` can hold raw materials, packaging, semi-finished goods, and finished goods.
- A `MainKitchen` cannot use POS.
- A `MainKitchen` can dispatch finished goods to sales branches.

---

# 4. Product Classification

Add product type classification.

```text
ProductType
- FinishedGood
- RawMaterial
- Packaging
- SemiFinished
```

Add or support these fields/flags:

```text
IsSellable
IsPurchasable
IsProducible
UnitOfMeasure
ProductionShelfLifeDays or reuse existing ShelfLifeDays
```

## Examples

| Product | Product Type | Sellable | Purchasable | Producible | Unit |
|---|---:|---:|---:|---:|---|
| Croissant | FinishedGood | Yes | No | Yes | Piece |
| Éclair | FinishedGood | Yes | No | Yes | Piece |
| Flour | RawMaterial | No | Yes | No | Kg |
| Butter | RawMaterial | No | Yes | No | Kg |
| Chocolate Cream | SemiFinished | Optional | No | Yes | Kg |
| Cake Box | Packaging | No | Yes | No | Piece |

## Business Rules

- Cashier POS must only show `IsSellable = true` products.
- Raw materials must never appear in POS.
- Purchase orders should normally use `IsPurchasable = true` products.
- Production orders should produce only `IsProducible = true` products.
- Production formulas should use ingredient products that are RawMaterial, Packaging, or SemiFinished.

---

# 5. Production Formulas

## Purpose

Production formulas define how a finished good is produced from ingredients.

They are not full recipes. They are deterministic costing and consumption definitions.

## Entity: AppProductionFormula

Suggested fields:

```text
AppProductionFormula
- Id
- FinishedProductId
- FormulaName
- Version
- OutputQuantity
- OutputUnit
- ExpectedWastePercent
- LaborCostPerBatch
- OverheadCostPerBatch
- EstimatedProductionMinutes
- IsActive
- IsDefault
- Notes
- CreationTime
- CreatorId
```

## Entity: AppProductionFormulaItem

Suggested fields:

```text
AppProductionFormulaItem
- Id
- FormulaId
- IngredientProductId
- Quantity
- UnitOfMeasure
- LossPercent
- SortOrder
```

## Example Formula

```text
Formula: Croissant Standard Formula
Output: 100 Croissants

Ingredients:
- Flour: 8 kg
- Butter: 5 kg
- Sugar: 1.5 kg
- Yeast: 0.2 kg
- Salt: 0.1 kg

Expected waste: 3%
Labor cost per batch: 12
Overhead cost per batch: 5
Estimated production time: 180 minutes
```

## Business Rules

- A finished product can have multiple formula versions.
- Only one active default formula should exist per finished product.
- A formula cannot be used if it has no ingredients.
- A formula cannot produce non-producible products.
- Formula ingredients must be valid stock-tracked products.
- Historical production orders must snapshot formula values.
- Changing a formula must not change old production orders.

---

# 6. Production Cost Calculation

The system must calculate both planned cost and actual cost.

## Planned Cost

Formula:

```text
RequiredIngredientQty =
    PlannedOutputQty / FormulaOutputQty
    × FormulaIngredientQty
    × (1 + IngredientLossPercent)

PlannedIngredientCost =
    RequiredIngredientQty × CurrentIngredientUnitCost

PlannedTotalCost =
    Sum(PlannedIngredientCost)
    + LaborCost
    + OverheadCost

PlannedUnitCost =
    PlannedTotalCost / PlannedOutputQty
```

## Actual Cost

Formula:

```text
ActualTotalCost =
    ActualIngredientCost
    + ActualLaborCost
    + ActualOverheadCost

ActualUnitCost =
    ActualTotalCost / AcceptedOutputQty
```

## Waste Cost

Formula:

```text
WasteCost =
    RejectedOutputQty × ActualUnitCost
```

## Ingredient Cost Source Priority

Use this priority:

```text
1. Stock batch unit cost if available.
2. Product CostPrice if batch cost is unavailable.
3. Zero cost is forbidden unless explicitly allowed by Admin.
```

## Important Rule

Do not automatically overwrite the product's global `CostPrice` after every production completion.

Store unit cost on the produced batch / production batch. Global cost price can be misleading if production costs fluctuate.

---

# 7. Branch Production Requests

Branches should request finished goods from the Main Kitchen.

## Flow

```text
BranchManager creates request
        ↓
KitchenManager receives request
        ↓
KitchenManager approves, adjusts, or rejects
        ↓
Approved request becomes input for production planning
```

## Entity: AppBranchProductionRequest

Suggested fields:

```text
AppBranchProductionRequest
- Id
- RequestNumber
- BranchId
- NeededByDate
- Priority
- Status
- RequestedByUserId
- ApprovedByUserId
- ApprovedAt
- Notes
- RejectionReason
- CreationTime
```

## Entity: AppBranchProductionRequestItem

Suggested fields:

```text
AppBranchProductionRequestItem
- Id
- RequestId
- ProductId
- RequestedQuantity
- ApprovedQuantity
- FulfilledQuantity
- Notes
```

## Statuses

```text
Draft
Submitted
Approved
PartiallyPlanned
Planned
PartiallyFulfilled
Fulfilled
Rejected
Cancelled
```

## BranchManager Actions

BranchManager can:

- Create request.
- Edit request while Draft.
- Submit request.
- Cancel request before approval.
- View request status.
- View approved quantity.
- View fulfilled quantity.

## KitchenManager Actions

KitchenManager can:

- View submitted requests.
- Approve request.
- Adjust approved quantities.
- Reject request with reason.
- Convert approved quantities into production plan.

## Business Rule

If approved quantity differs from requested quantity, the KitchenManager must provide a reason.

Example:

```text
Branch A requests:
- 80 Croissants
- 40 Éclairs

Kitchen approves:
- 70 Croissants
- 40 Éclairs

Reason:
Croissant butter shortage. Partial fulfillment today.
```

---

# 8. Production Planning

Production planning converts approved branch demand and deterministic forecast demand into production orders.

## Entity: AppProductionPlan

Suggested fields:

```text
AppProductionPlan
- Id
- PlanNumber
- KitchenBranchId
- ProductionDate
- Status
- CreatedByUserId
- ConfirmedByUserId
- ConfirmedAt
- Notes
```

## Entity: AppProductionPlanLine

Suggested fields:

```text
AppProductionPlanLine
- Id
- PlanId
- ProductId
- RequestedQuantity
- ForecastQuantity
- CurrentKitchenStock
- SuggestedQuantity
- PlannedQuantity
- EstimatedIngredientCost
- EstimatedLaborCost
- EstimatedOverheadCost
- EstimatedTotalCost
```

## Statuses

```text
Draft
Confirmed
InProgress
Closed
Cancelled
```

## Suggested Quantity Formula

```text
RequestedDemand =
    Sum(approved branch request quantities due for selected date)

ForecastDemand =
    Sum(expected branch demand for selected date or next N days)

KitchenAvailableFinishedStock =
    current finished goods stock in Main Kitchen

SuggestedProductionQty =
    Max(0, RequestedDemand + ForecastBuffer - KitchenAvailableFinishedStock)
```

Forecast demand should reuse the existing deterministic velocity / weekday forecast system.

## Override Rule

KitchenManager can override the suggested quantity, but the difference must be visible.

Example:

```text
Suggested: 210
Planned: 220
Difference: +10
Reason: extra buffer for morning demand
```

---

# 9. Ingredient Availability Check

Before starting production, the system must check whether the Main Kitchen has enough ingredients.

## Required Ingredients Calculation

For each planned finished product:

```text
RequiredIngredientQty =
    PlannedOutputQty / FormulaOutputQty
    × FormulaIngredientQty
    × (1 + IngredientLossPercent)
```

## Availability Formula

```text
AvailableQty =
    KitchenQuantityOnHand - ReservedQty

ShortageQty =
    Max(0, RequiredQty - AvailableQty)
```

## Example

```text
Plan:
- 200 Croissants
- 100 Éclairs

Required ingredients:
- Flour: 24 kg
- Butter: 12 kg
- Sugar: 5 kg
- Chocolate: 4 kg

Available in Main Kitchen:
- Flour: 30 kg
- Butter: 8 kg
- Sugar: 20 kg
- Chocolate: 6 kg

Shortage:
- Butter: 4 kg
```

## Optional Reservation Model

If implementing reservations:

```text
AppKitchenIngredientReservation
- Id
- ProductionOrderId
- IngredientProductId
- ReservedQuantity
- Status
- CreationTime
```

Statuses:

```text
Reserved
Consumed
Released
Cancelled
```

Recommended for first implementation:

```text
Skip formal reservations.
Check availability before Start Cook.
Consume ingredients when Start Cook is clicked.
```

This is simpler and enough for the graduation project.

---

# 10. Production Orders

Production Order is the execution document.

A Production Plan says:

```text
Today we need 220 Croissants.
```

A Production Order says:

```text
Cook 220 Croissants now.
```

## Entity: AppProductionOrder

Suggested fields:

```text
AppProductionOrder
- Id
- OrderNumber
- KitchenBranchId
- ProductionPlanId
- FinishedProductId
- FormulaId
- FormulaVersion
- Status
- Priority
- PlannedOutputQuantity
- ActualOutputQuantity
- AcceptedQuantity
- RejectedQuantity
- PlannedStartTime
- ActualStartTime
- CompletedAt
- ExpiryDate
- PlannedIngredientCost
- ActualIngredientCost
- LaborCost
- OverheadCost
- TotalProductionCost
- UnitProductionCost
- Notes
- CreatedByUserId
- StartedByUserId
- CompletedByUserId
```

## Entity: AppProductionOrderIngredient

Snapshot of required/consumed ingredients.

```text
AppProductionOrderIngredient
- Id
- ProductionOrderId
- IngredientProductId
- RequiredQuantity
- ConsumedQuantity
- UnitCostSnapshot
- TotalCost
```

## Entity: AppProductionOrderAllocation

Links production output to branch requests.

```text
AppProductionOrderAllocation
- Id
- ProductionOrderId
- BranchId
- BranchProductionRequestItemId
- AllocatedQuantity
- FulfilledQuantity
```

## Statuses

```text
Draft
ReadyToCook
WaitingForIngredients
InProduction
Completed
Cancelled
```

## Status Rules

```text
Draft → ReadyToCook
Allowed when formula exists.

ReadyToCook → WaitingForIngredients
Automatic/computed if ingredients are not enough.

ReadyToCook → InProduction
Allowed when ingredients are enough.

WaitingForIngredients → ReadyToCook
Allowed after raw material stock is received.

InProduction → Completed
Allowed when actual output is entered.

Draft/ReadyToCook/WaitingForIngredients → Cancelled
Allowed before cooking starts.

InProduction → Cancelled
Do not allow normal cancellation. Require waste/adjustment flow.
```

---

# 11. Cook Screen

Create a touch-friendly operational screen:

```text
/production/cook
```

This is the main execution screen for the KitchenManager.

## Screen Must Show

- Today's active production orders.
- Product name and image.
- Production order number.
- Status.
- Planned output quantity.
- Required ingredients.
- Ingredient availability.
- Estimated cost.
- Start Cook button.
- Complete Cook button.
- Waste/rejection input.
- Notes.

## Selected Order Example

```text
Croissant — Production Order PROD-00041

Planned Output:
220 pieces

Ingredients:
- Flour: 17.6 kg
- Butter: 11 kg
- Sugar: 3.3 kg
- Yeast: 0.44 kg

Estimated Cost:
Ingredients: 85
Labor: 26
Overhead: 11
Total: 122
Unit: 0.55

Actions:
[Start Cook]
[Complete Cook]
[Record Waste]
```

## Start Cook Action

When KitchenManager clicks `Start Cook`:

```text
1. Check ingredient availability.
2. If ingredients are not enough, block start and show shortage.
3. Consume ingredients from Main Kitchen inventory.
4. Record ingredient cost snapshot.
5. Create ingredient consumption stock movements.
6. Set order status to InProduction.
```

## Complete Cook Action

KitchenManager enters:

```text
Actual produced quantity
Accepted quantity
Rejected quantity
Waste reason
Expiry date if different from default
Notes
```

Then system:

```text
1. Calculates actual unit cost.
2. Creates finished goods stock batch.
3. Increases Main Kitchen finished goods inventory.
4. Records production output stock movement.
5. Records waste if rejected quantity > 0.
6. Marks order Completed.
7. Updates branch request fulfillment availability.
```

---

# 12. Production Batches and Expiry

When production completes, create a production batch and connect it to inventory stock batches.

## Entity: AppProductionBatch

Suggested fields:

```text
AppProductionBatch
- Id
- BatchNumber
- ProductionOrderId
- KitchenBranchId
- FinishedProductId
- QuantityProduced
- QuantityAccepted
- QuantityRejected
- UnitCost
- ProducedAt
- ExpiryDate
- Status
```

## Statuses

```text
Available
PartiallyDispatched
FullyDispatched
WrittenOff
Expired
```

## Expiry Rule

```text
ExpiryDate = ProductionCompletedDate + Product.ShelfLifeDays
```

If the product has no shelf life, block production completion or require a manual expiry date.

For patisserie products, expiry is mandatory.

## Inventory Integration

Recommended:

```text
Create AppProductionBatch for production traceability.
Create/update AppStockBatch for inventory FEFO/expiry.
Link them using ProductionBatchId or SourceDocumentId.
```

---

# 13. Kitchen Waste and Quality Control

Kitchen waste is different from branch expiry waste.

## Waste Reasons

```text
Burned
UnderBaked
OverBaked
ShapeDamaged
Dropped
Contaminated
IngredientSpoilage
PackagingDamage
ExpiredBeforeDispatch
TestBatch
Other
```

## Entity: AppProductionWaste

Suggested fields:

```text
AppProductionWaste
- Id
- ProductionOrderId
- ProductionBatchId
- KitchenBranchId
- ProductId
- WasteType
- Quantity
- UnitCost
- TotalCost
- Reason
- RecordedByUserId
- RecordedAt
```

## Waste Can Happen In Two Places

```text
1. During production
   Example: 10 croissants rejected because burned.

2. After production but before dispatch
   Example: 20 éclairs expired in kitchen fridge.
```

## Business Rules

- Rejected quantity during Complete Cook creates production waste.
- Expired finished goods in kitchen create waste write-off.
- Ingredient spoilage creates ingredient waste.
- Waste must reduce stock when the item exists in inventory.
- Waste must be costed.
- Waste must be visible in analytics.
- Do not allow silent deletion of waste.

---

# 14. Dispatch to Branches

Do not build a new shipping system.

Use existing stock transfers.

## Dispatch Flow

```text
Completed production batch
        ↓
Ready for Dispatch
        ↓
KitchenManager creates transfer
        ↓
Transfer from Main Kitchen to Branch A
        ↓
BranchManager receives transfer
        ↓
Branch inventory increases
```

## Dispatch Page

Route:

```text
/production/dispatch
```

The screen should show:

- Ready products by branch allocation.
- Available kitchen stock.
- Request due date.
- Dispatch quantity.
- Create Transfer button.
- Transfer status.

## Example

```text
Branch A — Due Today

Croissant
Requested: 80
Approved: 70
Produced/Available: 70
Dispatch: 70

Éclair
Requested: 40
Approved: 40
Produced/Available: 40
Dispatch: 40

[Create Transfer]
```

## Transfer Rules

```text
Transfer source branch = Main Kitchen
Transfer destination branch = sales branch
Transfer items = finished goods only
Transfer should preserve batch expiry if possible
Transfer should update fulfilled quantity on branch production request
```

If stock transfers are not batch-aware, improve transfer logic to preserve expiry/batch data.

---

# 15. Raw Material Purchasing Integration

The kitchen needs ingredients.

Use existing purchase orders.

## Flow

```text
Ingredient shortage detected
        ↓
KitchenManager creates draft ingredient PO or purchase request
        ↓
Admin/authorized manager approves
        ↓
Supplier delivers to Main Kitchen
        ↓
KitchenManager receives PO
        ↓
Raw material inventory increases
        ↓
Waiting production orders become ReadyToCook
```

## Permission Recommendation

KitchenManager:

```text
Can create draft ingredient PO.
Can receive approved ingredient PO for Main Kitchen.
```

Admin:

```text
Can approve PO.
Can manage suppliers.
Can manage product costs.
```

## Shortage Action

On ingredient shortage screens, add:

```text
[Create Draft Ingredient PO]
```

The PO should include:

```text
Supplier
Ingredient product
Shortage quantity
Suggested reorder quantity
Needed date
Reason: Production shortage for plan/order number
```

---

# 16. Kitchen Dashboard

KitchenManager should land on:

```text
/production/dashboard
```

## Dashboard Cards

Show:

```text
Pending Branch Requests
Today's Planned Production
Orders In Production
Ingredient Shortages
Ready To Dispatch
Waste Cost Today
Production Yield %
On-Time Fulfillment %
```

## Dashboard Sections

### 1. Today's Action Queue

Show next actions:

```text
Review pending branch requests
Start ready production orders
Complete active orders
Dispatch completed products
```

### 2. Ingredient Shortages

Columns:

```text
Ingredient
Required
Available
Shortage
Related production order
Create PO action
```

### 3. Production Timeline

Statuses:

```text
Planned
In Production
Completed
Ready to Dispatch
```

### 4. Cost Snapshot

Show:

```text
Estimated cost today
Actual cost today
Cost variance
```

### 5. Waste Snapshot

Show:

```text
Quantity wasted
Waste cost
Top waste reason
```

## Example Dashboard Numbers

```text
Pending Requests: 6
Orders Ready to Cook: 4
In Production: 2
Ready to Dispatch: 3
Ingredient Shortages: 1
Waste Today: 18.40
Yield Today: 96.2%
Fulfillment Today: 88%
```

---

# 17. Production Analytics

Create page:

```text
/production/analytics
```

## KPIs

```text
Produced Quantity
Accepted Quantity
Rejected Quantity
Waste %
Waste Cost
Average Unit Cost
Planned vs Actual Production
On-Time Completion %
Branch Fulfillment Rate
Ingredient Shortage Count
Top Produced Products
Top Waste Products
Cost Variance
```

## Reports

```text
Production by day
Production by product
Waste by reason
Waste by product
Cost per unit trend
Branch request fulfillment
Ingredient consumption trend
```

## Formulas

```text
Yield % =
    AcceptedQuantity / ProducedQuantity × 100

Waste % =
    RejectedQuantity / ProducedQuantity × 100

Fulfillment % =
    FulfilledQuantity / ApprovedRequestQuantity × 100

Cost Variance =
    ActualTotalCost - PlannedTotalCost

Unit Cost Variance =
    ActualUnitCost - PlannedUnitCost

On-Time Production % =
    Orders completed before due time / total orders due
```

---

# 18. Deterministic Production Intelligence

Keep this rule-based. No AI/ML.

Add new decision types:

```text
IngredientShortage
ProductionShortageRisk
HighKitchenWaste
ProductionCostVariance
LateProductionRisk
UnfulfilledBranchRequest
```

## Rule Examples

### IngredientShortage

Trigger:

```text
Required ingredient quantity > available quantity
```

Decision example:

```text
Butter shortage for Croissant production plan.
Required: 12 kg
Available: 8 kg
Shortage: 4 kg
Suggested Action: Create draft PO for Butter.
```

### ProductionShortageRisk

Trigger:

```text
Approved branch requests due tomorrow > planned production + kitchen finished stock
```

### HighKitchenWaste

Trigger:

```text
Waste % for product exceeds configured threshold
```

### ProductionCostVariance

Trigger:

```text
Actual unit cost exceeds planned unit cost by threshold percentage
```

### LateProductionRisk

Trigger:

```text
Production order is not started X hours before due time
```

### UnfulfilledBranchRequest

Trigger:

```text
Due date is today and fulfilled quantity < approved quantity
```

## Important Rule

Do not create `AppStockAlerts`.

Use existing decision log pattern.

---

# 19. Required Pages

Add new sidebar group:

```text
Production
```

## Production Pages

```text
/production/dashboard
/production/branch-requests
/production/plans
/production/orders
/production/cook
/production/ingredients
/production/dispatch
/production/formulas
/production/waste
/production/analytics
```

## KitchenManager Sidebar

Show:

```text
Production Dashboard
Branch Requests
Production Orders
Cook Screen
Ingredients
Dispatch
Waste
Analytics
```

## Admin Sidebar

Can also show:

```text
Production Formulas
Production Settings
All Production Analytics
```

## BranchManager Sidebar

Add:

```text
Request from Kitchen
My Kitchen Requests
```

## Cashier Sidebar

No change.

Cashier must not see production pages.

---

# 20. BranchManager Changes

Add a simple branch request screen.

Suggested route:

```text
/production/my-requests
```

or:

```text
/operations/request-from-kitchen
```

## BranchManager Actions

BranchManager can:

```text
Create request
Add finished goods
Select needed-by date
Set priority
Submit request
Track status
See approved quantity
See dispatched quantity
See received quantity
Cancel if not approved
```

## Request Form Example

```text
Needed By: Tomorrow 08:00
Priority: Normal/Urgent
Notes: Weekend demand expected

Items:
- Croissant: 80
- Éclair: 40
- Tart: 25

[Submit Request]
```

## Status View

```text
Submitted → Approved → Planned → Produced → Dispatched → Received
```

---

# 21. Ingredient Board

Create page:

```text
/production/ingredients
```

This is production-focused, not a generic inventory page.

## Columns

```text
Ingredient
Available Quantity
Reserved Quantity
Required Today
Shortage Quantity
Days of Production Cover
Supplier
Last Purchase Cost
Create PO Action
```

## Example

```text
Butter
Available: 8 kg
Required Today: 12 kg
Shortage: 4 kg
Supplier: Dairy Supplier
Action: Create Draft PO
```

## Formula

```text
DaysOfProductionCover =
    AvailableIngredientQty / AverageDailyRequiredIngredientQty
```

Average daily required ingredient quantity can be calculated from recent production or forecasted production.

---

# 22. Production Settings

Add admin-only settings.

## Suggested Settings

```text
Default Main Kitchen Branch
Production Planning Horizon Days
Default Forecast Buffer Days
Allow Production Without Enough Ingredients
Allow Manual Expiry Override
Require Waste Reason
Require Approval For Quantity Changes
Auto-create Transfer After Production Completion
Default Labor Cost Method
Default Overhead Cost Method
```

## Recommended Defaults

```text
Planning horizon: 1 day
Forecast buffer: 0.5 day or 1 day
Allow production without ingredients: No
Require waste reason: Yes
Require approval for branch quantity changes: No for KitchenManager
Auto-create transfer: No, let KitchenManager review dispatch
```

---

# 23. End-to-End Business Workflow

## 23.1 Admin Setup

```text
1. Admin creates Main Kitchen branch.
2. Admin assigns KitchenManager user.
3. Admin classifies products:
   - Croissant = FinishedGood, Sellable, Producible
   - Flour = RawMaterial, Purchasable
4. Admin creates formulas for producible finished goods.
5. Admin seeds raw materials into Main Kitchen inventory.
```

## 23.2 Branch Request

```text
1. BranchManager opens Request from Kitchen.
2. Adds finished goods and quantities.
3. Selects needed-by date.
4. Submits request.
5. Request status becomes Submitted.
```

## 23.3 Kitchen Approval

```text
1. KitchenManager opens Branch Requests.
2. Reviews submitted request.
3. Approves quantities or rejects.
4. Approved quantities become production demand.
```

## 23.4 Production Planning

```text
1. KitchenManager opens Production Plan.
2. Selects production date.
3. System pulls:
   - approved branch requests
   - deterministic forecast buffer
   - current kitchen finished stock
4. System suggests production quantities.
5. KitchenManager confirms plan.
6. System creates production orders.
```

## 23.5 Ingredient Check

```text
1. System checks required ingredients for all production orders.
2. If ingredients are enough:
   - order becomes ReadyToCook.
3. If ingredients are missing:
   - order becomes WaitingForIngredients.
   - system shows shortage.
   - KitchenManager can create draft ingredient PO.
```

## 23.6 Cook

```text
1. KitchenManager opens Cook Screen.
2. Selects ReadyToCook order.
3. Clicks Start Cook.
4. System consumes ingredients from Main Kitchen inventory.
5. Order becomes InProduction.
6. KitchenManager completes order.
7. Enters accepted quantity and rejected quantity.
8. System creates finished goods batch.
9. System calculates actual cost.
10. Order becomes Completed.
```

## 23.7 Dispatch

```text
1. KitchenManager opens Dispatch Board.
2. System groups completed products by branch allocation.
3. KitchenManager creates stock transfer from Main Kitchen to branch.
4. BranchManager receives transfer.
5. Branch inventory increases.
6. Branch request fulfillment updates.
```

## 23.8 Analytics

```text
1. Production Analytics updates.
2. Waste Analytics includes kitchen waste.
3. Product Story timeline shows production-related stock movements.
4. Decision Log shows production warnings and outcomes.
```

---

# 24. Required Database Changes Summary

## Existing Tables to Modify

```text
AppBranches
- BranchType

AppProducts
- ProductType
- UnitOfMeasure
- IsSellable
- IsPurchasable
- IsProducible
- ProductionShelfLifeDays or reuse ShelfLifeDays
```

## New Production Tables

```text
AppProductionFormulas
AppProductionFormulaItems

AppBranchProductionRequests
AppBranchProductionRequestItems

AppProductionPlans
AppProductionPlanLines

AppProductionOrders
AppProductionOrderIngredients
AppProductionOrderAllocations

AppProductionBatches
AppProductionWastes
```

## Optional Advanced Table

```text
AppKitchenIngredientReservations
```

## New Enums

```text
BranchType
ProductType
UnitOfMeasure
ProductionFormulaStatus
BranchProductionRequestStatus
ProductionPlanStatus
ProductionOrderStatus
ProductionBatchStatus
ProductionWasteType
ProductionPriority
```

## Stock Movement Type Additions

```text
ProductionConsumption
ProductionOutput
ProductionWaste
ProductionReturn
KitchenDispatch
```

---

# 25. Integration Rules

## Inventory Integration

Production must:

```text
Consume raw materials from Main Kitchen inventory.
Create finished goods stock in Main Kitchen inventory.
Create stock movements.
Create stock batches with expiry.
Respect FEFO.
Respect immutable stock movement ledger.
Use existing stock adjustment path.
```

## Operations Integration

Production must:

```text
Use Purchase Orders for raw materials.
Use Stock Transfers for dispatching finished goods.
Update branch requests when transfers are created/received.
```

## Intelligence Integration

Production should generate deterministic decision logs for:

```text
Ingredient shortage
Production shortage risk
High waste
Cost variance
Late production
Unfulfilled branch request
```

## Sales Integration

Production should use historical sales/forecast data only for planning suggestions.

It should not directly create sales.

## Cashier Integration

Cashier should only see sellable products.

Raw materials, packaging, and non-sellable semi-finished items must be hidden from POS.

---

# 26. User Experience Principles

Kitchen staff do not need accounting-style CRUD screens as their main workflow.

The KitchenManager should have a clear next-action flow:

```text
1. Review Requests
2. Confirm Plan
3. Check Ingredients
4. Start Cook
5. Complete Cook
6. Dispatch
```

Each screen should answer one business question:

```text
Dashboard:
What needs my attention?

Branch Requests:
What do branches need?

Production Plan:
What should we produce?

Cook Screen:
What are we cooking now?

Ingredients:
What is missing?

Dispatch:
What is ready to send?

Analytics:
How well is the kitchen performing?
```

Avoid forcing the KitchenManager to jump between generic inventory pages.

---

# 27. Demo Data to Seed

## Demo User

```text
username: kitchen.demo
role: KitchenManager
assigned branch: Main Kitchen
```

## Seed Main Kitchen

```text
Main Kitchen branch
Raw material inventory
Finished goods products
Production formulas
Submitted branch requests
One production plan
Several production orders
One ingredient shortage
One completed batch ready to dispatch
One waste example
```

## Finished Goods

```text
Croissant
Éclair
Chocolate Cake
Fruit Tart
Macaron Box
```

## Raw Materials

```text
Flour
Butter
Sugar
Eggs
Milk
Chocolate
Cream
Yeast
Salt
Strawberries
```

## Packaging

```text
Cake Box
Pastry Box
Paper Bag
```

## Formulas

```text
Croissant formula
Éclair formula
Chocolate Cake formula
Fruit Tart formula
```

## Demo Scenario

```text
1. Branch A requests 80 Croissants and 40 Éclairs.
2. Branch B requests 60 Croissants and 20 Cakes.
3. Kitchen dashboard shows pending requests.
4. Kitchen approves requests.
5. System creates production plan.
6. Ingredient check shows butter shortage.
7. Kitchen creates draft PO for butter.
8. Raw material PO is received.
9. Kitchen starts Croissant production.
10. Ingredients decrease.
11. Kitchen completes production with 5 rejected pieces.
12. Finished goods batch is created with expiry.
13. Kitchen dispatches products to Branch A.
14. Branch A receives transfer.
15. Analytics show production cost and waste.
```

---

# 28. Tests and Acceptance Criteria

The feature must include tests. Do not implement as UI-only glue.

## Formula Tests

```text
Given Croissant formula yields 100 pieces
And planned production is 200 pieces
Then required ingredients are exactly 2× formula quantities.
```

## Cost Tests

```text
Given ingredient costs, labor, and overhead
When production is planned
Then planned total cost and unit cost are calculated correctly.
```

## Ingredient Shortage Tests

```text
Given required butter is 12 kg
And kitchen has 8 kg
Then system reports 4 kg shortage
And production order cannot start.
```

## Start Cook Tests

```text
Given enough ingredients
When KitchenManager starts production
Then raw material inventory decreases
And stock movements are created
And order status becomes InProduction.
```

## Complete Cook Tests

```text
Given an InProduction order
When KitchenManager completes it with accepted quantity
Then finished goods inventory increases
And stock batch is created
And expiry date is assigned
And actual unit cost is stored.
```

## Waste Tests

```text
Given rejected quantity > 0
When order is completed
Then production waste record is created
And waste cost is calculated.
```

## Dispatch Tests

```text
Given completed production allocated to Branch A
When KitchenManager creates dispatch
Then stock transfer is created from Main Kitchen to Branch A.
```

## Role Tests

```text
KitchenManager can access production pages.
KitchenManager cannot access POS.
KitchenManager cannot manage unrelated branches.
Cashier cannot access production.
BranchManager can create kitchen request only for own branch.
```

## Historical Cost Snapshot Tests

```text
Given formula/product cost changes after production
Then old production order actual cost remains unchanged.
```

---

# 29. Documentation Updates Required

Update:

```text
PROJECT-STATUS.md
docs/architecture.md
docs/database.md
docs/demo-script.md
README.md
```

Add production section:

```text
Production module:
- Main Kitchen as production center
- KitchenManager role
- Branch production requests
- Production formulas
- Production planning
- Ingredient consumption
- Finished goods batches
- Kitchen waste
- Dispatch to branches
- Production analytics
```

Update thesis claim carefully:

```text
The production module remains deterministic.
All suggested production quantities, ingredient requirements, cost estimates, and shortage warnings are calculated from fixed formulas and rule-based logic.
No AI/ML is used.
```

---

# 30. Recommended Implementation Order

Do not start with dashboard UI.

Correct order:

```text
1. Product and branch classification
2. KitchenManager role and permissions
3. Production formulas
4. Formula cost calculator
5. Branch production requests
6. Production plan
7. Ingredient availability checker
8. Production orders
9. Start Cook / Complete Cook actions
10. Inventory integration: consume ingredients and create output batches
11. Dispatch integration using stock transfers
12. Waste tracking
13. Kitchen dashboard
14. Production analytics
15. Deterministic production decisions
16. Seeder/demo data
17. Tests
18. Docs/demo script
```

Reason:

```text
Database and domain first.
Then workflow.
Then integration.
Then UI.
Then analytics.
Then demo polish.
```

If the implementation starts with UI first, the result will likely be pretty screens with broken business logic.

---

# 31. Minimum Version If Time Is Limited

If time is short, build only this:

```text
1. Main Kitchen branch type
2. KitchenManager role
3. Product types
4. Production formulas
5. Branch production requests
6. Production orders
7. Start Cook consumes ingredients
8. Complete Cook creates finished goods batch
9. Dispatch via stock transfer
10. Kitchen dashboard
```

Temporarily skip:

```text
Production plans
Ingredient reservations
Advanced analytics
Production intelligence rules
Cost variance rules
```

Do not skip formulas.

Without formulas, there is no ingredient calculation and no production cost. That would make the feature fake.

---

# 32. Expected Final Result

When finished, the system should demonstrate this exact business story:

```text
A sales branch is running low on croissants and éclairs.
The BranchManager requests finished goods from the Main Kitchen.
The KitchenManager receives the request.
The system suggests what to produce based on approved requests and deterministic forecast demand.
The system checks flour, butter, sugar, eggs, and packaging.
It shows expected production cost.
The KitchenManager starts the cook.
The system consumes ingredients from Main Kitchen inventory.
The KitchenManager completes production and records accepted/rejected quantities.
The system creates finished goods batches with expiry dates and unit cost.
The KitchenManager dispatches products to branches using stock transfers.
The BranchManager receives the products.
The dashboard shows production output, waste, yield, cost, and fulfillment.
```

This feature connects:

```text
Inventory
Operations
Forecasting
Batches/expiry
Waste
Transfers
Roles/permissions
Dashboards
Decision support
```

It keeps the graduation thesis strong because all production suggestions, ingredient requirements, costs, shortages, and waste alerts are deterministic arithmetic and rule-based logic.

---

# 33. Non-Negotiable Constraints for the AI Coding Agent

The AI coding agent must follow these constraints:

```text
Do not add AI/ML.
Do not build a full recipe/nutrition/cooking-instruction system.
Do not bypass the existing stock movement system.
Do not directly mutate inventory quantities from application services.
Do not create AppStockAlerts.
Do not create repositories for child entities.
Do not put LINQ-heavy query logic in app services.
Do not allow raw materials to appear in POS.
Do not allow KitchenManager to access Cashier POS.
Do not overwrite historical production costs when formulas or product costs change.
Do not create production output without expiry for perishable finished goods.
```

---

# 34. Success Definition

The implementation is successful if:

```text
KitchenManager can operate the production workflow from Production screens.
Branches can request products from the kitchen.
The kitchen can approve demand and produce finished goods.
The system calculates ingredients and cost before production.
Starting production consumes ingredients.
Completing production creates costed batches with expiry.
Waste is recorded and costed.
Finished goods are dispatched to branches using stock transfers.
Branch stock increases after receiving transfers.
Production analytics show output, cost, yield, waste, and fulfillment.
All logic remains deterministic and explainable.
```
