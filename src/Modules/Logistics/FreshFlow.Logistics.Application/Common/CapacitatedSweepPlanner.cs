namespace FreshFlow.Logistics.Application.Common;

public static class CapacitatedSweepPlanner
{
    public static SweepPlan Plan(
        decimal hubLatitude,
        decimal hubLongitude,
        IReadOnlyList<SweepRestaurant> restaurants,
        IReadOnlyList<SweepVehicleCapacity> vehicles)
    {
        var fleet = vehicles.OrderByDescending(vehicle => vehicle.CapacityKg).ToList();
        var maxCapacity = fleet.Count == 0 ? 0m : fleet[0].CapacityKg;
        var ordered = restaurants
            .OrderBy(restaurant => Math.Atan2(
                (double)(restaurant.Latitude - hubLatitude),
                (double)(restaurant.Longitude - hubLongitude)))
            .ThenBy(restaurant => restaurant.RestaurantId)
            .ToList();
        var unassignable = ordered
            .Where(restaurant => restaurant.LoadKg > maxCapacity)
            .ToList();
        ordered.RemoveAll(restaurant => restaurant.LoadKg > maxCapacity);

        var clusters = new List<SweepCluster>();
        var nextRestaurant = 0;

        foreach (var vehicle in fleet)
        {
            if (nextRestaurant == ordered.Count)
                break;

            var cluster = new List<SweepRestaurant>();
            var loadKg = 0m;
            while (nextRestaurant < ordered.Count)
            {
                var restaurant = ordered[nextRestaurant];
                if (cluster.Count >= vehicle.MaxStops - 1 ||
                    loadKg + restaurant.LoadKg > vehicle.CapacityKg)
                {
                    break;
                }

                cluster.Add(restaurant);
                loadKg += restaurant.LoadKg;
                nextRestaurant++;
            }

            if (cluster.Count > 0)
                clusters.Add(new SweepCluster(vehicle.CapacityKg, vehicle.MaxStops, cluster));
        }

        unassignable.AddRange(ordered.Skip(nextRestaurant));
        return new SweepPlan(clusters, unassignable);
    }
}

public sealed record SweepRestaurant(
    Guid RestaurantId,
    decimal Latitude,
    decimal Longitude,
    decimal LoadKg);

public sealed record SweepVehicleCapacity(decimal CapacityKg, int MaxStops);

public sealed record SweepCluster(
    decimal CapacityKg,
    int MaxStops,
    IReadOnlyList<SweepRestaurant> Restaurants);

public sealed record SweepPlan(
    IReadOnlyList<SweepCluster> Clusters,
    IReadOnlyList<SweepRestaurant> Unassignable);
