# SCRUM-284 — [UC-PROC-?][BE] Monitor Procurement Progress — Implementation Plan

**Epic:** PROC (SCRUM-255) · **Branch:** `SCRUM-255-procurement-batching` · **Date:** 2026-07-15
**Author:** leader (plan only — no code/config edited) · **Status:** awaiting decisions D1–D3
**Nature:** admin-facing, **read-only** dashboard/monitoring endpoint over the batching pipeline. No writes, **no migration**.

---

## 1. Grounded survey (verified against code)

### 1a. What already exists (the reuse baseline)
`GET /api/v1/admin/order-groups` (`AdminController.GetOrderGroupsAsync`, line 208) → `GetProcurementBatchesQuery`
→ `ProcurementBatchListDto`. Per batch it already returns (see `Dtos/ProcurementBatchDtos.cs`):
- `Status`, `BatchDate`, `MarketId`, `ManifestedAt`, `AssignedAgentUserId`, `AssignedAt`, `HandedOffAt`,
  `HubId`, `TotalItemCount`.
- `Items[]` — each with `ActualQuantity`/`ActualUnitPrice`/`PurchasedAt` (**non-null ⇒ purchased, null ⇒
  pending**) and `ReferenceUnitPrice`.
- `Members[]` — `{OrderId, Status}` (order status resolved cross-module via `IConfirmedOrderReader`).
- `Exceptions[]` — soft-delete-filtered, `{Type, ReportedQuantity, Note, ProofImageUrl, ReportedByUserId, ReportedAt}`.

So the **raw per-batch detail is already served**. What a monitoring *dashboard* still lacks:
1. **Cross-cycle aggregate status counts** (how many batches Built/Manifested/Purchasing/HandedOff for a
   date). NOT derivable from a paginated list — this is the genuine net-new value.
2. **Per-batch progress rollup** (`itemsPurchased/itemsTotal`, `pending`, `exceptionCount`, `orderCount`)
   so the dashboard need not hydrate/iterate every item client-side.
3. **Date + status filtering** (the list endpoint has neither).
4. (Optional) **human-readable names** — list returns only GUIDs for agent/market/hub.

### 1b. Domain lifecycle — `ProcurementBatch.cs`
`Built → Manifested → Purchasing → HandedOff` (enum `ProcurementBatchStatus`). Timestamps captured:
`ManifestedAt`, `AssignedAt`, `HandedOffAt`. Item purchase state on `ProcurementBatchItem`
(`ActualQuantity`/`PurchasedAt` nullable). `ProcurementException` child (SCRUM-280) carries `Type`,
`ReportedQuantity`, soft-delete (`IsDeleted`). All of this is already loaded by the repository's `ListAsync`
via `.Include(Items).Include(Orders).Include(Exceptions)`.

### 1c. Repository — `ProcurementBatchRepository.cs`
`ListAsync(page, pageSize)` hydrates Items+Orders+Exceptions, filters `DeletedAt == null`, orders by
`BatchDate desc, MarketId`. **No date filter, no status filter, no aggregate/count-by-status method.**

### 1d. Query pattern to mirror (`Queries/GetProcurementBatches/`)
`record Query : IQuery<TDto>` + `internal sealed Handler(deps) : IRequestHandler<Query, Result<TDto>>` +
FluentValidation `Validator`. Handler returns `Result<T>.Success(...)`. Cross-module reads go through an
`Abstractions/I{X}Reader` interface (Application) implemented by a keyless-`Row` reader (Infrastructure).

### 1e. Cross-module name sources (verified to exist)
- **Agent name:** `users."FullName"` (`UserConfiguration.cs` line 17). Existing `MarketAgentUserRow`
  (keyless, `ToSqlQuery` over `users JOIN roles`) selects `Id/RoleId/RoleName/IsActive/DeletedAt` but
  **not** `FullName` — would need extending or a new `UserNameRow`.
- **Market name:** `markets."Name"` (Catalog owns the table; Pricing reads it via keyless `MarketRow` +
  `ToSqlQuery("SELECT Id, Name, ... FROM markets ...")` — `MarketRowConfiguration.cs`). Procurement has no
  market-name reader today (`MarketProductMarketRow` is pricing-only). Would need a new keyless row.
- **Hub name:** `hubs."Name"` (`Hub.cs` line 8, table `hubs`). No Procurement reader today.

### 1f. Auth & routing
All `order-groups` admin actions are `[Authorize(Roles = "admin")]` on `AdminController`
(`/api/v1/admin`, class-level `[Authorize]`). Market-assignment actions additionally allow
`operations_manager`.

### 1g. Requirements anchor
No explicit `UC-PROC` / `FR-PROC` block in `docs/01` (PROC criteria are derived per-task across this epic —
same as 271–282). Nearest citation: **`docs/01-requirements-spec.md` line 75, FR-ORD-005 acceptance #2** —
*"`GET /api/v1/admin/order-groups` lists all generated batches with member orders and statuses."* 284
extends that read surface into the **progress-monitoring / dashboard** view (aggregate status counts +
progress rollup + filtering). `docs/04-api-design.md` line 3103 ("`admin:orders` for Admin monitoring") and
line 3290 (GET read endpoints, dashboard-polling rate) confirm admin monitoring is an intended read surface.

---

## 2. Goal & acceptance criteria (derived)

**Goal:** Give Admin a single read-only endpoint to monitor the state of the procurement pipeline for a
cycle: how many batches sit in each lifecycle status, and a per-batch progress rollup (items purchased vs
pending, exceptions raised, assigned agent, key timestamps), filterable by date and status.

**Acceptance criteria:**
1. `GET /api/v1/admin/order-groups/progress` (admin) → 200 with a `summary` object (batch count per status
   + cycle totals) and a `batches[]` array of progress rows.
2. `?date=YYYY-MM-DD` scopes summary + rows to that `BatchDate`; omitted ⇒ latest/most-recent cycle date
   (see D2). `?status=Purchasing` filters rows (summary still reflects the full unfiltered cycle — decision D2).
3. Each progress row reports `itemsTotal`, `itemsPurchased`, `itemsPending` (= total − purchased),
   `exceptionCount` (open, soft-delete-filtered), `orderCount`, `status`, `assignedAgentUserId`,
   timestamps (`manifestedAt`, `assignedAt`, `handedOffAt`), `marketId`, `hubId`.
4. `summary` reports `totalBatches`, a per-status count map (`built/manifested/purchasing/handedOff`), and
   cycle rollups `totalItems`, `itemsPurchased`, `itemsPending`, `openExceptions`.
5. Non-`admin` → 403; no/invalid JWT → 401 (inherited from controller attributes).
6. Empty cycle (no batches for the date) → 200 with zeroed summary and empty `batches[]` (not 404).

---

## 3. Response shape (recommended MVP — IDs, no name enrichment; see D3)

```jsonc
{
  "data": {
    "date": "2026-07-15",
    "summary": {
      "totalBatches": 4,
      "statusCounts": { "Built": 0, "Manifested": 1, "Purchasing": 2, "HandedOff": 1 },
      "totalItems": 37,
      "itemsPurchased": 22,
      "itemsPending": 15,
      "openExceptions": 3
    },
    "batches": [
      {
        "batchId": "…", "marketId": "…", "status": "Purchasing",
        "assignedAgentUserId": "…", "hubId": null,
        "itemsTotal": 10, "itemsPurchased": 6, "itemsPending": 4,
        "exceptionCount": 1, "orderCount": 8,
        "manifestedAt": "…", "assignedAt": "…", "handedOffAt": null
      }
    ]
  }
}
```

New DTOs (new file `Dtos/ProcurementProgressDtos.cs` — keep separate from the fat `ProcurementBatchDto`):
`ProcurementProgressDto(DateOnly? Date, ProcurementProgressSummaryDto Summary, IReadOnlyList<ProcurementBatchProgressDto> Batches)`,
`ProcurementProgressSummaryDto(int TotalBatches, IReadOnlyDictionary<string,int> StatusCounts, int TotalItems, int ItemsPurchased, int ItemsPending, int OpenExceptions)`,
`ProcurementBatchProgressDto(Guid BatchId, Guid MarketId, string Status, Guid? AssignedAgentUserId, Guid? HubId, int ItemsTotal, int ItemsPurchased, int ItemsPending, int ExceptionCount, int OrderCount, DateTime? ManifestedAt, DateTime? AssignedAt, DateTime? HandedOffAt)`.
Optional name fields (`AssignedAgentName`, `MarketName`, `HubName`) added **only if D3 rules enrichment in**.

**Cross-module data & how sourced:**
- **Order count** — from the already-loaded `batch.Orders.Count` (no cross-module read needed; order
  *statuses* aren't part of the rollup, so we skip the `IConfirmedOrderReader` call the fat list makes).
- **Agent/market/hub *names*** — the only data requiring keyless Rows (D3). MVP omits them (returns IDs);
  if ruled in, add keyless readers per §1e: `UserNameRow` (`SELECT "Id","FullName" FROM users`),
  `MarketNameRow` (`SELECT "Id","Name" FROM markets WHERE "DeletedAt" IS NULL`, mirroring Pricing's
  `MarketRow`), `HubNameRow` (`SELECT "Id","Name" FROM hubs`), each behind an `Abstractions/I…Reader`,
  batch-resolved by the distinct id set (one query each — no N+1).

---

## 4. Implementation approach (lazy path — reuse the hydrated list)

The summary must span **all** batches for the cycle date, not one page. A daily cycle holds few batches
(≈ one per market — single digits). So: **load all non-deleted batches for the date, compute summary +
rollup in memory** from the already-`Include`d Items/Orders/Exceptions. Zero new SQL projections, zero
extra cross-module reads (unless D3 name enrichment).
`ponytail: loads all batches for the cycle date and rolls up in memory; correct and simple at market-count
scale — swap to a SQL GROUP BY only if a single date ever holds thousands of batches.`

Repository gains one read method (mirrors `ListAsync`, no paging, adds date filter):
`Task<IReadOnlyList<ProcurementBatch>> ListByDateAsync(DateOnly? date, CancellationToken ct)` — if `date`
is null, resolve the max `BatchDate` first (or return the most-recent cycle). Status filter is applied in
the handler over the loaded set (so `summary` stays whole-cycle while `batches[]` is filtered — D2).

---

## 5. Files to add / change (all under established patterns)

**Add:**
1. `src/Modules/Procurement/FreshFlow.Procurement.Application/Queries/GetProcurementProgress/GetProcurementProgressQuery.cs`
   — `record GetProcurementProgressQuery(DateOnly? Date, string? Status) : IQuery<ProcurementProgressDto>`.
2. `…/Queries/GetProcurementProgress/GetProcurementProgressQueryHandler.cs` — load via
   `ListByDateAsync`, apply status filter for rows, compute summary + rollup, map to DTO.
3. `…/Queries/GetProcurementProgress/GetProcurementProgressQueryValidator.cs` — `Status` (if present) must
   parse to `ProcurementBatchStatus`; `Date` optional.
4. `…/Application/Dtos/ProcurementProgressDtos.cs` — the three records in §3 + a small internal mapper.

**Change:**
5. `…/Application/Abstractions/IProcurementBatchRepository.cs` — add `ListByDateAsync`.
6. `…/Infrastructure/Repositories/ProcurementBatchRepository.cs` — implement `ListByDateAsync` (clone
   `ListAsync`'s `.Include`/`AsNoTracking`/`DeletedAt==null`; filter by date; no paging).
7. `src/FreshFlow.API/Controllers/AdminController.cs` — new action `GetProcurementProgressAsync`
   (`[HttpGet("order-groups/progress")] [Authorize(Roles = "admin")]`, `[FromQuery] DateOnly? date, string? status`),
   sends the query, maps `Result` → `Ok(ApiResponse.Ok(...))` / `Error.ToActionResult()`.

**Only if D3 = enrich names (add):** `Abstractions/IUserNameReader.cs`, `IMarketNameReader.cs`,
`IHubNameReader.cs` + `Infrastructure/CrossModule/{UserNameRow,MarketNameRow,HubNameRow}` (+ `…Configuration`
keyless `ToSqlQuery`) + `…Reader` impls, registered in `Procurement.Infrastructure/DependencyInjection.cs`.

**No migration** — read-only over existing columns. (Confirm with
`dotnet ef migrations has-pending-model-changes` → expect none.)

---

## 6. Tests (≥80% line+branch, mirror existing Procurement suites)

**Unit — Handler** (`tests/Unit/FreshFlow.Procurement.UnitTests/`, fake `IProcurementBatchRepository`):
- mixed-status cycle → `statusCounts` correct; `itemsPurchased` counts items with `ActualQuantity != null`;
  `itemsPending = total − purchased`; `openExceptions` excludes soft-deleted; per-row `orderCount` = orders.
- `?status=Purchasing` → `batches[]` filtered to that status, **summary still whole-cycle**.
- empty cycle → zeroed summary + empty list (not error).
- `?date` null → resolves to latest cycle date (assert selection).
**Unit — Validator:** bad `status` string → validation failure; null date/status → valid.
**Integration** (`tests/Integration/FreshFlow.IntegrationTests/Procurement/`): seed a cycle across statuses
(reuse existing batch/purchase/exception fixtures), `GET /api/v1/admin/order-groups/progress?date=…` as
admin → assert summary + rows; RBAC (non-admin → 403; anonymous → 401); `?status=` filter narrows rows.

---

## 7. Decisions needed (with recommended defaults)

**D1 — Route & auth.** *Recommend:* `GET /api/v1/admin/order-groups/progress`, `[Authorize(Roles="admin")]`
on `AdminController` (consistent with every other `order-groups` action; §1f). Alternative considered:
also allow `operations_manager` (as market-assignment actions do) — **recommend admin-only for MVP**, add
`operations_manager` only if the supervisor says ops managers watch this dashboard. (Proceed unless overruled.)

**D2 — Default date & summary-vs-filter semantics.** *Recommend:* omitted `date` ⇒ **most-recent cycle date**
(max `BatchDate`); `?status=` filters only the `batches[]` rows while `summary` always reflects the whole
unfiltered cycle (a dashboard needs the full status breakdown to be useful even when the operator drills
into one status). If you'd rather `summary` also honor the status filter, say so.

**D3 — Name enrichment (agent / market / hub).** *Recommend:* **DEFER — MVP returns IDs only.** The genuine,
non-derivable dashboard value is the aggregate status counts + progress rollup; names cost three new
cross-module keyless readers for display-only data the admin UI can already resolve from its own
market/agent/hub lookups. If the client truly can't resolve them, ruling this in adds the readers in §5
(bounded, one batched query each — no N+1). *Recommend deferring; flag to add if the frontend asks.*

---

## 8. Ordered steps
1. DTOs (`ProcurementProgressDtos.cs`). 2. `IProcurementBatchRepository.ListByDateAsync` + impl.
3. Query + Validator + Handler. 4. `AdminController` action. 5. Unit (handler, validator) → integration.
6. `dotnet test` + `dotnet format --verify-no-changes` + `dotnet ef migrations has-pending-model-changes`
   (expect: no pending model changes). Run `dotnet build-server shutdown` (+ `pkill` fallback) after
   one-shot dotnet calls.

**Dependency rules respected:** all reads via `IProcurementBatchRepository` (own module) + optional keyless
`Row` readers over shared tables (the established cross-module read pattern). No cross-module project refs;
no writes; no contract/event changes.
