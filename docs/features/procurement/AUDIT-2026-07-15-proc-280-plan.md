# SCRUM-280 — [UC-PROC-09+10][BE] Report Procurement Exception & Proof — PLAN (plan only)

**Epic:** PROC (SCRUM-255) · **Branch:** `SCRUM-255-procurement-batching` · **Date:** 2026-07-15
**Predecessors DONE (in tree / committed):** 271 (Build) · 272 (Manifest) · 274 (AssignAgent) · 276 (agent task queries) · 278 (ConfirmPurchase, happy path) · 282 (Handover).
**Scope:** 280 only — the market agent reports a **procurement exception** (product unavailable / short-supplied / wrong price / damaged) **with optional photo proof** while shopping. Downstream credit/order-quantity adjustment is a **decision** (see §8 D5), not assumed.

---

## 0. Grounding — what the code actually shows (verified, not assumed)

### 0.1 The core spec has NO literal UC-PROC-09/10 or FR-PROC
`docs/01-requirements-spec.md` and `docs/04-api-design.md` contain **no** `PROC-09`, `PROC-10`, or `FR-PROC` entries (grepped). The Procurement epic's acceptance criteria live in the epic's own plan docs (`docs/features/procurement/AUDIT-*.md`), not the master spec. The only spec anchors that touch this behaviour are:
- **FR-ORD-010 (Price Band, `Should`)** — actual purchase price vs `locked_unit_price`; deviation ≤10% auto-adjust, >10% notify + 30-min re-confirm. This is the closest thing to the "wrong price" exception, and 278's plan (D3) **explicitly deferred** all price-band logic. `docs/01-requirements-spec.md:80`.
- **FR-ORD-009 / FR-HUB-NEW-002/003 (Hub discrepancy, `Must`)** — hub-stage missing/damaged → auto refund. This is the **precedent shape** (record → event → credit refund), but it fires at the **hub**, not at the market. `docs/01-requirements-spec.md:79,104,105`.

**Consequence:** 280's acceptance criteria are **not** verbatim-quotable from the master spec. I state below what the code + epic docs imply, and every requirement that isn't in the spec is flagged as a **decision needing the user's ruling** (§8). Do not treat any acceptance detail here as spec-mandated.

### 0.2 What 278 deferred to 280 (verified in `ConfirmPurchase`)
`ProcurementBatch.ConfirmPurchase` (`ProcurementBatch.cs:168-216`) hard-requires **every** item present with **positive** actuals:
```
if (lines.Count != _items.Count || _items.Any(item => !lines.ContainsKey(item.MarketProductId)))  → PURCHASE_LINES_MISMATCH (422)
if (lines.Values.Any(line => line.ActualQuantity <= 0 || line.ActualUnitPrice <= 0))               → INVALID_PURCHASE_LINE (422)
```
278's plan (`AUDIT-2026-07-15-proc-278-plan.md:100,226`) states shortfalls / "couldn't buy line X" are **the 280 use case**. **This is the crux of 280** (see §8 D1): a line that is *Unavailable* can never satisfy ConfirmPurchase, so the batch would be stuck at `Manifested` forever unless 280 relaxes that guard.

### 0.3 Cloudinary signed-upload — the exact pattern to reuse (verified)
- Shared interface: `ICloudinarySignatureService.Sign(CloudinarySignatureRequest(Folder, Extra?))` → `CloudinarySignatureResult(Signature, Timestamp, ApiKey, CloudName, Folder)` at `src/Shared/FreshFlow.SharedKernel/Application/ICloudinarySignatureService.cs`. Impl `src/FreshFlow.Infrastructure.Media/CloudinarySignatureService.cs`.
- **Reference implementation to mirror = Logistics proof-of-delivery** (identical use case: a field worker uploads a photo):
  - Command handler `src/Modules/Logistics/.../Commands/CreateProofUploadSignature/CreateProofUploadSignatureCommandHandler.cs` — resolves the aggregate, **checks ownership (driver == route.DriverUserId) → `FORBIDDEN`**, then `signer.Sign(new CloudinarySignatureRequest("freshflow/proof-of-delivery"))`, returns `UploadSignatureResponse`.
  - Response DTO `.../CreateProofUploadSignature/UploadSignatureResponse.cs`.
  - Controller `src/FreshFlow.API/Controllers/DriverController.cs:47` — `POST deliveries/{id}/proof-of-delivery/upload-signature` (get signature), then `PUT deliveries/{id}/proof-of-delivery {proofUrl}` (store URL). Two-step: client uploads to Cloudinary between them, server only ever stores the returned URL. **No new upload path — reuse this exactly.**

### 0.4 Hub discrepancy end-to-end — the downstream precedent (verified)
- Aggregate `HubDiscrepancy` (own `AggregateRoot`, own repo) → `HubDiscrepancy.Create(...)` raises `HubDiscrepancyRecordedDomainEvent` (`src/Modules/Hub/.../Entities/HubDiscrepancy.cs`).
- Domain handler republishes `HubDiscrepancyRecordedIntegrationEvent` (`src/Shared/FreshFlow.Contracts/HubDiscrepancyRecordedIntegrationEvent.cs`).
- **Orders consumer** `HubDiscrepancyRecordedIntegrationEventHandler` (`src/Modules/Orders/.../EventHandlers/`) computes `AffectedQuantity * item.LockedUnitPrice`, calls `ICreditService.RefundAsync(...)`, then publishes `RestaurantRefundIssuedIntegrationEvent`. Note its `ponytail:` comment — **no dedupe table**, the id is the trace key.

### 0.5 Procurement infra patterns already in place (verified)
- `IProcurementBatchRepository.FindByIdAsync` **eager-loads `.Include(Items).Include(Orders)`** (`ProcurementBatchRepository.cs:34`). A new `_exceptions` child collection must be added to this Include or it won't load.
- Ownership check idiom (used by ConfirmPurchase/Handover handlers): `batch is null || batch.AssignedAgentUserId != request.AgentUserId → Error.NotFound("PROCUREMENT_BATCH", id)` (**IDOR → 404, not 403**) — `ConfirmPurchaseCommandHandler.cs:18-23`.
- Agent id comes **only** from JWT (`ProcurementController.TryResolveUserId`, `NameIdentifier`/`sub`), never route/body. Controller is `[Authorize(Roles = "market_agent")]`, `Route("api/v1/procurement")`.
- Domain→integration bridge idiom: `ProcurementBatchHandedOffDomainEventHandler` (`IPublisher.Publish(new …IntegrationEvent(...))`).
- Aggregate has an `UpdatedAt` **concurrency token** (`ProcurementBatchConfiguration.cs:47`). Child collections wired with `PropertyAccessMode.Field` + cascade delete.
- Item table `procurement_batch_items`: UUID PK `gen_random_uuid()`, snake_case columns, `numeric(12,2)` money, `deleted_at` soft-delete, `idx_procurement_batch_items_batch_id`.
- Enum→string conversion `.HasConversion<string>().HasMaxLength(20)` (`ProcurementBatchConfiguration.cs:26`).

---

## 1. Domain model

**Recommendation: a `ProcurementException` child entity of the `ProcurementBatch` aggregate** (not fields on the item, not a standalone aggregate).

Why child-of-batch (mirrors Items/Orders):
- One item can accumulate **multiple** exceptions over one shopping trip (short + damaged). Fields on `ProcurementBatchItem` can't hold N events. (Rules out fields-on-item.)
- Keeping it **inside the batch aggregate** (vs a standalone `HubDiscrepancy`-style aggregate) lets the whole report flow reuse the existing `FindByIdAsync` load + ownership check + status guard + `SaveChangesAsync`, and lets the **batch** raise the domain event. HubDiscrepancy is standalone because it has no owning aggregate loaded on the write path; here the batch is already the transaction boundary. Lazier and coherent.

**Entity `ProcurementException : BaseEntity`** (`Procurement.Domain/Entities/ProcurementException.cs`):
| Field | Type | Notes |
|---|---|---|
| `ProcurementBatchId` | `Guid` | FK to batch |
| `ProcurementBatchItemId` | `Guid` | FK to the affected item (resolved from `MarketProductId`) |
| `Type` | `ProcurementExceptionType` enum | `Unavailable / Shortfall / PriceDiscrepancy / Damaged` |
| `ReportedQuantity` | `int` | affected qty (> 0) |
| `Note` | `string?` | trimmed, max 500 |
| `ProofImageUrl` | `string?` | Cloudinary URL, **optional** (§8 D4); max 500 |
| `ReportedByUserId` | `Guid` | the agent (from JWT) |
| `ReportedAt` | `DateTime` | UTC |
| (`Id/CreatedAt/UpdatedAt/DeletedAt` from `BaseEntity`) | | soft-delete |

**Enum** `Procurement.Domain/Enums/ProcurementExceptionType.cs`: `Unavailable, Shortfall, PriceDiscrepancy, Damaged`.

**New aggregate method** `ProcurementBatch.ReportException(Guid marketProductId, ProcurementExceptionType type, int reportedQuantity, string? note, string? proofImageUrl, Guid reportedByUserId, DateTime nowUtc) : Result<ProcurementException>`:
- **Status gate (§8 D3):** allow only `Manifested` or `Purchasing`; reject `Built` (`BATCH_NOT_MANIFESTED`, Conflict) and `HandedOff` (`BATCH_ALREADY_HANDED_OFF`, Conflict).
- Resolve item: `_items.FirstOrDefault(i => i.MarketProductId == marketProductId)`; null → `Error.Validation("EXCEPTION_ITEM_NOT_IN_BATCH", …)`.
- Validate `reportedQuantity > 0` and `reportedQuantity <= item.TotalQuantity` (can't be short/unavailable for more than was ordered) → `INVALID_EXCEPTION_QUANTITY` (Validation). **Result pattern — no business-rule exceptions** (do NOT copy HubDiscrepancy's `throw`; this module uses `Result`).
- Append to `_exceptions`, stamp `UpdatedAt` (bumps concurrency token), raise `ProcurementExceptionReportedDomainEvent`.
- Return `Result<ProcurementException>.Success(exception)`.

**Optional attach-proof-later** (`ProcurementBatch.AttachExceptionProof`) — only if §8 D4 chooses post-hoc proof; default recommendation is inline `proofImageUrl` on report, so **skip**.

---

## 2. Proof handling (reuse, do not invent)

Mirror Logistics proof-of-delivery exactly:
- **Signature endpoint** `POST tasks/{batchId}/exceptions/upload-signature` → new command `CreateExceptionProofUploadSignatureCommand(BatchId, AgentUserId)` → handler loads batch, **ownership check (batch.AssignedAgentUserId == agent) → 404**, then `signer.Sign(new CloudinarySignatureRequest("freshflow/procurement-exceptions"))`, returns `UploadSignatureResponse` (reuse the same-shaped record; define a Procurement-local copy — do not cross-reference the Logistics project).
- Client uploads the photo to Cloudinary with the signed params, gets back a secure URL.
- Client passes that URL as **optional** `proofImageUrl` in the report body (§3). Server only ever **stores the URL string** — never handles the binary.

Interface to depend on: `ICloudinarySignatureService` (already registered via `AddMedia` / SharedKernel; confirm `Procurement.Infrastructure` DI resolves it — it's a shared singleton, no new registration expected).

---

## 3. Persistence + migration

- New config `Procurement.Infrastructure/Persistence/Configurations/ProcurementExceptionConfiguration.cs`, table **`procurement_exceptions`**:
  - `id` UUID PK `gen_random_uuid()`; `procurement_batch_id` (req), `procurement_batch_item_id` (req); `type` string `HasConversion<string>().HasMaxLength(20)`; `reported_quantity` int; `note` `varchar(500)` null; `proof_image_url` `varchar(500)` null; `reported_by_user_id` UUID; `reported_at` timestamptz; `created_at`/`updated_at` req; `deleted_at` null (soft-delete).
  - Index `idx_procurement_exceptions_batch_id` on `procurement_batch_id`.
- Wire the child collection in `ProcurementBatchConfiguration`: `HasMany(b => b.Exceptions).WithOne().HasForeignKey(e => e.ProcurementBatchId).OnDelete(Cascade).HasConstraintName("fk_procurement_exceptions_batch")` + `SetPropertyAccessMode(Field)`. (Optional FK item→exception; batch-level FK is enough.)
- Add `.Include(b => b.Exceptions)` to `FindByIdAsync` (and the two `List*` queries if the DTO surfaces exceptions — see §4).
- **One additive migration** `AddProcurementExceptions` in `FreshFlow.Infrastructure.Persistence` (per CLAUDE.md `dotnet ef migrations add`). New table only — no ALTER on existing tables (unless §8 D1 chooses to relax ConfirmPurchase, which is code-only, no schema change).

---

## 4. API (agent-facing, on existing `ProcurementController`)

All `[Authorize(Roles = "market_agent")]`, agent id from JWT, ownership → 404.

1. `POST api/v1/procurement/tasks/{batchId:guid}/exceptions`
   Body `ReportProcurementExceptionRequest(Guid MarketProductId, string Type, int ReportedQuantity, string? Note, string? ProofImageUrl)`
   → `ReportProcurementExceptionCommand(batchId, agentUserId, …)` → `Result<ProcurementBatchDto>` (return the refreshed batch, consistent with ConfirmPurchase/Handover) → `Ok(ApiResponse.Ok(...))`. Consider `201 Created` if the user prefers resource semantics (§8 D6) — recommend `200 Ok` + full batch to match sibling endpoints.
2. `POST api/v1/procurement/tasks/{batchId:guid}/exceptions/upload-signature`
   → `CreateExceptionProofUploadSignatureCommand(batchId, agentUserId)` → `Result<UploadSignatureResponse>` → `Ok(...)`.

**FluentValidation** co-located:
- `ReportProcurementExceptionCommandValidator`: `MarketProductId != Empty`; `Type` in enum set; `ReportedQuantity > 0`; `Note` maxlen 500; `ProofImageUrl` null-or-`https://…res.cloudinary.com` prefix (defensive — only accept Cloudinary URLs, don't store arbitrary user URLs → mitigates stored-SSRF/link-injection). Note: the endpoint goes through `ISender` so `ValidationBehavior` runs (unlike the tier-2 gotcha in memory — verified this controller uses `sender.Send`).
- Signature command needs no body validator.

**RBAC:** unchanged — `market_agent` only; ownership enforced in handler (404). No admin/ops endpoint in 280 (admin visibility of exceptions = separate task if wanted).

**DTO exposure:** add `IReadOnlyList<ProcurementExceptionDto> Exceptions` to `ProcurementBatchDto` + mapper so the agent app can render reported exceptions on the task screen. This touches `ProcurementBatchDtos.cs` and requires the `List*` queries to also `.Include(Exceptions)` if those DTOs are shared. (Recommend include — cheap, and 276's task list should show exception state.)

---

## 5. Events / seams

- **Domain event** `ProcurementExceptionReportedDomainEvent(BatchId, MarketId, ProcurementExceptionId, MarketProductId, Type, ReportedQuantity, CoveredOrderIds, ReportedAt)` raised by `ReportException`. Include `CoveredOrderIds` (from `_orders`) so a future consumer can fan out to orders without a second load.
- **Domain handler** `ProcurementExceptionReportedDomainEventHandler` (Application/EventHandlers/) — **producer-only seam**, mirrors `ProcurementBatchHandedOffDomainEventHandler`.
- **Integration event** `ProcurementExceptionReportedIntegrationEvent` in `FreshFlow.Contracts` — **shape mirrors `HubDiscrepancyRecordedIntegrationEvent`** (ids + type + quantity + occurredAt + covered orders).
- **Consumer (Orders → credit adjustment):** **DECISION §8 D5.** Per the epic's deferral discipline (278 D3 deferred its Contracts event until a consumer exists) and to avoid speculative fan-out, recommend: **raise the domain event now (cheap hook), but defer the Contracts integration event + Orders consumer until the user rules downstream credit/qty adjustment in-scope.** Adding a Contracts record with zero subscribers is exactly what 278's plan declined to do.

---

## 6. Test strategy (TDD, ≥80%)

**Unit — `FreshFlow.Procurement.UnitTests`** (`ProcurementBatchExceptionTests` / handler tests):
- `ReportException`: happy path from `Manifested` and from `Purchasing` (raises event, appends child, bumps `UpdatedAt`).
- Status gate: `Built` → `BATCH_NOT_MANIFESTED`; `HandedOff` → `BATCH_ALREADY_HANDED_OFF`.
- `MarketProductId` not in batch → `EXCEPTION_ITEM_NOT_IN_BATCH`.
- `reportedQuantity <= 0` and `> TotalQuantity` → `INVALID_EXCEPTION_QUANTITY`.
- Each `ProcurementExceptionType` accepted; unknown string rejected by validator.
- Handler ownership: `batch is null` and `AssignedAgentUserId != agent` → `NotFound` (404).
- Signature handler: ownership 404; success returns non-empty signature fields (mock `ICloudinarySignatureService`).
- **If §8 D1 chosen:** ConfirmPurchase now succeeds when an `Unavailable` line is exempted; still fails if a non-excepted line is missing.

**Integration — `FreshFlow.IntegrationTests/Procurement`:**
- Report exception happy path (`market_agent` owner) → 200 + batch shows the exception; row persisted with snake_case columns + `reported_at`.
- **Ownership 404:** a different agent's JWT → 404 (IDOR).
- Status gate → 409 on `Built`/`HandedOff`.
- Validation → 422 on bad type / non-positive qty / non-Cloudinary proof URL.
- Signature endpoint → 200 with `signature/timestamp/apiKey/cloudName/folder`, folder `freshflow/procurement-exceptions`.
- Wrong role (e.g. `restaurant`) → 403.

---

## 7. Ordered, file-level step list (concrete paths)

1. `src/Modules/Procurement/FreshFlow.Procurement.Domain/Enums/ProcurementExceptionType.cs` — enum.
2. `…/Procurement.Domain/Entities/ProcurementException.cs` — child entity (`BaseEntity`).
3. `…/Procurement.Domain/Events/ProcurementExceptionReportedDomainEvent.cs`.
4. `…/Procurement.Domain/Entities/ProcurementBatch.cs` — add `_exceptions` list + `Exceptions` read-only collection + `ReportException(...)`; **(D1)** relax `ConfirmPurchase` guard to exempt items with an open `Unavailable`/fully-`Damaged` exception.
5. `…/Procurement.Infrastructure/Persistence/Configurations/ProcurementExceptionConfiguration.cs` — new table.
6. `…/Procurement.Infrastructure/Persistence/Configurations/ProcurementBatchConfiguration.cs` — wire `HasMany(Exceptions)`.
7. `…/Procurement.Infrastructure/Repositories/ProcurementBatchRepository.cs` — add `.Include(b => b.Exceptions)` to `FindByIdAsync` (+ `List*` if DTO surfaces exceptions).
8. `…/Procurement.Application/Commands/ReportException/` — `Command`, `Handler`, `Validator`.
9. `…/Procurement.Application/Commands/CreateExceptionProofUploadSignature/` — `Command`, `Handler`, `UploadSignatureResponse` (Procurement-local record).
10. `…/Procurement.Application/EventHandlers/ProcurementExceptionReportedDomainEventHandler.cs` — producer seam (Contracts event **deferred** per D5).
11. `…/Procurement.Application/Dtos/ProcurementBatchDtos.cs` — add `ProcurementExceptionDto` + mapper wiring.
12. `src/FreshFlow.API/Controllers/ProcurementController.cs` — 2 endpoints + request records.
13. `src/Shared/FreshFlow.Contracts/ProcurementExceptionReportedIntegrationEvent.cs` — **only if D5 = wire downstream now.**
14. Migration: `dotnet ef migrations add AddProcurementExceptions --project src/FreshFlow.Infrastructure.Persistence --startup-project src/FreshFlow.API` → verify snapshot diff = new table only.
15. Tests: `tests/Unit/FreshFlow.Procurement.UnitTests/…` + `tests/Integration/FreshFlow.IntegrationTests/Procurement/…`.
16. `dotnet format FreshFlow.slnx --verify-no-changes`; `dotnet build-server shutdown` (+ `pkill` fallback) after one-shot dotnet.

---

## 8. Open decisions (need the user's ruling; each with a recommendation)

- **D1 — ConfirmPurchase coupling (the crux; must resolve to be coherent).** A line reported `Unavailable` can never satisfy 278's "all lines present with positive actuals" guard, so the batch would be stuck at `Manifested`/`Purchasing` and can never hand off. **Recommend:** 280 relaxes `ConfirmPurchase` so an item with an **open `Unavailable`** exception is **exempt** from the coverage/positivity guard (it's legitimately not purchased); `Shortfall`/`PriceDiscrepancy` items still require an actual line (agent bought a reduced/adjusted quantity). This is squarely 280's job (278 punted it here) and is code-only, no schema change. *Alternative:* keep ConfirmPurchase strict and treat unblocking as a follow-up — leaves a real dead-end, not recommended.

- **D2 — Entity shape.** **Recommend:** `ProcurementException` **child of the `ProcurementBatch` aggregate** (reuses load/ownership/status/save + batch raises the event). *Alternatives:* standalone aggregate (HubDiscrepancy-style — needed only if exceptions must be written without loading the batch) or fields-on-item (rejected: can't hold multiple exceptions).

- **D3 — Status gate.** **Recommend:** allow `Manifested` and `Purchasing`; block `Built` and `HandedOff`. Confirm.

- **D4 — Proof required or optional.** **Recommend:** **optional**, passed inline as `proofImageUrl` on the report (a `Damaged` report should have a photo; `Unavailable` often has none). No separate post-hoc attach endpoint in 280 (YAGNI). *Alternatives:* make proof mandatory for `Damaged` (soft rule), or add an `AttachExceptionProof` endpoint for after-the-fact upload.

- **D5 — Downstream effect (biggest scope lever).** Does reporting an exception (a) adjust the covered orders' credit/refund now (HubDiscrepancy-style), (b) reduce fulfilled quantities on covered orders, or (c) record + proof + notify only? **Recommend (c) for 280:** record + proof + **producer-only** domain-event seam; **defer** the `FreshFlow.Contracts` integration event + Orders/credit consumer. Rationale: no FR mandates market-stage refund; double-refund risk vs the hub-stage discrepancy path; consistent with the epic's deferral discipline; cross-module credit write is its own task. If the user wants the refund now, wire `ProcurementExceptionReportedIntegrationEvent` + an Orders consumer modelled on `HubDiscrepancyRecordedIntegrationEventHandler` (reuse `ICreditService.RefundAsync`), and add a **dedupe/trace** note (the hub handler has none).

- **D6 — Response contract.** **Recommend:** `200 OK` returning the refreshed `ProcurementBatchDto` (matches ConfirmPurchase/Handover). *Alternative:* `201 Created` returning the exception resource.

---

## 9. Conventions to enforce (pass to coder)
Result pattern — **no business-rule exceptions** (guards return `Result.Failure(Error.Conflict/Validation/NotFound)`; do NOT copy HubDiscrepancy's `throw`); record DTOs; `Async` suffix; `I`-prefixed interfaces; FluentValidation co-located with the command; agent id from **JWT only** (never route/body); **IDOR → 404 not 403**; reuse `ICloudinarySignatureService` + the Logistics proof two-step (folder `freshflow/procurement-exceptions`); reuse `IProcurementBatchRepository.FindByIdAsync`/`SaveChangesAsync`/`ProcurementBatchDtoMapper` — do not re-invent; child collection wired `PropertyAccessMode.Field` + cascade; `UpdatedAt` concurrency token; snake_case columns, UUID PK `gen_random_uuid()`, `numeric(12,2)` money, `deleted_at` soft-delete; one shared `AppDbContext`; **one additive migration** (`AddProcurementExceptions`); TDD ≥80%; build/test/format against **`FreshFlow.slnx`** (not `.sln`); `dotnet build-server shutdown` (+ `pkill` fallback) after one-shot dotnet; commit `feat(procurement): SCRUM-280 …` **only after** the supervisor supplies the go-ahead (no commit without the Jira key); **coder sends results to reviewer, not leader.**
