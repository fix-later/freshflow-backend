# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Status

This repository is in the **pre-implementation / documentation phase**. The `docs/` directory contains the complete design suite produced by the master architect agent. No `.NET` solution or source code exists yet. The next action is scaffolding the solution structure described in `docs/05-implementation-plan.md`.

---

## Commands (once solution is scaffolded)

```bash
# Build entire solution
dotnet build FreshFlow.sln

# Run all tests
dotnet test FreshFlow.sln

# Run a single test project
dotnet test tests/Unit/FreshFlow.Auth.UnitTests/

# Run a single test by name
dotnet test tests/Unit/FreshFlow.Auth.UnitTests/ --filter "FullyQualifiedName~LoginCommandHandlerTests"

# Run integration tests (requires Docker Compose to be up)
dotnet test tests/Integration/FreshFlow.IntegrationTests/

# Add a new EF Core migration
dotnet ef migrations add <MigrationName> \
  --project src/FreshFlow.Infrastructure.Persistence \
  --startup-project src/FreshFlow.API

# Apply migrations
dotnet ef database update \
  --project src/FreshFlow.Infrastructure.Persistence \
  --startup-project src/FreshFlow.API

# Start local stack
docker compose up -d

# Format check (CI gate — must pass before commit)
dotnet format FreshFlow.sln --verify-no-changes
```

---

## Architecture

### Pattern: Microservice-Ready Modular Monolith

Single deployable ASP.NET Core host (`FreshFlow.API`) that wires 7 self-contained modules. Each module has its own `.Domain`, `.Application`, and `.Infrastructure` projects, making extraction to a standalone microservice a 3-file operation (new `Program.cs`, swap MediatR handlers → message bus consumers, split `AppDbContext`).

### Solution Layout

```
src/
  Shared/
    FreshFlow.SharedKernel/       ← BaseEntity, AggregateRoot, Result<T>, ICommand/IQuery
    FreshFlow.Contracts/          ← Integration event records (cross-module DTOs only)
  Modules/
    Auth/        Pricing/        Orders/       Logistics/
    Hub/         Analytics/      Notifications/
      Each: {Module}.Domain / {Module}.Application / {Module}.Infrastructure
  FreshFlow.Infrastructure.Persistence/   ← Shared AppDbContext + all EF Migrations
  FreshFlow.API/                          ← Host: controllers, SignalR hubs, Program.cs
tests/
  Unit/   FreshFlow.{Module}.UnitTests/   (one per module)
  Integration/  FreshFlow.IntegrationTests/
```

### Dependency Rules (enforced by .csproj references)

```
Domain         →  SharedKernel only
Application    →  Domain + SharedKernel + Contracts
Infrastructure →  Application + EF Core/Redis packages
API (host)     →  all Infrastructure projects (DI wiring only)
```

No module's `Domain` or `Application` may reference another module's projects.

### Cross-Module Communication

**Domain Events** (internal): raised on the aggregate, handled within the same module via MediatR `INotificationHandler<T>`. One handler per domain event publishes the corresponding **Integration Event** to `FreshFlow.Contracts`.

**Integration Events** (cross-module): records in `FreshFlow.Contracts`, consumed by other modules' `EventHandlers/` via MediatR `INotificationHandler<T>`. Example flow:

```
Order.Create() raises OrderCreatedDomainEvent
  → OrderCreatedDomainEventHandler (Orders.Application) publishes OrderCreatedIntegrationEvent
    → Logistics.Application: OrderCreatedIntegrationEventHandler creates a delivery record
    → Notifications.Application: OrderCreatedIntegrationEventHandler persists a notification
```

### Database

One shared `AppDbContext` in `FreshFlow.Infrastructure.Persistence`. The context uses `ApplyConfigurationsFromAssembly()` to auto-discover all `IEntityTypeConfiguration<T>` implementations across module Infrastructure projects — the `AppDbContext` class itself lists no `DbSet<T>` properties explicitly.

All PKs are `UUID` (`gen_random_uuid()`). All mutable tables have `deleted_at TIMESTAMPTZ` for soft delete. `price_snapshots` and `refresh_tokens` are append-only (no `deleted_at`).

`price_snapshots` is range-partitioned by month on `recorded_at`. A background job (`PartitionMaintenanceJob`) creates the next month's partition on the 25th of each month.

### Module Registration Pattern

Each module's `Infrastructure` project exposes one extension method:

```csharp
// src/Modules/Auth/FreshFlow.Auth.Infrastructure/DependencyInjection.cs
public static IServiceCollection AddAuthModule(
    this IServiceCollection services, IConfiguration config) { ... }
```

`Program.cs` chains all of them: `services.AddAuthModule(config).AddPricingModule(config)...`

### Real-Time (SignalR + Redis)

Three hubs in `FreshFlow.API/SignalR/`: `PricingHub`, `OrderHub`, `DeliveryHub`. Redis is the SignalR scale-out backplane (StackExchange.Redis). Clients authenticate via JWT in the `access_token` query string during negotiate.

Group naming: `market:{marketId}`, `restaurant:{restaurantId}`, `kiosk:{marketId}`, `admin:orders`, `admin:delivery`.

Broadcast calls originate in `{Module}.Infrastructure/Realtime/` services that implement interfaces defined in `{Module}.Application/Abstractions/` (e.g., `IPricingBroadcastService`).

---

## Key Coding Patterns

### Result pattern (no business-rule exceptions from services)

```csharp
// Service returns Result<T>, never throws for business rules
public async Task<Result<OrderResponseDto>> CreateOrderAsync(
    CreateOrderCommand command, CancellationToken ct = default)

// Controller maps result to HTTP
var result = await _sender.Send(command, ct);
return result.IsSuccess
    ? CreatedAtAction(..., ApiResponse.Ok(result.Value))
    : result.Error.ToActionResult();
```

### C# conventions

- PascalCase: classes, methods, properties, filenames
- Async methods always suffixed `Async`
- Interfaces prefixed `I`
- DTOs use `record` types (immutable, value equality)
- FluentValidation for all request DTOs — co-located with the command: `Commands/Login/LoginCommandValidator.cs`
- No raw SQL except in `Analytics.Infrastructure` for complex aggregations (use `FromSqlRaw` with parameterized inputs only — no string interpolation)

### Angular

- `OnPush` change detection everywhere, `async` pipe in templates, no manual `.subscribe()`
- `inject()` function for DI in standalone components, no `any` type

### React Native

- Functional components + hooks only, all API calls through `api.service.ts`, tokens in `SecureStore` not `AsyncStorage`

---

## Git

| Branch | Purpose |
|--------|---------|
| `main` | Production-ready; PR-only merges |
| `dev` | Integration branch |
| `feature/T001-solution-structure` | Branches named by Task ID |
| `fix/T016-price-race-condition` | Bug fixes named by Task ID |

Commit format: `feat(auth): implement JWT refresh token rotation (T010)`

---

## Key Docs

| File | When to Read |
|---|---|
| `docs/01-requirements-spec.md` | Understanding what a feature must do; acceptance criteria |
| `docs/02-system-architecture.md` | Module responsibilities, SignalR design, caching strategy |
| `docs/03-database-schema.md` | Full DDL, index strategy, Redis key patterns |
| `docs/04-api-design.md` | Endpoint specs, request/response shapes, RBAC matrix |
| `docs/05-implementation-plan.md` | Task list, critical path, full folder structure, coding standards |
| `docs/REVIEW-REPORT.md` | Known gaps and 3 open design decisions (W-001, W-002, W-003) |

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
at `specs/003-auth-module/plan.md`.
<!-- SPECKIT END -->
