using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Application.Dtos;
using FreshFlow.Logistics.Application.Mappings;
using FreshFlow.Logistics.Domain.Entities;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Logistics.Application.Commands.PlanRoutes;

internal sealed class PlanRoutesCommandHandler(
    IRoutePlanningInputBuilder inputs,
    IMarketSessionReader sessions,
    IRouteMatrixProvider matrices,
    IRoutePlanningSolver solver,
    IRoutePlanRepository plans,
    IDeliveryRouteRepository routes,
    IVehicleCapacityPolicy settings,
    ILogger<PlanRoutesCommandHandler> logger)
    : IRequestHandler<PlanRoutesCommand, Result<RoutePlanDto>>
{
    public async Task<Result<RoutePlanDto>> Handle(PlanRoutesCommand request, CancellationToken ct)
    {
        var rawCriteria = string.IsNullOrWhiteSpace(request.OptimizationCriteria)
            ? nameof(OptimizationCriteria.distance) : request.OptimizationCriteria;
        if (!Enum.TryParse<OptimizationCriteria>(rawCriteria, true, out var criteria)
            || !Enum.IsDefined(criteria))
            return Result<RoutePlanDto>.Failure(Error.Validation(
                "VALIDATION_ERROR", "OptimizationCriteria must be DISTANCE, TIME, or COST."));

        var session = await sessions.FindByIdAsync(request.MarketSessionId, ct);
        if (session is null)
            return Result<RoutePlanDto>.Failure(Error.NotFound("MARKET_SESSION", request.MarketSessionId));

        var inputResult = await inputs.BuildAsync(session.HubId, session.ServiceDate, ct);
        if (!inputResult.IsSuccess)
            return Result<RoutePlanDto>.Failure(inputResult.Error);
        var input = inputResult.Value;
        var existing = await plans.FindProposedAsync(request.MarketSessionId, ct);
        if (input.Demands.Count == 0)
        {
            if (existing is not null)
            {
                existing.Supersede();
                foreach (var oldRoute in await plans.GetRoutesAsync(existing.Id, ct))
                    oldRoute.CancelPlanProposal();
                await plans.TrySaveChangesAsync(ct);
            }
            // Nothing routable — but "nothing" and "everything was excluded for bad data" are very
            // different for the operator, and no RoutePlan row is persisted here to carry the
            // reasons. Report input.Excluded directly, or the M7 exclusion reasons are lost and
            // this reads as an empty day (before M7 it was at least an actionable error).
            return Result<RoutePlanDto>.Success(new RoutePlanDto(
                null, "empty", session.HubId, session.ServiceDate, criteria.ToString().ToUpperInvariant(),
                "NONE", false, input.InputRevision, 0, 0, 0, 0, 0, [],
                input.Excluded.Select(x => x.ToDto()).ToList(), [], DateTime.UtcNow));
        }

        if (existing is not null && existing.InputRevision == input.InputRevision
            && existing.OptimizationCriteria == criteria)
        {
            var existingRoutes = await plans.GetRoutesAsync(existing.Id, ct);
            return Result<RoutePlanDto>.Success(existing.ToDto(existingRoutes, input.Vehicles.ToDictionary(x => x.Id)));
        }

        var points = new[] { new RoutePoint(input.HubLatitude, input.HubLongitude) }
            .Concat(input.Demands.Select(x => new RoutePoint(x.Latitude, x.Longitude))).ToList();
        var matrix = await matrices.GetMatrixAsync(
            points, input.Vehicles.Select(x => x.RoutingProfile).Distinct().ToList(), ct);
        var solutionResult = solver.Solve(input, matrix, criteria);
        if (!solutionResult.IsSuccess)
            return Result<RoutePlanDto>.Failure(solutionResult.Error);
        var solution = solutionResult.Value;

        var totalLoad = solution.Routes.Sum(route => route.Restaurants.Sum(x => x.LoadKg));
        var totalDistance = Math.Round(solution.Routes.Sum(x => x.DistanceMeters) / 1000m, 2);
        var totalDuration = solution.Routes.Sum(x =>
            checked((int)Math.Ceiling((x.RoadDurationSeconds + 60d * x.Restaurants.Count * settings.ServiceTimeMinutes) / 60d)));
        var totalCost = Math.Round(totalDistance * settings.CostPerKm, 2);
        // M7: solver-produced (didn't fit) and builder-produced (incomplete data) unassigned
        // entries both surface on the plan; RoutePlanUnassigned.ExcludedForIncompleteData tells
        // ApproveRoutePlanCommandHandler which kind still blocks approval.
        var unassigned = solution.Unassigned.Concat(input.Excluded).ToList();
        var plan = RoutePlan.Create(
            request.MarketSessionId, input.HubId, input.ServiceDate, criteria, matrix.Provider, matrix.IsEstimated,
            input.InputRevision, unassigned, solution.Routes.Count,
            totalLoad, totalDistance, totalDuration, totalCost);

        var demandPoint = input.Demands.Select((demand, index) => (demand.RestaurantId, Point: index + 1))
            .ToDictionary(x => x.RestaurantId, x => x.Point);
        var createdRoutes = new List<DeliveryRoute>();
        var start = TimeZoneInfo.ConvertTimeToUtc(
            input.ServiceDate.ToDateTime(new TimeOnly(settings.StartHour, 0)),
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));
        foreach (var solved in solution.Routes)
        {
            var profile = matrix.Profiles[solved.Vehicle.RoutingProfile];
            var current = start;
            var previousPoint = 0;
            var stops = new List<RouteStop>
            {
                new(0, StopEntityType.hub, input.HubId, input.HubName,
                    input.HubLatitude, input.HubLongitude, start, start)
            };
            foreach (var demand in solved.Restaurants)
            {
                var point = demandPoint[demand.RestaurantId];
                current = current.AddSeconds(profile.DurationSeconds[previousPoint][point]);
                var departure = current.AddMinutes(settings.ServiceTimeMinutes);
                stops.Add(new RouteStop(
                    stops.Count, StopEntityType.restaurant, demand.RestaurantId, demand.RestaurantName,
                    demand.Latitude, demand.Longitude, current, departure, demand.OrderIds, demand.LoadKg));
                current = departure;
                previousPoint = point;
            }
            current = current.AddSeconds(profile.DurationSeconds[previousPoint][0]);

            var routeDistance = Math.Round(solved.DistanceMeters / 1000m, 2);
            var routeDuration = checked((int)Math.Ceiling(
                (solved.RoadDurationSeconds + 60d * solved.Restaurants.Count * settings.ServiceTimeMinutes) / 60d));
            var route = DeliveryRoute.CreateHubRoute(
                input.HubId, input.ServiceDate, stops, null, request.MarketSessionId);
            route.ApplyOptimization(stops, routeDistance, routeDuration,
                Math.Round(routeDistance * settings.CostPerKm, 2), criteria);
            route.AttachToPlan(plan.Id, solved.Vehicle.Id, solved.Restaurants.Sum(x => x.LoadKg),
                solved.Vehicle.RoutingProfile, current);
            await routes.AddAsync(route, ct);
            createdRoutes.Add(route);
        }

        if (existing is not null)
        {
            existing.Supersede();
            foreach (var oldRoute in await plans.GetRoutesAsync(existing.Id, ct))
                oldRoute.CancelPlanProposal();
        }
        await plans.AddAsync(plan, ct);
        if (!await plans.TrySaveChangesAsync(ct))
            return Result<RoutePlanDto>.Failure(Error.Conflict(
                "ROUTE_PLAN_CONFLICT", "Another route proposal changed the same hub and service date."));

        logger.LogInformation(
            "Route plan created PlanId={PlanId} Inputs={InputCount} Provider={Provider} VehiclesUsed={VehiclesUsed} Unassigned={Unassigned}",
            plan.Id, input.Demands.Count, matrix.Provider, plan.VehiclesUsed, plan.Unassigned.Count);
        return Result<RoutePlanDto>.Success(plan.ToDto(createdRoutes, input.Vehicles.ToDictionary(x => x.Id)));
    }
}
