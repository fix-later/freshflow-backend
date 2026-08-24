# PLAN (ACTIVE) — Goong Road Distance + Delivery Fee Snapshot

Date: 2026-08-06 · Status: **APPROVED, not yet implemented** · Epic: LOG / Orders delivery fee

> Companion: `PLAN-2026-08-06-osrm-ortools-DEPRECATED.md` is an **older, cancelled** approach —
> ignore it. This file is the plan to execute.

## Context

FreshFlow already computes a **delivery fee at order confirmation** using a straight-line
**Haversine** distance (feature GAP-03), entirely inside the **Orders module**. We want the distance
to come from **real road distance via the Goong REST API** (backend-only), the fee formula to gain
BaseFee/MinimumFee/RoundingUnit, and a **stable delivery snapshot persisted onto the order at
confirmation** (distance in metres, duration, fee, provider, calculated-at, origin/destination
coords). When Goong fails/times out, fall back to Haversine × a configured factor and mark the result
as estimated.

**Explicitly out of scope:** OSRM, OR-Tools, VRP, vehicle assignment, multi-drop route optimization.

End goal: for one Hub/market → one restaurant, the system returns road distance, estimated duration,
and delivery fee, and stores them durably on the order once confirmed.

## What already exists (reuse, do not rebuild)

- `OrderPricingCalculator.Calculate(...)` — `src/Modules/Orders/FreshFlow.Orders.Application/Services/OrderPricingCalculator.cs`
  (static pure): VAT/MOQ/subtotal + delivery distance (max Haversine over product origins → dest) +
  `fee = round(distanceKm × deliveryFeePerKm, 2)`. Returns `OrderPricingQuote`.
- Invoked at **confirm** — `ConfirmOrderCommandHandler.cs` (persists via
  `order.ApplyConfirmationPricing(taxes, distanceKm, fee)`) — and **preview** —
  `PreviewOrderConfirmationQueryHandler.cs`.
- Coords already come from the **DB, not the client**: origin per product =
  `MarketProductSnapshotDto.OriginLatitude/Longitude` (SQL view in `MarketProductRowConfiguration.cs`
  = active hub coords else market coords); destination = restaurant delivery address
  (`IRestaurantReader.FindDeliveryAddressAsync`). Confirm request only carries `DeliveryAddressId`.
- Order entity already has `DeliveryFee`, `DeliveryDistanceKm`, `DeliveryLatitude/Longitude` (dest),
  and the snapshot pattern `ApplyConfirmationPricing` / `CaptureDeliveryAddress`
  (`src/Modules/Orders/FreshFlow.Orders.Domain/Entities/Order.cs`).
- Fee rate `DeliveryFeePerKm` lives in DB singleton `OperationalSettings` (default 5000), admin-edited
  via `/api/v1/admin/operational-settings`.
- HttpClient + IOptions + resilience template: `src/FreshFlow.API/Assistant/DependencyInjection.cs`
  (`AddHttpClient` + `AddStandardResilienceHandler`) and `ZenMuxOptions.cs`. Secret convention:
  appsettings placeholder `<set-via-user-secrets-or-environment>`, real value via env var.
- Migration template for adding order columns: `20260801151302_AddOrderCommercialTerms.cs`.
- Tests: `tests/Unit/FreshFlow.Orders.UnitTests/Services/OrderPricingCalculatorTests.cs`
  (golden: origin(10,106)→dest(10.1,106), rate 5000 ⇒ 11.12 km, fee 55 600) + handler tests.

## Locked decisions

1. **Provider lives in Orders module** (fee is Orders-owned; Orders cannot reference Logistics).
   New abstraction `IRoadDistanceProvider` in `Orders.Application/Abstractions`; impl
   `GoongRoadDistanceProvider` in `Orders.Infrastructure`.
2. **Goong endpoint choice:** 1 origin → **Directions** (`/Direction`); >1 origin →
   **Distance Matrix** (`/DistanceMatrix`, origins→single destination) then pick the **max-distance
   origin** (preserves the existing max-origin semantics). Coordinates sent in Goong's **`lat,lng`**
   order (verify against docs.goong.io during implementation), origins joined with `|`.
3. **Fallback:** on timeout / HTTP error / non-OK / empty / parse error, the provider returns
   Haversine × `FallbackRoadFactor` (duration derived at a nominal urban speed) with
   `Provider = "HAVERSINE_FALLBACK"`; success ⇒ `Provider = "GOONG"`. Caller propagates genuine
   cancellation. This satisfies both "mark as estimate" and the `RoutingProvider` snapshot field.
4. **Fee config in DB OperationalSettings:** add `BaseFee`, `MinimumFee`, `RoundingUnit` alongside
   `DeliveryFeePerKm`; extend the admin settings command/validator/DTOs.
   Formula: `raw = BaseFee + distanceKm × RatePerKm`; `fee = round-to-unit(max(raw, MinimumFee))`
   (`RoundingUnit = 0` ⇒ round to 2 dp — keeps the golden test at 55 600 with defaults 0).
5. **Both Preview and Confirm use the provider**; Preview stays non-fatal.
6. **Goong connection config in appsettings/IOptions** (`GoongOptions`, section `Delivery:Goong`):
   `BaseUrl` (default `https://rsapi.goong.io`), `ApiKey` (secret, env `Delivery__Goong__ApiKey`,
   placeholder only in appsettings), `Vehicle` (default `car`), `TimeoutSeconds`, `RetryCount`,
   `FallbackRoadFactor` (default 1.4).
7. **No external call inside the DB transaction:** in `ConfirmOrderCommandHandler`, load the
   destination address + product origins and call `IRoadDistanceProvider` **before**
   `BeginTransaction`; pass the resulting `RoadDistanceResult` into the transactional pricing/persist.

## Data model change (one migration `AddDeliveryRoadDistanceSnapshot`)

New nullable columns on **`orders`** (snake_case + `HasColumnName`, mirror `delivery_fee` mapping in
`OrderConfiguration.cs`), set only at confirm:
`delivery_distance_meters int`, `delivery_duration_seconds int`, `delivery_fee_calculated_at timestamptz`,
`routing_provider text`, `delivery_origin_latitude numeric(9,6)`, `delivery_origin_longitude numeric(9,6)`.
Keep existing `delivery_distance_km` (fee basis) and `delivery_latitude/longitude` (destination).

New columns on **`operational_settings`** (not-null, default 0, backfill 0): `base_fee numeric(14,2)`,
`minimum_fee numeric(14,2)`, `rounding_unit numeric(14,2)`.

## Files to ADD

- `Orders.Application/Abstractions/IRoadDistanceProvider.cs` — `GetDistanceAsync(IReadOnlyList<GeoCoordinate> origins, GeoCoordinate destination, CancellationToken)` → `RoadDistanceResult(int DistanceMeters, int DurationSeconds, GeoCoordinate ChosenOrigin, bool IsEstimated, string Provider)`; plus `GeoCoordinate(decimal Latitude, decimal Longitude)`.
- `Orders.Infrastructure/Goong/GoongOptions.cs` — sealed, `init` props, DataAnnotations (`[Required] BaseUrl`, `[Range] TimeoutSeconds`, `[Range] FallbackRoadFactor`).
- `Orders.Infrastructure/Goong/GoongRoadDistanceProvider.cs` — named `IHttpClientFactory` client, coord validation (lat∈[-90,90], lng∈[-180,180], non-null, no NaN/Inf), Directions/Matrix calls, parse `distance.value`/`duration.value`, Haversine fallback (reuse/extract the Haversine in `OrderPricingCalculator`).
- Unit tests (see below).

## Files to MODIFY

- `Orders.Application/Services/OrderPricingCalculator.cs` — stop computing Haversine distance itself;
  take a pre-computed `deliveryDistanceKm` + a fee config `(BaseFee, RatePerKm, MinimumFee, RoundingUnit)`
  and compute `deliveryFee` with the new formula. Keep VAT/MOQ/subtotal untouched.
- `Orders.Application/.../OrderPricingQuote` (+ `ConfirmOrderCommandHandler.cs`,
  `PreviewOrderConfirmationQueryHandler.cs`) — inject `IRoadDistanceProvider`, resolve origins+dest,
  call the provider (before the txn in Confirm), thread `RoadDistanceResult` + fee config through.
- `Order.cs` — widen the confirm-snapshot seam: `ApplyConfirmationPricing(...)` (or a sibling
  `ApplyDeliverySnapshot`) to also persist metres, duration, origin lat/lng, provider, calculatedAt;
  add the new private-set properties. `RecalculateTotal` zeroes them like the existing fee fields.
- `OrderConfiguration.cs` — map the six new order columns.
- `OperationalSettings.cs` + `OperationalSettingsConfiguration.cs` + `OperationalSettingsRepository`
  (upsert) + `UpdateOperationalSettingsCommand`/`Validator` + Admin DTOs in `AdminController.cs` —
  add BaseFee/MinimumFee/RoundingUnit.
- `Orders.Infrastructure/DependencyInjection.cs` — `AddOptions<GoongOptions>().Bind("Delivery:Goong").ValidateDataAnnotations().ValidateOnStart()`; named HttpClient + `AddStandardResilienceHandler`; register `IRoadDistanceProvider` → `GoongRoadDistanceProvider`. Add `Microsoft.Extensions.Http(.Resilience)` / Options package refs to the csproj if missing.
- `src/FreshFlow.API/appsettings.json` — add `Delivery:Goong` section (ApiKey = placeholder only).
- `Directory.Packages.props` — only if a new package version entry is needed (Http/Resilience already present).

## Tests (xUnit + FluentAssertions + NSubstitute; `[Trait("Category","Unit")]`)

- **Coordinate validation** — provider rejects out-of-range / NaN / null coords.
- **Goong parse** — fake `HttpMessageHandler`: Directions response → metres/seconds; Distance Matrix
  multi-origin → picks max-distance origin.
- **Fee formula** — BaseFee + distanceKm×RatePerKm; update the existing golden test to pass
  distanceKm=11.12 directly and still assert 55 600 with defaults.
- **MinimumFee** applied when raw < min; **RoundingUnit** rounds to nearest unit (and unit=0 ⇒ 2 dp).
- **Timeout / HTTP error / non-OK / empty** → Haversine fallback, `IsEstimated=true`,
  `Provider="HAVERSINE_FALLBACK"`.
- **Haversine fallback value** — factor applied to great-circle distance.
- Update `ConfirmOrderCommandHandlerTests` / `PreviewOrderConfirmationQueryHandlerTests` for the new
  provider dependency (substitute `IRoadDistanceProvider`).

## Verification (must actually run)

```bash
dotnet build FreshFlow.slnx
dotnet ef migrations add AddDeliveryRoadDistanceSnapshot --project src/FreshFlow.Infrastructure.Persistence --startup-project src/FreshFlow.API
dotnet ef migrations has-pending-model-changes --project src/FreshFlow.Infrastructure.Persistence --startup-project src/FreshFlow.API   # expect: none
dotnet test tests/Unit/FreshFlow.Orders.UnitTests/
dotnet test tests/Integration/FreshFlow.IntegrationTests/ --filter "FullyQualifiedName~Orders"
dotnet format FreshFlow.slnx --verify-no-changes
dotnet build-server shutdown
```
Manual smoke: confirm an order with a valid `DeliveryAddressId` → order row has road
`delivery_distance_meters`, `delivery_duration_seconds`, `delivery_fee`, `delivery_fee_calculated_at`,
`routing_provider = GOONG`, origin/destination coords. Break the Goong URL → confirm still succeeds
with `routing_provider = HAVERSINE_FALLBACK` and estimated values.

## Assumptions / out of scope

- Exact Goong response shape + `lat,lng` order + vehicle values verified against docs.goong.io at
  implementation time; a real Goong API key is needed to smoke-test the live path (env var, never
  committed).
- Snapshot origin = the max-distance product origin (matches current fee semantics for multi-market
  orders). No OSRM/OR-Tools/VRP/vehicle-assignment/multi-drop.
