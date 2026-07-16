using FluentAssertions;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.Analytics.Application.Queries.GetDashboardOverview;
using FreshFlow.Analytics.Application.Queries.GetDeliveryPerformance;
using FreshFlow.Analytics.Application.Queries.GetHubThroughput;
using FreshFlow.Analytics.Application.Queries.GetOrderMetrics;
using FreshFlow.Analytics.Application.Queries.GetPriceTrends;
using FreshFlow.Analytics.Application.Queries.GetProcurementMetrics;
using FreshFlow.Analytics.Infrastructure;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FreshFlow.Analytics.UnitTests.Bootstrap;

[Trait("Category", "Unit")]
public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddAnalyticsModule_ResolvesAnalyticsHandlers()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddAnalyticsModule(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider
            .GetRequiredService<IRequestHandler<
                GetDashboardOverviewQuery,
                Result<DashboardOverviewDto>>>()
            .Should()
            .NotBeNull();

        scope.ServiceProvider
            .GetRequiredService<IRequestHandler<
                GetPriceTrendsQuery,
                Result<PriceTrendsDto>>>()
            .Should()
            .NotBeNull();

        scope.ServiceProvider
            .GetRequiredService<IRequestHandler<
                GetOrderMetricsQuery,
                Result<OrderMetricsDto>>>()
            .Should()
            .NotBeNull();

        scope.ServiceProvider
            .GetRequiredService<IRequestHandler<
                GetProcurementMetricsQuery,
                Result<ProcurementMetricsDto>>>()
            .Should()
            .NotBeNull();

        scope.ServiceProvider
            .GetRequiredService<IRequestHandler<
                GetHubThroughputQuery,
                Result<HubThroughputDto>>>()
            .Should()
            .NotBeNull();

        scope.ServiceProvider
            .GetRequiredService<IRequestHandler<
                GetDeliveryPerformanceQuery,
                Result<DeliveryPerformanceDto>>>()
            .Should()
            .NotBeNull();
    }
}

