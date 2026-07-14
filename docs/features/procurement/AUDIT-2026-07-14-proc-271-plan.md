# SCRUM-271 — Build Procurement Batch — Implementation Plan

Epic: PROC (SCRUM-255). Date: 2026-07-14. Status: PLAN ONLY (no code written).
Author: backend-dev leader. Grounded against current `dev`-derived branch `SCRUM-353-ADM-admin-config`.

---

## 1. Goal & why

After an order reaches `Confirmed`, nothing advances it to `Batched`, so the whole
`Confirmed → Batched → PickedUp → AtHub → Delivering → Delivered` pipeline stalls. HUB, LOG,
DEL are all DONE but starved because no goods ever get batched/procured. **SCRUM-271 is the
missing root**: at the daily cutoff, aggregate that cycle's `Confirmed` orders into procurement
batches (the Market Agent's shopping list), grouped by market source, and flip each covered
order `Confirmed → Batched`. This starts the procurement → hub → delivery chain and retires the
manual `AdvanceOrderStatus` ops shim (PR #26) for the Confirmed→Batched transition.

### Acceptance criteria — verbatim from `docs/01-requirements-spec.md` FR-ORD-005

> The system shall automatically batch all `CONFIRMED` orders at the 22:00 daily cutoff time.
> Orders are grouped by delivery zone and market source into procurement batches. Admin may also
> manually trigger the same auto-batch logic on demand.
>
> 1. At 22:00 daily, the system transitions all eligible `CONFIRMED` orders to `BATCHED` status
>    by grouping them into `OrderGroup`/Procurement Batch entities by delivery zone and market source.
> 2. `GET /api/v1/admin/order-groups` lists all generated batches with member orders and statuses.
> 3. Admin can call `POST /api/v1/admin/order-groups/auto-batch` with optional `targetDate`,
>    `dryRun`, and `force` fields to run the same batching service manually; `dryRun=true` returns a
>    preview without writing to the database.
> 4. Manual and scheduled batching are idempotent: orders already assigned to an active batch are
>    skipped unless a documented `force=true` recovery path applies.
> 5. An order cannot belong to more than one active order group simultaneously — attempting to add
>    it to a second group returns HTTP 409.

Glossary (same doc):
- **Procurement Batch (Lô thu mua)**: system-generated grouping of `CONFIRMED` orders at cutoff,
  organized by delivery zone and source market. Represents the shopping list the Market Agent
  executes. Previously called `Order Group`; now auto-generated.
- **Cutoff Time**: 22:00 daily; configurable (now via `operational_settings.DailyCutoffTime`,
  SCRUM-355). Orders placed after cutoff queue for the next cycle.

> Note: docs use "UC-PROC-01..04" as the Jira umbrella; the requirement text lives in FR-ORD-005
> (there is no separate UC-PROC section in `01-requirements-spec.md`). Criteria 1–5 above are the
> real, testable acceptance bar for 271.

---

## 2. Grounded survey (verified this session)

| Fact | Evidence |
|---|---|
| No Procurement module exists | `src/Modules/`: Analytics, Auth, Catalog, Hub, Logistics, Notifications, Orders, Pricing |
| `Order.AdvanceStatus(OrderStatus.Batched)` already exists & enforces `Confirmed→Batched` | `src/Modules/Orders/FreshFlow.Orders.Domain/Entities/Order.cs:230` + `AllowedTransitions[Confirmed]=[Batched,Cancelled]` (line 14) |
| Advancing raises `OrderStatusChangedDomainEvent` | `Order.cs:240-246` (`TransitionTo`) |
| `OrderConfirmedIntegrationEvent` published on confirm; only Notifications consumes today | `src/Shared/FreshFlow.Contracts/OrderConfirmedIntegrationEvent.cs`; producer `Orders.Application/EventHandlers/OrderConfirmedDomainEventHandler.cs`; consumer `Notifications.Application/EventHandlers/OrderConfirmedIntegrationEventHandler.cs`. **Event carries only OrderId/RestaurantId/TotalAmount/OccurredAt — no line-item/market data.** |
| `OrderItem` carries `MarketProductId`, `ProductNameSnapshot`, `Quantity`, `LockedUnitPrice` | `src/Modules/Orders/FreshFlow.Orders.Domain/Entities/OrderItem.cs` |
| `MarketProductId` → market source lives in **Pricing** `MarketProduct` (has `MarketId`, `ProductId`) | `src/Modules/Pricing/.../Domain/Entities/MarketProduct.cs` |
| `operational_settings` stores `DailyCutoffTime` (TimeOnly) + `BatchingEnabled` (bool); **BatchingEnabled has no reader yet** | `src/Modules/Orders/.../Domain/Entities/OperationalSettings.cs` (default cutoff 22:00, batching=true) |
| Cutoff-time reader exists for scheduling math (not for batching) | `Orders.Application/Services/OrderCutoffScheduler.cs` (Asia/Ho_Chi_Minh, `DefaultCutoffLocalTime = 22:00`, D+1/D+2 rule) |
| Scheduled-job pattern to copy (BackgroundService + PeriodicTimer + scoped resolve) | `Orders.Infrastructure/Jobs/ScheduledOrderGenerationHostedService.cs`; registered `AddHostedService<...>()` in `Orders.Infrastructure/DependencyInjection.cs:52` |
| **Cross-module read pattern** (no project ref; keyless `db.Set<Row>()` over shared `AppDbContext`) | Orders' `CrossModule/MarketProductReader.cs` + `RestaurantReader.cs`; Hub's `CrossModule/OrderLookupReader.cs`; Logistics' `CrossModule/RestaurantCoordinateReader.cs` |
| Existing `MarketProductReader` returns price/name/qty but **not `MarketId`** — needs extension for market grouping | `Orders.Application/Abstractions/IMarketProductReader.cs` |
| `Order.OrderGroupId` column already exists (nullable) but there is **no `OrderGroup` entity** in code | `Order.cs:43`; grep finds only column + migration, no aggregate |
| Restaurant lives in Auth; **restaurants have coordinates but no stored delivery-zone FK** — zone is a Logistics routing derivation | `Logistics/.../CrossModule/RestaurantCoordinateRow.cs`; no `ZoneId` on any Restaurant row |
| Manual `AdvanceOrderStatus` shim (PR #26) is **not present on this branch** | grep for `AdvanceOrderStatus` returns 0 hits in `src` |
| Solution file is `FreshFlow.slnx`; migrations live in `src/FreshFlow.Infrastructure.Persistence`; one shared `AppDbContext` auto-discovers `IEntityTypeConfiguration<T>` | verified `ls *.slnx`; CLAUDE.md |

### Two survey-driven design consequences

1. **Market grouping requires resolving `OrderItem.MarketProductId → MarketProduct.MarketId`.**
   A single order can contain items from multiple markets, so **one order fans out across multiple
   market batches**. The batch↔order relationship is therefore **many-to-many**, and batch line
   items aggregate quantity **per (market, product) across all covered orders**.

2. **"Delivery zone" grouping has no data backing yet** (no restaurant→zone FK; zone is derived by
   Logistics at routing time). See Open Decision D1 — MVP groups by **market source only**.

---

## 3. Module & architecture decisions

### D-A. New greenfield `Procurement` module

```
src/Modules/Procurement/
  FreshFlow.Procurement.Domain/          → SharedKernel only
  FreshFlow.Procurement.Application/     → Domain + Contracts + SharedKernel + MediatR + FluentValidation
  FreshFlow.Procurement.Infrastructure/  → Application + EF Core + AppDbContext
```

Wire `AddProcurementModule(config)` into `src/FreshFlow.API/Program.cs` (chain after
`AddOrdersModule`, before/after `AddHubModule` — order irrelevant, DI only). Add the three
projects to `FreshFlow.slnx`. Unit test project `tests/Unit/FreshFlow.Procurement.UnitTests/`.

Dependency rules (enforced by `.csproj`): Procurement has **no** project reference to Orders,
Pricing, Auth, or Logistics. It reads their tables through **keyless cross-module Row readers**
over the shared `AppDbContext` (identical to `Orders.Infrastructure/CrossModule/*`).

### D-B. Status propagation Confirmed→Batched via integration event (Procurement → Orders)

Procurement must **not** mutate `Order` aggregates directly (module boundary). Mechanism, mirroring
the existing `OrderConfirmedIntegrationEvent` → Logistics/Notifications flow:

1. Building a batch raises `ProcurementBatchBuiltDomainEvent` (Procurement domain).
2. `ProcurementBatchBuiltDomainEventHandler` (Procurement.Application) publishes
   `ProcurementBatchBuiltIntegrationEvent` (in `FreshFlow.Contracts`) carrying `BatchId`, `MarketId`,
   `BatchDate`, and **`CoveredOrderIds`** (distinct order IDs in the batch).
3. `ProcurementBatchBuiltIntegrationEventHandler` (Orders.Application/EventHandlers) loads each
   covered order and calls `order.AdvanceStatus(OrderStatus.Batched)`, then saves. Idempotent by
   construction: an order already past `Confirmed` fails the transition guard → skip (log, don't
   throw). **This handler is the piece that RETIRES the manual `AdvanceOrderStatus` bridge for the
   Confirmed→Batched hop.**

> Because a single order fans out to several market batches, the same order may appear in several
> `CoveredOrderIds` lists in one cutoff run. The first successful flip moves it out of `Confirmed`;
> subsequent flips no-op (guard fails) — correct and idempotent. De-dup the union of covered order
> IDs before flipping if we want a single flip per order (recommended, cheaper).

---

## 4. Domain model (Procurement.Domain)

### `ProcurementBatch` (AggregateRoot)

| Field | Type | Notes |
|---|---|---|
| `Id` | Guid PK | `gen_random_uuid()` |
| `BatchDate` | DateOnly | the delivery/procurement cycle date this batch serves (local HCMC date) |
| `MarketId` | Guid | market source (grouping key) — resolved from covered items |
| `Status` | `ProcurementBatchStatus` enum | `Built → Manifested → Purchasing → HandedOff` (271 only creates `Built`; later states owned by 272/282 — declare the enum, drive only `Built`) |
| `TotalItemCount` | int | denormalized count of line items (convenience for list view) |
| `CreatedAt`/`UpdatedAt`/`DeletedAt` | via `BaseEntity`/`AggregateRoot` | soft-delete on mutable table |
| `_items` | `List<ProcurementBatchItem>` | aggregated lines |
| `_orders` | `List<ProcurementBatchOrder>` | coverage links |

Behavior (Result pattern, no business exceptions):
- `static ProcurementBatch Build(DateOnly batchDate, Guid marketId, IEnumerable<(Guid marketProductId, string productName, int qty, Guid orderId)> lines)`
  — aggregates qty per `marketProductId`, records distinct covered orders, raises
  `ProcurementBatchBuiltDomainEvent(Id, MarketId, BatchDate, coveredOrderIds)`.
- Justification vs FR-ORD-005: `MarketId` = "source market"; `_orders` gives criterion-2 "member
  orders"; `Status` gives criterion-2 "statuses"; the aggregate-per-(market,product) line model is
  what makes the batch a usable **shopping list** (glossary) rather than a raw order dump. Zone field
  intentionally omitted in MVP (D1).

### `ProcurementBatchItem` (BaseEntity, child)

`Id`, `ProcurementBatchId` (FK), `MarketProductId`, `ProductNameSnapshot`, `TotalQuantity`
(sum across covered orders). No price — 271 builds the shopping list; costing is out of scope.

### `ProcurementBatchOrder` (BaseEntity, child — coverage/idempotency link)

`Id`, `ProcurementBatchId` (FK), `OrderId`. Unique index on `OrderId` **where batch active** is the
DB-level guard for FR-ORD-005 criterion 5 ("an order cannot belong to >1 active batch → 409").

### Enums

`ProcurementBatchStatus { Built, Manifested, Purchasing, HandedOff }` (only `Built` used in 271).

---

## 5. Batch-building trigger

### `BatchConfirmedOrdersService` (Procurement.Application/Services, behind `IProcurementBatchingService`)

Signature: `Task<BatchingResult> BuildBatchesAsync(DateOnly batchDate, bool dryRun, bool force, CancellationToken ct)`.

Algorithm:
1. Read `operational_settings` (cross-module read — extend a settings Row reader or reuse via a new
   `IOperationalSettingsReader` in Procurement.Infrastructure/CrossModule): if `BatchingEnabled ==
   false`, return `Skipped(reason: "batching_disabled")` and write nothing. **This is the first-ever
   reader of `BatchingEnabled`.**
2. **Eligible-orders query** (cross-module read over `AppDbContext`): orders with
   `Status == Confirmed`, `DeletedAt == null`, whose `ScheduledFor` local date maps to `batchDate`'s
   cycle (see D2), and (unless `force`) whose `Id ∉ procurement_batch_orders(active)`. Include their
   items (`MarketProductId`, `ProductNameSnapshot`, `Quantity`).
3. Resolve each item's `MarketId` via extended market-product reader (batch-load all
   `MarketProductId`s in one query to avoid N+1).
4. Group items by `(MarketId)`; within each group aggregate `TotalQuantity` per `MarketProductId`;
   collect distinct covered `OrderId`s. Build one `ProcurementBatch` per market.
5. If `dryRun`: return a preview DTO (batches + members + line totals), **no SaveChanges**.
6. Else: persist batches (+ items + coverage links), `SaveChanges`. Domain events dispatched by the
   existing MediatR domain-event dispatcher → integration event → Orders flips to `Batched`.

`BatchingResult { BatchesCreated, OrdersBatched, ItemsAggregated, Skipped, Reason, Preview[] }`.

### `ProcurementBatchingHostedService` (Procurement.Infrastructure/Jobs)

Copy `ScheduledOrderGenerationHostedService` shape: `BackgroundService` + `PeriodicTimer`, scoped
resolve of `IProcurementBatchingService`, config gate `Procurement:Batching:Enabled` and
`Procurement:Batching:IntervalSeconds`. Each tick: compute "current cycle" — if local HCMC wall
clock has passed `operational_settings.DailyCutoffTime` for a `batchDate` not yet batched, call
`BuildBatchesAsync(batchDate, dryRun:false, force:false)`. Timezone via the same
`Asia/Ho_Chi_Minh` resolution `OrderCutoffScheduler` uses (fallback `SE Asia Standard Time`).

**Idempotency (three layers):** (a) status-based — eligibility is `Status == Confirmed`, so once
flipped to `Batched` an order is never re-picked; (b) coverage link — `procurement_batch_orders`
unique-active index rejects a second active batch for the same order (409 for manual path);
(c) "already batched this cycle" guard in the hosted service so the periodic timer doesn't rebuild
a cycle already built. `force=true` bypasses (b) for the documented recovery path only.

---

## 6. Persistence

- Configs: `ProcurementBatchConfiguration`, `ProcurementBatchItemConfiguration`,
  `ProcurementBatchOrderConfiguration` in `Procurement.Infrastructure/Persistence/Configurations/`
  — auto-discovered by `AppDbContext.ApplyConfigurationsFromAssembly` (confirm the Procurement
  Infrastructure assembly is scanned; if the scan is per-assembly, register it — check
  `AppDbContext` setup during impl).
- Tables (snake_case, UUID PKs, `deleted_at` on the mutable parent/children):
  `procurement_batches`, `procurement_batch_items`, `procurement_batch_orders`.
- Indexes: `procurement_batches (batch_date, market_id)`; `procurement_batch_items (procurement_batch_id)`;
  `procurement_batch_orders (procurement_batch_id)` + **partial unique** `(order_id) WHERE deleted_at IS NULL`
  for criterion 5.
- Cross-module Row keyless types (Procurement.Infrastructure/CrossModule): `ConfirmedOrderRow`,
  `ConfirmedOrderItemRow` (over orders/order_items), `MarketProductMarketRow` (over
  Pricing `market_products`, exposing `Id`, `MarketId`), `OperationalSettingsRow`. Mark keyless /
  `HasNoKey` or map to existing tables read-only exactly like `MarketProductRow`/`RestaurantRow`.
- **One EF migration** `AddProcurementBatches` via
  `dotnet ef migrations add AddProcurementBatches --project src/FreshFlow.Infrastructure.Persistence --startup-project src/FreshFlow.API`.
  Run `dotnet build-server shutdown` after. Verify no accidental model changes to other modules'
  tables (cross-module Rows must map to existing tables, `builder.ToTable(..., t => t.ExcludeFromMigrations())`
  or keyless — follow how Orders' `MarketProductRow`/`RestaurantRow` avoid re-emitting those tables).

---

## 7. API / queries (scope = FR-ORD-005 criteria 2 & 3 only)

Route naming in the spec is `/api/v1/admin/order-groups`. Keep that exact path for compatibility
(the batch IS the evolved "order group"), under `AdminController` or a new `OrderGroupsController`
in `FreshFlow.API/Controllers/` (Admin-authorized, mirror existing `AdminController` RBAC).

1. `GET /api/v1/admin/order-groups` → `GetProcurementBatchesQuery` → list batches with members +
   statuses (criterion 2). Paginate; `ApiResponse.Ok`.
2. `POST /api/v1/admin/order-groups/auto-batch` → `RunAutoBatchCommand { targetDate?, dryRun?, force? }`
   → invokes `IProcurementBatchingService.BuildBatchesAsync` (criterion 3). `dryRun=true` returns
   preview, no writes. FluentValidation co-located (`RunAutoBatchCommandValidator`): `targetDate`
   not in the past, etc.

Nothing else in scope — no per-batch mutation endpoints (those belong to 272/282).

---

## 8. Events

| Event | Where | Payload | Downstream |
|---|---|---|---|
| `ProcurementBatchBuiltDomainEvent` | Procurement.Domain/Events | BatchId, MarketId, BatchDate, CoveredOrderIds | internal → integration handler |
| `ProcurementBatchBuiltIntegrationEvent` | `FreshFlow.Contracts` | BatchId, MarketId, BatchDate, CoveredOrderIds | **Orders** (flip Confirmed→Batched, THIS task); **272** (generate procurement manifest); **282** (hub inbound/handover) — declare the seam, don't build |

Only the Orders consumer is built in 271. 272/282 consumers are future tasks; the event is the
stable contract seam.

---

## 9. Test strategy (repo bar: ≥80% coverage, TDD)

Unit (`tests/Unit/FreshFlow.Procurement.UnitTests/`):
- `ProcurementBatch.Build` aggregates quantity per (market, product) across multiple orders.
- One order with items from 2 markets fans out into 2 batches; covered-order set correct.
- Empty/no-eligible → no batch, `Skipped`.
- `BatchingEnabled == false` → writes nothing.
- Idempotency: re-run over the same confirmed set with existing active coverage → no duplicate batch
  (and, with `force=true`, documented recovery behavior).
- `dryRun` returns preview and performs zero writes.
- Cutoff/timezone: cycle selection at 21:59 vs 22:01 HCMC local picks the correct `batchDate`.

Orders unit test:
- `ProcurementBatchBuiltIntegrationEventHandler` flips `Confirmed→Batched`; second delivery of the
  same event no-ops (guard) without throwing.

Integration (`tests/Integration/FreshFlow.IntegrationTests/`):
- Seed confirmed orders across 2 markets → `POST .../auto-batch` (dryRun then real) → batches + items
  persisted, orders now `Batched`, 409 on second active batch for same order.
- `GET /api/v1/admin/order-groups` returns batches with members + statuses.

---

## 10. Ordered step list (file-level, for the coding agent)

1. **Scaffold module + solution/DI wiring.**
   - `src/Modules/Procurement/FreshFlow.Procurement.{Domain,Application,Infrastructure}/*.csproj`
     (references per D-A). Add all three to `FreshFlow.slnx`. Add
     `tests/Unit/FreshFlow.Procurement.UnitTests/`.
   - `Procurement.Infrastructure/DependencyInjection.cs` → `AddProcurementModule`; register readers,
     `IProcurementBatchingService`, repositories, `AddHostedService<ProcurementBatchingHostedService>()`.
   - `src/FreshFlow.API/Program.cs`: `.AddProcurementModule(builder.Configuration)`.
2. **Domain** (TDD unit tests first): `ProcurementBatch`, `ProcurementBatchItem`,
   `ProcurementBatchOrder`, `ProcurementBatchStatus`, `ProcurementBatchBuiltDomainEvent`.
3. **Contracts**: add `ProcurementBatchBuiltIntegrationEvent` to `src/Shared/FreshFlow.Contracts/`.
4. **Cross-module readers** (Procurement.Infrastructure/CrossModule): `ConfirmedOrderRow(+Config)`,
   `ConfirmedOrderItemRow(+Config)`, `MarketProductMarketRow(+Config)`, `OperationalSettingsRow(+Config)`
   and their reader interfaces in Application/Abstractions. (Copy exact style of
   `Orders.Infrastructure/CrossModule/MarketProductReader.cs`.)
5. **Batching service**: `IProcurementBatchingService` (Application/Abstractions) +
   `BatchConfirmedOrdersService` (Application/Services) + `BatchingResult`/preview DTOs (record types).
6. **Domain→integration event bridge**: `ProcurementBatchBuiltDomainEventHandler` (Procurement.Application/EventHandlers).
7. **Orders consumer**:
   `Orders.Application/EventHandlers/ProcurementBatchBuiltIntegrationEventHandler.cs` — loads orders,
   `AdvanceStatus(Batched)`, saves; de-dup covered IDs; log-and-skip on guard failure.
8. **Persistence**: three EF configs + register discovery; EF migration `AddProcurementBatches`;
   `dotnet build-server shutdown`.
9. **API**: `RunAutoBatchCommand(+Validator+Handler)`, `GetProcurementBatchesQuery(+Handler)`,
   controller endpoints `GET/POST /api/v1/admin/order-groups[/auto-batch]`, response DTOs.
10. **Hosted service**: `ProcurementBatchingHostedService` (copy `ScheduledOrderGenerationHostedService`),
    config keys in `appsettings*.json` (`Procurement:Batching:Enabled/IntervalSeconds`).
11. **Integration tests**; run `dotnet test`; `dotnet format --verify-no-changes`; `dotnet build-server shutdown`.

### Handoff points to 272 / 274 / 282
- **272 (procurement manifest)**: consumes `ProcurementBatchBuiltIntegrationEvent`; reads
  `procurement_batches` + `procurement_batch_items` as the shopping list. Do not build the manifest here.
- **274 (routing)** / LOG: batches (grouped by market, later by zone) are the unit routes are planned
  over. Zone grouping (D1) is where LOG meets PROC.
- **282 (hub handover / inbound)**: advances batch `Built → HandedOff` and drives order
  `Batched → PickedUp → AtHub`. 271 only produces `Built` and flips to `Batched`.

---

## 11. Open decisions / risks (resolve before implementation)

- **D1 — Grouping key (BLOCKER for exact FR match).** FR-ORD-005 says group by **delivery zone AND
  market source**, but there is no restaurant→zone FK today (zone is a Logistics derivation).
  **Recommendation (MVP):** group by **market source only** for 271; add `ZoneId` to the batch when
  LOG exposes a restaurant→zone resolver (272/274). Need user sign-off that market-only is acceptable
  for the demo chain-unblock.
- **D2 — Which cycle does a batch cover?** Orders confirmed before 22:00 get `ScheduledFor = D+1`,
  after cutoff `D+2` (`OrderCutoffScheduler`). Confirm the batch cycle key = orders whose
  `ScheduledFor` local date == the batch's delivery date (recommended), **not** "all Confirmed
  regardless of schedule" (which would sweep future-dated orders early).
- **D3 — Orders confirmed after cutoff.** With D2 they naturally land in the next cycle's batch;
  confirm no special handling required beyond `ScheduledFor` bucketing.
- **D4 — Partial-cycle / missed run recovery.** If the job is down at cutoff, next tick should batch
  the missed cycle (guard by "batchDate not yet built"). Confirm we log a `MISSED_EXECUTION` warning
  like `ScheduledOrderGenerationHostedService` does.
- **D5 — Endpoint path.** Spec says `/api/v1/admin/order-groups`. Confirm we keep the "order-groups"
  noun (batch == evolved order group) vs introducing `/procurement/batches`. Recommend keeping the
  spec path.
- **D6 — Re-emitting other modules' tables in the migration.** The cross-module Row types must map
  read-only to existing tables; verify the new migration touches **only** the three
  `procurement_*` tables (follow the `ExcludeFromMigrations`/keyless approach already used by Orders'
  `MarketProductRow`/`RestaurantRow`).
- **D7 — `force=true` semantics.** Define exactly what recovery it enables (bypass active-coverage
  guard, allow re-batch of an already-`Batched` order?). Recommend: `force` bypasses coverage-link
  dedupe only; it does **not** re-flip orders already past `Confirmed` (domain guard still wins).

---

## 12. Conventions to enforce (pass to coder)

Result pattern (no business exceptions from services); record DTOs; `Async` suffix; `I`-prefixed
interfaces; FluentValidation co-located with each command; MediatR domain + integration events;
EF `IEntityTypeConfiguration` auto-discovery, snake_case columns, UUID PKs, `deleted_at` soft delete;
one shared `AppDbContext`; migration in `FreshFlow.Infrastructure.Persistence`; TDD, ≥80% coverage;
`FreshFlow.slnx` (not `.sln`); `dotnet build-server shutdown` after one-shot dotnet commands;
commit format `feat(procurement): SCRUM-271 ... ` **only after** supervisor supplies the go-ahead.
