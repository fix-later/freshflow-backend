# FreshFlow Documentation Index

**Project:** FreshFlow (FFX) — B2B Food Procurement and Logistics Optimization Platform
**Status:** Implemented and under active feature work
**Docs reconciled with code:** 2026-08-22 (branch `dev-bao`, commit `bc5f1a4`)

**The code is the source of truth.** Where a document and the code disagree, the code wins.
Docs 02, 03, 04, 07 and `enums.md` were regenerated from the repository on the date above;
docs 01, 05 and `REVIEW-REPORT` are historical design artifacts, kept for traceability and
labelled as such.

---

## Document Map

### Core spec

| # | Document | What it answers | Kind |
|---|---|---|---|
| 01 | [01-requirements-spec.md](./01-requirements-spec.md) | What the system must do — FRs, NFRs, gaps, glossary | 📜 Historical (v1.0 requirements) |
| 02 | [02-system-architecture.md](./02-system-architecture.md) | How the system is built — modules, pipeline, events, jobs, integrations, deployment | ✅ As-built |
| 03 | [03-database-schema.md](./03-database-schema.md) | Schema conventions, the 59-table inventory, seam rules, SQL policy, Redis usage | ✅ As-built |
| 04 | [04-api-design.md](./04-api-design.md) | All 223 endpoints with roles, SignalR hubs, error mapping, RBAC, rate limits | ✅ As-built |
| 05 | [05-implementation-plan.md](./05-implementation-plan.md) | The original 50-task build plan | 📜 Historical |
| 06 | [06-context-decisions.md](./06-context-decisions.md) | Context decision log | Reference |
| 07 | [07-business-rules.md](./07-business-rules.md) | 119 business rules read out of the code, with enforcement point and error code | ✅ As-built |
| — | [enums.md](./enums.md) | Every enum/status string the API exposes | ✅ As-built |
| — | [REVIEW-REPORT.md](./REVIEW-REPORT.md) | Pre-implementation cross-reference review | 📜 Historical |
| — | [AUDIT-2026-08-23-business-flow-audit.md](./AUDIT-2026-08-23-business-flow-audit.md) | Whole-system business-flow audit — 6 CRITICAL / 5 HIGH / 7 MEDIUM findings, with fix order | ⚠️ Open findings |
| — | [api-response-envelope-migration.md](./api-response-envelope-migration.md) | Envelope migration notes | Reference |

### Database companions — [`database/`](./database/README.md)

Reconciled with the EF model on 2026-08-16. All three DBML views cover the same 59 tables.

| File | Content |
|---|---|
| [03-database-schema.dbml](./database/03-database-schema.dbml) | Physical: columns, types, indexes, defaults, checks, 42 enforced FKs |
| [03-database-schema.logical.dbml](./database/03-database-schema.logical.dbml) | Logical: business attributes, enforced + application relationships |
| [03-database-schema.conceptual.dbml](./database/03-database-schema.conceptual.dbml) · [.conceptual.md](./database/03-database-schema.conceptual.md) · [Chen ERD](./database/03A-conceptual-erd-chen-core.drawio) | Conceptual model |
| [03B-database-erd.md](./database/03B-database-erd.md) | Mermaid crow's-foot ERD per module |
| [03C-table-descriptions.md](./database/03C-table-descriptions.md) | Business purpose of each table |
| [03D-entities-description.md](./database/03D-entities-description.md) · [03E-conceptual-entities-description.md](./database/03E-conceptual-entities-description.md) | Entity and attribute descriptions |

### Diagrams — [`diagrams/`](./diagrams/README.md)

System overview, state machines, activity flows, package and class diagrams, plus their
Draw.io sources.

### Feature working docs — [`features/`](./features/README.md)

Per-epic surveys, audits, plans and task breakdowns. Status of an individual task lives in its
own document, not in the index.

---

## Where to look first

| You want to… | Read |
|---|---|
| Understand the shape of the system | [02](./02-system-architecture.md) §1–§3 |
| Add or change an endpoint | [04](./04-api-design.md) §1 (conventions) + §4 (error mapping), then the controller |
| Write a query across modules | [02](./02-system-architecture.md) §5.3 + [03](./03-database-schema.md) §1.2 and §4 |
| Write a migration | [03](./03-database-schema.md) §1 and §7 |
| Know what a status string means | [enums.md](./enums.md) |
| Know why the code refuses something | [07](./07-business-rules.md) — search by error code |
| Run the project | [README](../README.md) |

---

## Facts worth knowing before you write code

These are the ones that most often cost an afternoon.

**1. Ten modules, one process, strict boundaries.** No module's `Domain` or `Application` may
reference another module's projects. Cross-module reads go through a keyless EF Row seam with
`ToSqlQuery` — and a seam is only proven by an integration test against real PostgreSQL,
because `ToSqlQuery` does not run on the InMemory provider.

**2. Column casing is not consistent, and some tables mix both conventions.** `orders."Status"`
is quoted PascalCase while `orders.deleted_at` is snake_case. A wrong identifier fails at
runtime, not at build. Never guess — copy an existing seam or read the `*Configuration.cs`.

**3. Six roles exist.** `admin`, `operations_manager`, `market_agent`, `hub_staff`, `driver`,
`restaurant`. `[Authorize(Roles = "…")]` with any other name fails silently: green build,
permanent 403.

**4. An unregistered error code returns 500.** `ErrorExtensions.ToActionResult()` maps by
allow-list. Reuse an existing code, or add yours to that file.

**5. Route everything through `ISender`.** Bypassing MediatR skips `ValidationBehavior`, so the
validators never run.

**6. Payment is B2B credit (công nợ), not a gateway.** There are no `payments` or `refunds`
tables; a refund is a credit adjustment.

**7. "Phiên chợ" = `ProcurementBatch`.** Vietnamese UI says *phiên chợ*; code says
`ProcurementBatch`. The two-register convention is deliberate — do not rename the code. "Lô
chợ" is the old term and must not be used.

**8. SignalR is in-memory.** Four hubs, no Redis backplane, single instance only.

**9. Zero `FromSqlRaw` in this repository.** Keep it that way; see [03](./03-database-schema.md) §5.

**10. `localhost:5433` is production.** Confirm the connection string before any destructive
migration.

---

## Known gaps

| Gap | Where it bites |
|---|---|
| No SignalR backplane | Cannot run more than one API instance |
| In-process integration events | An event lost mid-handler after a crash is lost for good |
| VAT invoicing uses a stub provider | Real NCC/HSM integration and per-category VAT are still to do |
| `DeliveryRoute` has no concurrency token | Two concurrent route edits can silently overwrite |
| Price-alert trigger (SCRUM-173) | Threshold config exists; nothing fires the alert |
| Phone OTP | Not implemented — email only; `PHONE` returns `CHANNEL_NOT_SUPPORTED` |

---

**Project:** FreshFlow Capstone — Ho Chi Minh City · **Abbreviation:** FFX
