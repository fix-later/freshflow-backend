using FluentAssertions;
using FreshFlow.Analytics.Application.Dtos;
using FreshFlow.Analytics.Application.Queries.ExportAnalytics;
using FreshFlow.Analytics.Application.Queries.GetDashboardOverview;
using FreshFlow.Analytics.Application.Queries.GetDeliveryPerformance;
using FreshFlow.Analytics.Application.Queries.GetDemandHeatmap;
using FreshFlow.Analytics.Application.Queries.GetDemandTimeDistribution;
using FreshFlow.Analytics.Application.Queries.GetHubThroughput;
using FreshFlow.Analytics.Application.Queries.GetOrderMetrics;
using FreshFlow.Analytics.Application.Queries.GetPriceTrends;
using FreshFlow.Analytics.Application.Queries.GetProcurementMetrics;
using FreshFlow.Analytics.Infrastructure;
using FreshFlow.Contracts;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Infrastructure.Persistence.Audit;
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
        services.AddLogging();
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

        scope.ServiceProvider
            .GetRequiredService<IRequestHandler<
                GetDemandHeatmapQuery,
                Result<IReadOnlyList<DemandHeatmapPointDto>>>>()
            .Should()
            .NotBeNull();

        scope.ServiceProvider
            .GetRequiredService<IRequestHandler<
                GetDemandTimeDistributionQuery,
                Result<IReadOnlyList<TimeDistributionCellDto>>>>()
            .Should()
            .NotBeNull();

        scope.ServiceProvider
            .GetRequiredService<IRequestHandler<
                ExportAnalyticsQuery,
                Result<CsvExportDto>>>()
            .Should()
            .NotBeNull();
    }

    [Fact]
    public void AddPersistence_ResolvesAuditQueryAndAllRecentActivityHandlers()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=freshflow_test;Username=test;Password=test",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider
            .GetRequiredService<IRequestHandler<
                GetAuditLogsQuery,
                Result<AuditLogPageDto>>>()
            .Should()
            .NotBeNull();
        scope.ServiceProvider
            .GetRequiredService<INotificationHandler<OrderConfirmedIntegrationEvent>>()
            .Should()
            .NotBeNull();
        scope.ServiceProvider
            .GetRequiredService<INotificationHandler<ProcurementBatchBuiltIntegrationEvent>>()
            .Should()
            .NotBeNull();
        scope.ServiceProvider
            .GetRequiredService<INotificationHandler<ProcurementManifestGeneratedIntegrationEvent>>()
            .Should()
            .NotBeNull();
        scope.ServiceProvider
            .GetRequiredService<INotificationHandler<ProcurementAgentAssignedIntegrationEvent>>()
            .Should()
            .NotBeNull();
        scope.ServiceProvider
            .GetRequiredService<INotificationHandler<ProcurementBatchHandedOffIntegrationEvent>>()
            .Should()
            .NotBeNull();
        scope.ServiceProvider
            .GetRequiredService<INotificationHandler<DeliveryStartedIntegrationEvent>>()
            .Should()
            .NotBeNull();
        scope.ServiceProvider
            .GetRequiredService<INotificationHandler<DeliveryCompletedIntegrationEvent>>()
            .Should()
            .NotBeNull();
        scope.ServiceProvider
            .GetRequiredService<INotificationHandler<HubDiscrepancyRecordedIntegrationEvent>>()
            .Should()
            .NotBeNull();
    }
}

