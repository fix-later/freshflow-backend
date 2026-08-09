using System.Net;
using System.Text;
using FluentAssertions;
using FreshFlow.Logistics.Application.Abstractions;
using FreshFlow.Logistics.Infrastructure.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FreshFlow.Logistics.UnitTests.Routing;

[Trait("Category", "Unit")]
public sealed class GoongRouteMatrixProviderTests
{
    [Fact]
    public async Task GetMatrixAsync_MissingKey_ReturnsOneCompleteFallbackPerProfileAsync()
    {
        var factory = Substitute.For<IHttpClientFactory>();
        var provider = Create(factory, new Dictionary<string, string?>
        {
            ["Delivery:Goong:ApiKey"] = "<missing>",
            ["Delivery:Goong:FallbackRoadFactor"] = "1.4"
        });

        var result = await provider.GetMatrixAsync(
            [new RoutePoint(10m, 106m), new RoutePoint(10.01m, 106.01m)],
            ["car", "truck"], default);

        result.Provider.Should().Be("HAVERSINE_FALLBACK");
        result.IsEstimated.Should().BeTrue();
        result.Profiles.Keys.Should().BeEquivalentTo("car", "truck");
        result.Profiles["car"].DistanceMeters[0][1].Should().BePositive();
        result.Profiles["car"].DistanceMeters[0][1]
            .Should().Be(result.Profiles["truck"].DistanceMeters[0][1]);
        factory.DidNotReceiveWithAnyArgs().CreateClient(default!);
    }

    [Fact]
    public async Task GetMatrixAsync_ValidBatch_ParsesNxNAndUsesLatLngAsync()
    {
        var handler = new StubHandler(
            """
            {"rows":[
              {"elements":[{"status":"OK","distance":{"value":0},"duration":{"value":0}},{"status":"OK","distance":{"value":1234},"duration":{"value":120}}]},
              {"elements":[{"status":"OK","distance":{"value":1300},"duration":{"value":130}},{"status":"OK","distance":{"value":0},"duration":{"value":0}}]}
            ]}
            """);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(GoongRouteMatrixProvider.HttpClientName).Returns(client);
        var provider = Create(factory, new Dictionary<string, string?>
        {
            ["Delivery:Goong:ApiKey"] = "key"
        }, batchSize: 10);

        var result = await provider.GetMatrixAsync(
            [new RoutePoint(10.5m, 106.25m), new RoutePoint(11m, 107m)], ["car"], default);

        result.Provider.Should().Be("GOONG");
        result.Profiles["car"].DistanceMeters[0][1].Should().Be(1234);
        Uri.UnescapeDataString(handler.RequestUri!.Query).Should().Contain("10.5,106.25|11,107");
    }

    private static GoongRouteMatrixProvider Create(
        IHttpClientFactory factory,
        IDictionary<string, string?> values,
        int batchSize = 10)
    {
        var settings = Substitute.For<IVehicleCapacityPolicy>();
        settings.MatrixBatchSize.Returns(batchSize);
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new GoongRouteMatrixProvider(
            factory, config, settings, NullLogger<GoongRouteMatrixProvider>.Instance);
    }

    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
