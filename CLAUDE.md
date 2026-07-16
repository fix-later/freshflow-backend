# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Status

Implemented and under active feature work. 9 modules, 52 EF migrations, unit + integration suites.
Work is tracked in Jira (`SCRUM-xxx`); per-epic working docs live in `docs/features/<epic>/`.

---

## Commands

The solution file is **`FreshFlow.slnx`** (XML solution format). There is no `.sln`.

```bash
# Build entire solution
dotnet build FreshFlow.slnx

# Run all tests
dotnet test FreshFlow.slnx

# Run a single test project
dotnet test tests/Unit/FreshFlow.Auth.UnitTests/

# Run a single test by name
dotnet test tests/Unit/FreshFlow.Auth.UnitTests/ --filter "FullyQualifiedName~LoginCommandHandlerTests"

# Integration tests — spin up their own PostgreSQL via Testcontainers (postgres:16-alpine).
# Docker must be running, but `docker compose up` is NOT required.
dotnet test tests/Integration/FreshFlow.IntegrationTests/
dotnet test tests/Integration/FreshFlow.IntegrationTests/ --filter "FullyQualifiedName~Analytics"

# Add a new EF Core migration
dotnet ef migrations add <MigrationName> \
  --project src/FreshFlow.Infrastructure.Persistence \
  --startup-project src/FreshFlow.API

# Apply migrations
dotnet ef database update \
  --project src/FreshFlow.Infrastructure.Persistence \
  --startup-project src/FreshFlow.API

# Start local stack (Postgres/Redis for running the API — not needed for tests)
docker compose up -d

# Format check (CI gate — must pass before commit)
dotnet format FreshFlow.slnx --verify-no-changes
```

After a one-shot `dotnet` command, run `dotnet build-server shutdown` to avoid leaving idle
MSBuild worker processes behind.

---

## Architecture

### Pattern: Microservice-Ready Modular Monolith

Single deployable ASP.NET Core host (`FreshFlow.API`) that wires 9 self-contained modules. Each
module has its own `.Domain`, `.Application`, and `.Infrastructure` projects.

### Solution Layout

```
src/
  Shared/
    FreshFlow.SharedKernel/       ← BaseEntity, AggregateRoot, Result<T>, ICommand/IQuery
    FreshFlow.Contracts/          ← Integration event records (cross-module DTOs only)
  Modules/
    Auth/     Catalog/   Pricing/   Orders/    Procurement/
    Logistics/  Notifications/  Hub/   Analytics/
      Each: {Module}.Domain / {Module}.Application / {Module}.Infrastructure
  FreshFlow.Infrastructure.Persistence/   ← Shared AppDbContext + all EF Migrations
  FreshFlow.Infrastructure.Media/         ← Cloudinary signed upload (shared, not a module)
  FreshFlow.API/                          ← Host: controllers, Program.cs
    Assistant/                            ← AI assistant orchestrator (lives in the host, NOT a module)
tests/
  Unit/   FreshFlow.{Module}.UnitTests/   (+ Assistant, Infrastructure.Media, Infrastructure.Persistence)
  Integration/  FreshFlow.IntegrationTests/
```

### Dependency Rules (enforced by .csproj references)

```
Domain         →  SharedKernel only
Application    →  Domain + SharedKernel + Contracts
Infrastructure →  Application + Persistence + EF Core/Redis packages
API (host)     →  all Infrastructure projects (DI wiring only)
```

**No module's `Domain` or `Application` may reference another module's projects.** This is absolute.
Consequences worth knowing before you plan a cross-module read:

- You cannot use another module's enum. Re-declare the values locally as string constants (see
  `GetOrderMetricsQueryHandler` in Analytics).
- Cross-module reads go through a **keyless EF Row seam** (below), never a project reference.

### Cross-Module Reads: the keyless Row seam

To read another module's table, define a **keyless row** mapped with `ToSqlQuery` — never
`ToTable`, which would collide with the owning module's model. Trio pattern, all in
`{Module}.Infrastructure/CrossModule/`:

```
XxxRow.cs               ← POCO, init-only props, PascalCase
XxxRowConfiguration.cs  ← HasNoKey() + ToSqlQuery("""SELECT ... FROM other_table WHERE deleted_at IS NULL""")
XxxReader.cs            ← db.Set<XxxRow>().AsNoTracking() + LINQ
```

`ToSqlQuery` does **not** execute on the EF InMemory provider, so a seam is only actually proven by
an **integration test** against real Postgres. Prefer extending an existing seam over adding a
second one to the same table.

New keyless Rows **do** enter `AppDbContextModelSnapshot`. `DesignTimeDbContextFactory.cs` calls
`ForceLoadModuleAssemblies()` — every module Infrastructure assembly must be listed there, or the
snapshot silently drifts and another module's next migration erases your rows.

### ⚠️ Column casing is NOT consistent — never guess

Two conventions coexist, and some tables **mix both within one table**:

| Table family | Convention |
|---|---|
| `procurement_*`, `price_snapshots`, `hub_*`, `deliveries`, `delivery_routes`, `vehicles` | fully `snake_case` |
| `orders`, `order_items` | **mixed** — `"Id"`, `"RestaurantId"`, `"Status"`, `"CreatedAt"`, `"Quantity"` are quoted PascalCase, but `deleted_at` / `confirmed_receipt_at` are snake_case |
| `market_products` | **mixed** — `"Id"`, `"ProductId"`, `"MarketId"` PascalCase but `"deleted_at"` snake_case |
| `products`, `product_categories`, `markets` | PascalCase **including** `"DeletedAt"` |
| `restaurants`, `delivery_addresses` | **mixed** — `"Id"`, `"Name"`, `"RestaurantId"`, `"IsDefault"`, `"DeletedAt"` PascalCase but `status` is snake_case (values lowercase, e.g. `'active'`) |

Enum-ish string values are **not** consistent either: `orders."Status"` is PascalCase
(`Draft`/`Confirmed`/…), `deliveries.status` is lowercase (`pending`/`delivered`/`failed`), and
`hub_*.status` is SCREAMING_CASE (`ARRIVED_AT_HUB`, `CHECKED_OUT`). Check the entity's constants.

Not every table has soft-delete: **`order_items` has none** (`OrderItemConfiguration.cs` calls
`builder.Ignore(i => i.DeletedAt)`); `price_snapshots` / `refresh_tokens` are append-only.
Filtering `deleted_at` on those fails at runtime.

Always confirm against the module's `*Configuration.cs` or copy an existing seam. A wrong quoted
identifier fails at runtime, not build.

### Cross-Module Communication

**Domain Events** (internal): raised on the aggregate, handled within the same module via MediatR
`INotificationHandler<T>`. One handler per domain event publishes the corresponding **Integration
Event** to `FreshFlow.Contracts`.

**Integration Events** (cross-module): records in `FreshFlow.Contracts`, consumed by other modules'
`EventHandlers/` via MediatR `INotificationHandler<T>`:

```
Order.Create() raises OrderCreatedDomainEvent
  → OrderCreatedDomainEventHandler (Orders.Application) publishes OrderCreatedIntegrationEvent
    → Logistics.Application: OrderCreatedIntegrationEventHandler creates a delivery record
    → Notifications.Application: OrderCreatedIntegrationEventHandler persists a notification
```

### Database

One shared `AppDbContext` in `FreshFlow.Infrastructure.Persistence`, using
`ApplyConfigurationsFromAssembly()` to auto-discover every `IEntityTypeConfiguration<T>` across
module Infrastructure projects — `AppDbContext` declares no `DbSet<T>` properties.

All PKs are `UUID` (`gen_random_uuid()`). All mutable tables have `deleted_at TIMESTAMPTZ` for soft
delete — **every seam and query must filter it**. `price_snapshots` and `refresh_tokens` are
append-only (no `deleted_at`).

`price_snapshots` is a **plain table** with ordinary indexes. It is *not* partitioned: `docs/03`,
the comment in `PriceSnapshotConfiguration.cs`, and older docs all describe monthly range
partitioning and a `PartitionMaintenanceJob` — **neither exists in this repo**. Do not plan around
them.

Timestamps are stored UTC as `timestamptz`. Business dates are **`Asia/Ho_Chi_Minh`** — convert at
the handler boundary (`Analytics.Application/Common/VietnamTime.cs`). But note `batch_date`-style
`date` columns are already business dates and must **not** be converted.

### Background Jobs

`BackgroundService` implementations under `{Module}.Infrastructure/Jobs/`. Copy an existing one:

- `Orders`: `ScheduledOrderGenerationHostedService`, `MonthlyCreditStatementHostedService`
- `Procurement`: `ProcurementBatchingHostedService`
- `Notifications`: `NotificationRetryHostedService`

### Module Registration Pattern

Each module's `Infrastructure` project exposes one extension method:

```csharp
// src/Modules/Auth/FreshFlow.Auth.Infrastructure/DependencyInjection.cs
public static IServiceCollection AddAuthModule(
    this IServiceCollection services, IConfiguration config) { ... }
```

`Program.cs` chains them all (`AddAuthModule` → … → `AddAnalyticsModule`, then `AddMediaModule`,
`AddAssistant`).

`ValidationBehavior` is **copy-duplicated in each module** (10 copies). This is deliberate — do not
refactor it into a shared project.

### Real-Time (SignalR)

Three hubs, each living in its **owning module's** `Infrastructure/Realtime/` folder (there is no
`FreshFlow.API/SignalR/`):

| Hub | Location | Route |
|---|---|---|
| `PricingHub` | `Pricing.Infrastructure/Realtime/` | `/hubs/pricing` |
| `OrderHub` | `Orders.Infrastructure/Realtime/` | `/hubs/orders` |
| `DeliveryHub` | `Logistics.Infrastructure/Realtime/` | `/hubs/delivery` |

Registered with a plain `AddSignalR()` — **in-memory, no Redis backplane** (single-instance only;
Redis is used for the Pricing board cache, not scale-out). Clients authenticate via JWT in the
`access_token` query string during negotiate.

Groups in use: `market:{marketId}`, `restaurant:{restaurantId}`, `admin:orders`, `admin:delivery`.

Broadcast calls originate in `{Module}.Infrastructure/Realtime/` services implementing interfaces
declared in `{Module}.Application/Abstractions/` (e.g. `IPricingBroadcastService`).

---

## Key Coding Patterns

### Result pattern (no business-rule exceptions from services)

```csharp
// Handler returns Result<T>, never throws for business rules
public async Task<Result<OrderResponseDto>> Handle(CreateOrderCommand command, CancellationToken ct)

// Controller maps result to HTTP
var result = await sender.Send(command, ct);
return result.IsSuccess
    ? CreatedAtAction(..., ApiResponse.Ok(result.Value))
    : result.Error.ToActionResult();
```

`ErrorExtensions.ToActionResult()` maps on the **error code string** — `_NOT_FOUND` → 404,
`VALIDATION_ERROR` → 400, `FORBIDDEN` → 403. An unregistered code falls through to **500**, so
always reuse an existing code.

Route controllers through `ISender`. Bypassing it skips `ValidationBehavior`, so validators never
run.

### RBAC — verify role names against the seed, never the docs

The only roles that exist (seeded in `20260606152229_AddRolesTable.cs`):

`admin` · `market_agent` · `restaurant` · `hub_staff` · `driver` · `operations_manager`

`docs/04-api-design.md` cites `restaurant_manager` / `restaurant_staff` — **neither exists**.
`[Authorize(Roles = "...")]` with an unknown name fails **silently**: green build, permanent 403.

### SQL policy

This repo contains **zero `FromSqlRaw`**. Keep it that way. Ladder, in order:

1. **LINQ over a keyless Row** — the default; covers nearly everything.
2. Aggregate inside a **static** `ToSqlQuery` (no parameters) — for functions LINQ can't translate
   (e.g. `stddev_samp`, see `PriceSnapshotRowConfiguration`). Apply parameterized filters outside in LINQ.
3. `FromSql($"")` (FormattableString) — last resort.

**Forbidden:** `FromSqlRaw`, string interpolation/concatenation/`string.Format` into SQL, and any
`ORDER BY {input}`. Client-chosen sort/group columns go through an allow-list `switch` mapped to
LINQ expressions.

### C# conventions

- PascalCase: classes, methods, properties, filenames
- Async methods always suffixed `Async`
- Interfaces prefixed `I`; **interface members need an explicit `public`** (IDE0040 is enforced —
  omitting it fails `dotnet format`)
- DTOs use `record` types (immutable, value equality)
- FluentValidation co-located with the command/query: `Commands/Login/LoginCommandValidator.cs`
- Guard aggregates on empty sets: `SumAsync(x => (decimal?)x.Value) ?? 0m`, and guard every divide

---

## Git

| Branch | Purpose |
|--------|---------|
| `main` | Production-ready; PR-only merges |
| `dev` | Integration branch — PRs target this |
| `SCRUM-301-analytics` | Feature branches named by Jira key + short slug |

Commit format: `type(scope): SCRUM-XXX description` — **Jira key before the description**, e.g.
`feat(analytics): SCRUM-301 order metrics`. Commits use the **epic** key, not the per-task key.

---

## Key Docs

| File | When to Read |
|---|---|
| `docs/01-requirements-spec.md` | What a feature must do; acceptance criteria |
| `docs/02-system-architecture.md` | Module responsibilities, SignalR design, caching strategy |
| `docs/03-database-schema.md` | DDL and index intent (⚠️ partitioning section is stale) |
| `docs/04-api-design.md` | Endpoint specs, request/response shapes (⚠️ RBAC role names are wrong) |
| `docs/05-implementation-plan.md` | Original task list and folder structure |
| `docs/06-context-decisions.md` | Context decisions |
| `docs/features/<epic>/` | Per-epic working docs: plans, audits, decisions (`AUDIT-*.md`) |
| `docs/features/README.md` | Index of feature docs |
| `docs/REVIEW-REPORT.md` | Known gaps and open design decisions |

**The design docs predate the code.** Where a doc and the code disagree, the code wins — verify
column names, role names, and enum values against migrations and `*Configuration.cs` before relying
on a doc.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
at `specs/003-auth-module/plan.md`.
<!-- SPECKIT END -->
