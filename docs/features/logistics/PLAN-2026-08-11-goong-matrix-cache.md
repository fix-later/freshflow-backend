# PLAN (ACTIVE) — Cache Goong Distance Matrix (pairwise, durable)

Date: 2026-08-11 · Status: **APPROVED, not yet implemented** · Epic: LOG (SCRUM-298 route planning)

## Problem

`PlanRoutesCommandHandler` calls `IRouteMatrixProvider.GetMatrixAsync` on every plan. For P points
(1 hub + N restaurants) the Goong `DistanceMatrix` call fans out to `ceil(P/batch)²` HTTP requests
**per routing profile** (`MatrixBatchSize` default 10). N=50 restaurants ≈ 25 requests/profile,
re-paid on every re-plan and every service date — even though **hub and restaurant coordinates are
stable** and the road distance between two fixed points barely changes.

The handler already has an `InputRevision` short-circuit, but it only skips a re-solve for the **same
hub + same date + same input**. It does **not** help the common case this cache targets: the same
restaurants (regular B2B customers) re-planned on a **different day** → today that re-fetches the
whole matrix from Goong.

Goong calls cost real money and quota, so the cache must be **durable** (survive app restart), not
in-process.

## Goal

Reuse previously-fetched road distance/duration for a coordinate pair across plans and dates.
Steady state (restaurant set unchanged day to day) → **zero Goong calls**. A cache miss falls back to
exactly today's behaviour (call Goong; on Goong failure, Haversine estimate — unchanged).

**Out of scope:** changing the solver, the fallback maths, the API contract, or `InputRevision`.

## Design

### Storage — new Logistics-owned table `route_matrix_cache`

Durable pairwise KV in Postgres (reuses shared `AppDbContext`; route planning is an admin batch op,
not latency-sensitive, so a table beats Redis and avoids the Pricing-module Redis coupling — Logistics
may not reference Pricing, and `UseRedis` is `false` by default). Snake_case (matches the
`deliveries`/`delivery_routes`/`vehicles` table family).

| column | type | notes |
|---|---|---|
| `pair_key` | text PK | `"{profile}:{fromLat}:{fromLng}:{toLat}:{toLng}"`, coords `ToString("F7", Invariant)` |
| `profile` | text | `car` / `truck` / `bike` (stored for pruning/debug) |
| `from_lat` `from_lng` `to_lat` `to_lng` | numeric | stored for debug/audit |
| `distance_meters` | bigint | Goong value |
| `duration_seconds` | bigint | Goong value |
| `calculated_at` | timestamptz (UTC) | for max-age filtering |

- No `deleted_at` — append/upsert only, like `price_snapshots`. Reads filter `calculated_at`.
- Upsert on refetch (`ON CONFLICT (pair_key) DO UPDATE`). EF: look up existing, update or `Add`.
- Only **real Goong** values are ever written. `HAVERSINE_FALLBACK` (estimated) is **never cached** —
  correctness gate: we must not later serve an estimate as if it were a real road distance.
- Diagonal pairs (`from == to`) are never stored/looked up — always assembled as `0`.

New files — **all in Logistics.Infrastructure, all `internal`** (own table → normal EF entity, **not** a
keyless cross-module Row; auto-discovered by `ApplyConfigurationsFromAssembly`). The store interface,
its impl, the entity, and the decorator are a self-contained Infrastructure seam — **nothing lives in
Application**, because no Application code (handler/query) references the cache; `PlanRoutesCommandHandler`
only depends on `IRouteMatrixProvider`. `InternalsVisibleTo("FreshFlow.Logistics.UnitTests")` already
exists (see `OrToolsRoutePlanningSolver`, also `internal`), so `internal` types are unit-testable.

> ⚠️ **Do NOT put `IRouteMatrixCacheStore` in Application.** Its signature speaks in the Infrastructure
> entity `RouteMatrixCacheEntry`, and Application must not reference Infrastructure. Keep the interface
> in Infrastructure next to its impl.

- `Persistence/Entities/RouteMatrixCacheEntry.cs`
- `Persistence/Configurations/RouteMatrixCacheEntryConfiguration.cs`
- `Persistence/IRouteMatrixCacheStore.cs` (**Infrastructure**, `internal interface`)
- `Persistence/RouteMatrixCacheStore.cs` (`internal sealed`, uses `AppDbContext`)
  - `Task<IReadOnlyDictionary<string,(long dist,long dur)>> GetAsync(IEnumerable<string> keys, DateTime minCalculatedAt, ct)`
    — single `WHERE pair_key = ANY(@keys) AND calculated_at >= @min` (LINQ `keys.Contains`).
  - `Task UpsertAsync(IEnumerable<RouteMatrixCacheEntry> rows, ct)`.

`FreshFlow.Logistics.Infrastructure` is already in `DesignTimeDbContextFactory.ForceLoadModuleAssemblies`
(line 84) — no snapshot-drift risk.

### Integration — `CachingRouteMatrixProvider` decorator

Wraps the existing `GoongRouteMatrixProvider` behind `IRouteMatrixProvider` (Goong provider itself is
untouched):

```
GetMatrixAsync(points, profiles, ct):
  if !settings.MatrixCacheEnabled: return inner.GetMatrixAsync(...)
  minAge = DateTime.UtcNow - MatrixCacheMaxAgeDays
  needed = all (profile, i, j) pairs where i != j
  hits   = store.GetAsync(keys(needed), minAge)
  if hits covers every needed pair:
      assemble ProfileRouteMatrix per profile (diagonal = 0) from hits
      return RouteMatrixResult(matrices, "GOONG_CACHED", isEstimated: false, warnings: [])
  result = await inner.GetMatrixAsync(points, profiles, ct)   // any miss → full fetch (see ceiling)
  if result.Provider == "GOONG":                              // never persist estimates
      store.UpsertAsync(all non-diagonal pairs from result)
  return result
```

`// ponytail: coarse cache — a single new pair triggers a full-matrix refetch. Sparse fetch (call
Goong only for missing pairs) is the upgrade if fleets/menus churn coordinates often; needs the Goong
provider to accept a sparse pair list, breaking its rectangular batching. Not worth it until measured.`

Rationale for coarse-grained: the target case is a **stable** restaurant set replanned across days →
all-hit → skip Goong entirely. The expensive sparse path buys little there and costs real complexity.

### DI change (`Logistics.Infrastructure/DependencyInjection.cs`)

```csharp
// was: services.AddScoped<IRouteMatrixProvider, GoongRouteMatrixProvider>();
services.AddScoped<GoongRouteMatrixProvider>();
services.AddScoped<IRouteMatrixCacheStore, RouteMatrixCacheStore>();
services.AddScoped<IRouteMatrixProvider>(sp => new CachingRouteMatrixProvider(
    sp.GetRequiredService<GoongRouteMatrixProvider>(),
    sp.GetRequiredService<IRouteMatrixCacheStore>(),
    sp.GetRequiredService<IVehicleCapacityPolicy>(),
    sp.GetRequiredService<ILogger<CachingRouteMatrixProvider>>()));
```

### Config (extend `IVehicleCapacityPolicy` / `VehicleCapacityPolicy` — the routing-config home)

```jsonc
"Logistics": { "Routing": {
  "MatrixCache": { "Enabled": true, "MaxAgeDays": 30 }
}}
```
- `MatrixCacheEnabled` (default `true`), `MatrixCacheMaxAgeDays` (default `30`, clamp `1..3650`).

## Migration

```bash
dotnet ef migrations add AddRouteMatrixCache \
  --project src/FreshFlow.Infrastructure.Persistence \
  --startup-project src/FreshFlow.API
```
⚠️ `localhost:5433` is **production** — do **not** `database update` against it without confirming.
Review the generated migration + snapshot before applying.

## Tests

- **Unit** `CachingRouteMatrixProviderTests` (mock `IRouteMatrixCacheStore` + inner):
  - all pairs cached & fresh → inner **never** called, `Provider == "GOONG_CACHED"`, values correct,
    diagonal = 0.
  - one pair missing → inner called once, all non-diagonal pairs upserted.
  - inner returns `HAVERSINE_FALLBACK` → **nothing** written to store.
  - `Enabled == false` → passthrough, store never touched.
  - expired hit (`calculated_at < min`) treated as miss.
- **Integration** (Postgres, Testcontainers) `RouteMatrixCacheStoreTests`: upsert then read round-trips;
  `ANY(@keys)` + max-age filter behave on the real table.
- Existing `GoongRouteMatrixProviderTests` and `PlanRoutesCommandHandlerTests` stay green
  (handler mocks `IRouteMatrixProvider`, unaffected).

## Files

**New (all Logistics.Infrastructure, all `internal`):** `RouteMatrixCacheEntry.cs`,
`RouteMatrixCacheEntryConfiguration.cs`, `IRouteMatrixCacheStore.cs`, `RouteMatrixCacheStore.cs`,
`CachingRouteMatrixProvider.cs` (`internal sealed`), migration, 2 test files.
**Edit:** `DependencyInjection.cs`, `IVehicleCapacityPolicy.cs`, `VehicleCapacityPolicy.cs`,
`appsettings.json`.

## Risks / notes

- Coord format must be **identical** on write and read (`F7`, InvariantCulture) or every lookup misses.
  Restaurant/hub coords come from DB unchanged between builds, so exact-decimal keys are stable.
- If a restaurant relocates, its coords change → new key; old rows go stale harmlessly (max-age +
  optional future prune). **Skip a prune job (YAGNI)** — rows are tiny; add one only if the table grows.
- No behaviour change on Goong failure: fallback path is untouched and never cached.
- Single-instance deployment (per CLAUDE.md) — no cross-instance concern; Postgres is the shared store.
