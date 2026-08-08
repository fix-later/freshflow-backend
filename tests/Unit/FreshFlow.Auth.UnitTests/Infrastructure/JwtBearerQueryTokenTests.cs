using FluentAssertions;
using FreshFlow.Auth.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FreshFlow.Auth.UnitTests.Infrastructure;

[Trait("Category", "Unit")]
public sealed class JwtBearerQueryTokenTests
{
    [Theory]
    [InlineData("/hubs/notifications", "query-token")]
    [InlineData("/api/v1/notifications", null)]
    public async Task QueryAccessToken_IsAcceptedOnlyForHubPathsAsync(
        string path,
        string? expectedToken)
    {
        var services = new ServiceCollection();
        services.AddAuthModule(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT:Key"] = "unit-test-secret-key-that-is-at-least-32-chars!!",
                ["JWT:Issuer"] = "https://test.freshflow",
                ["JWT:Audience"] = "freshflow-api",
            })
            .Build());
        using var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        httpContext.Request.QueryString = new QueryString("?access_token=query-token");
        var context = new MessageReceivedContext(
            httpContext,
            new AuthenticationScheme(
                JwtBearerDefaults.AuthenticationScheme,
                null,
                typeof(JwtBearerHandler)),
            options);

        await options.Events.MessageReceived(context);

        context.Token.Should().Be(expectedToken);
    }
}
