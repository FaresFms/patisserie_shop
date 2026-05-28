# CLAUDE.md — Patisserie Shop Project Guide

> **Read this file completely before writing any code in this project.**

---

## Project Identity

**Name:** Smart Multi-Branch Inventory Management System
**Purpose:** Graduation project — Rule-Based Inventory Decision Support System
**Solution:** `patisserie_shop`
**Framework:** ABP Framework v10.1.1, .NET 10, Blazor Server, PostgreSQL, EF Core 10


---

## Architecture

```
patisserie_shop/
├── src/                              # Main application
│   ├── patisserie_shop.Domain
│   ├── patisserie_shop.Domain.Shared
│   ├── patisserie_shop.Application
│   ├── patisserie_shop.Application.Contracts
│   ├── patisserie_shop.EntityFrameworkCore
│   ├── patisserie_shop.HttpApi
│   ├── patisserie_shop.Blazor              # Entry point
│   └── patisserie_shop.DbMigrator
├── modules/
│   ├── inventory/src/                # Products, Branches, Stock
│   │   ├── Inventory.Domain
│   │   ├── Inventory.Domain.Shared
│   │   ├── Inventory.Application
│   │   ├── Inventory.Application.Contracts
│   │   ├── Inventory.EntityFrameworkCore
│   │   ├── Inventory.HttpApi
│   │   ├── Inventory.HttpApi.Client
│   │   └── Inventory.Installer
│   ├── operations/src/               # POs, Sales, Transfers
│   │   └── (same layer structure)
│   ├── intelligence/src/             # Rules, Decision Logs
│   │   └── (same layer structure)
│   └── Shared/
│       └── patisserie_shop.Blazor.Shared/  # SoftComponents, shared UI
└── test/
```

Each module follows Clean / Onion Architecture:
- **Domain** — Entities, domain services, repository interfaces
- **Domain.Shared** — ETOs, enums, error codes, constants, localization
- **Application.Contracts** — DTOs, AppService interfaces, permissions
- **Application** — AppService implementations, event handlers, mappers
- **EntityFrameworkCore** — DbContext, migrations, EF configuration

---

## DDD Rules — Enforced Strictly

This project follows ABP's Rich Domain Model. Every entity must follow these rules.

### Entity Encapsulation

```csharp
// ✅ CORRECT — Rich entity with private setters and behavior methods
public class AppProduct : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; private set; } = null!;
    public decimal SalePrice { get; private set; }

    protected AppProduct() { } // ORM constructor

    public AppProduct(Guid id, string name, decimal salePrice) : base(id)
    {
        SetName(name);
        SetSalePrice(salePrice);
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: 128);
    }

    public void SetSalePrice(decimal price)
    {
        if (price < 0) throw new BusinessException(InventoryErrorCodes.NegativePrice);
        SalePrice = price;
    }
}

// ❌ WRONG — Anemic entity, public setters, no validation
public class AppProduct : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; set; } = null!;
    public decimal SalePrice { get; set; }
}
```

### Aggregate Root Rules

1. **Aggregate roots own their children.** Child entities (AppStockTransferItem, AppPurchaseOrderItem, AppSaleItem) are accessed ONLY through the root.
2. **Never create IRepository for child entities.** No DbSet for children.
3. **Business logic lives in the entity**, not in AppServices.
4. **AppServices only orchestrate** — they call entity methods, never set properties directly.
5. **Domain events are published from aggregate methods**, not from services.
6. **Reference other aggregates by Id only** — no navigation properties across aggregate boundaries.

```csharp
// ✅ CORRECT — Logic in entity
public void AddItem(Guid productId, int qty, decimal unitPrice)
{
    if (Status != "Draft")
        throw new BusinessException("Cannot add items to a non-draft order");
    if (qty <= 0)
        throw new BusinessException("Quantity must be positive");

    Items.Add(new AppPurchaseOrderItem(GuidGenerator.Create(), Id, productId, qty, unitPrice));
    RecalculateTotal();
}

// ❌ WRONG — Logic in AppService
public async Task AddItemAsync(Guid orderId, Guid productId, int qty)
{
    var order = await _repo.GetAsync(orderId);
    order.Items.Add(new AppPurchaseOrderItem(...)); // Bypasses business rules
    order.TotalAmount = order.Items.Sum(i => i.Subtotal); // Logic outside entity
}
```

### Domain Services (*Manager)

Use domain services ONLY when logic spans multiple aggregates or needs repository queries:

```csharp
public class ProductManager : DomainService
{
    // ✅ Cross-aggregate rule: SKU uniqueness requires querying other products
    public async Task EnsureSkuIsUniqueAsync(string sku, Guid? ignoreId = null) { ... }
}
```

### Constructor Pattern

Every aggregate root must have:
1. A **primary constructor** accepting `Guid id` + required fields, enforcing invariants
2. A **protected parameterless constructor** for EF Core ORM
3. Collections initialized in the primary constructor

```csharp
public class AppPurchaseOrder : FullAuditedAggregateRoot<Guid>
{
    public string PONumber { get; private set; } = null!;
    public ICollection<AppPurchaseOrderItem> Items { get; private set; }

    protected AppPurchaseOrder() { } // ORM

    public AppPurchaseOrder(Guid id, string poNumber, ...) : base(id)
    {
        PONumber = Check.NotNullOrWhiteSpace(poNumber, nameof(poNumber));
        Items = new List<AppPurchaseOrderItem>();
    }
}
```

---

## Entity Base Classes

| Entity | Base Class | Repo? | Notes |
|--------|-----------|-------|-------|
| AppCategory | FullAuditedAggregateRoot<Guid> | Yes | Simple CRUD, soft delete |
| AppSupplier | FullAuditedAggregateRoot<Guid> | Yes | Simple CRUD, soft delete |
| AppProduct | FullAuditedAggregateRoot<Guid> | Yes | SKU unique via ProductManager |
| AppBranch | FullAuditedAggregateRoot<Guid> | Yes | ManagerUserId FK to AbpUsers |
| AppBranchInventory | AuditedAggregateRoot<Guid> | Yes | No soft delete. UpdateStock() fires StockChangedEto |
| AppStockMovement | CreationAuditedAggregateRoot<Guid> | Yes | **IMMUTABLE** — never update/delete |
| AppStockTransfer | FullAuditedAggregateRoot<Guid> | Yes | Status machine, owns Items |
| AppStockTransferItem | Entity<Guid> | **NO** | Child of AppStockTransfer |
| AppPurchaseOrder | FullAuditedAggregateRoot<Guid> | Yes | Status machine, owns Items |
| AppPurchaseOrderItem | Entity<Guid> | **NO** | Child of AppPurchaseOrder |
| AppSale | FullAuditedAggregateRoot<Guid> | Yes | Owns Items |
| AppSaleItem | Entity<Guid> | **NO** | Child of AppSale |
| AppInventoryRule | FullAuditedAggregateRoot<Guid> | Yes | Admin-managed rule bank |
| AppDecisionLog | CreationAuditedAggregateRoot<Guid> | Yes | **IMMUTABLE** — never update/delete. Constructor fires DecisionMadeEto |

---

## Immutable Entities

`AppStockMovement` and `AppDecisionLog` are immutable ledgers:
- Inherit `CreationAuditedAggregateRoot<Guid>` — no modification audit, no soft delete
- **NEVER** call Update() or Delete() on them
- Data is set exclusively through the constructor
- No public setters, no behavior methods that modify state (except AppDecisionLog.Acknowledge which only updates Status)

---

## Event-Driven Architecture

This system is event-driven, not CRUD-based. The core loop:

```
Stock changes (sale, purchase, transfer, manual adjust)
    ↓
AppBranchInventory.UpdateStock(newQty) 
    → calls AddDistributedEvent(new StockChangedEto)
    ↓
ABP Event Bus delivers it
    ↓
StockChangedEventHandler.HandleEventAsync() in Intelligence module
    ↓
DecisionMakerService.EvaluateAsync()
    → loads active AppInventoryRules matching product+branch scope
    → evaluates LowStock and ExcessStock rules
    → creates AppDecisionLog rows for triggered rules
    ↓
AppDecisionLog constructor fires DecisionMadeEto
    → can push to SignalR hub for real-time dashboard
```

### ETOs (Event Transfer Objects)

| ETO | Published By | Handled By | Module |
|-----|-------------|------------|--------|
| StockChangedEto | AppBranchInventory.UpdateStock() | StockChangedEventHandler → DecisionMakerService | Inventory → Intelligence |
| TransferCompletedEto | AppStockTransfer | StockMovementService | Operations |
| PurchaseReceivedEto | AppPurchaseOrder | StockMovementService | Operations |
| SaleRecordedEto | AppSale | InventoryService | Operations |
| DecisionMadeEto | AppDecisionLog constructor | SignalR Hub | Intelligence |

### Publishing Events — Always from Aggregates

```csharp
// ✅ CORRECT — Published inside the aggregate method
public void UpdateStock(int newQty)
{
    var oldQty = QuantityOnHand;
    QuantityOnHand = newQty;
    AddDistributedEvent(new StockChangedEto
    {
        BranchId = BranchId,
        ProductId = ProductId,
        OldQty = oldQty,
        NewQty = newQty
    });
}

// ❌ WRONG — Published from AppService
public async Task AdjustStockAsync(...)
{
    inventory.QuantityOnHand = newQty;
    await _eventBus.PublishAsync(new StockChangedEto { ... }); // Don't do this
}
```

---

## Intelligence Rules Engine

**No AI. No ML. No dynamic expressions. Pure if/else logic.**

### AppInventoryRule Scope

| ProductId | BranchId | Scope |
|-----------|----------|-------|
| null | null | Global — all products, all branches |
| set | null | This product in all branches |
| null | set | All products in this branch |
| set | set | This product in this specific branch |

Higher `Priority` wins when multiple rules match.

### Rule Types

| RuleType | Uses ThresholdValue | Uses ThresholdDays | Evaluated By |
|----------|--------------------|--------------------|--------------|
| LowStock | ✅ (stock qty) | ✗ | StockChangedEventHandler (real-time) |
| ExcessStock | ✅ (stock qty) | ✗ | StockChangedEventHandler (real-time) |
| DeadStock | ✗ | ✅ (days without sale) | Background job (nightly) |
| TransferSuggestion | ✅ | ✗ | Background job (nightly) |

### DecisionMakerService Pattern

```csharp
// Uses ABP's AsyncExecuter — NOT EF Core directly in Domain layer
var queryable = (await _rulesRepo.GetQueryableAsync())
    .Where(r => r.IsActive
        && (r.ProductId == null || r.ProductId == eto.ProductId)
        && (r.BranchId  == null || r.BranchId  == eto.BranchId))
    .OrderByDescending(r => r.Priority);

var rules = await AsyncExecuter.ToListAsync(queryable);
```

**APPROVED DEVIATION:** DecisionMakerService uses `AsyncExecuter.ToListAsync()` instead of `.GetQueryableAsync().Result` — this avoids EF Core leak into Domain layer and deadlock risk. Do not revert.

---

## Schema Decisions (v2)

These decisions are final. Do not revert them.

1. **AppStockAlerts does NOT exist** — merged into AppDecisionLogs which handles both decision output and manager workflow (Status: Pending|Acknowledged|Dismissed|Executed)
2. **AppProducts.DeadStockThresholdDays does NOT exist** — ThresholdDays lives exclusively in AppInventoryRules
3. **Currency nvarchar(3)** exists on AppProducts, AppPurchaseOrders, AppSales — flat column, no Money value object
4. **No Money value object, no StockSnapshot value object** — flat columns only
5. **All 3 modules use the Default connection string** — one PostgreSQL database for everything

---

## Permissions Pattern

Define in Application.Contracts, register in PermissionDefinitionProvider:

```csharp
public static class InventoryPermissions
{
    public const string GroupName = "Inventory";

    public static class Products
    {
        public const string Default = GroupName + ".Products";
        public const string Manage  = Default + ".Manage";
    }
}
```

Two roles:
- **Admin** — gets all permissions
- **BranchManager** — gets Default (read) + specific action permissions (Adjust, Receive, Confirm)

Role-aware UI: show/hide buttons based on permissions, never just by checking role names.

---

## Application Layer Conventions

### AppService Pattern (Clean DDD — no LINQ in services)

App services are **thin orchestrators**. They MUST NOT contain `GetQueryableAsync`,
`AsyncExecuter`, `from … join …`, `.Where(...)`, `.OrderBy(...)`, `.GroupBy(...)`,
or direct entity property writes (`entity.Foo = input.Foo`). A service body should
read like: `manager.X` → `repo.Y` → `entity.Z(...)` → `Map`.

**Anatomy of a write method:**

```csharp
[Authorize(InventoryPermissions.Products.Manage)]
public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductDto input)
{
    var product = await _productRepository.GetAsync(id);

    await _productManager.EnsureReferencesAsync(input.CategoryId, input.DefaultSupplierId);
    await _productManager.ChangeSkuAsync(product, input.SKU);
    product.UpdateInfo(input.CategoryId, input.DefaultSupplierId, input.Name, ...);

    await _productRepository.UpdateAsync(product, autoSave: true);
    return ObjectMapper.Map<AppProduct, ProductDto>(product);
}
```

**Anatomy of a read method:**

```csharp
public async Task<PagedResultDto<ProductDto>> GetListAsync(GetProductsInput input)
{
    var totalCount = await _productRepository.CountFilteredAsync(
        input.Filter, input.CategoryId, input.DefaultSupplierId, input.IsActive);

    var items = await _productRepository.GetFilteredListAsync(
        input.Filter, input.CategoryId, input.DefaultSupplierId, input.IsActive,
        input.Sorting ?? string.Empty, input.SkipCount, input.MaxResultCount);

    return new PagedResultDto<ProductDto>(
        totalCount,
        [.. items.ConvertAll(p => ObjectMapper.Map<AppProduct, ProductDto>(p))]);
}
```

### Where each concern lives

| Concern | Lives in | Why |
|---|---|---|
| Filter LINQ, joins, sort-key translation | Custom `I{Entity}Repository` (Domain) + `{Entity}Repository` (EF Core) | Persistence detail — never leaks into the service |
| Name/SKU uniqueness, cross-aggregate existence checks ("category must exist") | `{Entity}Manager : DomainService` | Invariants that need repository queries |
| Field mutations (Name, Description, prices, etc.) | Behavior methods on the entity (`UpdateInfo`, `SetName`, `Activate`) | Encapsulation — setters are `private`/`internal` |
| Concurrency stamp checks | Domain manager (e.g. `BranchInventoryManager.EnsureConcurrencyStamp`) | Treats stale-data as a domain invariant, not a service `if` |
| Branch-scoped authorization | `BranchAccessChecker` (already exists) | Single rule: ManageAll OR manager-of-branch |
| Auto-generated REST endpoints | `IApplicationService` interface in `*.Application.Contracts` | See "Auto API controllers" above |

### Encapsulating entities

Bare `public set` on entities is forbidden. Pattern:

- `private set` for fields the app service never touches directly.
- `internal set` for fields a domain manager mutates (e.g. `SKU`, `Name`).
- `internal` constructor — only the manager constructs the aggregate.
- Behavior methods (`UpdateInfo`, `SetName`, `Activate`, `UpdateStock`) validate
  inputs and raise events.

### Custom repositories — required when

You MUST define `I{Entity}Repository : IRepository<T, Guid>` in `Inventory.Domain`
(or the owning module's Domain) and implement it in the EF Core layer whenever an
app service needs any of:

- Filtering by multiple optional parameters
- Joining across aggregates (returning typed read-models, never anonymous types)
- `GroupBy` aggregations
- Sort-key translation (mapping public DTO field names to query expressions)
- Lookup queries (top-N active rows)

Register via `options.AddRepository<TEntity, TRepo>()` in the module's
`ConfigureServices`. Return typed read-models — define a small class like
`BranchInventoryWithProduct { Inventory; Product; }` next to the interface in
Domain. **No anonymous types or tuples crossing the repository boundary.**

### Manager rules

- One manager per aggregate that has invariants worth enforcing.
- Constructors of the entity are `internal` — only the manager calls them.
- Manager methods follow the shape `CreateAsync(...)`, `ChangeNameAsync(entity, newName)`,
  `EnsureReferencesAsync(...)`, `EnsureConcurrencyStamp(entity, stamp)`.
- Throw `BusinessException(InventoryErrorCodes.X)` with a constant — never magic
  strings. Add new codes to `Inventory.Domain.Shared/InventoryErrorCodes.cs`.

### Mapping with Mapperly

- All entity↔DTO mappings live in `Inventory.Application/InventoryApplicationMappers.cs`.
- For join-row DTOs (e.g. `BranchInventoryDto` carries `ProductName`), use
  `[MapperIgnoreTarget(nameof(Dto.X))]` on the partial method for fields populated
  manually from the joined entity, then overlay them after `ObjectMapper.Map(...)`.

### Reference implementations

See `BranchAppService` + `BranchManager`, `BranchInventoryAppService` +
`BranchInventoryRepository`, `CategoryAppService` + `CategoryManager`, and
`ProductAppService` + `ProductManager` for the canonical shape.

### DTO Naming

```
CreateProductDto     — input for creation
UpdateProductDto     — input for update
ProductDto           — output with all fields
GetProductsInput     — extends PagedAndSortedResultRequestDto, filter fields
ProductLookupDto     — lightweight, for dropdowns (Id, Name, essentials only)
```

### Lookup Endpoints

Every entity that appears in dropdowns on other screens needs a `GetLookupAsync()`:
- Returns active items only
- Ordered by Name
- Capped at 100-200 items
- Lightweight DTO (Id + Name + maybe one more field)
- No pagination needed

---

## Blazor UI Conventions

### SoftComponents

All UI uses shared SoftComponents from `modules/Shared/patisserie_shop.Blazor.Shared/Components/SoftComponents/`:

```
SoftCard, SoftCardHeader, SoftCardContent
SoftDataGrid, SoftTable
SoftButton, SoftIconButton
SoftTextField, SoftNumericField, SoftSelect, SoftLookupSelect
SoftDialog, SoftForm
SoftStack, SoftGrid, SoftGridItem
SoftChip, SoftAlert, SoftTooltip
SoftText, SoftIcon, SoftDivider
SoftCheckBox, SoftRadioGroup
SoftProgressLinear, SoftProgressCircular
SoftSkeleton
```
always use those component and if you want to use mud blazor component and you did not find it make a soft component for it 
### Glass Theme

The app uses a frosted glass visual design. Every card, modal, and panel must use the glass CSS variables defined in `glass-theme.css`:


### Page Structure

Every page follows this pattern:

```razor
@page "/module/feature"
@attribute [Authorize(ModulePermissions.Feature.Default)]
@inherits patisserie_shopComponentBase

<SoftCard>
    <SoftCardHeader>
        <!-- Title + action buttons -->
    </SoftCardHeader>
    <SoftCardContent>
        <!-- Filter bar -->
        <!-- SoftDataGrid with ServerData -->
    </SoftCardContent>
</SoftCard>

<!-- Create/Edit modal -->
<!-- Delete confirmation modal -->
```

### Modal Pattern

Two styles used in this project:

**Simple modal** (Categories, Suppliers, Products) — inline form for CRUD:
```razor
<div class="soft-modal-backdrop">
    <div class="soft-modal-container soft-modal-container--lg">
        <SoftCard Elevation="24" Class="soft-modal-card">
            <!-- header, form, footer -->
        </SoftCard>
    </div>
</div>
```

**Detail modal** (Purchase Orders, Sales) — read-only detail view + actions:
- Opened by clicking a row in the grid
- Shows full entity details with items table
- Status-aware action footer
- Sub-modals for adding items / receiving items

### Status Chips

Consistent color coding across the app:
- **Green filled** — Confirmed, Received, Active, Acknowledged
- **Blue filled** — Approved, In Transit
- **Orange filled** — Pending, Partial, Draft (with activity)
- **Grey outlined** — Draft (inactive)
- **Red outlined** — Cancelled, Dismissed

### Menu Registration

All pages registered in `patisserie_shopMenuContributor.cs` under module groups:
- Inventory: Categories, Suppliers, Products, Branches, Branch Inventory, Stock Movements
- Operations: Purchase Orders, Sales, Stock Transfers
- Intelligence: Inventory Rules, Decision Log

---

## What NOT to Do

1. **Do not create AppStockAlerts** — it does not exist in this project
2. **Do not add DeadStockThresholdDays to AppProduct** — use AppInventoryRule.ThresholdDays
3. **Do not create IRepository for child entities** — AppStockTransferItem, AppPurchaseOrderItem, AppSaleItem
4. **Do not call Update() or Delete() on AppStockMovement or AppDecisionLog**
5. **Do not put business logic in AppServices** — entity methods enforce invariants
6. **Do not put LINQ in AppServices** — no `GetQueryableAsync`, no `AsyncExecuter`, no `.Where`/`.Join`/`.OrderBy`/`.GroupBy`. Move it to a custom repository in the EF Core layer.
7. **Do not write entity properties from AppServices** — `entity.Name = input.Name` is forbidden. Call an entity behavior method (`entity.UpdateInfo(...)`) or a manager method.
8. **Do not publish events from AppServices** — always from aggregate methods
9. **Do not reference EF Core in Domain projects** — use AsyncExecuter (Domain services) or define an `I{Entity}Repository` abstraction
10. **Do not create new migrations without being explicitly asked**
11. **Do not modify existing entity files unless explicitly asked**
12. **Do not add AI/ML features** — this is a deterministic rule-based system

---

