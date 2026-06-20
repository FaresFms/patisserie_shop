# AGENTS.md — Patisserie Shop Project Guide

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

Always use these. If a needed MudBlazor component isn't wrapped, create a new Soft component for it (don't reach for the raw Mud one inline).

**Class-passthrough gotcha:** Most Soft components inherit their MudBlazor counterpart directly — `SoftCard : MudCard`, `SoftTextField : MudTextField<T>`, `SoftDataGrid : MudDataGrid<T>`, `SoftNumericField : MudNumericField<T>`, `SoftSelect : MudSelect<T>` — so `Class="..."` passes through to the underlying element. The exception is **`SoftLookupSelect`**: it's a composite component with a fixed parameter set and **no `Class` parameter**. When you need to attach a CSS class to it (e.g. `filter-chip` for the toolbar pattern below), wrap it in `<div class="...">` instead — the page-flat CSS uses descendant selectors so the styling still lands.

### Theme (warm cream + terracotta)

The active theme lives in `modules/Shared/patisserie_shop.Blazor.Shared/wwwroot/Themes/glass-theme.css`. The filename is preserved from the original liquid-glass design; the content is now the warm palette. Use these CSS custom properties instead of hex literals or `--glass-*` variables (which no longer exist):

```
Surfaces:  --bg, --bg-2, --surface, --surface-2, --hover, --selected
Borders:   --bd-1 (hairline), --bd-2 (light), --bd-3 (medium)
Text:      --fg-1 (primary), --fg-2 (secondary), --fg-3 (muted), --fg-4 (faint)
Accent:    --accent (terracotta), --accent-hover, --accent-soft, --accent-line, --accent-fg
Status:    --success, --warn, --danger, --green-text, --amber-text
Shadows:   --shadow-sm, --shadow-md, --shadow-modal
Radii:     --r-sm, --r-md, --r-lg
Fonts:     --font-sans (Inter), --font-serif (Source Serif 4), --font-mono (JetBrains Mono)
```

Dark mode is opt-in via `<html data-theme="dark">`. The `wwwroot/js/theme-bridge.js` helper writes and restores this attribute when the top-bar moon icon toggles — the warm dark tokens flip via the `html[data-theme="dark"]` selector inside the same CSS file, with no Razor change required. The toggle handler in `MainLayout.razor` calls `applyDataTheme(_isDarkMode)` after flipping the bool.

### Data-list page pattern (`page-flat`)

Every data-list page (Products, Categories, Suppliers, Branches, Branch Inventory, Stock Movements, Purchase Orders, Sales, Stock Transfers, Inventory Rules, Decision Log) follows the **page-flat** pattern: no outer card chrome, hairline-separated rows, dashed-pill filter chips, hover-revealed row actions.

```razor
@page "/module/feature"
@attribute [Authorize(ModulePermissions.Feature.Default)]
@inherits patisserie_shopComponentBase

<SoftCard Elevation="2" Class="pa-2 page-flat">
    <div class="page-head">
        <div>
            <h1 class="page-title">@L["Feature"]</h1>
            <div class="page-sub">Short descriptive subtitle.</div>
        </div>
        <div class="page-head-actions">
            <SoftButton Variant="Variant.Filled" Color="Color.Primary"
                        StartIcon="@Icons.Material.Filled.Add" OnClick="OpenCreateDialog">
                @L["NewFeature"]
            </SoftButton>
        </div>
    </div>

    <SoftCardContent>
        <div class="toolbar">
            <SoftTextField @bind-Value="_filter" Placeholder="@L["Filter"]"
                           Variant="Variant.Outlined" Margin="Margin.Dense"
                           Adornment="Adornment.Start"
                           AdornmentIcon="@Icons.Material.Filled.Search"
                           Class="toolbar-search" />

            <div class="filter-chip">
                <SoftLookupSelect ... />   @* class lives on the wrapper, not the component *@
            </div>

            <div class="toolbar-right">
                <SoftButton Variant="Variant.Text" Color="Color.Primary"
                            StartIcon="@Icons.Material.Filled.Refresh" OnClick="ReloadAsync">
                    @L["Refresh"]
                </SoftButton>
            </div>
        </div>

        <SoftDataGrid ...>
            <Columns>
                ...
                <TemplateColumn Title="@L["Actions"]" Sortable="false">
                    <CellTemplate>
                        <span class="row-actions">
                            <SoftIconButton Icon="@Icons.Material.Filled.Edit" ... />
                            <SoftIconButton Icon="@Icons.Material.Filled.Delete"
                                            Color="Color.Error" ... />
                        </span>
                    </CellTemplate>
                </TemplateColumn>
            </Columns>
        </SoftDataGrid>
    </SoftCardContent>
</SoftCard>
```

Marker classes (defined in Sections 7–8 of `glass-theme.css`):

| Class | Where it goes | Effect |
|---|---|---|
| `.page-flat` | outer `SoftCard` | makes the card transparent, zero padding/border/shadow |
| `.page-head` / `.page-title` / `.page-sub` / `.page-head-actions` | top banner | serif `<h1>` title, muted subtitle, right-aligned action buttons |
| `.toolbar`, `.toolbar-right` | filter row | flex layout with hairline top/bottom borders |
| `.toolbar-search` | search `SoftTextField` | borderless inline input, focused-ring on accent |
| `.filter-chip` | wrapper `<div>` around a `SoftLookupSelect` / `MudSelect` | renders as a small dashed pill with an 11px uppercase label; hides any `AdornmentIcon` |
| `.row-actions` | `<span>` wrapping table-row icon buttons | `opacity: 0` by default, `1` on row hover |
| `.stats`, `.stat-label`, `.stat-value` | optional summary row under the head | flat tabular-num numbers for dashboards (e.g. Decision Log) |

**Adornments inside dialogs:** prefer `AdornmentText="$"` over decorative `AdornmentIcon` on currency / numeric fields. Reserve `AdornmentIcon` for genuinely functional icons (search magnifier, calendar picker). Decorative leading icons on text fields, selects, and lookups have been swept out of every dialog.

### Modal pattern (custom inline modals)

Modals in this project are **not** `MudDialog` / `DialogService.Show<>()`. They're hand-rolled overlay markup using the `glass-modal-*` class system (the class names predate the warm theme; the CSS aliases them to the new tokens). Render them conditionally in the same `.razor` file as the page:

```razor
@if (_dialogVisible)
{
    <div class="glass-modal-backdrop" @onclick="CloseDialog">
        <div class="glass-modal-container glass-modal-container--lg" @onclick:stopPropagation="true">
            <SoftCard Elevation="24" Class="glass-modal">
                <SoftCardHeader Class="pa-4">
                    <CardHeaderContent>
                        <SoftStack Row="true" AlignItems="AlignItems.Center" Spacing="2">
                            <SoftIcon Icon="@Icons.Material.Filled.AddCircle"
                                      Color="Color.Primary" Size="Size.Medium" />
                            <SoftText Typo="Typo.h6">@L["NewItem"]</SoftText>
                        </SoftStack>
                    </CardHeaderContent>
                    <CardHeaderActions>
                        <SoftIconButton Icon="@Icons.Material.Filled.Close"
                                        Size="Size.Small" OnClick="CloseDialog" />
                    </CardHeaderActions>
                </SoftCardHeader>
                <SoftDivider />
                <SoftCardContent Class="pa-4 glass-modal-body">
                    @* form body *@
                </SoftCardContent>
                <SoftDivider />
                <div class="pa-3 d-flex justify-end" style="gap: 8px;">
                    <SoftButton Variant="Variant.Text" OnClick="CloseDialog">@L["Cancel"]</SoftButton>
                    <SoftButton Variant="Variant.Filled" Color="Color.Primary"
                                StartIcon="@Icons.Material.Filled.Save" OnClick="SaveAsync">
                        @L["Save"]
                    </SoftButton>
                </div>
            </SoftCard>
        </div>
    </div>
}
```

Container sizes: `glass-modal-container--sm` (420 px), default (560 px), `--md` (640 px), `--lg` (880 px).

Two functional variants of this skeleton are in use:

- **Form modal** (Categories, Suppliers, Products, Branches, New Purchase Order, New Sale, New Stock Transfer, New Rule, Adjust Stock, Initialize Product) — inline create/edit form with Save / Cancel footer.
- **Detail modal** (`PurchaseOrderDetailModal.razor`, `SaleDetailModal.razor`, `StockTransferDetailModal.razor`) — separate `.razor` component opened by row click; shows full entity detail + items table + status-aware action footer; may host its own sub-modals (e.g. add item, receive items).

The dialog header icon is always a `SoftIcon` with `Color="Color.Primary"`; the warm theme CSS recolors it to terracotta automatically. Don't hardcode `Color.Info` / hex colors / inline styles on dialog title icons — the override won't reach them.

### Status chips

Used in tables, detail modals, and decision-log cards:

- **Green filled** — Confirmed, Received, Active, Acknowledged
- **Blue filled** — Approved, In Transit, Executed
- **Orange filled** — Pending, Partial
- **Grey outlined** — Draft (inactive), Dismissed
- **Red outlined / red filled** — Cancelled, Out of stock, Dead-stock alert

On the Decision Log dashboard the chip variant doubles as a state-loudness signal: **Pending** chips are filled (loud); **Acknowledged / Dismissed / Executed** chips are outlined (quieter), and the card's left-border color carries the decision-type accent (amber for LowStock, blue for ExcessStock, red for DeadStock, teal for Transfer).

### Sidebar nav glow

Nav-link icons in the sidebar drawer render with a soft terracotta drop-shadow so the active page glows. The effect is purely CSS (Section 8 of the theme file) — every `<MudNavLink>` inside the drawer gets the treatment automatically; no Razor change needed.

### Menu registration

All routed pages are registered in `src/patisserie_shop.Blazor/Menus/patisserie_shopMenuContributor.cs` under the three module groups:

- **Inventory** — Dashboard, Categories, Suppliers, Products, Branches, Branch Inventory, Stock Movements
- **Operations** — Purchase Orders, Sales, Stock Transfers
- **Intelligence** — Inventory Rules, Decision Log

Each menu item is gated with `.RequirePermissions(...)` against the appropriate `*Permissions.*.Default` constant, so BranchManagers (who lack `Intelligence.Rules.Default`) don't see the Inventory Rules entry at all. Menu IDs and routes live in `Menus/patisserie_shopMenus.cs`.

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

