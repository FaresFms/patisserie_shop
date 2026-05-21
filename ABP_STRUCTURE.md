# Patisserie Shop - ABP Framework Architecture

## Overview
This is an **ABP (Abp.io) Framework v10.1.1** based Blazor Server application with a modular architecture. The solution uses .NET 10.0 and is organized into a main application with three separate business modules.

## Solution Structure

### Main Application (`/src`)
The core application containing domain logic, business services, data persistence, and UI layer. always use SoftComponents from the shared project model

#### Projects:
- **patisserie_shop.Domain** - Domain entities, aggregates, and domain services
- **patisserie_shop.Domain.Shared** - Shared DTOs and constants used across layers
- **patisserie_shop.Application** - Application services, DTOs, and business logic orchestration
- **patisserie_shop.Application.Contracts** - Application service interfaces and contracts (consumed by clients)
- **patisserie_shop.EntityFrameworkCore** - EF Core DbContext, migrations, and data repository implementations
- **patisserie_shop.HttpApi** - REST API controllers for HTTP requests
- **patisserie_shop.HttpApi.Client** - Generated HTTP client proxies for remote consumption
- **patisserie_shop.Blazor** - Blazor Server UI (main entry point)
- **patisserie_shop.DbMigrator** - CLI tool for running database migrations
- **patisserie_shop.TestBase** - Shared test infrastructure and fixtures

#### Testing Projects:
- patisserie_shop.Domain.Tests
- patisserie_shop.Application.Tests
- patisserie_shop.EntityFrameworkCore.Tests
- patisserie_shop.HttpApi.Client.ConsoleTestApp

---

## Modules

The application is extended with three independently developed **ABP Modules**. Each module follows the same layered architecture as the main application.

### 1. Inventory Module (`/modules/inventory/src`)
Manages product inventory, stock levels, and stock transactions.

**Projects:**
- Inventory.Domain.Shared
- Inventory.Domain
- Inventory.Application.Contracts
- Inventory.Application
- Inventory.EntityFrameworkCore
- Inventory.HttpApi
- Inventory.HttpApi.Client
- Inventory.Installer (module configuration and seeding)

---

### 2. Operations Module (`/modules/operations/src`)
Handles operational workflows, order management, and operational processes.

**Projects:**
- Operations.Domain.Shared
- Operations.Domain
- Operations.Application.Contracts
- Operations.Application
- Operations.EntityFrameworkCore
- Operations.HttpApi
- Operations.HttpApi.Client
- Operations.Installer

---

### 3. Intelligence Module (`/modules/intelligence/src`)
Analytics, reporting, and business intelligence functionality.

**Projects:**
- Intelligence.Domain.Shared
- Intelligence.Domain
- Intelligence.Application.Contracts
- Intelligence.Application
- Intelligence.EntityFrameworkCore
- Intelligence.HttpApi
- Intelligence.HttpApi.Client
- Intelligence.Installer

---

## Architecture Layers

Each module and the main application follow the **Clean Architecture / Onion Architecture** pattern with these layers:

### 1. **Domain Layer** (Domain)
- **Responsibility**: Core business logic, entities, and domain services
- **Dependencies**: No external dependencies (except ABP framework)
- **Examples**: Product entity, InventoryManager domain service
- **Location**: `*.Domain.csproj`

### 2. **Application Layer** (Application)
- **Responsibility**: Orchestrate domain operations, business rules enforcement
- **Dependencies**: Domain layer + ABP Application contracts
- **Examples**: CreateProductAppService, InventoryService
- **Location**: `*.Application.csproj`

### 3. **Application Contracts** (Application.Contracts)
- **Responsibility**: Define service interfaces and DTOs for external consumers
- **Dependencies**: None (only DTOs, interfaces, constants)
- **Usage**: Implemented by Application layer, consumed by presentation and HTTP clients
- **Location**: `*.Application.Contracts.csproj`

### 4. **Infrastructure Layer** (EntityFrameworkCore)
- **Responsibility**: Data access, ORM configuration, repository implementations
- **Dependencies**: Domain + EF Core + ABP EntityFrameworkCore
- **Technology**: PostgreSQL (configured in appsettings)
- **Location**: `*.EntityFrameworkCore.csproj`

### 5. **Presentation Layer** (Blazor)
- **Responsibility**: UI, user interactions, API integration
- **Technology**: Blazor Server with LeptonX Lite theme
- **Features**: Authentication (OpenIddict), authorization, localization
- **Location**: `patisserie_shop.Blazor.csproj`

### 6. **HTTP API Layer** (HttpApi)
- **Responsibility**: REST API controllers, API routing
- **Auto-generated**: Controllers are auto-generated from Application services via conventions
- **Location**: `*.HttpApi.csproj`

---
