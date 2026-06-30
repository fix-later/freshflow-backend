# FreshFlow Documentation Index

**Project:** FreshFlow (FFX) — B2B Food Procurement and Logistics Optimization Platform  
**Version:** 1.0  
**Date:** 2026-05-09  
**Status:** Pre-implementation analysis complete — Ready for implementation

---

## Document Map

| # | Document | Description | Lines | Status |
|---|---|---|---|---|
| 01 | [01-requirements-spec.md](./01-requirements-spec.md) | Functional & non-functional requirements, gaps, glossary | 283 | Complete |
| 02 | [02-system-architecture.md](./02-system-architecture.md) | Architecture pattern, component diagrams, module breakdown, caching, deployment | 713 | Complete |
| 02A | [02A-system-overview-diagrams.md](./02A-system-overview-diagrams.md) | Current backend system overview diagrams, runtime wiring, flows, deployment view, Draw.io source | — | Current |
| 02B | [02B-state-machine-diagrams.md](./02B-state-machine-diagrams.md) | State machine cho các entity chính (Order, Restaurant, User, RefreshToken…); Draw.io source | — | Current |
| 02C | [02C-activity-diagrams.md](./02C-activity-diagrams.md) | Activity/flowchart các luồng nghiệp vụ chính (có Actor, swimlane); Draw.io source | — | Current |
| 03 | [03-database-schema.md](./03-database-schema.md) | PostgreSQL DDL, index strategy, Redis key design, migration strategy (reconciled w/ code 2026-06-29) | — | Current |
| 03A | [03-database-schema.dbml](./03-database-schema.dbml) | Unified DBML for dbdiagram.io: Part A implemented + Part B planned (reconciled 2026-06-29) | — | Current |
| 04 | [04-api-design.md](./04-api-design.md) | REST endpoints, SignalR hubs, validation rules, RBAC matrix | 2,641 | Complete |
| 05 | [05-implementation-plan.md](./05-implementation-plan.md) | 50-task breakdown, critical path, MVP scope, folder structure, coding standards | 912 | Complete |
| — | [REVIEW-REPORT.md](./REVIEW-REPORT.md) | Cross-reference gaps, inconsistencies, readiness assessment | — | Complete |

> **Feature working docs** (survey/audit/context/design/tasks per feature) sống ở [`features/`](./features/README.md) — tách khỏi bộ spec core này để dễ tracking.

---

## Quick Start for Developers

### Reading Order

1. **Start with 01** — understand what the system must do before touching any code. Pay special attention to Section 3 (Gaps & Assumptions) — these are active decisions that shape the implementation.

2. **Read 02 next** — understand the overall shape of the system: Modular Monolith, 7 ASP.NET Core modules, SignalR with Redis backplane, Docker Compose deployment.

3. **Skim 03** — familiarize yourself with the database schema. The most important tables for Day 1 are `users`, `refresh_tokens`, `market_products`, and `price_snapshots`.

4. **Reference 04 while building** — the API design doc is a reference, not reading material. Pull it up when implementing a specific endpoint.

5. **Use 05 as your task list** — the 50-task breakdown with dependencies tells you exactly what to build and in what order. Start from the critical path in Section 2.

### Where to Start Implementing

Begin with the critical path:

```
T001 (solution setup) → T002 (DB + EF Core) → T003 (Redis) → T004 (JWT middleware)
→ T005 (Docker Compose) → T009 (login endpoint) → T010 (refresh token)
→ T013 (pricing migrations) → T015 (market products list) → T016 (price update)
→ T017 (PricingHub SignalR) → T022 (order creation) → T025 (order status + OrderHub)
```

This path delivers the MVP: **Auth + Real-time Pricing + Basic Orders**.

### Repository Structure (to be created)

```
FreshFlow.sln
├── src/
│   ├── Shared/
│   │   ├── FreshFlow.SharedKernel/          # BaseEntity, AggregateRoot, Result<T>, ICommand/IQuery
│   │   └── FreshFlow.Contracts/             # Integration events (cross-module DTOs)
│   ├── Modules/
│   │   ├── Auth/       { .Domain / .Application / .Infrastructure }
│   │   ├── Pricing/    { .Domain / .Application / .Infrastructure }
│   │   ├── Orders/     { .Domain / .Application / .Infrastructure }
│   │   ├── Logistics/  { .Domain / .Application / .Infrastructure }
│   │   ├── Hub/        { .Domain / .Application / .Infrastructure }
│   │   ├── Analytics/  { .Application / .Infrastructure }          # no Domain (read-only)
│   │   └── Notifications/ { .Domain / .Application / .Infrastructure }
│   ├── FreshFlow.Infrastructure.Persistence/ # Shared AppDbContext + all EF Migrations
│   └── FreshFlow.API/                        # Host: controllers, SignalR hubs, Program.cs
├── tests/
│   ├── Unit/   FreshFlow.{Module}.UnitTests/ (one per module)
│   └── Integration/   FreshFlow.IntegrationTests/
├── docs/                                     # ← you are here
└── docker-compose.yml
```

---

## Key Architectural Decisions

These five decisions have the largest impact on implementation. Understand them before writing code.

### 1. Modular Monolith (not Microservices)

The system is a single deployable unit with strict module boundaries enforced at code level. Modules communicate through interfaces, not HTTP. This keeps the team moving fast without distributed systems overhead. **Trade-off accepted:** horizontal scaling requires scaling the entire API, not individual modules. This is acceptable for the capstone scope.

> See: `02-system-architecture.md` Section 1 and Section 6 (Decisions Log)

### 2. SignalR with Redis Pub/Sub Backplane

Real-time price updates use SignalR WebSocket connections. When multiple API instances are running, Redis acts as the message backplane so a price update hitting Instance A reaches clients connected to Instance B. **Implication:** Redis must be running and healthy for SignalR to work in a multi-instance setup. In development (single instance), the backplane is optional.

> See: `02-system-architecture.md` Section 4

### 3. UUID Primary Keys (All Tables)

Every table uses `UUID` as the primary key (`gen_random_uuid()`). This avoids sequential ID guessing in URLs, supports future data sharding, and simplifies cross-database merging. **Trade-off accepted:** slightly larger index size vs. BIGINT serial; mitigated by using UUIDv4 which PostgreSQL handles efficiently.

> See: `03-database-schema.md` Section 2

### 4. Soft Reservation for Stock (Redis + PostgreSQL)

When a restaurant places an order, stock is soft-reserved in Redis immediately (fast, non-blocking). A background job reconciles reservations against the PostgreSQL source of truth every 5 minutes. Reservations expire after 30 minutes if the order is not confirmed. **Implication:** there is a small window where two simultaneous orders could both succeed on the Redis check but one would fail on the database constraint. The reconciliation job handles this.

> See: `01-requirements-spec.md` GA-003 and `03-database-schema.md` Section 6

### 5. Nearest-Neighbor + 2-opt VRP Heuristic (not OR-Tools)

Route calculation uses a custom nearest-neighbor heuristic with 2-opt improvement passes. This is 5–15% from optimal but completes in well under 3 seconds for up to 20 stops, which satisfies FR-LOG-006. OR-Tools (Google) is the identified upgrade path if route quality becomes a business concern post-MVP.

> See: `02-system-architecture.md` Section 6 and `05-implementation-plan.md` T030

---

## Known Risks

| # | Risk | Severity | Mitigation |
|---|---|---|---|
| R-001 | Redis unavailability breaks SignalR backplane and price caching simultaneously | High | Redis AOF persistence enabled; Docker healthcheck restarts Redis on failure; API degrades gracefully (reads from PostgreSQL if Redis miss) |
| R-002 | `price_snapshots` table grows unboundedly (high-frequency writes from kiosk staff) | High | Monthly range partitioning on `recorded_at`; background job creates next month's partition on the 25th; old partitions can be archived or dropped per retention policy |
| R-003 | Concurrent price updates from two kiosk staff members at the same market cause lost updates | Medium | Last-write-wins with `updated_at` timestamp comparison; HTTP 409 on detected conflict; flagged in GA-004 — optimistic concurrency to be implemented in `PricingService` |
| R-004 | Route calculation exceeds 3-second SLA as order volumes grow (more stops per route) | Medium | Hard limit of 20 stops per route (FR-LOG-006); route results cached in Redis with SHA-256 key; OR-Tools as upgrade path if heuristic becomes bottleneck |
| R-005 | Restaurant approval flow (GA-009) not fully specified — could block restaurant onboarding | Low | Assumption: Admin manually approves restaurants via `PATCH /api/v1/admin/restaurants/{id}/approve`; default behavior is unapproved (cannot place orders) until Admin approves |

---

## Requirement Coverage Summary

| Domain | FRs | Must | Should | Could | Tables | Endpoints | Tasks |
|---|---|---|---|---|---|---|---|
| Auth | 11 | 11 | 0 | 0 | 2 | 9 | 4 |
| Pricing | 5 | 5 | 0 | 0 | 4 | 5 | 6 |
| Orders | 7 | 6 | 1 | 0 | 4 | 9 | 9 |
| Logistics | 6 | 6 | 0 | 0 | 3 | 5 | 6 |
| Hub | 5 | 4 | 1 | 1 | 4 | 5 | 5 |
| Analytics | 4 | 1 | 2 | 1 | 1 | 5 | 4 |
| Notifications | 3 | 3 | 0 | 0 | 1 | 2 | 2 |
| **Total** | **41** | **36** | **4** | **2** | **19** | **40** | **36** |

> Infrastructure tasks (T001–T008) and frontend tasks (T045–T050) are not counted in the domain task column above.

---

## Contact

**Project:** FreshFlow Capstone — Ho Chi Minh City  
**Abbreviation:** FFX  
**Generated by:** FreshFlow Master Architect Agent  
**Date:** 2026-05-09
