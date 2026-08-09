using System.Net;
using System.Text;
using FluentAssertions;
using FreshFlow.Orders.Application.Abstractions;
using FreshFlow.Orders.Infrastructure.Goong;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FreshFlow.Orders.UnitTests.Infrastructure;

[Trait("Category", "Unit")]
public sealed class GoongRoadDistanceProviderTests : IDisposable
{
    private readonly List<HttpClient> _clients = [];

    [Fact]
    public async Task GetDistanceAsync_Direction_ParsesMetersAndSeconds()
    {
        var provider = CreateProvider((request, _) =>
        {
            request.RequestUri!.Query.Should().Contain("origin=10.1,106.2");
            return Json("""
                {"routes":[{"legs":[{"distance":{"value":12345},"duration":{"value":987}}]}]}
                """);
        });
        var origin = new GeoCoordinate(10.1m, 106.2m);

        var result = await provider.GetDistanceAsync(
            [origin], new GeoCoordinate(10.2m, 106.3m), default);

        result.Should().Be(new RoadDistanceResult(12_345, 987, origin, false, "GOONG"));
    }

    [Fact]
    public async Task GetDistanceAsync_Matrix_PicksFarthestOrigin()
    {
        var provider = CreateProvider((request, _) =>
        {
            Uri.UnescapeDataString(request.RequestUri!.Query).Should().Contain("10,106|11,107");
            return Json("""
                {"rows":[
                  {"elements":[{"status":"OK","distance":{"value":1000},"duration":{"value":100}}]},
                  {"elements":[{"status":"OK","distance":{"value":3000},"duration":{"value":250}}]}
                ]}
                """);
        });
        var farthest = new GeoCoordinate(11m, 107m);

        var result = await provider.GetDistanceAsync(
            [new GeoCoordinate(10m, 106m), farthest],
            new GeoCoordinate(12m, 108m),
            default);

        result.Should().Be(new RoadDistanceResult(3_000, 250, farthest, false, "GOONG"));
    }

    [Theory]
    [InlineData("http")]
    [InlineData("empty")]
    [InlineData("non_ok")]
    [InlineData("timeout")]
    public async Task GetDistanceAsync_GoongFailure_ReturnsHaversineFallback(string failure)
    {
        var provider = CreateProvider((_, _) => failure switch
        {
            "http" => new HttpResponseMessage(HttpStatusCode.BadGateway),
            "empty" => Json("{}"),
            "non_ok" => Json("{\"rows\":[]}"),
            _ => throw new TaskCanceledException()
        });
        var origin = new GeoCoordinate(10m, 106m);

        var result = await provider.GetDistanceAsync(
            [origin], new GeoCoordinate(10.01m, 106m), default);

        result.IsEstimated.Should().BeTrue();
        result.Provider.Should().Be("HAVERSINE_FALLBACK");
        result.ChosenOrigin.Should().Be(origin);
        result.DistanceMeters.Should().BeInRange(1_550, 1_565);
        result.DurationSeconds.Should().BePositive();
    }

    [Theory]
    [InlineData(91, 106)]
    [InlineData(10, 181)]
    public async Task GetDistanceAsync_InvalidCoordinates_RejectsBeforeHttp(decimal latitude, decimal longitude)
    {
        var provider = CreateProvider((_, _) => throw new InvalidOperationException("HTTP must not be called"));

        var act = () => provider.GetDistanceAsync(
            [new GeoCoordinate(latitude, longitude)], new GeoCoordinate(10m, 106m), default);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("{\"rows\":null}")]
    [InlineData("{\"rows\":[{\"elements\":[{\"status\":123}]}]}")]
    public async Task GetDistanceAsync_MalformedMatrix_ReturnsFallback(string response)
    {
        var provider = CreateProvider((_, _) => Json(response));

        var result = await provider.GetDistanceAsync(
            [new GeoCoordinate(10m, 106m), new GeoCoordinate(11m, 107m)],
            new GeoCoordinate(12m, 108m),
            default);

        result.IsEstimated.Should().BeTrue();
        result.Provider.Should().Be("HAVERSINE_FALLBACK");
    }

    public void Dispose() => _clients.ForEach(client => client.Dispose());

    private GoongRoadDistanceProvider CreateProvider(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> response)
    {
        var client = new HttpClient(new FakeHandler(response))
        {
            BaseAddress = new Uri("https://rsapi.goong.io/")
        };
        _clients.Add(client);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(GoongRoadDistanceProvider.HttpClientName).Returns(client);
        return new GoongRoadDistanceProvider(factory, Options.Create(new GoongOptions
        {
            ApiKey = "test-key",
            FallbackRoadFactor = 1.4
        }), NullLogger<GoongRoadDistanceProvider>.Instance);
    }

    private static HttpResponseMessage Json(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private sealed class FakeHandler(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response(request, cancellationToken));
    }
}
