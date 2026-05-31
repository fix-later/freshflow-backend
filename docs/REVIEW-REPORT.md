# FreshFlow Pre-Implementation Review Report

**Date:** 2026-05-09  
**Reviewer:** FreshFlow Master Architect Agent (Phase 6 Cross-Reference)  
**Documents Reviewed:** 01-requirements-spec.md, 02-system-architecture.md, 03-database-schema.md, 04-api-design.md, 05-implementation-plan.md

---

## Overall Readiness Assessment

> **READY TO IMPLEMENT**
>
> All Must-priority functional requirements are covered end-to-end: requirement → schema entity → API endpoint → implementation task. No Blocker-severity gaps were found. Three Warning-level issues require a team decision before implementation of their respective modules begins. All Info-level observations are noted for awareness and do not block implementation.

---

## Cross-Reference Verification

### Requirement → Schema → Endpoint → Task Coverage

| FR ID | Requirement | Schema Entity | API Endpoint | Task |
|---|---|---|---|---|
| FR-AUTH-001 | Login | `users` | `POST /auth/login` | T009 |
| FR-AUTH-002 | Refresh token rotation | `refresh_tokens` | `POST /auth/refresh` | T010 |
| FR-AUTH-003 | Logout | `refresh_tokens` | `POST /auth/logout` | T011 |
| FR-AUTH-004 | Admin creates users | `users` | `POST /auth/register` | T012 |
| FR-AUTH-005 | RBAC enforcement | `users.role` + JWT claims | All endpoints (middleware) | T004 |
| FR-PRI-001 | Kiosk updates price | `market_products`, `price_snapshots` | `PATCH /markets/{id}/products/{id}/price` | T016 |
| FR-PRI-002 | Kiosk updates quantity | `market_products` | `PATCH /markets/{id}/products/{id}/price` (same endpoint) | T016 |
| FR-PRI-003 | Real-time broadcast | SignalR + Redis backplane | PricingHub `PriceUpdated` event | T017 |
| FR-PRI-004 | Price history | `price_snapshots` (append-only) | `GET /markets/{id}/products/{id}/price-history` | T018 |
| FR-PRI-005 | Significant price change alert | — (computed in service) | PricingHub `SignificantPriceChange` event | T019 |
| FR-PRI-006 | Product catalog management | `products` | `GET /products`, `POST /products` | T014 |
| FR-ORD-001 | Create bulk order | `orders`, `order_items` | `POST /orders` | T022 |
| FR-ORD-002 | Scheduled recurring orders | `scheduled_orders` | `POST /orders/scheduled` | T027 |
| FR-ORD-003 | Real-time order status | `orders.status` | OrderHub `OrderStatusChanged` event | T025 |
| FR-ORD-004 | Order cancellation | `orders.cancelled_at` | `PATCH /orders/{id}/cancel` | T024 |
| FR-ORD-005 | Order grouping | `order_groups` | `POST /order-groups` | T026 |
| FR-ORD-006 | Order history | `orders` | `GET /orders` (paginated) | T023 |
| FR-ORD-007 | Stock validation + soft-reservation | Redis `order:reservation:{id}` | `POST /orders` (validation logic) | T022 |
| FR-LOG-001 | Market→Hub→Restaurant route | `delivery_routes` | `POST /routes/calculate` | T030, T031 |
| FR-LOG-002 | Direct Market→Restaurant route | `delivery_routes` | `POST /routes/calculate` (routeType=direct) | T030, T031 |
| FR-LOG-003 | Route optimization criteria | — (algorithm parameter) | `POST /routes/calculate` (optimizationCriteria field) | T030 |
| FR-LOG-004 | Vehicle registration + assignment | `vehicles` | `POST /vehicles`, `POST /routes/{id}/assign-vehicle` | T029, T031 |
| FR-LOG-005 | Delivery scheduling with capacity | `delivery_routes`, `vehicles` | `POST /logistics/schedules` | T031 |
| FR-LOG-006 | Multi-drop routing (up to 20 stops) | `delivery_routes.route_metadata` JSONB | `POST /routes/calculate` (stops array) | T030 |
| FR-HUB-001 | Hub CRUD | `hubs` | `GET/POST /hubs`, `PATCH /hubs/{id}` | T035 |
| FR-HUB-002 | Cross-docking | `cross_dock_transfers` | `POST /hubs/{id}/cross-dock` | T037 |
| FR-HUB-003 | Inbound tracking | `hub_inbound_events` | `POST /hubs/{id}/inbound` | T036 |
| FR-HUB-004 | Outbound tracking | `hub_outbound_events` | `POST /hubs/{id}/outbound` | T037 |
| FR-HUB-005 | Redistribution suggestion | — (computed query) | `GET /hubs/{id}/redistribution-suggestions` | T038 |
| FR-ANA-001 | Price trend dashboard | `price_snapshots` (read-only) | `GET /analytics/price-trends` | T039 |
| FR-ANA-002 | Demand heatmap | `orders`, `restaurants` (read-only) | `GET /analytics/demand-heatmap` | T040 |
| FR-ANA-003 | Delivery performance KPIs | `delivery_routes`, `deliveries` | `GET /analytics/delivery-performance` | T041 |
| FR-ANA-004 | Async CSV export | `export_jobs` | `POST /analytics/export`, `GET /analytics/export/{id}/status` | T042 |
| FR-NOT-001 | Price-change push notification | `notifications` | PricingHub `PriceUpdated` + `SignificantPriceAlert` | T043 |
| FR-NOT-002 | Order status push notification | `notifications` | OrderHub `OrderStatusChanged` | T043 |
| FR-NOT-003 | Delivery update push notification | `notifications` | DeliveryHub `DeliveryStarted`, `DeliveryCompleted` | T043 |

**Result: 36/36 functional requirements have full chain coverage. No orphaned requirements.**

---

### Orphaned Schema Check

Tables in `03-database-schema.md` verified against endpoints in `04-api-design.md`:

| Table | Used By Endpoint(s) | Status |
|---|---|---|
| `users` | Auth endpoints, Admin user management | ✓ |
| `refresh_tokens` | `POST /auth/refresh`, `POST /auth/logout` | ✓ |
| `user_market_assignments` | Kiosk Staff market authorization (middleware) | ✓ |
| `markets` | `GET /markets`, price update endpoints | ✓ |
| `products` | `GET /products`, `POST /products` | ✓ |
| `market_products` | `GET /markets/{id}/products`, price PATCH | ✓ |
| `price_snapshots` | `GET .../price-history`, analytics price trends | ✓ |
| `restaurants` | `PATCH /admin/restaurants/{id}/approve`, orders | ✓ |
| `orders` | All orders endpoints | ✓ |
| `order_items` | `POST /orders`, `GET /orders/{id}` | ✓ |
| `order_groups` | `POST /order-groups`, `GET /order-groups` | ✓ |
| `scheduled_orders` | `POST /orders/scheduled`, `GET /orders/scheduled` | ✓ |
| `hubs` | All hub endpoints | ✓ |
| `hub_inventory` | `GET /hubs/{id}/inventory` | ✓ |
| `hub_inbound_events` | `POST /hubs/{id}/inbound` | ✓ |
| `hub_outbound_events` | `POST /hubs/{id}/outbound` | ✓ |
| `cross_dock_transfers` | `POST /hubs/{id}/cross-dock` | ✓ |
| `vehicles` | `POST /vehicles`, route assignment | ✓ |
| `delivery_routes` | `POST /routes/calculate`, `GET /routes` | ✓ |
| `deliveries` | `GET /routes/{id}` (detail includes deliveries) | ✓ |
| `notifications` | `GET /notifications`, `PATCH /notifications/{id}/read` | ✓ |
| `export_jobs` | `POST /analytics/export`, `GET /analytics/export/{id}` | ✓ |
| `analytics_aggregations` | Read by analytics endpoints as cache layer | ✓ |
| `system_config` | `GET/PATCH /admin/system-config` | ✓ |

**Result: 24/24 tables have at least one consuming endpoint. No orphaned tables.**

---

### Orphaned Endpoint Check

All endpoints in `04-api-design.md` are traceable to a functional requirement. No endpoint exists without a requirement source.

---

## Gap and Inconsistency Log

### Warning-Level Issues (team decision required before implementing the affected module)

| ID | Severity | Document | Description | Recommended Fix |
|---|---|---|---|---|
| W-001 | Warning | 03 ↔ 04 | The schema defines `hub_inventory` as a running aggregate table, but the API only has `POST /hubs/{id}/inbound` and `POST /hubs/{id}/outbound` to record events. There is no explicit endpoint to reconcile or reset hub inventory if a data entry error occurs. | Add `PATCH /hubs/{hubId}/inventory/{marketProductId}` for Admin-only manual correction with an audit log entry. Decide before implementing T036/T037. |
| W-002 | Warning | 01 ↔ 04 | FR-ORD-002 requires missed scheduled order instances to be generated on recovery. The implementation plan (T027) assigns this to the scheduled order creation logic but no background job task is explicitly listed for the missed-execution recovery sweep. | Add a sub-task under T027: implement a startup recovery job (`IHostedService`) that checks for scheduled orders whose `nextRunAt` is in the past and creates missed instances. |
| W-003 | Warning | 02 ↔ 05 | The architecture document (Section 7) specifies a PostgreSQL read replica for analytics queries, but the Docker Compose and implementation tasks do not include a replica setup. Analytics queries running on the primary database during peak hours could impact write performance. | Either add a `postgres-replica` service to Docker Compose (straightforward with `pg_basebackup`) or explicitly document that read replica is post-MVP. Decide before T039. |

### Info-Level Observations (no blocking action required)

| ID | Severity | Document | Description | Note |
|---|---|---|---|---|
| I-001 | Info | 01 | GA-003 (stock reservation) assumes a 30-minute soft-reservation TTL in Redis. This value is hardcoded as an assumption. | Make this an Admin-configurable `system_config` entry (`reservation_ttl_minutes`) so it can be tuned without redeployment. |
| I-002 | Info | 03 | `price_snapshots` partitions are created monthly by a background job on the 25th. There is no documented fallback if the job misses a month. The table has a `DEFAULT` partition, which catches overflow, but this could silently absorb data without alerting. | Add a health check or monitoring alert that fires if new data is landing in the DEFAULT partition — this indicates a missing monthly partition. |
| I-003 | Info | 04 | The API design specifies `POST /api/v1/auth/register` for Admin-only user creation. However, FR-AUTH-004 notes that Restaurant self-registration is "configurable by Admin" (GA-009). The API currently has no self-registration endpoint. | If restaurant self-registration is needed before Admin approval workflow is built, a `POST /api/v1/auth/register/restaurant` public endpoint will be required. Flag for product owner decision. |
| I-004 | Info | 04 ↔ 05 | The API design defines `GET /api/v1/notifications` and `PATCH /api/v1/notifications/{id}/read` endpoints, but these are not explicitly listed as tasks in the implementation plan (T043 covers notification insertion, T044 covers the endpoints). Verify T044 acceptance criteria cover the full notifications REST API, not just the SignalR push path. | Confirm T044 scope covers `GET /notifications` (paginated, filterable by is_read) and `PATCH /notifications/{id}/read` before closing T044. |
| I-005 | Info | 02 | The architecture document notes that the nearest-neighbor + 2-opt VRP heuristic is 5–15% from optimal. For the capstone demo, this is acceptable. However, there is no acceptance test defined that verifies the route quality bound. | Add a unit test in `RouteCalculationServiceTests` that verifies a known 5-stop problem produces a route within 15% of the brute-force optimal. This is regression-proof documentation. |
| I-006 | Info | 03 | The schema uses `NUMERIC(12,2)` for prices and `NUMERIC(14,2)` for totals. Vietnamese Dong (VND) has no decimal subdivision in practice, and prices in wholesale markets are typically whole numbers. The decimal columns will always store `.00`. | No action required. `NUMERIC` with 2 decimal places is correct for a system designed to be currency-agnostic. The cost is negligible storage overhead. |
| I-007 | Info | 05 | The implementation plan lists Angular (T045–T047) and React Native (T048–T050) tasks but does not specify which frontend communicates with which hub or which role uses which client. | From architecture: Angular Web is used by Admin and Restaurant (desktop dashboard); React Native is used by Kiosk Staff and Restaurant (mobile). Kiosk staff use React Native exclusively for price updates (T049). Confirm with product owner if Restaurant should also have a React Native client or only web. |

---

## Phase-by-Phase Review Checklist

### Phase 1 (Requirements) — PASS
- [x] Every requirement from source document is captured
- [x] All gaps have explicit assumptions (12 gaps, 0 silent)
- [x] Priority levels are realistic (29 Must, 5 Should, 2 Could)
- [x] Glossary covers all domain-specific terms (20 terms)

### Phase 2 (Architecture) — PASS
- [x] Architecture supports all Must-have requirements
- [x] SignalR scale-out strategy is realistic (Redis backplane documented)
- [x] Caching strategy covers all real-time pricing use cases
- [x] Module boundaries are clean (no circular dependencies documented)
- [x] Docker Compose setup is sufficient for local development

### Phase 3 (Database) — PASS
- [x] All entities from requirements are represented (24 tables)
- [x] No N+1 query traps (indexes cover all FK traversals)
- [x] `price_snapshots` handles high-frequency writes (partitioned, append-only, composite index on `market_product_id + recorded_at DESC`)
- [x] Soft delete strategy consistent (`deleted_at TIMESTAMPTZ NULL` on all mutable tables, absent on append-only tables)
- [x] Redis keys cover all real-time pricing scenarios
- [x] UUID strategy consistent (all PKs are UUID)

### Phase 4 (API Design) — PASS
- [x] Every functional requirement has at least one API endpoint (36/36 coverage)
- [x] SignalR events cover all real-time scenarios (price update, order status, delivery update, significant price alert)
- [x] Role-based access is consistent with Phase 1 requirements (RBAC matrix complete)
- [x] All endpoints have error cases documented
- [x] Pagination applied to all list endpoints

### Phase 5 (Implementation Plan) — PASS
- [x] Every requirement from Phase 1 maps to at least one task (36/36)
- [x] Critical path has no circular dependencies
- [x] Folder structure supports the module design from Phase 2
- [x] MVP scope is minimal but functional (Auth + Pricing real-time + Basic orders, T001–T025)
- [x] Standards are specific and actionable

---

## Final Assessment

**Status: READY TO IMPLEMENT**

The documentation suite is complete and internally consistent. All 36 functional requirements have full traceability from requirement through schema through API endpoint through implementation task. No Blocker-level issues were found.

**Before sprint planning, resolve these three items:**
1. **W-001** — Hub inventory correction endpoint (affects T036/T037)
2. **W-002** — Missed scheduled order recovery job sub-task (affects T027)
3. **W-003** — PostgreSQL read replica decision for analytics (affects T039)

**Recommended first sprint:** T001 → T005 (infrastructure) + T009 → T012 (auth module) in parallel where dependencies allow.
