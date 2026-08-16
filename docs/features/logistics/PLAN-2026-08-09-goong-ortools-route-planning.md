# Goong + OR-Tools multi-vehicle route planning

**Date:** 2026-08-09  
**Status:** Implemented  
**Supersedes as active engine:** Haversine nearest-neighbor/2-opt planning. The 2026-08-06 plans remain historical.

## Locked decisions

- Goong Distance Matrix supplies road distance and duration; profiles map `van → car`, `truck → truck`, and `motorbike → bike`.
- Any missing/invalid Goong batch discards the complete provider result and rebuilds every profile with Haversine × `Delivery:Goong:FallbackRoadFactor` at 30 km/h.
- Google.OrTools `9.15.6755` solves a multi-vehicle CVRP. Every route starts and ends at the hub, one restaurant is one indivisible node, and every vehicle is limited to 90% registered capacity.
- Objective priority is dropped restaurants, then activated vehicles, then `DISTANCE`, `TIME`, or `COST`.
- `ActualQuantity` is kilograms. Line load is `actual kg + ceil(actual kg / packing capacity kg) × box tare kg`.
- Planning creates a proposal only. Approval snapshots deliveries and reserves the suggested vehicles atomically; driver assignment remains a later operator action.
- V1 excludes route geometry, realtime traffic, time windows, order splitting, cache, OSRM, and Google Routes.

## Pipeline

`POST /api/v1/logistics/routes/plan` reads only `AtHub` orders for the requested hub and Vietnam service date. Orders are grouped by restaurant with sorted order IDs, coordinates, and load. Missing coordinates fail with `MISSING_COORDINATES`; missing packing capacity fails with `ROUTE_WEIGHT_INCOMPLETE`.

The matrix contains hub node 0 plus restaurant nodes. Requests are sequential blocks controlled by `MatrixBatchSize`, for fleet profiles actually present. Caller cancellation propagates; timeout, HTTP, JSON, row, element, or status failures select one complete Haversine fallback matrix.

The solver uses per-vehicle gram capacities and profile-specific transit callbacks. A count dimension permits 19 restaurants plus the hub. Oversized nodes return `LOAD_EXCEEDS_LARGEST_VEHICLE`; other dropped nodes return `FLEET_CAPACITY_UNAVAILABLE`. Metrics include the final hub edge. ETA starts at `StartHour`, adds road and service time, and persists `EstimatedReturnAt`.

## Proposal and approval

`route_plans` persists status, revision, routing provenance, totals, and unassigned JSON. `delivery_routes` stores the plan link, suggested vehicle, load, profile, return ETA, and nullable stop `OrderIds`/`LoadKg`.

`InputRevision` is a stable SHA-256 of hub coordinates, sorted orders and packing, restaurant coordinates, fleet capacity/availability, and planning settings. An identical proposed revision and criterion is reused; changed input supersedes the proposal and cancels its planned routes.

`POST /api/v1/logistics/routes/plans/{planId}/approve` rebuilds the revision, rejects stale or unassigned plans, snapshots deliveries, reserves suggested vehicles, marks routes reviewed, and marks the plan approved in one transaction. Unique indexes protect one proposed plan per hub/day, one delivery per order, and one vehicle reservation per service date.

`GET /api/v1/logistics/routes/plans/{planId}` returns the proposal. Approved manifests use delivery snapshots; proposed manifests use stop order IDs; legacy routes retain the prior `AtHub` lookup. Approved pickup validates the exact snapshot without recreating deliveries.

## Configuration

```json
{
  "Logistics": {
    "CapacityUtilizationPercent": 90,
    "MaxStopsPerVehicle": 20,
    "Routing": {
      "MatrixBatchSize": 10,
      "SolverTimeLimitSeconds": 3,
      "StartHour": 6,
      "ServiceTimeMinutes": 10,
      "CostPerKm": 5000
    }
  }
}
```

Goong reuses `Delivery:Goong`; no second secret is introduced. Migration: `AddRoutePlanningBatchesAndReservations`. The OR-Tools unit smoke test loads the native runtime and covers the `60,50,40,50` two-bin trap.
