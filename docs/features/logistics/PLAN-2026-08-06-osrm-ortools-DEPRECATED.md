# PLAN (DEPRECATED) — OSRM Road Routing + OR-Tools VRP

Date: 2026-08-06 · Status: **⛔ DEPRECATED / CANCELLED — DO NOT IMPLEMENT**

> This approach was **cancelled by the user** in favour of a simpler scope. The active plan is
> `PLAN-2026-08-06-goong-delivery-fee.md` (Goong REST road distance + delivery-fee snapshot on order
> confirmation; Hub/market → Restaurant only; **no** OSRM, OR-Tools, VRP, vehicle assignment, or
> multi-drop optimization).
>
> **Any agent must ignore this file and follow the active plan.** Kept only for history. Nothing from
> this plan was implemented (all exploratory code was reverted).

---

## Original goal (no longer pursued)

Make `POST /api/v1/logistics/routes/plan` compute routes from real road-network distance/duration
(OSRM Table service), solve a multi-vehicle capacitated VRP with Google OR-Tools (assignment +
ordering in one solve), attach route geometry (OSRM Route service, GeoJSON), and compute planned ETAs,
with the existing Haversine + nearest-neighbor/2-opt engine kept as an automatic fallback.

## Why it was dropped

The user narrowed the scope to a single, simpler need: real **road distance + delivery fee** for one
Hub/market → one restaurant, saved onto the order at confirmation. Multi-vehicle routing, VRP solving,
OR-Tools, OSRM self-hosting, and route geometry were all deemed out of scope for the current phase.

## Summary of the cancelled design (for reference only)

- Two abstractions in `Logistics.Application/Abstractions`: `IRoutingProvider` (OSRM Table/Route) and
  `IRouteOptimizationService` (OR-Tools CVRP), orchestrated by `PlanRoutesCommandHandler`.
- `OsrmRoutingProvider` — OSRM `/table` + `/route`, lng,lat order, Haversine × factor fallback.
- `OrToolsRouteOptimizationService` — single-depot capacitated VRP, minimize duration, return-to-depot.
- New `route_geometry` column on `delivery_routes` for the map polyline.
- Google.OrTools (native binary, linux-x64); OSRM default = public demo server, optional HCMC self-host.
- Would have deleted `CapacitatedSweepPlanner` (superseded by OR-Tools assignment).

The full detail lives in the personal plan file history; it is intentionally not reproduced here
because it must not be executed.
