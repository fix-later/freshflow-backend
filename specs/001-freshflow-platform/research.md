# Research: FreshFlow Platform

**Branch**: `001-freshflow-platform` | **Date**: 2026-05-11
**Input**: Existing design documents in `docs/` (architecture, database schema, API design,
implementation plan) plus user plan requirements.

> All decisions below are derived from the pre-approved docs (source of truth) or
> explicitly resolved conflicts where the docs and user requirements diverge.

---

## Decision 1: VRP Algorithm for Route Calculation

**Decision**: Nearest-neighbor heuristic with 2-opt local improvement, capped at 20 stops.

**Rationale**: The constraint is ≤ 20 delivery stops (FR-LOG-006 in requirements spec). At this
scale, a nearest-neighbor + 2-opt heuristic runs in < 100 ms in memory and produces solutions
within 5–15% of optimal — acceptable for a capstone. The VrpSolver class is pure in-memory with
no database or Redis dependencies, making it independently unit-testable.

**Alternatives considered**:
- **Google OR-Tools**: Produces globally optimal results but requires a native binary dependency.
  Not justified for ≤ 20 stops. Noted in docs as the v2 upgrade path.
- **Brute-force permutation**: O(n!) complexity — infeasible beyond 10 stops.
- **Commercial routing API (Google Maps Directions Matrix)**: Requires external API key and network
  call per calculation. Not suitable for a capstone with no external dependencies.

**Accepted trade-off**: Heuristic may produce routes 5–15% longer than optimal for some stop
combinations. Documented in `docs/02-system-architecture.md` Technology Decisions Log.

---

## Decision 2: SignalR Scale-Out Strategy

**Decision**: Use Redis as the SignalR backplane from day one
(`services.AddSignalR().AddStackExchangeRedis(...)`).

**Rationale**: Redis is already required for price caching (`price:{marketId}:{productId}`)
and soft-reservation counters. Adding the SignalR backplane costs zero additional infrastructure
and enables horizontal scaling with no code changes. A single API container is sufficient for
capstone load (500 concurrent sessions), but the backplane is free when Redis is already present.

**Alternatives considered**:
- **Single-instance mode (no backplane)**: Sufficient for capstone, but requires a code change
  and Redis config change to enable scale-out later. Given Redis is already there, this buys
  nothing and creates future friction.
- **Azure SignalR Service**: External managed backplane. Overkill and adds a paid dependency.

**Capstone implication**: `SignalR__UseRedis=false` is supported in `appsettings.Development.json`
for developers who want to run without Redis locally. CI and staging always use the backplane.

---

## Decision 3: Authentication Token Storage — CONFLICT RESOLUTION

**⚠ Conflict detected**: The user's plan requirements specify "httpOnly cookie setup in
ASP.NET Core." The existing `docs/04-api-design.md` (source of truth) designs tokens in the
JSON response body with `Authorization: Bearer` headers. React Native cannot use httpOnly
cookies (browser-only mechanism).

**Resolution (follow the docs)**: Tokens are returned in the JSON response body. Clients store
them in:
- **Angular**: `localStorage` or `sessionStorage` (angular interceptor attaches Bearer header)
- **React Native**: `expo-secure-store` / `react-native-keychain` (platform secure storage)

httpOnly cookies are NOT used. The CSRF attack surface does not apply to mobile apps, and the
SPA's XSS risk is mitigated by the 15-minute access token TTL and content-security-policy headers.

**Implication for plan**: Remove the httpOnly cookie item from the auth implementation scope.
Bearer token in response body is the authoritative design.

---

## Decision 4: Token Refresh Strategy

**Decision**: Proactive client-side refresh before expiry + server-side rotation enforcement.

**Details**:
- Angular `AuthInterceptor`: schedules a token refresh at `exp - 60 seconds`. If a request
  receives a 401, also attempts a refresh and retries once. Uses a shared `refreshPromise$`
  to prevent multiple simultaneous refresh calls.
- React Native `api.service.ts`: same pattern using Axios interceptors and an in-flight
  `refreshPromise` guard.
- Server: on `POST /api/v1/auth/refresh`, the old token is immediately revoked and a new
  token pair returned. Reuse detection invalidates the entire token family.

---

## Decision 5: AI Price Prediction

**Decision**: Out of scope for v1. Not implemented.

**Rationale**: The spec explicitly marks AI recommendations ("buy now or wait") as a nice-to-have
item that is out of scope for this capstone version.

**If revisited in v2**: A simple 7-day or 30-day rolling average over `price_snapshots`, computed
during the `AnalyticsAggregationJob` nightly run and served via the analytics endpoint, would
provide a credible demo without a real ML model. No external ML dependency would be needed.

---

## Decision 6: price_snapshots High-Frequency Writes

**Decision**: Monthly RANGE partitioning + `PARTITION BY RANGE (recorded_at)` + append-only
no-update-no-delete policy + `PartitionMaintenanceJob` background service.

**Rationale**: Kiosk staff update prices continuously from 2–6 AM across 3 markets × ~100
products. This generates up to 300 writes per session, which is not high by PostgreSQL standards,
but the table will grow unboundedly. Monthly partitioning keeps each partition small (one month
of data), enables fast range scans for analytics queries, and allows old partitions to be
archived without touching the main table.

**Details**:
- `PartitionMaintenanceJob` runs on the 25th of each month and creates next month's partition.
- Default partition (`price_snapshots_default`) catches any rows that don't match a monthly
  partition (failsafe only).
- Index `idx_price_snapshots_market_product_recorded_at` on the parent table propagates to
  all child partitions automatically.
- EF Core 8 + Npgsql support partitioned tables without special configuration. The
  `IEntityTypeConfiguration<PriceSnapshot>` uses `ToTable("price_snapshots")` and the
  migration generates the `PARTITION BY RANGE` DDL via a raw SQL migration.

---

## Decision 7: Notification Module = 7th Module

**Note**: The user's plan requirements mention "6 modules" but the system has 7:
Auth, Pricing, Orders, Logistics, Hub, Analytics, **Notifications**. The Notifications module
owns all three SignalR hubs (PricingHub, OrderHub, DeliveryHub) and the broadcast service
implementations. Other modules call the broadcast interfaces; the Notifications module provides
the concrete hub implementations. This is documented in `docs/02-system-architecture.md`.

---

## Decision 8: Sprint 1 Scope (Immediately Deliverable)

**Sprint 1 definition**: Auth API (login + refresh + logout) running + Angular login page +
React Native login screen. All three talking to the same backend.

**Tasks**: T001 → T002 → T003 → T004 → T005 → T007 → T008 → T009 → T010 → T011 → T045
(scaffold) → T048 (scaffold)

**Why this is achievable in Sprint 1**: T001–T011 + scaffolds are infrastructure and auth
only. No business logic. No complex queries. End-to-end demo: user can log in from both
Angular and React Native, access token refreshes automatically, logout invalidates token.

---

## Decision 9: Testing Framework Selection

**Decision**:
- Unit tests: xUnit + FluentAssertions + Moq (mocking for service-layer tests only — never
  for integration tests)
- Integration tests: xUnit + `Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory` +
  `Testcontainers.PostgreSql` + `Testcontainers.Redis` (spin up real containers per test run)
- All tests categorized via `[Trait("Category", "Unit")]` / `[Trait("Category", "Integration")]`
  for CI filter support

**Rationale**: Real-container integration tests are mandated by Constitution Principle III
("real PostgreSQL and Redis — no mocks"). Testcontainers-dotnet is the standard approach for
this pattern in .NET; it starts lightweight container instances during the test run and tears
them down automatically.
