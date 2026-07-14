# SCRUM-274 — [UC-PROC-06][BE] Assign Market Agent — Implementation Plan

Epic: PROC (SCRUM-255). Date: 2026-07-15. Status: PLAN ONLY (no code written).
Author: backend-dev leader. Branch context: `SCRUM-353-ADM-admin-config` (SCRUM-271 + 272 landed in the working tree).
Scope: **274 only** — assign a market agent to a `Manifested` procurement batch so 276 (agent view) and 278 (confirm purchases) can proceed.

---

## 0. Spec grounding (verbatim — this is all the docs say)

`docs/01-requirements-spec.md` has **no dedicated FR / UC-PROC-06 row** and no "assign agent" acceptance criteria. The only load-bearing text:

> **Market Agent** (line 276): "An **internal FreshFlow employee** who physically attends wholesale markets. Performs three jobs in one trip: (1) Price Scout…; (2) **Buyer — purchases goods according to the procurement manifest**; (3) Hub Handoff… **Assigned to one or more markets via `user_market_assignments`.**"

> **Procurement Batch** (line 299): "…organized by delivery zone and source market. Represents the shopping list the Market Agent executes at the wholesale market."

> **GA-013** (line 226): "…The `user_market_assignments` table continues to work as designed — it now maps which Agent covers which market."

> **Role isolation** (line 167): "Market Agent can only update prices at their assigned market…"

`docs/04-api-design.md`: **no assign-agent endpoint exists.** The `order-groups` group (lines 1134-1136, 1449-1616) specs only list / create / auto-batch. The RBAC matrix (lines 3258-3260) grants **both `admin` and `operations_manager`** to every `order-groups` row. `user_market_assignments` is the canonical agent↔market map (lines 2539, 2589, 3026, 3324, 3330).

**Consequence:** UC-PROC-06 is a Jira-level use case with no written acceptance criteria. This plan derives assignment semantics from the glossary (agent is *assigned to markets*, and *buys per manifest*) plus the existing 271/272 code. The §10 open decisions are what the user must ratify before coding.

---

## 1. Where the assigned agent lives — recommendation: **a field on `ProcurementBatch`, not a new entity**

Verified facts on the ground this session:

| Fact | Evidence |
|---|---|
| A batch is per-market; agent↔market is one-to-many | `ProcurementBatch.MarketId`; `user_market_assignments (UserId, MarketId)` |
| 272 already extended the aggregate with a nullable timestamp column (`ManifestedAt`) — the exact precedent | `ProcurementBatch.ManifestedAt`, migration `AddProcurementManifest` |
| The status enum reserves `Purchasing` **after** `Manifested` | `Domain/Enums/ProcurementBatchStatus.cs = { Built, Manifested, Purchasing, HandedOff }` |
| Agent validation data (role + assignment + active) is all readable cross-module without a project ref | `users."RoleId"→roles."Name"`, `user_market_assignments`, `users."IsActive"/"DeletedAt"` |
| Precedent for the agent-assignment cross-module read already exists in Pricing | `Pricing.Infrastructure/CrossModule/AssignedMarketReader.HasAssignmentAsync(agentUserId, marketId)` |

**Decision:** add two nullable columns to the existing aggregate — `AssignedAgentUserId (Guid?)` + `AssignedAt (DateTime?)`. A separate `ProcurementBatchAssignment` entity would be a 1:1 satellite of the batch (one agent per batch) — speculative (YAGNI). This mirrors exactly how 272 added `ManifestedAt`. No new table.

---

## 2. Status semantics — recommendation: **assignment keeps `Manifested`; it does NOT flip to `Purchasing`**

The enum orders `Manifested → Purchasing → HandedOff`. `Purchasing` means the agent has *started buying* (first purchase confirm, 278) — assignment is a setup step that happens *before* the agent is at the market. Flipping to `Purchasing` at assignment time would (a) misrepresent state on the ops dashboard, and (b) collide with 278, which is the natural owner of `Manifested → Purchasing`.

So `AssignAgent` requires `Status == Manifested` (409 otherwise) and **leaves status `Manifested`**. 278 later transitions `Manifested → Purchasing` on the first confirmed purchase. This keeps the assigned-agent field orthogonal to the lifecycle, which is why a plain field beats a status hop.

---

## 3. Domain changes (`Procurement.Domain`)

All in the existing aggregate — Result pattern, no business-rule exceptions.

**`ProcurementBatch`** — add:
- `Guid? AssignedAgentUserId { get; private set; }`
- `DateTime? AssignedAt { get; private set; }`
- `Result AssignAgent(Guid agentUserId, DateTime assignedAtUtc)`:
  1. **Guard input:** `agentUserId == Guid.Empty` → `Error.Validation("INVALID_AGENT", …)`. (Cross-module eligibility is checked in the handler, §4 — the aggregate can't read other tables.)
  2. **Guard state:** allowed only from `Manifested`. From `Built` → `Error.Conflict("BATCH_NOT_MANIFESTED", …)` (can't assign before the manifest exists). From `Purchasing`/`HandedOff` → `Error.Conflict("BATCH_ALREADY_IN_PROGRESS", …)` (agent already buying — reassigning mid-run is unsafe).
  3. **Idempotency / reassignment** (still `Manifested`): allowed. Overwrite `AssignedAgentUserId` + refresh `AssignedAt`, re-raise the event. Reassigning the *same* agent is a harmless refresh. (See §10 D3.)
  4. Set `AssignedAgentUserId = agentUserId; AssignedAt = assignedAtUtc; UpdatedAt = assignedAtUtc`.
  5. `RaiseDomainEvent(new ProcurementAgentAssignedDomainEvent(Id, MarketId, agentUserId, assignedAtUtc))`.

**New event:** `Domain/Events/ProcurementAgentAssignedDomainEvent.cs`
`record (Guid BatchId, Guid MarketId, Guid AgentUserId, DateTime AssignedAt) : IDomainEvent`.

---

## 4. Cross-module validation — active market_agent assigned to `batch.MarketId`

The aggregate cannot read Auth tables (no project ref). The **handler** validates eligibility before calling `AssignAgent`, via a new keyless reader in `Procurement.Infrastructure/CrossModule/`, modeled on the existing `MarketProductMarketReader` and Pricing's `AssignedMarketReader.HasAssignmentAsync`.

**New abstraction** `Application/Abstractions/IMarketAgentReader.cs`:
```
Task<bool> IsEligibleMarketAgentAsync(Guid agentUserId, Guid marketId, CancellationToken ct);
```
Returns true iff the user is **(a) an active market_agent and (b) assigned to `marketId`**.

**New impl** `Infrastructure/CrossModule/MarketAgentReader.cs` — join three cross-module projections (all PascalCase columns, verified against `AppDbContextModelSnapshot`):

| Table | Columns read | Purpose |
|---|---|---|
| `user_market_assignments` | `"UserId"`, `"MarketId"` | agent covers this market (existing keyless-row precedent: Pricing `UserMarketAssignmentRow`) |
| `users` | `"Id"`, `"RoleId"`, `"IsActive"`, `"DeletedAt"` | user is active + not soft-deleted |
| `roles` | `"Id"`, `"Name"` | `Name == "market_agent"` (constant `RoleNames.MarketAgent`) |

Two new keyless Infrastructure-only rows + configs (in Procurement, do NOT ref Auth):
- `UserMarketAssignmentRow { Guid UserId; Guid MarketId; }` → `ToView("user_market_assignments")` (or `ToSqlQuery` per Pricing precedent).
- `MarketAgentUserRow { Guid Id; Guid RoleId; string RoleName; bool IsActive; DateTime? DeletedAt; }` → `ToSqlQuery('SELECT u."Id", u."RoleId", r."Name" AS "RoleName", u."IsActive", u."DeletedAt" FROM users u JOIN roles r ON u."RoleId" = r."Id"')`.
  - Query: `assignments.Where(a => a.UserId == agentUserId && a.MarketId == marketId).Join(agents.Where(u => u.IsActive && u.DeletedAt == null && u.RoleName == "market_agent"), a => a.UserId, u => u.Id, …).AnyAsync(ct)`.

On `false` in the handler → `Error.Validation("AGENT_NOT_ELIGIBLE", "User is not an active market agent assigned to this market.")` → **422** (matches api-design line 2876 `INVALID_ROLE` = 422 for the analogous "not a market_agent" case).

> Reuse note: register `MarketAgentReader` beside the other readers in `Procurement.Infrastructure/DependencyInjection.cs` (line 35-38). The keyless rows auto-register via the existing `EfAssemblyRegistry.Register(...)` call already in that file (line 23).

---

## 5. Persistence + migration (columns only — no new tables)

Additive nullable columns on `procurement_batches` via the auto-discovered `ProcurementBatchConfiguration` (snake_case, matching 271/272):
- `assigned_agent_user_id UUID NULL`
- `assigned_at TIMESTAMPTZ NULL`
- **Index** `ix_procurement_batches_assigned_agent` on `assigned_agent_user_id WHERE deleted_at IS NULL` — 276 ("agent views their assigned batches") filters by this column.

The two new keyless rows (`UserMarketAssignmentRow`, `MarketAgentUserRow`) must be **excluded from migrations** (`ToView` / `ToSqlQuery` are read-only and non-migrating by default — verify the generated migration touches **only** `procurement_batches`, same guard 271/272 applied for `market_products`).

**One EF migration** `AssignMarketAgentToBatch`:
```
dotnet ef migrations add AssignMarketAgentToBatch \
  --project src/FreshFlow.Infrastructure.Persistence --startup-project src/FreshFlow.API
dotnet build-server shutdown   # per repo memory note
```

---

## 6. API / Application

**Endpoint** (add to `AdminController`, mirrors the `order-groups/{batchId:guid}/manifest` action at line 235):
- `POST /api/v1/admin/order-groups/{batchId:guid}/agent`
  body `AssignAgentRequest(Guid AgentUserId)` → `AssignAgentCommand(BatchId, AgentUserId)` → returns the updated `ProcurementBatchDto`.
- `[Authorize(Roles = "admin")]` to match the sibling actions (see §10 D5 re: Ops).

**Command** `Application/Commands/AssignAgent/`:
- `AssignAgentCommand(Guid BatchId, Guid AgentUserId) : ICommand<Result<ProcurementBatchDto>>`
- `AssignAgentCommandValidator` (co-located): `BatchId` and `AgentUserId` not empty.
- `AssignAgentCommandHandler` (mirrors `GenerateManifestCommandHandler`):
  1. `batch = await batches.FindByIdAsync(BatchId, ct)`; null → `Error.NotFound("PROCUREMENT_BATCH", BatchId)` → 404. (`FindByIdAsync` already exists — includes `Items` + `Orders`.)
  2. `eligible = await marketAgents.IsEligibleMarketAgentAsync(AgentUserId, batch.MarketId, ct)`; false → `AGENT_NOT_ELIGIBLE` (422).
  3. `result = batch.AssignAgent(AgentUserId, timeProvider.GetUtcNow().UtcDateTime)`; on failure return the error (409 for state guards).
  4. `await batches.SaveChangesAsync(ct)`; read order statuses (as GenerateManifest does) and return `ProcurementBatchDtoMapper.Map(batch, statuses)`.

**DTO change:** add `Guid? AssignedAgentUserId` + `DateTime? AssignedAt` to `ProcurementBatchDto` (`Dtos/ProcurementBatchDtos.cs`) and map them in `ProcurementBatchDtoMapper.Map`. `GetProcurementBatches` (271) then surfaces the assignment on the list too. No new GET in 274 (that is 276).

---

## 7. Events / seams toward 276 / 278 (declare, don't build)

- **Domain:** `ProcurementAgentAssignedDomainEvent` (§3) — internal to Procurement.
- **Integration (new, `FreshFlow.Contracts`):** `ProcurementAgentAssignedIntegrationEvent(Guid BatchId, Guid MarketId, Guid AgentUserId, DateTime AssignedAt) : INotification`, published by a `ProcurementAgentAssignedDomainEventHandler` (mirror the existing `ProcurementManifestGeneratedDomainEventHandler`). **No consumer built in 274.** Natural future consumer: Notifications ("you've been assigned batch X"). Recommend adding the Contracts record + producer now so 276/278 and Notifications have a clean seam; acceptable to defer entirely if the user wants 274 minimal (§10 D6).
- **276** reads `assigned_agent_user_id == currentUser` to list an agent's batches. **278** reads `reference_unit_price` (from 272) as the FR-ORD-010 target and drives `Manifested → Purchasing → HandedOff`. Do not build those here.

---

## 8. Test strategy (repo bar: ≥80%, TDD)

**Unit** (`tests/Unit/FreshFlow.Procurement.UnitTests/`):
- `AssignAgent` on a `Manifested` batch → `AssignedAgentUserId`/`AssignedAt` set, `Status` stays `Manifested`, `ProcurementAgentAssignedDomainEvent` raised once.
- `AssignAgent` with `Guid.Empty` → `INVALID_AGENT`, no state change, no event.
- `AssignAgent` on `Built` → `BATCH_NOT_MANIFESTED`, unchanged.
- `AssignAgent` on `Purchasing`/`HandedOff` → `BATCH_ALREADY_IN_PROGRESS`, unchanged.
- Reassignment on `Manifested` → overwrites agent, refreshes `AssignedAt`, re-raises event (per §10 D3).
- Handler: batch not found → `NotFound`; ineligible agent (reader returns false) → `AGENT_NOT_ELIGIBLE` (mock `IMarketAgentReader`).

**Integration** (`tests/Integration/FreshFlow.IntegrationTests/Procurement/`):
- Seed a `Manifested` batch + a `market_agent` user assigned to `batch.MarketId` → `POST …/{batchId}/agent` → 200, DB shows `assigned_agent_user_id` + `assigned_at`; `GET /order-groups` surfaces them.
- Agent NOT assigned to that market → 422 `AGENT_NOT_ELIGIBLE`.
- User assigned but wrong role (e.g. `hub_staff`) → 422. Inactive market_agent → 422.
- Unknown batchId → 404. Assign on a `Built` batch → 409.

---

## 9. Ordered, file-level step list (paths under the existing module)

1. **Domain** (tests first):
   - `Domain/Entities/ProcurementBatch.cs` — add `AssignedAgentUserId`, `AssignedAt`, `AssignAgent(...)` with guards + event.
   - `Domain/Events/ProcurementAgentAssignedDomainEvent.cs` (new).
2. **Cross-module read:**
   - `Application/Abstractions/IMarketAgentReader.cs` (new).
   - `Infrastructure/CrossModule/UserMarketAssignmentRow.cs` + `…RowConfiguration.cs` (new — or reuse if a Procurement one already exists; verify).
   - `Infrastructure/CrossModule/MarketAgentUserRow.cs` + `…RowConfiguration.cs` (new, `ToSqlQuery` users⋈roles).
   - `Infrastructure/CrossModule/MarketAgentReader.cs` (new).
3. **Application:**
   - `Application/Commands/AssignAgent/{AssignAgentCommand, …Validator, …Handler}.cs` (new).
   - `Application/EventHandlers/ProcurementAgentAssignedDomainEventHandler.cs` (new — publish integration event) [if §10 D6 = yes].
4. **Contracts:** `src/Shared/FreshFlow.Contracts/ProcurementAgentAssignedIntegrationEvent.cs` (new) [if D6 = yes].
5. **DTO:** add `AssignedAgentUserId` + `AssignedAt` to `ProcurementBatchDto` and map in `ProcurementBatchDtoMapper` (`Dtos/ProcurementBatchDtos.cs`).
6. **Persistence:** update `Infrastructure/Persistence/Configurations/ProcurementBatchConfiguration.cs` (`assigned_agent_user_id`, `assigned_at`, filtered index); migration `AssignMarketAgentToBatch`; `dotnet build-server shutdown`.
7. **DI:** register `IMarketAgentReader → MarketAgentReader` in `Infrastructure/DependencyInjection.cs` (beside the other readers).
8. **API:** add `POST order-groups/{batchId:guid}/agent` action + `AssignAgentRequest` record to `AdminController` (admin role).
9. **Tests:** unit + integration (§8); `dotnet test`; `dotnet format --verify-no-changes`; `dotnet build-server shutdown`.

---

## 10. Open decisions (resolve before coding — recommendation each)

- **D1 — Does assignment change status? (BLOCKER — defines the contract with 278.)** **Recommend: NO** — set the agent field, keep `Status = Manifested`; let 278 own `Manifested → Purchasing`. Alternative (flip to `Purchasing` at assignment) collides with 278 and misreports ops state. Tests depend on this.
- **D2 — Field vs. new `ProcurementBatchAssignment` entity.** **Recommend: field** (`AssignedAgentUserId` + `AssignedAt` on the batch), mirroring 272's `ManifestedAt`. One agent per batch = no need for a satellite table. Revisit only if history of reassignments must be audited (then an append-only log — YAGNI now).
- **D3 — Reassignment / idempotency on an already-assigned `Manifested` batch.** **Recommend: allow reassign** (overwrite agent, refresh `AssignedAt`, re-raise event) while `Manifested`; **block once `Purchasing`** (409). Alternative: reject any second assignment (409 always). Pick one — tests depend on it.
- **D4 — Manual admin action vs. auto-consume `ProcurementManifestGeneratedIntegrationEvent`.** **Recommend: manual** — assignment is an operator decision (a market can have several agents; the system has no policy to auto-pick one), and 272's event only signals "ready to assign". Auto-assignment would need a selection rule that no spec defines. Keep generate → assign as distinct operator steps.
- **D5 — RBAC: `admin` only vs. `admin` + `operations_manager`.** The api-design RBAC matrix (lines 3258-3260) grants **both** to every `order-groups` endpoint, but the shipped 271/272 actions are `admin`-only. **Recommend: `admin` only** for consistency with the sibling `auto-batch` + `manifest` actions; widen to `operations_manager` only if the user wants matrix-fidelity (cheap one-word change).
- **D6 — Build the integration-event seam now?** **Recommend: yes — Contracts record + producer only** (no consumer), mirroring how 272 shipped `ProcurementManifestGeneratedIntegrationEvent` producer-only. Defer entirely if the user wants 274 minimal; 276 can read the column directly without the event.
- **D7 — Endpoint verb/shape.** `POST /order-groups/{batchId}/agent` (matches verb-style siblings `auto-batch`, `manifest`) vs. `PUT /order-groups/{batchId}/agent` (more RESTful for an idempotent field-set). **Recommend: POST** for sibling consistency. Body carries `agentUserId`.

---

## 11. Conventions to enforce (pass to coder)

Result pattern (no business exceptions from domain/services); record DTOs; `Async` suffix; `I`-prefixed interfaces; FluentValidation co-located with the command; MediatR domain→integration event bridge; keyless cross-module Row readers (no Auth project ref); EF `IEntityTypeConfiguration` auto-discovery, snake_case **owned** columns / PascalCase for cross-module Row mappings to Auth tables (verified casing), UUID PKs, additive nullable columns; one shared `AppDbContext`; migration in `FreshFlow.Infrastructure.Persistence`; TDD ≥80%; `FreshFlow.slnx` (not `.sln`); `dotnet build-server shutdown` after one-shot dotnet commands; commit `feat(procurement): SCRUM-274 …` **only after** the supervisor supplies the go-ahead.
</content>
</invoke>
