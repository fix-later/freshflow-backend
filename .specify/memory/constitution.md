<!--
SYNC IMPACT REPORT
==================
Version change: (blank template) → 1.0.0 (initial ratification)

Modified principles: N/A (all new — template placeholders replaced)

Added sections:
  - Core Principles (5 principles)
  - Technology Stack Constraints
  - Development Workflow
  - Governance

Removed sections: N/A

Templates requiring updates:
  ✅ .specify/templates/tasks-template.md
     — Updated "Tests: OPTIONAL" note to reflect Principle III (mandatory CI gates)
  ✅ .specify/templates/plan-template.md
     — No structural change required; Constitution Check gate references this file by design
  ✅ .specify/templates/spec-template.md
     — No change required; FR-ID convention already consistent with project docs

Commands directory: .specify/templates/commands/ does not exist — no updates needed.

Deferred TODOs: none.
-->

# FreshFlow Constitution

## Core Principles

### I. Strict Module Boundary Enforcement (NON-NEGOTIABLE)

Each of the seven modules (Auth, Pricing, Orders, Logistics, Hub, Analytics,
Notifications) MUST own its data tables and internal services exclusively.

- No module's Domain or Application project MAY reference another module's projects.
  This is enforced by `.csproj` references — any violation fails `dotnet build`.
- Cross-module communication MUST go through one of two approved channels:
  1. Integration event records in `FreshFlow.Contracts` consumed via MediatR
     `INotificationHandler<T>`.
  2. Application-layer interfaces (e.g., `IProductCatalogReader`) defined in the
     calling module's Application project and injected at the host level.
- The Analytics module MAY read tables owned by other modules (read-only, read
  replica preferred) but MUST NOT write outside its own data ownership.
- Direct table queries that bypass the owning module's services are forbidden.

**Rationale:** Enforced boundaries allow any module to be extracted into an
independent microservice with three changes (new `Program.cs`, swap MediatR for
message bus, split `AppDbContext`). Violations today create the coupling that makes
extraction painful tomorrow.

### II. Clean Architecture Layer Rules

Dependency direction is strictly enforced via `.csproj` project references:

```
Domain         → SharedKernel only
Application    → Domain + SharedKernel + Contracts
Infrastructure → Application + Domain + packages
API (host)     → all Infrastructure projects (DI wiring only)
```

No layer MAY reference a "higher" layer. No lateral references between modules.
Any plan that requires violating these rules MUST document the violation in the
plan's Complexity Tracking table with a written justification and explicit approval.

**Rationale:** Layer rules are the primary guard against the "big ball of mud."
They are already machine-enforceable via `.csproj`; the cost of keeping them is zero.

### III. Test-First with Mandatory CI Gates (NON-NEGOTIABLE)

All production code MUST have corresponding test coverage before merging to `main`.
The CI pipeline enforces four gates in order; all four MUST pass:

1. **Unit tests** — `dotnet test --filter Category=Unit` — one test project per module.
2. **Integration tests** — `dotnet test --filter Category=Integration` — MUST run against
   real PostgreSQL and Redis containers. Database and cache MUST NOT be mocked.
3. **Format check** — `dotnet format --verify-no-changes --severity error` MUST exit 0.
4. **Security scan** — OWASP dependency check; fail on known critical CVEs.

Features failing any gate MUST NOT merge. Mocked database tests are prohibited for
integration scenarios; the prior-incident risk (mock passes, production migration
fails) is accepted as a standing policy reason.

**Rationale:** Real-infrastructure integration tests catch migration-level bugs that
in-memory mocks cannot surface. The format gate prevents style drift without manual
enforcement.

### IV. Result Pattern and Defensive API Surface

- Services MUST return `Result<T>` and MUST NOT throw exceptions for business-rule
  violations. Only infrastructure failures (database unreachable, unexpected I/O)
  may propagate as exceptions.
- Controllers MUST map `Result<T>` to HTTP responses via `result.Error.ToActionResult()`.
- All request DTOs MUST have a co-located FluentValidation validator
  (e.g., `Commands/Login/LoginCommandValidator.cs`).
- RBAC MUST be declared on every API endpoint. Any new endpoint without an explicit
  role annotation MUST cause a CI lint check failure.
- Raw SQL is forbidden outside `Analytics.Infrastructure`. When raw SQL is
  required there, it MUST use parameterized `FromSqlRaw` — string interpolation
  is prohibited.
- DTOs MUST use C# `record` types (immutable, value equality).

**Rationale:** Consistent Result<T> usage eliminates hidden control-flow exceptions
and makes error propagation explicit and auditable at every layer boundary.

### V. Microservice-Readiness by Default

Every design decision MUST preserve the three-change extraction path for any module.
Concretely:

- Shared runtime state MUST live in Redis or PostgreSQL — never in-process memory
  (e.g., static dictionaries, `IMemoryCache` for shared-state use cases).
- All broadcast service interfaces (`IPricingBroadcastService`, `IOrderBroadcastService`,
  `IDeliveryBroadcastService`) MUST be injected via DI; modules MUST NOT reference
  concrete hub types.
- Each module's Infrastructure project MUST expose exactly one `AddXModule()` extension
  method as its registration entry point.
- All primary keys MUST be UUID (`gen_random_uuid()`). BIGINT serial keys are
  prohibited to support distributed ID generation upon extraction.
- All mutable tables MUST include `deleted_at TIMESTAMPTZ` for soft delete.
  Append-only tables (`price_snapshots`, `refresh_tokens`) are exempt.

**Rationale:** These constraints have near-zero implementation cost and prevent the
structural coupling that makes microservice extraction a rewrite rather than a refactor.

## Technology Stack Constraints

The following technology decisions are ratified for FreshFlow v1. Substitutions require
a new entry in `docs/02-system-architecture.md` Technology Decisions Log and a MINOR
version bump to this constitution.

| Layer | Technology |
|-------|-----------|
| Backend runtime | ASP.NET Core 10 (C# 14), EF Core 10, MediatR, FluentValidation |
| Primary database | PostgreSQL 16 (partitioned `price_snapshots`, UUID PKs) |
| Cache + backplane | Redis 7 (StackExchange.Redis; AOF persistence enabled) |
| Real-time | SignalR (ASP.NET Core) + Redis backplane; JWT via `access_token` query param |
| Web frontend | Angular (OnPush change detection, `async` pipe, `inject()` DI, no `any`) |
| Mobile frontend | React Native (functional components + hooks, `SecureStore`, typed `api.service.ts`) |
| Reverse proxy | Nginx 1.27 (TLS termination, WebSocket upgrade, static Angular serving) |
| Infrastructure | Docker Compose; GitHub Actions CI/CD |

Analytics aggregate queries MUST target the PostgreSQL read replica, not the primary.
Redis failure MUST degrade gracefully (log warning, fall back to PostgreSQL) and MUST
NOT block the write path.

## Development Workflow

- Branch naming: `feature/T{ID}-{slug}` or `fix/T{ID}-{slug}` (Task ID required).
- Commit format: `{type}({scope}): {description} (T{ID})`.
- All merges to `main` MUST go through a Pull Request with at least one reviewer approval.
- EF Core migrations MUST be backward-compatible within a single release. Column drops
  MUST NOT appear in the same release as the code that removes them.
- Secrets (JWT keys, database/Redis passwords, seed credentials) MUST NOT be committed.
  Use environment variables; reference `.env.example` for the expected variable set.
- The `GET /health` endpoint MUST be unauthenticated and MUST check PostgreSQL and Redis
  connectivity. PostgreSQL unhealthy → HTTP 503 overall. Redis unhealthy → HTTP 200
  Degraded (graceful degradation is acceptable).

## Governance

This constitution supersedes all other FreshFlow development practices. In case of
conflict between this document and any other guideline, the constitution takes precedence.

**Amendment procedure:**
1. Author writes a rationale explaining why the principle must change.
2. The constitution file is updated with the appropriate version bump (see below).
3. Propagation checks are performed against all `.specify/` templates and `CLAUDE.md`.
4. The project owner approves the amendment before the change is merged to `main`.

**Versioning policy:**
- MAJOR: Principle removal, redefinition, or backward-incompatible governance change.
- MINOR: New principle or section added, or material expansion of existing guidance.
- PATCH: Clarifications, wording improvements, typo fixes.

**Compliance review:** Every Pull Request to `main` must satisfy the Constitution Check
gate in `plan.md` before merging. The gate is re-evaluated after Phase 1 design.

**Version**: 1.0.0 | **Ratified**: 2026-05-11 | **Last Amended**: 2026-05-11
