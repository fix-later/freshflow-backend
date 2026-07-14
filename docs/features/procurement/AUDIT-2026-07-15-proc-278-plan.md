# SCRUM-278 — [UC-PROC-08][BE] Confirm Purchased Items — Implementation Plan

Epic: PROC (SCRUM-255). Date: 2026-07-15. Status: **PLAN ONLY** (no code written).
Author: backend-dev leader. Branch: `SCRUM-255-procurement-batching` (271 + 272 + 274 + 276 landed in commit `bbf5992`).
Scope: **278 only — the happy-path purchase confirmation.** The assigned market agent records what they *actually* bought per manifest line (actual quantity + actual unit price) at the wholesale market. This is the first transition that drives `Manifested → Purchasing`. Exceptions/shortfalls (280) and hub handover (282) are **separate later tasks** and are explicitly out of scope here.

---

## 0. Spec grounding (verbatim — this is all the docs say)

`docs/01-requirements-spec.md` has **no `UC-PROC-08` row** and **no "confirm purchased items" acceptance criteria.** The load-bearing text:

- Line 276 (Glossary, Market Agent): *"An internal FreshFlow employee who physically attends wholesale markets. Performs three jobs in one trip: (1) Price Scout…; (2) **Buyer — purchases goods according to the procurement manifest**; (3) Hub Handoff…"*
- Line 299 (Glossary, Procurement Batch): *"…Represents the shopping list the Market Agent executes at the wholesale market."*
- **FR-ORD-010** (line 80, priority **Should**), verbatim: *"The system shall enforce the Price Band rule: if the actual purchase price paid by the Market Agent at the wholesale market differs from the `locked_unit_price` by ≤ 10%, the system auto-adjusts the final charge without requiring restaurant re-confirmation. If the difference exceeds 10%, the system notifies the restaurant and waits up to 30 minutes for confirmation before auto-confirming."* Its acceptance criteria: *"1. When Market Agent records actual purchase prices, the system computes `|actual - locked| / locked * 100` for each item. 2. Deviation ≤ 10%: final invoice updated automatically; restaurant receives informational notification only. 3. Deviation > 10%: restaurant receives a `PriceBandExceeded` SignalR event … 30 minutes to respond. 4. If no response within 30 minutes, auto-confirms at actual price. 5. If actual price is LOWER than locked price, the restaurant pays the lower actual price…"*
- GA-013 (line 226): *"Price Band tolerance (±10%) is configurable via `system_config` key `price_band_tolerance_percent` (default `10.00`)."*
- Glossary "Price Band" (line 297): tolerance window ±10% vs `locked_unit_price`, deviations >10% trigger notification + 30-min window.

`docs/04-api-design.md`: **no agent-facing "confirm purchase" endpoint exists.** The only procurement/order-group endpoints are admin-only (list / create / auto-batch). The agent-facing surface (`GET /api/v1/procurement/tasks[...]`) was introduced by **276** (this plan's read side).

**Consequence:** 278 is a Jira-level use case with no written HTTP contract. This plan derives the write shape from the glossary ("the agent purchases according to the manifest"), FR-ORD-010's first acceptance criterion ("when Market Agent records actual purchase prices…"), and the existing 271/272/274/276 code. The §10 open decisions are what the user must ratify before coding.

### 0.1 Critical grounding finding — price band is NOT a batch-item concern (scope boundary)

FR-ORD-010's price band compares **actual vs `locked_unit_price`**. Verified in code: `locked_unit_price` lives on **`Orders.OrderItem.LockedUnitPrice`** (`src/Modules/Orders/.../Entities/OrderItem.cs:26`, `LockPrice(...)` L44) — it is **per-order-item**, in the **Orders** module. The Procurement batch item's `ReferenceUnitPrice` (272) is a **market reference price** (from the pricing snapshot), aggregated by `MarketProductId` across many orders — it is **not** the per-restaurant locked price.

Therefore full FR-ORD-010 enforcement (per-order-item deviation math, `PriceBandExceeded` SignalR event, 30-minute restaurant-confirm window, auto-adjust of the final invoice/charge) is a **cross-module Orders + Notifications + SignalR workflow**, not something 278 can or should own. **278 records actuals on the batch and transitions state; it does NOT re-charge orders, does NOT compute per-order price-band deviation, does NOT emit `PriceBandExceeded`.** That propagation belongs to a later task (part of handover 282, or a dedicated price-band task). See §3 and §10 D3.

---

## 1. What already exists (verified this session)

| Fact | Evidence |
|---|---|
| Batch aggregate + state machine | `ProcurementBatch { Status(Built→Manifested→Purchasing→HandedOff), ManifestedAt, AssignedAgentUserId, AssignedAt, TotalItemCount, Items, Orders }`, methods `Build`/`Manifest`/`AssignAgent` (Domain/Entities/ProcurementBatch.cs) |
| `Purchasing` + `HandedOff` are declared but **unreachable** today | `ProcurementBatchStatus` enum has all 4; only `Built`/`Manifested` are ever set. 278 is the first writer of `Purchasing`. |
| Manifest line = the shopping-list row 278 confirms against | `ProcurementBatchItem { MarketProductId, ProductNameSnapshot, TotalQuantity, ReferenceUnitPrice }` — **no actual-purchased fields yet** (Domain/Entities/ProcurementBatchItem.cs) |
| 272 added `ReferenceUnitPrice` as a nullable field + `SetReferencePrice` mutator | Exact precedent for how 278 adds actual fields (fields on the item, `internal` mutator, one migration) |
| Item mutator pattern | `internal void SetReferencePrice(decimal price)` — 278 adds a sibling `internal` setter |
| Write-command precedent (agent-scoped, ownership, Result, save, remap) | `AssignAgentCommand`/`Handler`/`Validator` (Application/Commands/AssignAgent/) |
| Ownership/IDOR precedent → 404 | `GetAssignedProcurementTaskQueryHandler` L17-22: `if (batch is null || batch.AssignedAgentUserId != request.AgentUserId) → Error.NotFound(...)` |
| Agent-facing controller, JWT-only agent id | `ProcurementController` (`[Authorize(Roles="market_agent")]`, `TryResolveUserId` from `ClaimTypes.NameIdentifier ?? "sub"`, else `Unauthorized()`) |
| Repo detail fetch includes Items + Orders + tracks (not `AsNoTracking`) | `ProcurementBatchRepository.FindByIdAsync` (Include Items, Include Orders, `DeletedAt == null`, trackable → mutable) + `SaveChangesAsync` |
| Response DTO + mapper | `ProcurementBatchDto` / `ProcurementBatchItemDto` / `ProcurementBatchDtoMapper.Map(batch, orderStatuses)` (Dtos/ProcurementBatchDtos.cs) |
| Domain→integration event seam already used 3× | Contracts has `ProcurementBatchBuiltIntegrationEvent`, `ProcurementManifestGeneratedIntegrationEvent`, `ProcurementAgentAssignedIntegrationEvent`; each domain event has a `…DomainEventHandler` that republishes to Contracts |
| EF item config conventions (snake_case, numeric(12,2)) | `ProcurementBatchItemConfiguration` — `reference_unit_price numeric(12,2)`, nullable |
| Additive-migration precedent | `20260714173504_AssignMarketAgentToBatch.cs` — `AddColumn` only, matching `Up`/`Down` |
| `market_agent` is the stored role; `kiosk_staff` is only a request alias | docs/04 line 191, 3228 |
| Concurrency token on batch | `UpdatedAt` `.IsConcurrencyToken()` (ProcurementBatchConfiguration L42-45) — protects against double-confirm races |

**Bottom line (ladder rung 2 — reuse):** 278 needs **new fields on `ProcurementBatchItem`** (mirroring 272), **one domain method** on `ProcurementBatch`, **one command/handler/validator**, **one controller action**, **one additive migration**, plus DTO surfacing of the new fields. No new entity, no new table, no new repository method (`FindByIdAsync` already fetches trackable Items), no new cross-module reader.

---

## 2. Where "actual purchased" data lives — recommend fields on `ProcurementBatchItem` (not a child entity)

**Recommendation: add nullable fields to `ProcurementBatchItem`**, exactly mirroring how 272 added `ReferenceUnitPrice`:

```
public int? ActualQuantity { get; private set; }
public decimal? ActualUnitPrice { get; private set; }
public DateTime? PurchasedAt { get; private set; }
internal void ConfirmPurchase(int actualQuantity, decimal actualUnitPrice, DateTime at) { ... }
```

Justification (ladder): the manifest line is 1:1 with "what to buy" and now "what was bought". A separate `ProcurementPurchaseLine` child entity/table would be premature — it buys nothing 278 needs and adds a join + config + migration. A child/history table is only justified if the spec requires **multiple purchase events per line** (partial buys, re-buys) — the happy path (278) is a single confirmation per line, so fields suffice. If 280 (exceptions) later needs a purchase *history*, add the child table then (YAGNI). See §10 D2.

**Confirmation granularity — recommend whole-batch (one call confirms all lines).** The agent finishes shopping the whole market list in one trip, then records results; the manifest is small (per-day, per-market). A single `PATCH …/purchase` carrying an array of per-line actuals is the natural unit of work, keeps the `Manifested → Purchasing` transition atomic, and matches FR-ORD-010's "records actual purchase prices" (plural). Per-item PATCH endpoints are a heavier surface with partial-state ambiguity — defer unless the mobile app demands incremental save. See §10 D1.

---

## 3. Domain — `ConfirmPurchase(lines, at)` on `ProcurementBatch`

Add one method (mirrors `Manifest`'s shape — dictionary in, guards, per-item mutate, transition, raise event):

```
public Result ConfirmPurchase(
    IReadOnlyDictionary<Guid /*MarketProductId*/, (int ActualQuantity, decimal ActualUnitPrice)> actuals,
    DateTime capturedAtUtc)
```

**Guards (order matters):**
1. **Status gate:** must be `Manifested` **or** `Purchasing`.
   - `Built` → `Result.Failure(Error.Conflict("BATCH_NOT_MANIFESTED", …))` → **409**. (An unmanifested batch has no reference prices and no assigned agent.)
   - `HandedOff` → `Result.Failure(Error.Conflict("BATCH_ALREADY_HANDED_OFF", …))` → **409**. (Cannot re-purchase after handover.)
   - Mirrors `Manifest`'s `is not Built and not Manifested` guard style.
2. **Coverage / unknown line:** every key in `actuals` must match an existing item's `MarketProductId`; every item must be covered (for whole-batch confirm) — unknown or missing product → `Error.Validation("PURCHASE_LINE_MISMATCH", …)` → 422. (Mirrors `Manifest`'s `REFERENCE_PRICE_MISSING` missing-item check.)
3. **Per-line values:** `ActualQuantity > 0`, `ActualUnitPrice > 0` (defense-in-depth; also enforced by the validator at §5). Non-positive → `Error.Validation("INVALID_PURCHASE_VALUES", …)` → 422.

**Effect:**
- For each item, call `item.ConfirmPurchase(actualQty, actualUnitPrice, capturedAtUtc)` (sets the three new fields).
- `Status = ProcurementBatchStatus.Purchasing;`
- `UpdatedAt = capturedAtUtc;` (bumps the concurrency token).
- `RaiseDomainEvent(new ProcurementPurchaseConfirmedDomainEvent(Id, MarketId, BatchDate, AssignedAgentUserId!.Value, capturedAtUtc));`

**"Fully purchased" sub-state — recommend NO new enum value in 278.** With whole-batch confirm, one successful call sets actuals on *all* lines and moves to `Purchasing` = fully purchased. A separate `Purchased` state buys nothing until handover (282) needs to distinguish "bought, awaiting handoff" from "handoff started" — and `HandedOff` already marks the terminal step. Keep the 4-value enum; "all lines have `ActualQuantity != null`" is derivable if ever needed. Adding a state = migration churn + a transition guard nobody reads yet (YAGNI). See §10 D2.

**Idempotency / re-confirm — recommend allow re-confirm while `Purchasing`.** The status gate deliberately admits `Purchasing` (not just `Manifested`) so an agent who mistyped can re-submit corrected actuals before handover; each call overwrites the line actuals and re-stamps `PurchasedAt`/`UpdatedAt`. The `UpdatedAt` concurrency token still guards against two concurrent writers (stale token → `DbUpdateConcurrencyException`). Re-confirm after `HandedOff` is blocked by guard 1. See §10 D4.

**Partial confirmation — recommend NOT allowed in 278 (whole-batch, all lines required).** Shortfalls / "couldn't buy this line" are the **280 (exceptions)** use case. 278 is the happy path: every manifest line gets an actual. Guard 2 enforces full coverage. See §10 D2/D5.

**Domain event (producer-only seam):** add `ProcurementPurchaseConfirmedDomainEvent` (Domain/Events/) + a `ProcurementPurchaseConfirmedDomainEventHandler` (Application/EventHandlers/) that republishes to Contracts **only if a downstream consumer is needed now**. Per ladder rung 1: **no consumer exists yet** (280/282 not built; price-band propagation deferred per §0.1). Recommend **raise the domain event now** (cheap, consistent with 271/272/274, gives 282 a hook) but **defer the `FreshFlow.Contracts` integration event + handler until 282/price-band actually consumes it** — adding a Contracts record with zero subscribers is speculative. See §10 D3.

---

## 4. Price-band / FR-ORD-010 validation — recommend DEFER enforcement, allow any positive actual price

Per §0.1, the price band is a per-order-item (`locked_unit_price`) cross-module workflow (Orders re-charge + Notifications + SignalR `PriceBandExceeded` + 30-min window). None of that is reachable from the Procurement aggregate, and it is priority **Should** (not Must). **278 must NOT gate the purchase on deviation** — that would block the agent from recording reality, which is the opposite of FR-ORD-010's intent (record actuals, *then* the system decides).

**Recommendation for 278:** accept any `ActualUnitPrice > 0` (no band check, no 422 on deviation). The batch stores actuals faithfully. Price-band computation/notification is a **later task** that will read these actuals (or the raised domain event) and reconcile against Orders' `locked_unit_price`. Note this explicitly in the plan handed to the coder so the omission is intentional, not an oversight. See §10 D3.

*(If the user rules that 278 must at least flag deviation vs the batch item's `ReferenceUnitPrice`, that is a cheap additive `DeviationPercent` computed field — but it is NOT FR-ORD-010, which is vs `locked_unit_price`. Recommend against conflating the two.)*

---

## 5. API — one action on the existing `ProcurementController`

Add to `src/FreshFlow.API/Controllers/ProcurementController.cs` (already `[Authorize(Roles="market_agent")]`, already has `TryResolveUserId`):

| Verb | Path | Role | Purpose |
|---|---|---|---|
| PATCH | `/api/v1/procurement/tasks/{batchId:guid}/purchase` | `market_agent` | Record actual purchased qty/price for the manifest lines; moves batch to `Purchasing` |

**Why PATCH (not POST):** it mutates existing manifest lines in place (partial update of the batch resource), consistent with the pricing module's `PATCH …/price`. Recommend `…/purchase` sub-resource verb for clarity. See §10 D1.

**Request DTO** (record, in `Application/Commands/ConfirmPurchase/`):
```
public sealed record ConfirmPurchaseRequest(IReadOnlyList<ConfirmPurchaseLineRequest> Lines);
public sealed record ConfirmPurchaseLineRequest(Guid MarketProductId, int ActualQuantity, decimal ActualUnitPrice);
```
Controller maps → `ConfirmPurchaseCommand(batchId, agentUserId /*from JWT*/, Lines)`. `agentUserId` comes **only** from the token (never body/route). Returns `Ok(ApiResponse.Ok(result.Value))` (the updated `ProcurementBatchDto`) or `result.Error.ToActionResult()`.

**Response:** reuse `ProcurementBatchDto` (now surfacing actuals — §7), so the app immediately sees the confirmed state. Same remap-after-save pattern as `AssignAgentCommandHandler`.

---

## 6. Application — `ConfirmPurchaseCommand` (mirror AssignAgent)

`Application/Commands/ConfirmPurchase/`:

- **`ConfirmPurchaseCommand(Guid BatchId, Guid AgentUserId, IReadOnlyList<ConfirmPurchaseLineDto> Lines) : ICommand<ProcurementBatchDto>`**
- **`ConfirmPurchaseCommandValidator`** (FluentValidation, co-located): `BatchId` NotEmpty; `AgentUserId` NotEmpty; `Lines` NotEmpty; `RuleForEach(Lines)` → `MarketProductId` NotEmpty, `ActualQuantity` GreaterThan 0, `ActualUnitPrice` GreaterThan 0; no duplicate `MarketProductId` in `Lines`.
- **`ConfirmPurchaseCommandHandler`** (mirrors `AssignAgentCommandHandler`):
  1. `batch = await batches.FindByIdAsync(BatchId, ct)` (trackable, includes Items).
  2. **Ownership/IDOR guard:** `if (batch is null || batch.AssignedAgentUserId != request.AgentUserId) → Result.Failure(Error.NotFound("PROCUREMENT_BATCH", BatchId))` → **404** (not 403; no ownership leak — same posture as 276 §3).
  3. Build the `Dictionary<Guid,(int,decimal)>` from `Lines`; `var op = batch.ConfirmPurchase(dict, timeProvider.GetUtcNow().UtcDateTime);` — return `op.Error` on failure (409/422 per §3).
  4. `await batches.SaveChangesAsync(ct);` (concurrency token guards double-submit).
  5. Remap: `orders.ReadStatusesAsync(batch.Orders…)` → `ProcurementBatchDtoMapper.Map(batch, statuses)` → `Result.Success`.

No new repository method (reuse `FindByIdAsync` + `SaveChangesAsync`). Handler + validator auto-register via the existing MediatR + `AddValidatorsFromAssembly` scan (DI unchanged).

---

## 7. DTO — surface the new actuals

Extend `ProcurementBatchItemDto` (Dtos/ProcurementBatchDtos.cs) with `int? ActualQuantity, decimal? ActualUnitPrice, DateTime? PurchasedAt`, and extend `ProcurementBatchDtoMapper.Map` to project them. This is additive (nullable) — the 276 read endpoints automatically start returning them (null until purchased), which is desirable (the agent app sees confirmed state on GET too). No new DTO type.

---

## 8. Persistence + migration — additive columns only

- **EF config** (`ProcurementBatchItemConfiguration`): add
  - `actual_quantity` (int, nullable),
  - `actual_unit_price` `numeric(12,2)` nullable (match `reference_unit_price`),
  - `purchased_at` `timestamp with time zone` nullable.
- **One migration** `AddProcurementPurchaseActuals` (mirror `20260714173504_AssignMarketAgentToBatch`): three `AddColumn` on `procurement_batch_items`, matching `Down` drops. No index needed (queried via the batch, not standalone). No new table.
- Generate with the CLAUDE.md command (`--project src/FreshFlow.Infrastructure.Persistence --startup-project src/FreshFlow.API`); update the model snapshot (EF does this).

---

## 9. Test strategy (repo bar: ≥80%, TDD)

**Unit — `tests/Unit/FreshFlow.Procurement.UnitTests/`:**
- *Domain* (`Domain/ProcurementBatchTests.cs`):
  - `Manifested` batch + full-coverage actuals → `Success`, `Status == Purchasing`, each item's `ActualQuantity/ActualUnitPrice/PurchasedAt` set, `UpdatedAt` bumped, `ProcurementPurchaseConfirmedDomainEvent` raised.
  - `Built` → `Conflict("BATCH_NOT_MANIFESTED")`.
  - `HandedOff` → `Conflict("BATCH_ALREADY_HANDED_OFF")`.
  - Re-confirm while `Purchasing` → `Success`, actuals overwritten (idempotent-ish).
  - Unknown / missing `MarketProductId` (partial coverage) → `Validation("PURCHASE_LINE_MISMATCH")`.
  - Non-positive qty/price → `Validation("INVALID_PURCHASE_VALUES")`.
- *Handler* (`Commands/ConfirmPurchaseCommandTests.cs`, mock `IProcurementBatchRepository` + `IConfirmedOrderReader` + `TimeProvider`):
  - Happy path → `SaveChangesAsync` called, returns `ProcurementBatchDto` with actuals populated.
  - **IDOR:** batch assigned to A, command carries B → `NotFound` (404), `SaveChangesAsync` **not** called.
  - Batch null → `NotFound`.
  - Domain failure (e.g. `Built`) surfaced as the same `Error` (409).
- *Validator* (`ConfirmPurchaseCommandValidatorTests`): empty ids, empty `Lines`, non-positive qty/price, duplicate `MarketProductId`.

**Integration — `tests/Integration/FreshFlow.IntegrationTests/Procurement/`:**
- Seed a `Manifested` batch assigned to agent A → `PATCH /procurement/tasks/{id}/purchase` as A with all lines → **200**, body shows `Purchasing` + actuals; re-`GET` (276) shows persisted actuals.
- **IDOR:** same batch, called by agent B → **404** (assert no ownership leak).
- `Built`/unassigned batch → the agent isn't the owner → **404** (ownership check fires before status). A batch owned by A but still `Built` is impossible (assignment requires `Manifested`), but assert the status guard via a direct domain/handler test.
- Confirm on a `HandedOff` batch owned by A → **409** `BATCH_ALREADY_HANDED_OFF`.
- Missing line coverage → **422** `PURCHASE_LINE_MISMATCH`.
- Non-positive price → **422** (validator).
- Wrong role (`restaurant`) → **403** (class-level attribute); missing JWT → **401**.

---

## 10. Ordered, file-level step list (all under existing Procurement module + API)

1. **Domain (tests first):**
   - `Domain/Entities/ProcurementBatchItem.cs` — add `ActualQuantity`, `ActualUnitPrice`, `PurchasedAt` + `internal void ConfirmPurchase(int qty, decimal price, DateTime at)`.
   - `Domain/Events/ProcurementPurchaseConfirmedDomainEvent.cs` — new record `(Guid BatchId, Guid MarketId, DateOnly BatchDate, Guid AgentUserId, DateTime ConfirmedAt) : IDomainEvent`.
   - `Domain/Entities/ProcurementBatch.cs` — add `ConfirmPurchase(dict, at)` (guards §3, transition, raise event).
2. **Application:**
   - `Application/Commands/ConfirmPurchase/{ConfirmPurchaseCommand, ConfirmPurchaseCommandValidator, ConfirmPurchaseCommandHandler}.cs` (new; ownership→404, remap).
   - `Application/Dtos/ProcurementBatchDtos.cs` — extend `ProcurementBatchItemDto` + mapper (§7).
   - *(Optional, deferred per §3/D3)* `Application/EventHandlers/ProcurementPurchaseConfirmedDomainEventHandler.cs` + Contracts record — only if the user rules a consumer is needed now.
3. **Infrastructure:**
   - `Infrastructure/Persistence/Configurations/ProcurementBatchItemConfiguration.cs` — map 3 new columns (§8).
   - New migration `AddProcurementPurchaseActuals` (§8) + snapshot update.
4. **API:**
   - `src/FreshFlow.API/Controllers/ProcurementController.cs` — add `PATCH tasks/{batchId:guid}/purchase` action + request DTOs (§5), reuse `TryResolveUserId`.
5. **Tests:** unit + integration (§9); `dotnet test`; `dotnet format FreshFlow.slnx --verify-no-changes`; `dotnet build-server shutdown` (+ `pkill` fallback) after one-shot dotnet commands.

No changes to DI wiring (auto-scan), no new repository method, no new cross-module reader, no new entity/table.

---

## 11. Open decisions (resolve before coding — recommendation each)

- **D1 — Endpoint shape & granularity.** **Recommend:** whole-batch `PATCH /api/v1/procurement/tasks/{batchId}/purchase` carrying `{ lines: [{ marketProductId, actualQuantity, actualUnitPrice }] }`, all lines required. Alternative: per-item `PATCH …/items/{marketProductId}/purchase` for incremental save — heavier surface, partial-state ambiguity; defer unless the mobile app needs incremental save. (docs/04 defines no contract here — needs user sign-off.)
- **D2 — Fields vs child entity, and a `Purchased` sub-state.** **Recommend:** nullable fields on `ProcurementBatchItem` (mirror 272) + **no** new enum state (`Purchasing` = fully purchased under whole-batch confirm). Alternative: a `ProcurementPurchaseLine` child table (needed only if 280 requires per-line purchase *history*) and/or a distinct `Purchased` state — defer to 280/282 (YAGNI).
- **D3 — Price band (FR-ORD-010) & downstream event.** **Recommend:** 278 stores actuals only; **no** deviation gating, **no** `PriceBandExceeded`, **no** order re-charge (all deferred — they're per-order-item vs `locked_unit_price`, cross-module, priority *Should*). Raise `ProcurementPurchaseConfirmedDomainEvent` as a producer-only seam but **defer the Contracts integration event + handler until 282/price-band consumes it.** Confirm the deferral is intended.
- **D4 — Idempotency / re-confirm.** **Recommend:** allow re-confirm while `Manifested`/`Purchasing` (overwrite actuals, re-stamp), blocked once `HandedOff`; rely on the `UpdatedAt` concurrency token for concurrent-writer safety. Alternative: one-shot (reject any confirm when already `Purchasing`) — rejected, agents mistype and need to correct before handoff.
- **D5 — Partial confirmation.** **Recommend:** **not allowed** in 278 — every manifest line must carry an actual (guard 2 → 422 on gaps). "Couldn't buy line X" / shortfalls = **280 (exceptions)**, out of scope. Confirm the boundary.
- **D6 — Ownership failure code.** **Recommend:** **404** (not 403) when the batch isn't assigned to the caller — no existence/ownership leak, matches 276 §3 and repo memory's IDOR posture. Tests bind to this.

---

## 12. Conventions to enforce (pass to coder)

Result pattern (no business exceptions — guards return `Result.Failure(Error.Conflict/Validation/NotFound)`); record DTOs; `Async` suffix; `I`-prefixed interfaces; FluentValidation co-located with the command; command returns `Result<ProcurementBatchDto>` via `ICommand<T>`; reuse `ProcurementBatchDto`/`ProcurementBatchDtoMapper`/`IConfirmedOrderReader`/`FindByIdAsync`/`SaveChangesAsync` (do **not** re-invent); agent id from JWT only (never route/body); IDOR → 404 not 403; nullable-additive columns + one additive migration (snake_case, `numeric(12,2)`); one shared `AppDbContext`; TDD ≥80%; build/test/format against `FreshFlow.slnx` (not `.sln`); `dotnet build-server shutdown` (+ `pkill` fallback) after one-shot dotnet commands; commit `feat(procurement): SCRUM-278 …` **only after** the supervisor supplies the go-ahead (no commit without the Jira key). Coder sends results to **reviewer**, not leader.
