# ADM Epic — Admin & System Configuration (Backend Plan / Audit)

**Epic:** ADM – Admin & System Configuration
**Branch:** `SCRUM-353-ADM-admin-config` (from `dev`)
**Date:** 2026-07-13
**Scope:** Backend only (no FE-WEB). 5 tickets: SCRUM-353, 355, 357, 359, 361.

## Guiding decision
No new "Admin" module. Config lives in the module that owns the domain and is
exposed through the API host (`AdminController` / dedicated controllers), matching
the existing pattern (AdminController already aggregates Auth + Orders commands).
Creating an Admin module would violate the dependency rule (a module's
Domain/Application may not reference another module).

## Survey findings (grounded in current code, 2026-07-13)
- **SCRUM-353** largely already implemented in Auth: CreateUser, GetUsers,
  ActivateUser, UnlockUser, AssignRole, ApproveRestaurant, ReplaceMarketAssignments,
  UpdateRestaurantProfile, Get(Restaurant)Profile/ApprovalStatus, all wired in
  `AdminController`. `RestaurantStatus` enum = Pending | Active | Suspended (no "Rejected").
- **SCRUM-357** credit-limit already done: `SetRestaurantCreditLimitCommand` (Orders) +
  `PUT /admin/restaurants/{id}/credit/limit`. Only the price-alert threshold is new.
- **SCRUM-359** audit log is fully net-new — no `audit_logs` table/entity exists.
  Contracts already has `OrderCancelledIntegrationEvent`,
  `CreditLimitThresholdReachedIntegrationEvent`, `RestaurantRefundIssuedIntegrationEvent`.
  Pricing already has `PriceUpdatedDomainEvent` + `PriceUpdatedDomainEventHandler`
  and references Contracts, so publishing a new `PriceUpdatedIntegrationEvent` is trivial.
- **SCRUM-361** "Kiosk" = Market Agent (legacy alias `kiosk_staff`→`market_agent`,
  docs/06). Market CRUD (Catalog), market-agent assignment (Auth
  ReplaceMarketAssignments), and price/quantity update (Pricing) all already exist.

## Decisions locked by supervisor (2026-07-13)
1. **SCRUM-355** → Option A: one shared `operational_settings` table in Orders;
   Logistics/Hub read via Contracts only if a real consumer exists.
2. **SCRUM-357** → GLOBAL price-alert threshold (single system-wide %), not per-market/product.
3. **SCRUM-359** → event-driven: `audit_logs` append-only + `IAuditLogWriter` in SharedKernel,
   written by consumers of existing integration events + a new `PriceUpdatedIntegrationEvent`.
4. **SCRUM-361** → NO real gap. Verify-only; documented here as already covered. No new feature code.
5. Branch: new `SCRUM-353-ADM-admin-config` from `dev`.

## Task breakdown
| Task | Ticket | Module | Type | Summary |
|------|--------|--------|------|---------|
| T1 | SCRUM-353 | Auth | gap-only | Suspend/Reactivate restaurant + pending-approval/status filter on restaurant list. Verify existing endpoints first; add only true gaps. |
| T2 | SCRUM-355 | Orders | net-new | `operational_settings` table + entity, Update/Get commands, admin endpoints, refactor `OrderCutoffScheduler` to read cutoff from config (hardcoded 22:00 as default). Batching/hub-vs-direct stored but no cross-module plumbing until a real reader exists (YAGNI). |
| T3 | SCRUM-357 | Pricing | net-new (small) | Persist a single global price-alert threshold %; Update/Get command + admin endpoint. `PricingOptions` (appsettings) is not admin-editable, so a small single-row settings store is needed. |
| T4 | SCRUM-359a | Pricing/Contracts | net-new (small) | Add `PriceUpdatedIntegrationEvent` to Contracts; publish it from existing `PriceUpdatedDomainEventHandler`. |
| T5 | SCRUM-359b | Persistence/API | net-new | `audit_logs` append-only table + `AuditLog` entity + `IAuditLogWriter` (SharedKernel) + impl; INotificationHandler consumers for OrderCancelled, CreditLimitThresholdReached, RestaurantRefundIssued, PriceUpdated integration events; `GET /admin/audit-logs` reader (filter actor/action/entity/time). Blocked by T4. |
| — | SCRUM-361 | Catalog/Auth/Pricing | verify-only | Already covered by existing code (Market CRUD + market assignments + price/qty update). No new code — verified. |

## Progress log
- 2026-07-13 — Plan approved by supervisor; branch `SCRUM-353-ADM-admin-config` created from dev. Tasks T1–T5 created. SCRUM-361 closed as already-covered (verify-only).
- 2026-07-14 — T1–T5 all implemented + PASSED review (T5 SCRUM-359b last, reviewed by reviewer). Full suite green (2245+ tests incl. new FreshFlow.Infrastructure.Persistence.UnitTests 11/11, IntegrationTests 120/120), build 0 errors, format clean. Uncommitted in working tree — awaiting Jira keys + commit order from user. Open follow-up: GetAuditLogsQueryHandler validates Page/PageSize inline instead of a FluentValidation validator (LOW, non-blocking).
