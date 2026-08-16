using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Domain.Enums;
using FreshFlow.Logistics.Domain.ValueObjects;
using FreshFlow.SharedKernel.Application;
using Google.OrTools.ConstraintSolver;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;

namespace FreshFlow.Logistics.Infrastructure.Optimization;

internal sealed class OrToolsRoutePlanningSolver(
    IVehicleCapacityPolicy settings,
    ILogger<OrToolsRoutePlanningSolver> logger) : IRoutePlanningSolver
{
    public Result<RoutePlanningSolution> Solve(
        RoutePlanningInput input,
        RouteMatrixResult matrix,
        OptimizationCriteria criteria)
    {
        var maxCapacity = input.Vehicles.Count == 0 ? 0m : input.Vehicles.Max(x => x.EffectiveCapacityKg);
        var oversized = input.Demands
            .Where(demand => demand.LoadKg > maxCapacity)
            .Select(demand => Unassigned(demand, "LOAD_EXCEEDS_LARGEST_VEHICLE"))
            .ToList();
        var demands = input.Demands.Where(demand => demand.LoadKg <= maxCapacity).ToList();
        if (demands.Count == 0 || input.Vehicles.Count == 0)
        {
            oversized.AddRange(demands.Select(demand => Unassigned(demand, "FLEET_CAPACITY_UNAVAILABLE")));
            return Result<RoutePlanningSolution>.Success(new RoutePlanningSolution([], oversized));
        }

        var originalPoints = input.Demands
            .Select((demand, index) => (demand.RestaurantId, Point: index + 1))
            .ToDictionary(x => x.RestaurantId, x => x.Point);
        var originalPointByNode = new[] { 0 }
            .Concat(demands.Select(demand => originalPoints[demand.RestaurantId]))
            .ToArray();
        var starts = Enumerable.Repeat(0, input.Vehicles.Count).ToArray();
        var ends = Enumerable.Repeat(0, input.Vehicles.Count).ToArray();
        using var manager = new RoutingIndexManager(demands.Count + 1, input.Vehicles.Count, starts, ends);
        using var routing = new RoutingModel(manager);

        var costCallbacks = new int[input.Vehicles.Count];
        long maxArcCost = 1;
        for (var vehicleIndex = 0; vehicleIndex < input.Vehicles.Count; vehicleIndex++)
        {
            var vehicle = input.Vehicles[vehicleIndex];
            var profile = matrix.Profiles[vehicle.RoutingProfile];
            long Cost(long fromIndex, long toIndex)
            {
                var from = originalPointByNode[manager.IndexToNode(fromIndex)];
                var to = originalPointByNode[manager.IndexToNode(toIndex)];
                return criteria switch
                {
                    OptimizationCriteria.time => profile.DurationSeconds[from][to],
                    OptimizationCriteria.cost => checked((long)Math.Round(
                        profile.DistanceMeters[from][to] * settings.CostPerKm / 1000m,
                        MidpointRounding.AwayFromZero)),
                    _ => profile.DistanceMeters[from][to]
                };
            }

            costCallbacks[vehicleIndex] = routing.RegisterTransitCallback(Cost);
            routing.SetArcCostEvaluatorOfVehicle(costCallbacks[vehicleIndex], vehicleIndex);
            for (var from = 0; from < originalPointByNode.Length; from++)
                for (var to = 0; to < originalPointByNode.Length; to++)
                    maxArcCost = Math.Max(maxArcCost, Cost(manager.NodeToIndex(from), manager.NodeToIndex(to)));
        }

        var demandCallback = routing.RegisterUnaryTransitCallback(index =>
        {
            var node = manager.IndexToNode(index);
            return node == 0 ? 0L : ToGrams(demands[node - 1].LoadKg);
        });
        routing.AddDimensionWithVehicleCapacity(
            demandCallback, 0,
            input.Vehicles.Select(x => ToGrams(x.EffectiveCapacityKg)).ToArray(),
            true, "Capacity");

        var countCallback = routing.RegisterUnaryTransitCallback(index => manager.IndexToNode(index) == 0 ? 0L : 1L);
        routing.AddDimension(countCallback, 0, Math.Max(1, settings.MaxStopsPerVehicle - 1), true, "Stops");

        var activationPenalty = checked(maxArcCost * (demands.Count + 2L) + 1L);
        var dropPenalty = checked(activationPenalty * (input.Vehicles.Count + 1L)
            + maxArcCost * (demands.Count + input.Vehicles.Count + 2L) + 1L);
        for (var vehicleIndex = 0; vehicleIndex < input.Vehicles.Count; vehicleIndex++)
            routing.SetFixedCostOfVehicle(activationPenalty, vehicleIndex);
        for (var node = 1; node <= demands.Count; node++)
            routing.AddDisjunction([manager.NodeToIndex(node)], dropPenalty);

        var search = operations_research_constraint_solver.DefaultRoutingSearchParameters();
        search.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.ParallelCheapestInsertion;
        search.LocalSearchMetaheuristic = LocalSearchMetaheuristic.Types.Value.GuidedLocalSearch;
        search.TimeLimit = new Duration { Seconds = settings.SolverTimeLimitSeconds };
        var started = DateTime.UtcNow;
        var solution = routing.SolveWithParameters(search);
        if (solution is null)
            return Result<RoutePlanningSolution>.Failure(Error.Conflict(
                "ROUTE_PLAN_INFEASIBLE", "No route plan solution was found within the solver limit."));

        var dropped = oversized;
        for (var node = 1; node <= demands.Count; node++)
        {
            var index = manager.NodeToIndex(node);
            if (solution.Value(routing.NextVar(index)) == index)
                dropped.Add(Unassigned(demands[node - 1], "FLEET_CAPACITY_UNAVAILABLE"));
        }

        var routes = new List<SolvedVehicleRoute>();
        for (var vehicleIndex = 0; vehicleIndex < input.Vehicles.Count; vehicleIndex++)
        {
            var profile = matrix.Profiles[input.Vehicles[vehicleIndex].RoutingProfile];
            var index = routing.Start(vehicleIndex);
            if (routing.IsEnd(solution.Value(routing.NextVar(index))))
                continue;

            var assigned = new List<RestaurantDemand>();
            long distance = 0;
            long duration = 0;
            while (!routing.IsEnd(index))
            {
                var next = solution.Value(routing.NextVar(index));
                var from = originalPointByNode[manager.IndexToNode(index)];
                var to = originalPointByNode[manager.IndexToNode(next)];
                distance += profile.DistanceMeters[from][to];
                duration += profile.DurationSeconds[from][to];
                if (!routing.IsEnd(next))
                    assigned.Add(demands[manager.IndexToNode(next) - 1]);
                index = next;
            }
            routes.Add(new SolvedVehicleRoute(input.Vehicles[vehicleIndex], assigned, distance, duration));
        }

        logger.LogInformation(
            "Route solver completed Status={Status} ElapsedMs={ElapsedMs} VehiclesUsed={VehiclesUsed} Unassigned={Unassigned}",
            "SOLUTION", (DateTime.UtcNow - started).TotalMilliseconds, routes.Count, dropped.Count);
        return Result<RoutePlanningSolution>.Success(new RoutePlanningSolution(routes, dropped));
    }

    private static long ToGrams(decimal kilograms) => checked((long)Math.Round(
        kilograms * 1000m, MidpointRounding.AwayFromZero));

    private static RoutePlanUnassigned Unassigned(RestaurantDemand demand, string reason) =>
        new(demand.RestaurantId, demand.RestaurantName, demand.OrderIds, demand.LoadKg, reason);
}
