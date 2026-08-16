# PLAN — ProcurementBatch "Completed" status

**Date:** 2026-08-06
**Jira:** SCRUM-383 (epic PROC / SCRUM-255 — commit scope uses the epic key)
**Module:** Procurement
**Gap:** Phiên chợ (`ProcurementBatch`) has no persisted status for when its whole session
finishes — i.e. every order it covers has been delivered. `Status` stops at `HandedOff` and
never moves again.

---

## 1. Current state

Lifecycle (in `ProcurementBatchStatus`):

```
Built → Manifested → Purchasing → HandedOff        (+ Cancelled from Built/Manifested)
```

- `HandedOff` is **terminal today**. Set in `ProcurementBatch.HandoverToHub`. The agent's job ends
  at hub handover; the orders keep moving Hub → Delivery, but the batch's `Status` is frozen.
- A **derived** `IsCompleted` bool already lives in `ProcurementBatchDtoMapper.IsCompleted`
  (`ProcurementBatchDtos.cs:67`): `true` when the batch is `Cancelled`, or every covered order is
  `Delivered`/`Cancelled`. Computed at read-time via `IConfirmedOrderReader.ReadStatusesAsync`.
- Procurement consumes **no** delivery event. `DeliveryCompletedIntegrationEvent(OrderId, …)`
  already exists in `FreshFlow.Contracts` and is consumed by Orders / Invoicing / Notifications /
  Logistics — Procurement is simply not subscribed.

So the machinery to *know* a session is done already exists; what's missing is a **persisted**
status + timestamp + event.

---

## 2. Two options

### Option A — Presentation-only (laziest, ~3 lines, no migration)

The derived `IsCompleted` already answers "is this session done?". Just surface it as a display
status: in the mapper, emit `"Completed"` for the `Status` string when
`IsCompleted && batch.Status == HandedOff`.

- **Cost:** ~3 lines in `ProcurementBatchDtoMapper.Map`. No enum change, no migration, no handler.
- **Gives up:** can't filter/index by completed in SQL; no `CompletedAt`; no completion event for
  audit/notifications; recomputed every read (already the case for `IsCompleted`).

**Take A if** the only need is that the UI shows "Completed" instead of "HandedOff".

### Option B — Persisted `Completed` status  ✅ recommended ("cho đủ")

Add a real terminal state driven by delivery completion.

- **Cost:** enum value + 1 nullable column + 1 event handler + 1 domain method + 1 repo lookup + a
  migration. ~1 focused change, TDD.
- **Gives:** queryable/indexable status, `CompletedAt`, a `ProcurementBatchCompletedDomainEvent`
  (→ audit / notifications later), and `Status` finally reflects reality.

Rest of this plan details **B**.

---

## 3. Design (Option B)

### 3.1 Domain

`ProcurementBatchStatus`: add `Completed` after `HandedOff`. Storage is **string by name**
(confirmed §3.4), so placement is free — no renumbering risk.

`ProcurementBatch`:
- new `public DateTime? CompletedAt { get; private set; }`
- new method:

```csharp
public Result MarkCompleted(DateTime capturedAtUtc)
{
    if (Status == ProcurementBatchStatus.Completed)
        return Result.Success();               // idempotent — the last delivery re-fires safely
    if (Status != ProcurementBatchStatus.HandedOff)
        return Result.Failure(Error.Conflict(
            "BATCH_NOT_COMPLETABLE",
            $"Procurement batch '{Id}' cannot complete from status '{Status}'."));

    Status = ProcurementBatchStatus.Completed;
    CompletedAt = capturedAtUtc;
    UpdatedAt = capturedAtUtc;
    RaiseDomainEvent(new ProcurementBatchCompletedDomainEvent(Id, MarketId, capturedAtUtc));
    return Result.Success();
}
```

Idempotency matters: the handler re-checks on **every** delivery in the batch; only the delivery
that settles the *last* open order should flip it, and a duplicate/re-delivered event must no-op.

### 3.2 Completion trigger — consume `DeliveryCompletedIntegrationEvent`

New `EventHandlers/DeliveryCompletedIntegrationEventHandler.cs` (Procurement.Application),
`INotificationHandler<DeliveryCompletedIntegrationEvent>`. Model on the existing
`ProcurementBatchHandedOffDomainEventHandler` for structure. Logic:

1. Find the batch that covers `event.OrderId` — new repo method
   `FindByOrderIdAsync(Guid orderId, CancellationToken)` (LINQ:
   `.Where(b => b.Orders.Any(o => o.OrderId == orderId) && b.DeletedAt == null)`, `Include(Orders)`).
   No batch → no-op (order wasn't batched).
2. If `batch.Status != HandedOff` → no-op (not yet handed off, or already Completed/Cancelled).
3. Read all covered order statuses: `IConfirmedOrderReader.ReadStatusesAsync(batch.Orders…)`.
4. If **all** settled (`Delivered`/`Cancelled`, reuse the `SettledOrderStatuses` set — lift it out
   of the mapper into one shared place so the rule lives once) → `batch.MarkCompleted(now)` +
   `SaveChangesAsync`. Else no-op.

> ⚠️ `ReadStatusesAsync` order-status strings must match what Orders actually writes
> (`Delivered`/`Cancelled` — PascalCase per CLAUDE.md `orders."Status"`). The derived `IsCompleted`
> already relies on exactly these values, so the seam is proven; **reuse the same constants**.

### 3.3 DTO

Keep `IsCompleted` (backward-compat), but it becomes redundant with `Status == "Completed"`.
Lazy: leave `IsCompleted` as-is so no consumer breaks; it now agrees with the persisted status
except for the brief window before the event lands. Optionally simplify it later to
`Status == Completed || Status == Cancelled`.

### 3.4 Persistence / migration

- **Status storage — CONFIRMED string by name.** `ProcurementBatchConfiguration.cs:24-29`:
  `.HasConversion<string>()`, `.HasMaxLength(20)`. `"Completed"` = 9 chars, fits — **no column
  width change, no data backfill, enum placement free.**
- `ProcurementBatchConfiguration`: map `CompletedAt` (`completed_at timestamptz null`, snake_case —
  `procurement_*` tables are fully snake_case per CLAUDE.md).
- One EF migration: `AddProcurementBatchCompletedAt` — **single `completed_at` column add**, nothing
  else (the enum value is code-only under string storage).
- No new keyless Row / seam — reuses `IConfirmedOrderReader`.

### 3.5 Contracts?

`ProcurementBatchCompletedDomainEvent` is **internal** to Procurement (domain event only). Do **not**
add a `FreshFlow.Contracts` integration event yet — nobody consumes it. Add it later if
Analytics/Notifications want a "session closed" feed (YAGNI).

---

## 4. Files touched (Option B)

| File | Change |
|---|---|
| `Domain/Enums/ProcurementBatchStatus.cs` | + `Completed` (placement per §3.4) |
| `Domain/Entities/ProcurementBatch.cs` | + `CompletedAt`, + `MarkCompleted()` |
| `Domain/Events/ProcurementBatchCompletedDomainEvent.cs` | new |
| `Application/EventHandlers/DeliveryCompletedIntegrationEventHandler.cs` | new |
| `Application/Abstractions/IProcurementBatchRepository.cs` | + `FindByOrderIdAsync` |
| `Infrastructure/Repositories/ProcurementBatchRepository.cs` | impl `FindByOrderIdAsync` |
| `Infrastructure/Persistence/Configurations/ProcurementBatchConfiguration.cs` | map `completed_at`; confirm status storage |
| `Application/Dtos/ProcurementBatchDtos.cs` | lift `SettledOrderStatuses` to shared; `IsCompleted` unchanged |
| Persistence migration | `AddProcurementBatchCompletedAt` |

No controller/endpoint change — status flows through the existing GET batch queries.

---

## 5. Tests (repo bar ≥80%, TDD)

**Unit** (`FreshFlow.Procurement.UnitTests`):
- `MarkCompleted`: `HandedOff → Completed` sets `CompletedAt` + raises event; from `Built`/
  `Purchasing`/`Cancelled` → `BATCH_NOT_COMPLETABLE`; from `Completed` → idempotent success, no
  second event.
- Handler: last order delivered + all settled → `MarkCompleted` called; one order still
  `Delivering` → no-op; order not in any batch → no-op; batch already `Completed` → no-op.

**Integration** (`FreshFlow.IntegrationTests`, real Postgres — the seam only proves here):
- Build → manifest → purchase → handover a batch of 2 orders; publish
  `DeliveryCompletedIntegrationEvent` for order 1 → batch still `HandedOff`; for order 2 → batch
  `Completed`, `completed_at` set. (Reuses the proc-282 end-to-end fixture pattern.)

---

## 6. Open questions for supervisor

1. **Scope:** Option A (display-only, ~3 lines) or B (persisted status)? "cho đủ" → recommend **B**.
2. **`Cancelled` orders:** ✅ DECIDED — **all-settled = Completed** (an order settled as
   `Delivered` or `Cancelled` counts). Matches the existing derived `IsCompleted`; no special
   "≥1 delivered" rule.
3. **Integration event:** add `ProcurementBatchCompletedIntegrationEvent` to Contracts now for a
   future ops/audit feed, or defer until a consumer exists? (Recommend: defer, YAGNI.)
4. ✅ **Jira:** SCRUM-383. Commit uses the **epic** key per CLAUDE.md:
   `feat(procurement): SCRUM-255 add Completed status to ProcurementBatch`.
