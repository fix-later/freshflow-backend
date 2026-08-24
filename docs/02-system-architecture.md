# FreshFlow (FFX) — System Architecture

**Version:** 2.0 · **Reconciled with code:** 2026-08-22 (branch `dev-bao`, commit `bc5f1a4`)
**Runtime:** .NET 10 · ASP.NET Core · PostgreSQL 16 · Redis 7
**Status:** As-built. This document describes the system that exists in the repository today.

---

## Table of Contents

1. [Pattern and Solution Layout](#1-pattern-and-solution-layout)
2. [Dependency Rules](#2-dependency-rules)
3. [The Ten Modules](#3-the-ten-modules)
4. [Request Pipeline](#4-request-pipeline)
5. [Cross-Module Communication](#5-cross-module-communication)
6. [Real-Time (SignalR)](#6-real-time-signalr)
7. [Background Jobs](#7-background-jobs)
8. [External Integrations](#8-external-integrations)
9. [Caching](#9-caching)
10. [Configuration and Secrets](#10-configuration-and-secrets)
11. [Deployment](#11-deployment)
12. [Decisions and Their Consequences](#12-decisions-and-their-consequences)

---

## 1. Pattern and Solution Layout

**Microservice-ready modular monolith.** One deployable ASP.NET Core host (`FreshFlow.API`)
wires ten self-contained modules. Each module owns its own `.Domain`, `.Application` and
`.Infrastructure` projects; boundaries are enforced by `.csproj` references, not by convention.

The solution file is **`FreshFlow.slnx`** (XML solution format). There is no `.sln`.

```
src/
  Shared/
    FreshFlow.SharedKernel/       ← BaseEntity, AggregateRoot, Result<T>, ICommand/IQuery
    FreshFlow.Contracts/          ← Integration event records (cross-module DTOs only)
  Modules/
    Auth/  Catalog/  Pricing/  Orders/  Procurement/
    Logistics/  Notifications/  Hub/  Analytics/  Invoicing/
      Each: {Module}.Domain / {Module}.Application / {Module}.Infrastructure
  FreshFlow.Infrastructure.Persistence/   ← Shared AppDbContext + all 97 EF migrations
  FreshFlow.Infrastructure.Media/         ← Cloudinary signed upload (shared, not a module)
  FreshFlow.API/                          ← Host: 31 controllers, Program.cs
    Assistant/                            ← AI assistant orchestrator (host-level, NOT a module)
tests/
  Unit/   FreshFlow.{Module}.UnitTests/   (+ Assistant, Infrastructure.Media, Infrastructure.Persistence)
  Integration/  FreshFlow.IntegrationTests/   ← Testcontainers, real postgres:16-alpine
```

**Why a monolith.** Small team, capstone scope, one deployment unit, one connection pool. The
module boundaries and the read seams (§5) are what make later extraction tractable — they are
the price paid up front for that option.

**Accepted trade-off.** All modules share one PostgreSQL connection pool and one process. A
runaway Analytics query can affect Auth. Mitigated by query timeouts and the fact that
Analytics is read-only and lightly used.

---

## 2. Dependency Rules

```
Domain         →  SharedKernel only
Application    →  Domain + SharedKernel + Contracts
Infrastructure →  Application + Persistence + EF Core / Redis packages
API (host)     →  all Infrastructure projects (DI wiring only)
```

**No module's `Domain` or `Application` may reference another module's projects.** This is
absolute, and it has two consequences that shape most cross-module work:

- You cannot use another module's enum. Re-declare the values locally as string constants (see
  `GetOrderMetricsQueryHandler` in Analytics).
- Cross-module reads go through a **keyless EF Row seam** (§5.3), never a project reference.

---

## 3. The Ten Modules

| Module | Owns | Tables | Notes |
|---|---|---:|---|
| **Auth** | Users, roles, sessions, restaurants, delivery addresses, driver profiles, market assignments | 9 | Seeds the six roles and the default admin at startup |
| **Catalog** | Products, categories, units, markets, packing codes | 5 | Master data, not market-specific |
| **Pricing** | Market-product listings, price snapshots, tags | 4 | Owns the live price board and its Redis cache |
| **Orders** | Orders, items, issues, claims, scheduled orders, B2B credit + statements, favorites, operational settings | 12 | Credit / công nợ lives here, not in a payments module |
| **Procurement** | Procurement batches (**phiên chợ**), batch items/orders, exceptions, market sessions and their agent/vehicle assignments | 7 | |
| **Hub** | Hubs, inbound/outbound events, inventory, sorting, discrepancies, cross-dock, staff/driver assignments, driver handover | 10 | |
| **Logistics** | Deliveries, delivery issues, routes, route plans, route matrix cache, vehicles | 6 | Last-mile execution + route planning |
| **Invoicing** | VAT invoices and lines | 2 | Issued per completed delivery |
| **Notifications** | Notifications, device push tokens | 2 | |
| **Analytics** | — | **0** | Read-only; built entirely on cross-module Row seams |

Plus two host-level concerns that are deliberately *not* modules:

| Concern | Where | Why |
|---|---|---|
| AI Shopping Assistant | `FreshFlow.API/Assistant/` | It orchestrates across modules; putting it inside one would break the dependency rules |
| Cloudinary signed upload | `FreshFlow.Infrastructure.Media` | Shared utility used by six modules |

Each module's Infrastructure project exposes exactly one registration extension method, and
`Program.cs` chains them:

```csharp
builder.Services.AddAuthModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);
// … Pricing, Orders, Procurement, Logistics, Notifications, Hub, Analytics, Invoicing …
builder.Services.AddMediaModule(builder.Configuration);
builder.Services.AddAssistant(builder.Configuration);
```

> `ValidationBehavior` is **copy-duplicated in each module** (one copy per module). This is
> deliberate — extracting it into a shared project would require every module's Application
> layer to reference it, which is exactly the coupling the rules forbid. Do not refactor it.

---

## 4. Request Pipeline

```
Client
  → UseExceptionHandler          ValidationException → 400, UnauthorizedAccess → 401,
                                 DB conflict → 409, everything else → 500 (logged, not exposed)
  → UseHsts / UseHttpsRedirection   (non-Development only)
  → UseForwardedHeaders          so the rate limiter partitions by real client IP, not the proxy
  → UseCors("AllowFrontend")
  → UseAuthentication            JWT bearer; SignalR reads ?access_token=
  → UseAuthorization             [Authorize(Roles = …)], stacked as AND
  → UseRateLimiter               auth / orders / assistant policies
  → MapControllers               ISender → MediatR → ValidationBehavior → handler → Result<T>
  → MapHealthChecks("/health")
  → MapHub<…>                    4 SignalR hubs
```

### 4.1 The Result pattern

Handlers return `Result<T>` and never throw for business rules. Controllers map the result to
HTTP:

```csharp
var result = await sender.Send(command, ct);
return result.IsSuccess
    ? CreatedAtAction(..., ApiResponse.Ok(result.Value))
    : result.Error.ToActionResult();
```

`ErrorExtensions.ToActionResult()` maps on the **error code string**. An unregistered code
falls through to **500**, so always reuse an existing code.

Route everything through `ISender`. Bypassing it skips `ValidationBehavior`, so the validators
never run — a real bug class that has already shipped once in this repo.

### 4.2 Startup sequence

`AdminSeeder` is registered as an `IHostedService` and runs before the app serves traffic:
applies pending EF migrations (`db.Database.MigrateAsync`), seeds the six roles, seeds the
default admin account if absent. `dotnet ef database update` is therefore not part of the
normal dev loop.

---

## 5. Cross-Module Communication

Three mechanisms, in order of preference.

### 5.1 Domain events (inside one module)

Raised on the aggregate, dispatched post-save by `DomainEventDispatchInterceptor`, handled in
the same module via MediatR `INotificationHandler<T>`.

### 5.2 Integration events (across modules)

Records in `FreshFlow.Contracts`, published by a domain-event handler in the owning module and
consumed by other modules' `EventHandlers/`. In-process MediatR — there is no message broker.

```
Order.Confirm() raises OrderConfirmedDomainEvent
  → OrderConfirmedDomainEventHandler (Orders.Application) publishes OrderConfirmedIntegrationEvent
    → Notifications.Application : persists + pushes a notification
```

The 18 contracts currently defined:

| Area | Events |
|---|---|
| Orders | `OrderConfirmed`, `OrderCancelled`, `ScheduledOrderNeedsAttention` |
| Credit | `CreditLimitThresholdReached`, `CreditStatementGenerated`, `RestaurantRefundIssued` |
| Pricing | `PriceUpdated` |
| Procurement | `ProcurementBatchBuilt`, `ProcurementManifestGenerated`, `ProcurementPurchaseConfirmed`, `ProcurementBatchHandedOff`, `ProcurementBatchCancelled`, `ProcurementAgentAssigned` |
| Hub | `HubDiscrepancyRecorded` |
| Logistics | `DeliveryStarted`, `DeliveryStopUpdated`, `DeliveryCompleted` |

### 5.3 The keyless Row seam (reads)

To read another module's table, define a **keyless row** mapped with `ToSqlQuery` — never
`ToTable`, which would collide with the owning module's model. Trio pattern, all in
`{Module}.Infrastructure/CrossModule/`:

```
XxxRow.cs               ← POCO, init-only props, PascalCase
XxxRowConfiguration.cs  ← HasNoKey() + ToSqlQuery("""SELECT … FROM other_table WHERE deleted_at IS NULL""")
XxxReader.cs            ← db.Set<XxxRow>().AsNoTracking() + LINQ
```

`ToSqlQuery` does **not** execute on the EF InMemory provider, so a seam is only actually
proven by an **integration test** against real PostgreSQL. Prefer extending an existing seam
over adding a second one against the same table.

> New keyless Rows enter `AppDbContextModelSnapshot`. `DesignTimeDbContextFactory.cs` calls
> `ForceLoadModuleAssemblies()` — every module Infrastructure assembly must be listed there, or
> the snapshot silently drifts and another module's next migration erases your rows.

See [`03-database-schema.md`](./03-database-schema.md) §4 for the column-casing traps that make
seams fail at runtime rather than at build.

---

## 6. Real-Time (SignalR)

Four hubs, each in its **owning module's** `Infrastructure/Realtime/` folder — there is no
`FreshFlow.API/SignalR/`.

| Hub | Route | Authorization | Groups |
|---|---|---|---|
| `PricingHub` | `/hubs/pricing` | any authenticated | `market:{marketId}` |
| `OrderHub` | `/hubs/orders` | `admin`, `operations_manager`, `restaurant` | `restaurant:{id}`, `admin:orders` |
| `DeliveryHub` | `/hubs/delivery` | `admin`, `operations_manager`, `restaurant` | `restaurant:{id}`, `admin:delivery` |
| `NotificationHub` | `/hubs/notifications` | any authenticated | `user:{userId}` |

Registered with a plain `AddSignalR()` — **in-memory, single-instance, no Redis backplane.**
Clients authenticate with the JWT in the `access_token` query string during negotiate.
Broadcast calls originate in `{Module}.Infrastructure/Realtime/*BroadcastService.cs`, behind
interfaces declared in `{Module}.Application/Abstractions/`.

Event names and payload targets are in [`04-api-design.md`](./04-api-design.md) §3.

> Scaling out to more than one API instance requires adding a backplane first. Nothing in the
> current code does this.

---

## 7. Background Jobs

Plain `BackgroundService` implementations under `{Module}.Infrastructure/Jobs/`. No Hangfire,
no Quartz, no external scheduler.

| Job | Module | Does |
|---|---|---|
| `ScheduledOrderGenerationHostedService` | Orders | Materializes recurring schedules into real orders and auto-confirms them |
| `MonthlyCreditStatementHostedService` | Orders | Generates the immutable monthly công nợ statement |
| `ProcurementBatchingHostedService` | Procurement | Bundles confirmed orders into a phiên chợ at cutoff |
| `NotificationRetryHostedService` | Notifications | Retries failed push sends |
| `InvoiceIssuanceRetryHostedService` | Invoicing | Retries failed VAT invoice issuance |
| `HubInboundBackfillHostedService` | Hub | Backfills inbound events for batches handed over before the feature existed |

Copy an existing one when adding a job; `ScheduledOrderGenerationHostedService` is the
reference for a scheduled tick.

---

## 8. External Integrations

| Service | Used for | Where | Failure behaviour |
|---|---|---|---|
| **Cloudinary** | Signed direct image upload (avatars, product/category/market images, proof photos) | `FreshFlow.Infrastructure.Media` | Signing fails → 500; the upload itself never touches the API |
| **Resend** | Password-reset and verification email | `Auth.Infrastructure/Services/Resend*Sender.cs` | Logged; the flow still returns 202 (no user enumeration) |
| **Goong** | Road distance and route matrix (Vietnam-specific maps) | `Orders.Infrastructure/Goong/`, `Logistics.Infrastructure/Routing/GoongRouteMatrixProvider.cs` | `CachingRouteMatrixProvider` fronts it with the `route_matrix_cache` table |
| **ZenMux (GLM)** and **Gemini on Vertex AI** | AI shopping assistant LLM | `FreshFlow.API/Assistant/` | Configurable provider priority list + `FailoverChatClient`; fails over on everything except `AuthenticationFailed` |
| **Expo Push** | Mobile push notifications | `Notifications.Infrastructure/Push/ExpoPushSender.cs` | Retried by `NotificationRetryHostedService` |

Gemini authenticates through Google Application Default Credentials, not an API key.

---

## 9. Caching

Redis holds exactly **one** thing: the Pricing board.

| Key | Type | TTL |
|---|---|---|
| `price:{marketId}:{productId}` | Hash — `price`, `quantity`, `updated_at`, `updated_by` | 5 min absolute, reset on every write |

A miss falls back to PostgreSQL, so a Redis outage degrades latency, not correctness.

The route matrix is cached in **PostgreSQL** (`route_matrix_cache`), not Redis, because it is
expensive to recompute and worth surviving a restart.

> Redis is **not** a SignalR backplane and **not** a stock-reservation store. Earlier design
> drafts described both; neither was built.

---

## 10. Configuration and Secrets

Sections in `appsettings.json`: `ConnectionStrings`, `JWT`, `Cors`, `RateLimiting`,
`Procurement`, `Orders`, `Delivery`, `Logistics`, `SignalR`, `Notifications`, `Email`,
`Assistant`, `Logging`, `AllowedHosts`.

Secrets are supplied by **.NET User Secrets** in Development and by environment variables in
deployed environments — never by a committed file. Required values are validated at startup;
a missing `JWT:Key`, `Cloudinary:*` or `AdminSeed:Password` fails the boot rather than
degrading silently. Setup commands are in the [README](../README.md) §4.

---

## 11. Deployment

**Local development** — `docker compose up -d` starts PostgreSQL 16 and Redis 7 only. The API
runs on the host via `dotnet run`. Integration tests do not need it: Testcontainers spins up
its own `postgres:16-alpine`.

**Dev VPS** — `docker-compose.dev-vps.yml` additionally builds and runs the API container,
bound to `127.0.0.1:${API_PORT}`. A reverse proxy in front of it is the operator's
responsibility; there is no nginx service in the repository.

There is no PostgreSQL read replica, no service mesh, and no multi-instance configuration.

---

## 12. Decisions and Their Consequences

| Decision | Consequence you have to live with |
|---|---|
| Modular monolith with one shared `AppDbContext` | Module isolation is a code-review discipline plus `.csproj` references, not a runtime boundary |
| No project references between modules | Cross-module reads need a keyless Row seam and an integration test to prove them |
| In-memory SignalR | Single API instance only until a backplane is added |
| In-process MediatR integration events | No delivery guarantee across a crash; an event lost mid-handler is lost |
| B2B credit instead of a payment gateway | No `payments`/`refunds` tables; refunds are credit adjustments |
| Zero `FromSqlRaw` | Anything LINQ cannot express goes in a **static** `ToSqlQuery`, with parameters applied outside in LINQ |
| Six roles, checked by string | `[Authorize(Roles = "…")]` with an unknown name fails silently — green build, permanent 403 |
| Two column-casing conventions in one database | Never guess a column name; copy an existing seam or read the `*Configuration.cs` |

---

## Changelog

| Version | Date | Change |
|---|---|---|
| 2.0 | 2026-08-22 | Rewritten as-built. Corrected: ten modules (was seven), four SignalR hubs (was three), no Redis backplane, no read replica, no nginx container, no soft stock reservation, no Hangfire. Added Invoicing, the AI assistant, the six background jobs, the real external integrations, and the keyless Row seam. |
| 1.0 | 2026-05-09 | Original pre-implementation design. |
