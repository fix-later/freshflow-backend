# SCRUM-282 — [UC-PROC-11][BE] Handover Goods to Hub — Implementation Plan

**Epic:** PROC (SCRUM-255) · **Branch:** `SCRUM-255-procurement-batching` · **Date:** 2026-07-15
**Author:** leader (plan only — no code/config edited) · **Status:** awaiting decision #2 ruling before coding

> **This is the loop-closing task.** After 271 advances `Confirmed→Batched`, nothing today advances an
> order any further. DEL driver pickup (`ConfirmPickupCommandHandler`) refuses any order whose status is
> not `AtHub`, so the whole downstream chain (HUB inbound already built, DEL pickup already built) is dead.
> 282 must drive the covered orders `Batched → PickedUp → AtHub` so the already-DONE downstream can run.

---

## 1. Grounded survey (every claim verified against code)

### 1a. Order status pipeline — `src/Modules/Orders/FreshFlow.Orders.Domain/Entities/Order.cs`
`AllowedTransitions` (lines 11-21) is a **one-hop** table; `AdvanceStatus` (lines 230-238) rejects any
non-adjacent jump with `ORDER_INVALID_TRANSITION` (Conflict):
```
Draft → Confirmed → Batched → PickedUp → AtHub → Delivering → Delivered
[Batched]=[PickedUp]   [PickedUp]=[AtHub]   [AtHub]=[Delivering]
```
**Consequence:** you CANNOT jump `Batched → AtHub`. Reaching `AtHub` from `Batched` requires **two
sequential `AdvanceStatus` calls** (`PickedUp`, then `AtHub`). `TransitionTo` raises
`OrderStatusChangedDomainEvent` on each hop (free SignalR/notification fan-out — FR-ORD-003 / FR-NOT-002).

### 1b. Who advances the order today — `grep AdvanceStatus` (3 callers, all in Orders.Application/EventHandlers)
| Caller | Target | Source |
|---|---|---|
| `ProcurementBatchBuiltIntegrationEventHandler` | `Batched` | 271 |
| `DeliveryStartedIntegrationEventHandler` | `Delivering` | DEL |
| `DeliveryCompletedIntegrationEventHandler` | `Delivered` | DEL |

**`Batched→PickedUp` and `PickedUp→AtHub` have ZERO callers.** This is the exact gap 282 fills.
(Independently confirmed by `docs/features/delivery/AUDIT-2026-07-11-del-backend-plan.md` line 43.)

### 1c. DEL pickup gate — `ConfirmPickupCommandHandler.cs` line 47
`if (order.Status != "AtHub") → ORDER_NOT_AT_HUB`. It only reads order status; it does **not** require a
`HubInboundEvent` to exist. So the loop is closed the moment orders reach `AtHub` — nothing else is needed
for DEL to proceed.

### 1d. HUB inbound side — `src/Modules/Hub/...`
- `HubInboundEvent` (Domain): keyed by `HubId` + nullable `SourceMarketId`/`DeliveryRouteId`/
  `DeliveryScheduleId`; items are `{MarketProductId, QuantityKg}` (**weight**, decimal). It **does not
  reference orders** and **never touches order status**. Status `PENDING → ARRIVED_AT_HUB` via `ConfirmArrival`.
- `RecordInbound` (admin/hub_staff) creates a PENDING inbound; `ScanInbound` confirms arrival, builds
  `HubInventory`, and bumps `Hub.OccupiedCapacityKg`.
- **No procurement/order/batch linkage anywhere in HUB.** `grep` for batch in Hub = 0 hits.
- `HubHandoverEvent` is the **downstream** hub→driver checkout (DEL journey start) — **not** this task's
  procurement-agent→hub handover. Do not conflate.
- **No market→hub mapping exists.** `Hub` entity has no `MarketId`; `HubInboundEvent.SourceMarketId` is a
  nullable free field. There is no resolver that says "market X ships to hub Y", and batch quantities are
  **integer units**, not Kg — so a faithful `HubInboundEvent` cannot be reconstructed from a batch today
  without net-new machinery (hub resolver + unit→Kg conversion). See Decision D2/D3.

### 1e. Batch domain & agent surface (271-278, all DONE)
- `ProcurementBatch` (`.../Entities/ProcurementBatch.cs`): `Built→Manifested→Purchasing→HandedOff`.
  **`HandedOff` is declared but unreachable** — `ConfirmPurchase` (line 206) sets `Purchasing`; nothing
  sets `HandedOff`. Covered orders live in `_orders` (`ProcurementBatchOrder.OrderId`). Ownership =
  `AssignedAgentUserId`. Guards return `Result`/`Error.Conflict`.
- Event pattern to mirror: aggregate raises `{X}DomainEvent` → `EventHandlers/{X}DomainEventHandler`
  (Application) publishes `{X}IntegrationEvent` (Contracts) → each consumer's
  `EventHandlers/{X}IntegrationEventHandler` runs **log-and-skip, idempotent, per-order try/catch,
  SaveChanges per order** (see `ProcurementBatchBuiltDomainEventHandler` + the Orders consumer).
  `ProcurementBatchBuiltIntegrationEvent` already carries `CoveredOrderIds` — reuse that shape.
- `ProcurementController` (`/api/v1/procurement`, `[Authorize(Roles="market_agent")]`): agent id from JWT
  (`TryResolveUserId`), ownership mismatch → **404** (`ConfirmPurchaseCommandHandler` line 19). Existing
  write endpoint = `PATCH tasks/{batchId}/purchase`. Mirror for handover.
- **Actor confirmed = market_agent.** `docs/04-api-design.md` line 2567: market_agent "Updates prices,
  procures goods, **and hands off to hub**." So handover is agent-facing on `ProcurementController`.

### 1f. Requirements
- There is **no explicit UC-PROC-11 / FR-PROC block** in `docs/01`; PROC acceptance criteria have been
  derived per-task throughout this epic (same as 271-278). Nearest anchors: **FR-ORD-005** (batching),
  **FR-HUB-003 / FR-HUB-NEW-001** (hub inbound is weight-based, admin/hub_staff-triggered, capacity-checked),
  and the market_agent role definition above. Acceptance criteria for 282 are therefore derived (see §7).

---

## 2. Goal & acceptance criteria

**Goal:** Let the assigned market agent hand the purchased goods for a batch to the hub, transition the
batch `Purchasing → HandedOff`, and drive every covered order `Batched → PickedUp → AtHub` so DEL pickup
can run — closing the end-to-end Confirmed→…→AtHub loop.

**Acceptance criteria (derived):**
1. `PATCH /api/v1/procurement/tasks/{batchId}/handover` by the **assigned** market agent on a batch in
   `Purchasing` → 200, batch `Status=HandedOff`, `HandedOffAt` set; returns updated `ProcurementBatchDto`.
2. Every covered order that is currently `Batched` advances to `AtHub` (through `PickedUp`); orders not in
   `Batched` are skipped without failing the batch (idempotent, log-and-skip).
3. Batch not in `Purchasing`: `Built`/`Manifested` → 409 `BATCH_NOT_PURCHASED`; already `HandedOff` →
   409 `BATCH_ALREADY_HANDED_OFF` (idempotent guard).
4. Ownership: a different agent → 404 (mirror `ConfirmPurchase`); non-`market_agent` → 403; no/invalid JWT → 401.
5. After handover, DEL `ConfirmPickup` on a covered order succeeds (proves the loop is closed).

---

## 3. Domain change — `ProcurementBatch.HandoverToHub(...)`

`src/Modules/Procurement/FreshFlow.Procurement.Domain/Entities/ProcurementBatch.cs` — add method:

```
public Result HandoverToHub(Guid? hubId, DateTime capturedAtUtc)
```
- Guard `Status == HandedOff` → `Error.Conflict("BATCH_ALREADY_HANDED_OFF", ...)` (idempotent).
- Guard `Status != Purchasing` (i.e. Built/Manifested) → `Error.Conflict("BATCH_NOT_PURCHASED",
  "must be purchased before handover")`.
- Set `Status = HandedOff`, `HandedOffAt = capturedAtUtc`, `HubId = hubId` (nullable — see D3),
  `UpdatedAt = capturedAtUtc`.
- Raise `ProcurementBatchHandedOffDomainEvent(Id, MarketId, HubId, capturedAtUtc, CoveredOrderIds)`
  where `CoveredOrderIds = _orders.Select(o => o.OrderId).Distinct().ToList().AsReadOnly()` (mirror `Build`).
- Add private-set props `DateTime? HandedOffAt` and `Guid? HubId`.

New file: `.../Domain/Events/ProcurementBatchHandedOffDomainEvent.cs` (record; mirror
`ProcurementBatchBuiltDomainEvent`, carrying `IReadOnlyList<Guid> CoveredOrderIds` + nullable `HubId`).

---

## 4. Cross-module contract + consumers (**the loop-closing decision — see D2**)

**Recommended design (Option A, split responsibility):**

**Producer** — `.../Procurement.Application/EventHandlers/ProcurementBatchHandedOffDomainEventHandler.cs`
publishes `ProcurementBatchHandedOffIntegrationEvent` (mirror `ProcurementBatchBuiltDomainEventHandler`).

**Contract** — `src/Shared/FreshFlow.Contracts/ProcurementBatchHandedOffIntegrationEvent.cs`:
```
public sealed record ProcurementBatchHandedOffIntegrationEvent(
    Guid BatchId, Guid MarketId, Guid? HubId, DateTime HandedOffAt,
    IReadOnlyList<Guid> CoveredOrderIds) : INotification;
```

**Consumer #1 (Orders — REQUIRED, closes the loop)** —
`src/Modules/Orders/FreshFlow.Orders.Application/EventHandlers/ProcurementBatchHandedOffIntegrationEventHandler.cs`.
Clone `ProcurementBatchBuiltIntegrationEventHandler` exactly (per-order try/catch, log-and-skip, SaveChanges
per order), but **walk two hops** per covered order:
```
var toPickedUp = order.AdvanceStatus(OrderStatus.PickedUp);
if (toPickedUp.IsFailure) { log+continue; }   // e.g. order already past Batched → idempotent skip
var toAtHub = order.AdvanceStatus(OrderStatus.AtHub);
if (toAtHub.IsFailure) { log+continue; }
await orders.SaveChangesAsync(ct);
```
Justification for two hops: `AllowedTransitions` forbids `Batched→AtHub` directly; the intermediate
`PickedUp` has no other producer in the system, so handover is the natural moment it occurs (goods leave the
agent = PickedUp; delivered to hub = AtHub). Doing both in one handler is the smallest change that reaches
the state DEL requires. Idempotency: re-delivery of the event finds orders already `AtHub` → first
`AdvanceStatus(PickedUp)` fails → log+continue (no throw, no double-transition).

**Consumer #2 (HUB inbound — RECOMMEND DEFER, see D3).** Not required to close the loop (DEL only checks
order status, §1c). Auto-creating a `HubInboundEvent` from a batch needs a hub resolver + unit→Kg
conversion that do not exist. Recommend NOT building it in 282; leave `HubId` in the event for a future
HUB consumer. If the user rules it in-scope, add
`.../Hub.Application/EventHandlers/ProcurementBatchHandedOffIntegrationEventHandler.cs` — but that pulls in
D3's unresolved modelling and should be its own task.

---

## 5. Persistence + migration
- `.../Infrastructure/.../ProcurementBatchConfiguration` (the existing `IEntityTypeConfiguration` for the
  batch — locate under `Procurement.Infrastructure`): map `HandedOffAt` → `handed_off_at TIMESTAMPTZ NULL`,
  `HubId` → `hub_id UUID NULL`. Both additive/nullable, snake_case (matches Orders/Logistics/Notifications
  explicit `HasColumnName` convention — memory `project_ef_column_casing`).
- One migration `AddProcurementBatchHandover` via the CLAUDE.md `dotnet ef migrations add` command
  (project `FreshFlow.Infrastructure.Persistence`, startup `FreshFlow.API`). Two `ADD COLUMN`s, no data
  backfill. Run `dotnet build-server shutdown` (+ `pkill` fallback) after the one-shot dotnet call.

---

## 6. API
`ProcurementController` (`src/FreshFlow.API/Controllers/ProcurementController.cs`) — new action mirroring
`ConfirmPurchaseAsync`:
```
[HttpPatch("tasks/{batchId:guid}/handover")]
public async Task<IActionResult> HandoverAsync(Guid batchId, [FromBody] HandoverRequest body, CancellationToken ct)
```
- `TryResolveUserId` → 401 if missing.
- Send `HandoverBatchCommand(batchId, agentUserId, body.HubId)`; map Result → `Ok(ApiResponse.Ok(...))` /
  `Error.ToActionResult()`.
- `record HandoverRequest(Guid? HubId)` (optional; notes not needed for MVP — YAGNI).
- Controller already `[Authorize(Roles="market_agent")]` → 403 for others for free.

**Command/validator/handler** under `.../Procurement.Application/Commands/HandoverBatch/`:
- `HandoverBatchCommand(Guid BatchId, Guid AgentUserId, Guid? HubId)`.
- `HandoverBatchCommandValidator` (FluentValidation, co-located): `BatchId` not empty; if `HubId` provided,
  not `Guid.Empty`.
- `HandoverBatchCommandHandler` — clone `ConfirmPurchaseCommandHandler`: load batch; if
  `null || AssignedAgentUserId != AgentUserId` → 404; call `batch.HandoverToHub(HubId, timeProvider.now)`;
  on failure return error; `SaveChangesAsync`; re-read covered-order statuses via `IConfirmedOrderReader.
  ReadStatusesAsync`; return `ProcurementBatchDtoMapper.Map(batch, statuses)`.

---

## 7. Test strategy (≥80% line+branch, mirror existing Procurement/Orders suites)
**Unit — Domain** (`tests/Unit/FreshFlow.Procurement.UnitTests/`): `HandoverToHub` from Built→409
`BATCH_NOT_PURCHASED`; Manifested→409 same; Purchasing→success (status/`HandedOffAt`/`HubId` set + event
raised with covered ids); HandedOff→409 `BATCH_ALREADY_HANDED_OFF`.
**Unit — Handler:** ownership mismatch→404; unknown batch→404; success maps DTO + persists; wrong-status
propagates 409.
**Unit — Orders consumer** (`tests/Unit/FreshFlow.Orders.UnitTests/EventHandlers/`): covered `Batched`
order → ends `AtHub` (two hops); missing order → log+skip, others still advance; order already `AtHub`
(re-delivery) → skipped, no throw; order in unexpected status → skip.
**Integration** (`tests/Integration/FreshFlow.IntegrationTests/Procurement/`): full flow
Confirmed→Batched (271) → purchase (278) → **handover (282)** → assert both covered orders `AtHub`, batch
`HandedOff`, columns persisted; then DEL `ConfirmPickup` on a covered order **succeeds** (loop proof);
RBAC (non-agent→403); ownership (other agent→404); wrong status→409.

---

## 8. Ordered file-level steps
1. `Domain/Events/ProcurementBatchHandedOffDomainEvent.cs` (new).
2. `Domain/Entities/ProcurementBatch.cs` — add `HandedOffAt`, `HubId` props + `HandoverToHub`.
3. `Shared/FreshFlow.Contracts/ProcurementBatchHandedOffIntegrationEvent.cs` (new).
4. `Procurement.Application/EventHandlers/ProcurementBatchHandedOffDomainEventHandler.cs` (new, publisher).
5. `Procurement.Application/Commands/HandoverBatch/{Command,Validator,Handler}.cs` (new).
6. `Orders.Application/EventHandlers/ProcurementBatchHandedOffIntegrationEventHandler.cs` (new, two-hop consumer).
7. `FreshFlow.API/Controllers/ProcurementController.cs` — add `HandoverAsync` + `HandoverRequest`.
8. `Procurement.Infrastructure/.../ProcurementBatchConfiguration` — map two columns.
9. `dotnet ef migrations add AddProcurementBatchHandover` (+ build-server shutdown).
10. Unit tests (domain, handler, Orders consumer) → integration test.
11. `dotnet test` + `dotnet format --verify-no-changes` + `dotnet ef migrations has-pending-model-changes`.

**Dependency rules respected:** Orders/Procurement stay decoupled — communication only through
`FreshFlow.Contracts`. No cross-module project refs. Orders consumer needs only `CoveredOrderIds` from the
event (no cross-module read).

---

## 9. Open decisions (need the user's ruling)

**D1 — Actor & endpoint.** *Recommendation: DECIDED — agent-facing.* `market_agent` on
`ProcurementController`, `PATCH tasks/{batchId}/handover`, ownership→404. Grounded in docs/04 line 2567
+ the existing 274/278 agent surface. (Flag only; proceed unless overruled.)

**D2 — How covered orders reach `AtHub` (THE key call).** *Recommendation: Option A — Orders consumer
owns status and walks `Batched→PickedUp→AtHub` (two `AdvanceStatus` hops) in one idempotent handler;
HUB inbound decoupled (D3).* Rationale: DEL only checks `order.Status=="AtHub"` (not a HubInboundEvent),
so this alone closes the loop; it reuses the proven Built-handler pattern and needs no new cross-module
reads. Rejected: (b) advancing inside the Procurement command handler (would cross module boundaries and
break the event-driven convention); (c) routing status through HUB (HUB inbound is weight-based and never
touches order status today — would need new machinery). **Sub-question:** confirm walking BOTH hops in the
handover consumer (vs. leaving `PickedUp` for a future hub-scan step). *Recommend both hops* — there is no
other `PickedUp` producer, and stopping at `PickedUp` re-breaks the loop.

**D3 — Auto-create a HUB `HubInboundEvent` on handover?** *Recommendation: DEFER to a separate task.*
Blockers grounded in code: (i) no market→hub resolver (`Hub` has no `MarketId`); (ii) batch quantities are
integer units, `HubInboundEvent.Items` are `QuantityKg` (decimal weight) — no conversion exists; (iii) DEL
does not need it. 282 should carry `HubId` (nullable) in the event for a future HUB consumer but not build
the inbound reconstruction now (YAGNI). If the user wants inbound wired in 282, it needs a hub-resolution
rule + a unit→Kg source first — surface those as prerequisites.

**D4 — `HubId` capture & validation.** *Recommendation: accept optional `hubId` in the request body, store
on the batch/event for traceability, and skip cross-module hub validation for MVP* (no `IHubReader` seam
exists in Procurement; adding one to validate a value we otherwise only record is speculative under D3-defer).
If D3 is ruled in-scope, add the hub-exists/active check as part of that.
