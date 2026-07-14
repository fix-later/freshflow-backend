# SCRUM-272 — [UC-PROC-05][BE] Generate Procurement Manifest — Implementation Plan

Epic: PROC (SCRUM-255). Date: 2026-07-14. Status: PLAN ONLY (no code written).
Author: backend-dev leader. Branch context: `SCRUM-353-ADM-admin-config` (SCRUM-271 landed in the working tree).
Scope: **272 only** — generate the manifest from a `Built` batch and advance it to `Manifested`.

---

## 0. Spec grounding (verbatim — this is all the spec says about a "manifest")

`docs/01-requirements-spec.md` has **no dedicated FR / UC-PROC-05 row**. "Manifest" appears in exactly
one place — the glossary — and the batch is already defined as the shopping list:

> **Market Agent** (line 276): "…Performs three jobs in one trip: (1) Price Scout…; (2) **Buyer —
> purchases goods according to the procurement manifest**; (3) Hub Handoff…"

> **Procurement Batch** (line 299): "A system-generated grouping of `CONFIRMED` orders created at the
> 22:00 cutoff, organized by delivery zone and source market. **Represents the shopping list the
> Market Agent executes at the wholesale market.** Previously called `Order Group`…"

Adjacent, load-bearing requirement the manifest feeds:

> **FR-ORD-010** (Price Band): "…if the **actual purchase price** paid by the Market Agent differs
> from the `locked_unit_price` by ≤ 10%, the system auto-adjusts… When Market Agent records actual
> purchase prices, the system computes `|actual - locked| / locked * 100` for each item."

`docs/04-api-design.md` (lines 1134-1136, 1449-1556, 3258-3260) specs only the `order-groups` +
`auto-batch` endpoints (271). **There is no manifest endpoint or manifest schema anywhere in the docs.**

**Consequence:** UC-PROC-05 is a Jira-level use case with no written acceptance criteria. This plan
derives the manifest's meaning from the two glossary lines + FR-ORD-010, and the open decisions in §9
are the ones the user must ratify before coding. The single sharpest question (§9 D1) is *what a
manifest adds over the batch that already exists.*

---

## 1. What "manifest" is — recommendation: **projection over the batch, not a new aggregate**

### The facts on the ground (verified this session)

| Fact | Evidence |
|---|---|
| The `Built` batch already **is** the per-market shopping list (aggregated `(market, product) → total qty`) | `ProcurementBatch.Build(...)` + `ProcurementBatchItem{MarketProductId, ProductNameSnapshot, TotalQuantity}` |
| The status enum already reserves `Manifested` (next hop after `Built`) | `Domain/Enums/ProcurementBatchStatus.cs` = `{ Built, Manifested, Purchasing, HandedOff }` |
| Current reference price per market-product lives in Pricing | `Pricing.Domain/Entities/MarketProduct.CurrentPrice` (decimal) |
| A keyless cross-module reader over that table already exists in Procurement | `Infrastructure/CrossModule/MarketProductMarketReader` + `MarketProductMarketRow{Id, MarketId, DeletedAt}` (add `CurrentPrice`) |
| Batch line items carry **no price** today (271 explicitly deferred costing) | `ProcurementBatchItem.cs` — no price field |
| Order lines carry `LockedUnitPrice` (nullable) + `ActualQuantity`, i.e. the FR-ORD-010 baseline | `Orders.Domain/Entities/OrderItem.cs:26,28` |

### Decision: extend the existing `ProcurementBatch` aggregate; do NOT introduce `ProcurementManifest`

A separate `ProcurementManifest` + `ProcurementManifestLine` aggregate would duplicate the batch's
identity, market, date, and line set 1:1 (one manifest per batch, same lines). That is a speculative
abstraction (YAGNI) — nothing in 272/274/276/278 needs a manifest lifecycle independent of its batch.
The glossary already equates batch = shopping list, and the enum already has `Manifested`. So:

- **The manifest = the batch at `Status = Manifested`, enriched with a reference-price snapshot.**
- "Generate manifest" = a domain transition `Built → Manifested` that captures, per line, the current
  Pricing reference price as a point-in-time snapshot, plus a `ManifestedAt` timestamp.

The **only new persisted state** is the reference-price snapshot + the timestamp (§4). That snapshot
is the concrete thing a manifest adds over a raw batch: a stable "buy at roughly this" target the
agent shops against and that 276/278 compare actual purchases to (FR-ORD-010). Reading price live at
view-time instead was considered and rejected — a manifest is a *document*; prices drift between
cutoff and the market run, so it must be frozen at generation time.

> If the user decides the reference-price snapshot is out of scope for 272 (see §9 D1), the task
> collapses to a pure `Built → Manifested` status flip + `ManifestedAt` — no Pricing read, no new
> columns on items, ~half the work. Flagged, not assumed.

---

## 2. Domain changes (`Procurement.Domain`)

All in the existing aggregate — Result pattern, no business-rule exceptions.

**`ProcurementBatchItem`** — add snapshot fields + an internal setter:
- `ReferenceUnitPrice` (`decimal?`, null until manifested)
- `void SetReferencePrice(decimal price)` — `internal`, called only by `ProcurementBatch.Manifest`.

**`ProcurementBatch`** — add:
- `DateTime? ManifestedAt { get; private set; }`
- `Result Manifest(IReadOnlyDictionary<Guid, decimal> referencePrices, DateTime capturedAt)`:
  1. **Guard state:** allowed from `Built` (first generation) and `Manifested` (refresh — see idempotency).
     From `Purchasing`/`HandedOff` → `Result.Failure(Error.Conflict("BATCH_ALREADY_IN_PROGRESS", …))`
     (agent has started buying; freezing a new reference is unsafe).
  2. **Guard coverage:** every `_items[].MarketProductId` must have an entry in `referencePrices`;
     a missing one → `Error.Validation("REFERENCE_PRICE_MISSING", …)` (don't manifest a partial doc).
  3. For each item: `item.SetReferencePrice(referencePrices[item.MarketProductId])`.
  4. `Status = Manifested; ManifestedAt = capturedAt`.
  5. `RaiseDomainEvent(new ProcurementManifestGeneratedDomainEvent(Id, MarketId, BatchDate, capturedAt))`.

**Idempotency** (re-generate on an already-`Manifested` batch): re-running **refreshes** the reference
snapshot + `ManifestedAt`, stays `Manifested`, re-raises the event. This is the documented recovery /
"prices moved, re-snapshot before assigning the agent" path. Blocked once `Purchasing` (step 1). See §9 D3.

**New event:** `Domain/Events/ProcurementManifestGeneratedDomainEvent.cs`
`record (Guid BatchId, Guid MarketId, DateOnly BatchDate, DateTime GeneratedAt) : IDomainEvent`.

---

## 3. Trigger — recommendation: **explicit admin endpoint, NOT auto-after-batch**

`POST /api/v1/admin/order-groups/{batchId}/manifest`.

Justification (grounded, not assumed):
- The use case is titled **"Generate"** — a deliberate operator action, not a cutoff side effect.
- It is the setup step for **274 (assign agent)**; keeping generate/assign as distinct operator steps
  matches the ops flow (build at 22:00 → review → generate manifest near market-run time → assign agent).
- The reference-price snapshot (§1) is only meaningful captured **close to when the agent shops**;
  auto-generating at the 22:00 cutoff would freeze stale overnight prices.
- Auto-after-batch would couple Procurement's `ProcurementBatchBuiltIntegrationEventHandler` (currently
  owned by Orders for the `Batched` flip) to manifest generation — more coupling, less control.

No hosted-job / scheduled path in 272. (A per-batch "auto-manifest" flag is YAGNI; add only if ops asks.)

---

## 4. Persistence + migration (columns only — no new tables)

Because the manifest is the batch (§1), **no new table.** Add nullable columns to existing tables via
`AppDbContext`-discovered configs (already auto-scanned — `EfAssemblyRegistry.Register` in Procurement DI):

- `procurement_batches.manifested_at TIMESTAMPTZ NULL`
- `procurement_batch_items.reference_unit_price NUMERIC NULL`

Update `ProcurementBatchConfiguration` / `ProcurementBatchItemConfiguration` (snake_case, matching 271).
Nullable ⇒ safe additive migration for existing `Built` rows.

**One EF migration** `AddProcurementManifest`:
```
dotnet ef migrations add AddProcurementManifest \
  --project src/FreshFlow.Infrastructure.Persistence --startup-project src/FreshFlow.API
dotnet build-server shutdown   # per repo memory note
```
Verify the diff touches **only** those two tables (the cross-module `market_products` Row must stay
`ExcludeFromMigrations`/keyless — same guard 271 applied; see 271 plan D6).

---

## 5. API / Application (scope = generate only)

**Endpoint** (add to `AdminController`, `[Authorize(Roles = "admin")]`, mirrors the existing
`order-groups/auto-batch` action at line 219):
- `POST /api/v1/admin/order-groups/{batchId:guid}/manifest` → `GenerateManifestCommand(BatchId)` →
  returns the manifested batch (`ProcurementBatchDto`, reusing `GetProcurementBatches` shape).

**Command** `Application/Commands/GenerateManifest/`:
- `GenerateManifestCommand(Guid BatchId) : ICommand<Result<ProcurementBatchDto>>`
- `GenerateManifestCommandValidator` (co-located): `BatchId` not empty.
- `GenerateManifestCommandHandler`:
  1. Load the batch (add `FindByIdAsync(Guid, ct)` to `IProcurementBatchRepository` — repo currently
     only has `AddRange/SaveChanges/CycleExists/List`; needs a tracked single-load incl. `Items`).
     Null → `Error.NotFound("PROCUREMENT_BATCH_NOT_FOUND", …)` → 404.
  2. Read reference prices: extend `IMarketProductMarketReader` with
     `Task<IReadOnlyDictionary<Guid, decimal>> ReadReferencePricesAsync(IReadOnlyCollection<Guid>, ct)`
     (add `CurrentPrice` to `MarketProductMarketRow`; same keyless read, batch-loaded, no N+1).
  3. `batch.Manifest(prices, timeProvider.GetUtcNow().UtcDateTime)`; on failure return the error
     (409 for `BATCH_ALREADY_IN_PROGRESS`, 422 for `REFERENCE_PRICE_MISSING`).
  4. `SaveChangesAsync`; map to `ProcurementBatchDto`.

**DTO change:** add `decimal? ReferenceUnitPrice` to `ProcurementBatchItemDto` (`Dtos/ProcurementBatchDtos.cs`)
so the manifested list/response exposes the snapshot. `GetProcurementBatchesQueryHandler.ToDto` maps it.

No new GET in 272 — viewing the manifest is **276** (agent view). The POST returns the generated
manifest, and the existing `GET /order-groups` now surfaces `Status = "Manifested"` + reference prices.

---

## 6. Events / seams toward 274 (declare, don't build)

- **Domain:** `ProcurementManifestGeneratedDomainEvent` (§2) — internal to Procurement.
- **Integration (new, in `FreshFlow.Contracts`):** `ProcurementManifestGeneratedIntegrationEvent(Guid BatchId, Guid MarketId, DateOnly BatchDate, DateTime GeneratedAt) : INotification`, published by a
  `ProcurementManifestGeneratedDomainEventHandler` (mirrors the existing
  `ProcurementBatchBuiltDomainEventHandler`). **No consumer built in 272.** 274 (assign agent) is the
  first consumer — or 274 may simply query `Manifested` batches; either way the event is the stable seam.
  Building the handler now is optional (YAGNI) — recommend adding the Contracts record + producer so
  274 has the seam ready, but skip if the user wants 272 minimal.

---

## 7. Test strategy (repo bar: ≥80%, TDD)

**Unit** (`tests/Unit/FreshFlow.Procurement.UnitTests/`):
- `Manifest` on a `Built` batch → `Status = Manifested`, `ManifestedAt` set, every item has
  `ReferenceUnitPrice`, `ProcurementManifestGeneratedDomainEvent` raised once.
- Missing reference price for one line → `REFERENCE_PRICE_MISSING`, no state change, no event.
- `Manifest` on `Purchasing`/`HandedOff` → `BATCH_ALREADY_IN_PROGRESS`, unchanged.
- Idempotency: `Manifest` twice (Built→Manifested→refresh) → still `Manifested`, prices refreshed,
  event re-raised (per §9 D3 decision).
- Handler: batch not found → `NotFound`.

**Integration** (`tests/Integration/FreshFlow.IntegrationTests/Procurement/`):
- Seed a `Built` batch + matching `market_products` rows → `POST …/{batchId}/manifest` → 200, DB shows
  `manifested_at` + `reference_unit_price` populated, `GET /order-groups` shows `Manifested` + prices.
- POST on an unknown batchId → 404. POST twice → second still 200 (refresh). (Optionally: force a
  `Purchasing` batch → 409.)

---

## 8. Ordered, file-level step list

1. **Domain** (tests first):
   - `Domain/Entities/ProcurementBatchItem.cs` — add `ReferenceUnitPrice` + internal `SetReferencePrice`.
   - `Domain/Entities/ProcurementBatch.cs` — add `ManifestedAt` + `Manifest(...)` with guards + event.
   - `Domain/Events/ProcurementManifestGeneratedDomainEvent.cs` (new).
2. **Contracts:** `src/Shared/FreshFlow.Contracts/ProcurementManifestGeneratedIntegrationEvent.cs` (new).
3. **Cross-module read:** add `CurrentPrice` to `Infrastructure/CrossModule/MarketProductMarketRow.cs`
   (+ its `…RowConfiguration` if the column needs mapping); add `ReadReferencePricesAsync` to
   `IMarketProductMarketReader` + `MarketProductMarketReader`.
4. **Repository:** add `FindByIdAsync(Guid, ct)` (tracked, `Include(Items)`) to
   `IProcurementBatchRepository` + `ProcurementBatchRepository`.
5. **Application:** `Application/Commands/GenerateManifest/{GenerateManifestCommand, …Validator, …Handler}.cs`;
   `Application/EventHandlers/ProcurementManifestGeneratedDomainEventHandler.cs` (publish integration event).
6. **DTO:** add `ReferenceUnitPrice` to `ProcurementBatchItemDto` (`Dtos/ProcurementBatchDtos.cs`);
   map it in `GetProcurementBatchesQueryHandler.ToDto`.
7. **Persistence:** update `ProcurementBatchConfiguration` + `ProcurementBatchItemConfiguration`
   (`manifested_at`, `reference_unit_price`); migration `AddProcurementManifest`; `dotnet build-server shutdown`.
8. **API:** add `POST order-groups/{batchId:guid}/manifest` action to `AdminController` (admin role).
9. **Tests:** unit + integration (§7); `dotnet test`; `dotnet format --verify-no-changes`;
   `dotnet build-server shutdown`.

Handoff to 274 (assign agent): consumes `Manifested` batches / the new integration event. To
276/278 (agent views + records actual purchase): reads `reference_unit_price` as the FR-ORD-010
target; advances `Manifested → Purchasing → HandedOff`. Do not build those here.

---

## 9. Open decisions (resolve before coding — recommendation each)

- **D1 — Does the manifest persist a reference-price snapshot, or is it a bare `Built → Manifested`
  flip? (BLOCKER — sizes the whole task.)** The spec is silent; the manifest's only real value-add
  over the existing batch is a frozen "buy-at" reference feeding FR-ORD-010. **Recommend: include the
  snapshot** (`reference_unit_price` from Pricing `MarketProduct.CurrentPrice` at generate time). If
  the user says no, drop steps 3/6 and the item column — 272 becomes a status flip + `ManifestedAt`.
- **D2 — Separate `ProcurementManifest` aggregate vs. batch projection.** **Recommend: projection**
  (extend `ProcurementBatch`, no new tables). One-manifest-per-batch with identical lines makes a
  separate aggregate pure duplication. Revisit only if a manifest must outlive/differ from its batch.
- **D3 — Idempotency semantics on re-generate.** **Recommend: allow refresh while `Manifested`**
  (re-snapshot prices, stay `Manifested`, re-raise event); **block once `Purchasing`** (409). Alternative:
  hard-idempotent no-op on second call. Pick one — tests depend on it.
- **D4 — Reference-price source.** `MarketProduct.CurrentPrice` (live latest) vs a `price_snapshots`
  historical row. **Recommend: `CurrentPrice`** — simplest, and "current" is exactly what a
  just-before-purchase reference should be. `price_snapshots` adds a partitioned-table read for no gain.
- **D5 — Endpoint shape.** `POST /admin/order-groups/{batchId}/manifest` (per-batch, recommended) vs a
  bulk "manifest all Built batches for a date". **Recommend: per-batch** — matches 274's per-batch
  agent assignment and keeps the operator in control. Bulk is YAGNI.
- **D6 — Build the 274 integration-event seam now?** **Recommend: yes, producer + Contracts record
  only** (no consumer), so 274 starts clean. Acceptable to defer entirely if the user wants 272 minimal.
- **D7 — RBAC.** 271's `order-groups` endpoints are `admin` only (AdminController). FR-ORD-005 RBAC
  matrix (api-design 3258-3260) also grants `operations_manager`. **Recommend: `admin` only for 272**
  to match the sibling `auto-batch` action; widen to `operations_manager` only if ops asks.

---

## 10. Conventions to enforce (pass to coder)

Result pattern (no business exceptions from domain/services); record DTOs; `Async` suffix; `I`-prefixed
interfaces; FluentValidation co-located with the command; MediatR domain→integration event bridge;
EF `IEntityTypeConfiguration` auto-discovery, snake_case columns, UUID PKs, additive nullable columns;
one shared `AppDbContext`; migration in `FreshFlow.Infrastructure.Persistence`; TDD ≥80%; `FreshFlow.slnx`
(not `.sln`); `dotnet build-server shutdown` after one-shot dotnet commands; commit
`feat(procurement): SCRUM-272 …` **only after** the supervisor supplies the go-ahead.
</content>
</invoke>
