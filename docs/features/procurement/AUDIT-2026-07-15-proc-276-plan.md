# SCRUM-276 — [UC-PROC-07][BE] View Procurement Task & Manifest — Implementation Plan

Epic: PROC (SCRUM-255). Date: 2026-07-15. Status: PLAN ONLY (no code written).
Author: backend-dev leader. Branch context: `SCRUM-353-ADM-admin-config` (271 + 272 + 274 landed in the working tree).
Scope: **276 only — READ ONLY.** The assigned market agent views their own procurement task(s) = the batch(es) assigned to them, with the manifest detail (per-market shopping list: products, quantities, reference prices). No state change, no new persisted state, no migration. This is the read side that 278 (Confirm Purchased Items) then acts on.

---

## 0. Spec grounding (verbatim — this is all the docs say)

`docs/01-requirements-spec.md` has **no `UC-PROC-07` row and no "view task / view manifest" acceptance criteria.** The only load-bearing text:

> **Market Agent** (line 276): "An **internal FreshFlow employee** who physically attends wholesale markets. Performs three jobs in one trip: (1) Price Scout…; (2) **Buyer — purchases goods according to the procurement manifest**; (3) Hub Handoff… **Assigned to one or more markets via `user_market_assignments`.** Also referred to as Procurement Agent."

> **Procurement Batch** (line 299): "…organized by delivery zone and source market. **Represents the shopping list the Market Agent executes at the wholesale market.**"

> **FR-AUTH-010 / GA-013** (lines 46, 226): RBAC is enforced per-endpoint; the agent role value is `market_agent` (legacy request alias `kiosk_staff`). Role isolation: "Market Agent can only update prices at their assigned market."

`docs/04-api-design.md`: **no agent-facing procurement endpoint exists.** The only `order-groups` endpoints (lines 1134-1136, 1449-1616, RBAC matrix 3258-3260) are **admin-only list / create / auto-batch**. There is no `GET .../tasks`, no market-agent "my batches" row anywhere in the RBAC matrix.

**Consequence:** UC-PROC-07 is a Jira-level use case with no written acceptance criteria and no API contract. This plan derives the read shape from the glossary ("the shopping list the agent executes") plus the existing 271/272/274 code, and proposes the endpoint contract. The §8 open decisions are what the user must ratify before coding.

---

## 1. What already exists (verified this session — 276 is almost pure reuse)

| Fact | Evidence |
|---|---|
| Batch aggregate carries everything the manifest view needs | `ProcurementBatch { BatchDate, MarketId, Status, ManifestedAt, AssignedAgentUserId, AssignedAt, TotalItemCount, Items, Orders }` (Domain/Entities/ProcurementBatch.cs) |
| Item = the shopping-list line, incl. the target price 272 set | `ProcurementBatchItem { MarketProductId, ProductNameSnapshot, TotalQuantity, ReferenceUnitPrice }` |
| A read DTO already exposes the exact task/manifest shape | `ProcurementBatchDto` (Dtos/ProcurementBatchDtos.cs): Id, BatchDate, MarketId, Status, ManifestedAt, **AssignedAgentUserId, AssignedAt**, TotalItemCount, **Items[](…ReferenceUnitPrice)**, **Members[](OrderId, Status)** |
| A mapper already produces it, joining live order statuses | `ProcurementBatchDtoMapper.Map(batch, orderStatuses)`; statuses from `IConfirmedOrderReader.ReadStatusesAsync` |
| The list handler pattern is ready to copy | `GetProcurementBatchesQueryHandler` = `repo.ListAsync(page,pageSize)` → collect order ids → `ReadStatusesAsync` → map |
| The filtered index for the "my batches" query already shipped in 274 | `ix_procurement_batches_assigned_agent` on `assigned_agent_user_id WHERE "assigned_agent_user_id" IS NOT NULL` (ProcurementBatchConfiguration.cs L65-67) |
| Repo detail fetch already includes Items + Orders | `ProcurementBatchRepository.FindByIdAsync(batchId)` (Include Items, Include Orders, `DeletedAt == null`) |
| Current-user-id extraction pattern | `User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")` → `Guid.TryParse` → `Unauthorized()` (AdminController L67-69) |
| Pagination validator convention | `Page > 0`, `PageSize InclusiveBetween(1,100)` (GetProcurementBatchesQueryValidator) |
| A `market_agent`-scoped controller precedent exists | `PricingController`, `ProductsController` use `[Authorize(Roles="…market_agent…")]` |

**Bottom line (ladder rung 2 — reuse what's here):** 276 needs **no new DTO, no new mapper, no new domain code, no migration, no new cross-module reader.** It needs one agent-scoped list query, one agent-scoped detail query (with an ownership guard), one repo list-by-agent method, and one thin agent-facing controller. That's it.

---

## 2. Endpoints — recommend a new `ProcurementController` (agent-facing), NOT `/admin`

This is the market agent's own view, not an admin view, so it must not live under `/admin/order-groups` (admin-only, RBAC-matrix). Propose:

| Verb | Path | Role | Purpose |
|---|---|---|---|
| GET | `/api/v1/procurement/tasks` | `market_agent` | List **my** assigned procurement tasks (paged) |
| GET | `/api/v1/procurement/tasks/{batchId:guid}` | `market_agent` | One task + full manifest, **only if assigned to me** |

**New file** `src/FreshFlow.API/Controllers/ProcurementController.cs`, class-level `[Authorize(Roles = "market_agent")]` (mirrors `DriverController`'s class-level role). A new controller (vs. bolting agent routes onto `AdminController`) is warranted: different route prefix, different role, different auth-scoping semantics (own-resource, not admin-all). It is ~2 thin actions — cheaper and clearer than overloading the admin surface.

Both actions:
1. Extract `agentUserId` from the JWT (`ClaimTypes.NameIdentifier ?? "sub"`, `Guid.TryParse`, else `Unauthorized()`), exactly as AdminController does.
2. Send the query carrying `agentUserId`.
3. `result.IsSuccess ? Ok(ApiResponse.Ok(result.Value)) : result.Error.ToActionResult()`.

---

## 3. Auth scoping & IDOR guard (the security-load-bearing part)

The agent id comes **only from the authenticated token**, never from a route/body param — an agent can't pass another agent's id. Both queries filter by `AssignedAgentUserId == agentUserId`.

- **List:** repo query `WHERE assigned_agent_user_id == agentUserId AND deleted_at IS NULL` (uses the 274 index). An agent with no assignments gets an empty page — never another agent's batches.
- **Detail (IDOR):** fetch via existing `FindByIdAsync(batchId)`, then in the handler: if `batch is null` **or** `batch.AssignedAgentUserId != agentUserId` → `Error.NotFound("PROCUREMENT_TASK", batchId)` → **404**. Must be **404, not 403** — a 403 would confirm the batch exists and leak that another agent owns it. Same guard shape as the AI-assistant IDOR fix in repo memory (compare owner to JWT, return not-found).

---

## 4. Query / DTO — reuse `ProcurementBatchDto` + mapper verbatim

No new DTO. `ProcurementBatchDto` already carries items (with `ReferenceUnitPrice`), covered-order members (id + live status), status, and all dates — that *is* the task+manifest. Add two queries + handlers under `Procurement.Application/Queries/`:

**4a. List** `Queries/GetAssignedProcurementTasks/`:
- `GetAssignedProcurementTasksQuery(Guid AgentUserId, int Page = 1, int PageSize = 20) : IQuery<ProcurementBatchListDto>`
- `…Validator`: `AgentUserId` not empty; `Page > 0`; `PageSize InclusiveBetween(1,100)` (copy the existing validator).
- `…Handler`: `repo.ListByAgentAsync(AgentUserId, Page, PageSize)` → collect order ids → `orders.ReadStatusesAsync` → `ProcurementBatchDtoMapper.Map` → `ProcurementBatchListDto` (reuse `ProcurementBatchListDto` + `ProcurementBatchPaginationDto`). Structurally identical to `GetProcurementBatchesQueryHandler`.

**4b. Detail** `Queries/GetAssignedProcurementTask/`:
- `GetAssignedProcurementTaskQuery(Guid AgentUserId, Guid BatchId) : IQuery<ProcurementBatchDto>`
- `…Validator`: both ids not empty.
- `…Handler`: `batch = repo.FindByIdAsync(BatchId)`; `if (batch is null || batch.AssignedAgentUserId != AgentUserId)` → `Error.NotFound("PROCUREMENT_TASK", BatchId)`; else `ReadStatusesAsync(batch.Orders…)` → `Map` → the single `ProcurementBatchDto`.

**Repo change** (`IProcurementBatchRepository` + `ProcurementBatchRepository`): add one method
```
Task<(IReadOnlyList<ProcurementBatch> Batches, int Total)> ListByAgentAsync(
    Guid agentUserId, int page, int pageSize, CancellationToken ct);
```
= copy `ListAsync` with an added `.Where(b => b.AssignedAgentUserId == agentUserId)` before the count/paging. Same `Include(Items)/Include(Orders)`, same `OrderByDescending(BatchDate).ThenBy(MarketId)`, same `DeletedAt == null`. `FindByIdAsync` is reused as-is for detail (ownership checked in the handler, not the repo — keeps the repo query-only and the 404 semantics in one place).

`ProcurementBatchDtoMapper` is `internal` today and already reachable from the Application assembly — no visibility change needed.

---

## 5. Which statuses are visible — recommend: all *assigned* batches (Manifested + Purchasing + HandedOff)

Filtering by `AssignedAgentUserId == agentUserId` **inherently excludes `Built`** (a batch only gets an agent after it's `Manifested`, per 274's `AssignAgent` guard). So the agent naturally sees:
- `Manifested` — assigned, not yet started (the actionable "to buy" task for 278),
- `Purchasing` — in progress (278 flipped it on first confirm),
- `HandedOff` — completed history.

Recommend **no status filter in v1** — return everything assigned to the agent, newest `BatchDate` first, so the app can show active + recent history in one call. A `?status=` query param (or `?active=true` to hide `HandedOff`) is a cheap additive follow-up if the mobile app wants it — defer (YAGNI) until asked. See §8 D2.

---

## 6. No new persisted state — confirmed

276 is read-only:
- No new columns, entities, or tables. `AssignedAgentUserId`/`AssignedAt` (274) and `ReferenceUnitPrice` (272) already exist.
- **No EF migration.** The one index this feature needs (`ix_procurement_batches_assigned_agent`) already shipped in 274 specifically for this query — verify the plan adds nothing DB-side.
- No new cross-module reader (order statuses reuse the existing `IConfirmedOrderReader.ReadStatusesAsync`).

---

## 7. Test strategy (repo bar: ≥80%, TDD)

**Unit** (`tests/Unit/FreshFlow.Procurement.UnitTests/`):
- List handler: repo returns agent's batches → mapped to `ProcurementBatchListDto`, pagination echoed; empty result → empty page (mock `IProcurementBatchRepository` + `IConfirmedOrderReader`).
- List handler: verify it calls `ListByAgentAsync(agentUserId, …)` (not `ListAsync`) — the scoping is the contract.
- Detail handler: batch assigned to caller → returns the `ProcurementBatchDto` with items (incl. `ReferenceUnitPrice`) + members.
- Detail handler **IDOR**: batch exists but `AssignedAgentUserId != caller` → `NotFound` (404), **not** 403; batch null → `NotFound`.
- Validators: empty `AgentUserId`/`BatchId`, `PageSize` out of 1..100.

**Integration** (`tests/Integration/FreshFlow.IntegrationTests/Procurement/`):
- Seed a `Manifested` batch assigned to agent A + a `Purchasing` + a `HandedOff` one → `GET /procurement/tasks` as A → 200, all three returned, none belonging to agent B; items carry `referenceUnitPrice`.
- `GET /procurement/tasks` as agent B (no assignments) → 200, empty data.
- **IDOR:** `GET /procurement/tasks/{batchId}` for a batch assigned to A, called by B → **404** (assert body has no leak of ownership).
- `GET /procurement/tasks/{batchId}` by the rightful agent A → 200 with full manifest.
- Unknown `batchId` → 404. Missing JWT → 401. Wrong role (e.g. `restaurant`) → 403 (class-level role attribute).

---

## 8. Ordered, file-level step list (all under existing Procurement module + API)

1. **Repo (tests first):**
   - `Application/Abstractions/IProcurementBatchRepository.cs` — add `ListByAgentAsync(Guid agentUserId, int page, int pageSize, CancellationToken ct)`.
   - `Infrastructure/Repositories/ProcurementBatchRepository.cs` — implement it (copy `ListAsync` + `.Where(b => b.AssignedAgentUserId == agentUserId)`).
2. **List query:** `Application/Queries/GetAssignedProcurementTasks/{Query, …Validator, …Handler}.cs` (new).
3. **Detail query:** `Application/Queries/GetAssignedProcurementTask/{Query, …Validator, …Handler}.cs` (new; ownership → 404 guard).
4. **API:** new `src/FreshFlow.API/Controllers/ProcurementController.cs` — `[Authorize(Roles="market_agent")]`, two GET actions, JWT user-id extraction (copy AdminController L67-69).
5. **Tests:** unit (§7) + integration (§7); `dotnet test`; `dotnet format --verify-no-changes`; `dotnet build-server shutdown`.

No changes to Domain, Contracts, EF configs, DI (queries/handlers auto-register via the existing MediatR + validation scan; no new interface impl to bind beyond the repo method, which is on an already-registered class), or migrations.

---

## 9. Open decisions (resolve before coding — recommendation each)

- **D1 — Endpoint paths & controller.** **Recommend:** new `ProcurementController` at `GET /api/v1/procurement/tasks` (list) + `GET /api/v1/procurement/tasks/{batchId}` (detail). Rationale: agent-owned resource, not `/admin`. Alternative: reuse a `market-agent`-scoped prefix like `/api/v1/procurement/my-tasks` — cosmetic. Needs user sign-off since the API doc defines **no** contract here.
- **D2 — Visible status set.** **Recommend:** all assigned (`Manifested` + `Purchasing` + `HandedOff`); `Built` is excluded automatically (never assigned). No `?status` filter in v1; add `?active=true` (hide `HandedOff`) later if the app asks. Alternative: `Manifested` only (hides in-progress/history) — rejected, the agent needs to resume `Purchasing` work.
- **D3 — List vs detail response shape.** **Recommend:** reuse `ProcurementBatchDto` (full items + members) for **both** list and detail — one DTO, one mapper, zero new code; batches are small (per-day, per-market, usually one per agent). Alternative: a lightweight list summary DTO (drop `Items`) to shrink the list payload — premature optimization (YAGNI); revisit only if payloads measurably hurt.
- **D4 — RBAC role.** **Recommend:** `[Authorize(Roles = "market_agent")]` (the canonical v1 role; `kiosk_staff` is only a legacy *request* alias, roles are stored as `market_agent`). Should `admin`/`operations_manager` also hit `/procurement/tasks`? **Recommend no** — admins use `/admin/order-groups` (which already shows assignment). Widen only if the user wants ops to impersonate the agent view.
- **D5 — Detail ownership: 404 vs 403.** **Recommend:** **404** when the batch isn't assigned to the caller (no existence/ownership leak). Confirm — this is the security posture and the tests bind to it.
- **D6 — Repo shape for detail.** **Recommend:** reuse `FindByIdAsync` + guard in the handler (keeps 404 logic in one place, repo stays query-only). Alternative: a `FindByIdForAgentAsync(batchId, agentUserId)` that filters in SQL — marginally fewer rows but splits the 404 decision across layers; not worth it for a single-row fetch.

---

## 10. Conventions to enforce (pass to coder)

Result pattern (no business exceptions); record DTOs; `Async` suffix; `I`-prefixed interfaces; FluentValidation co-located with each query; queries return `Result<T>` via `IQuery<T>`; reuse `ProcurementBatchDto`/`ProcurementBatchDtoMapper`/`IConfirmedOrderReader` (do **not** re-invent); agent id from JWT only (never from route/body); IDOR → 404 not 403; one shared `AppDbContext`; **no migration** (read-only); TDD ≥80%; `FreshFlow.slnx` (not `.sln`); `dotnet build-server shutdown` after one-shot dotnet commands; commit `feat(procurement): SCRUM-276 …` **only after** the supervisor supplies the go-ahead.
