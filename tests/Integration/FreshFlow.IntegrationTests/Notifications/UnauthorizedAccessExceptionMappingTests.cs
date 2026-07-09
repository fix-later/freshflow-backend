using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using FreshFlow.IntegrationTests.Infrastructure;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FreshFlow.IntegrationTests.Notifications;

/// <summary>
/// Global exception handler contract: UnauthorizedAccessException (thrown by controller
/// ResolveUserId() when the JWT "sub" claim is missing/malformed) must map to 401,
/// not fall through to the generic 500 branch.
/// </summary>
[Trait("Category", "Integration")]
public sealed class UnauthorizedAccessExceptionMappingTests(AuthWebAppFactory factory)
    : IClassFixture<AuthWebAppFactory>
{
    private const string TestJwtKey = "integration-test-secret-key-min-32-chars!!";
    private const string TestIssuer = "https://test.freshflow";
    private const string TestAudience = "freshflow-api";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ProtectedEndpoint_WithMalformedSubClaim_Returns401WithUnauthorizedCode()
    {
        // Arrange — structurally/cryptographically valid JWT, but "sub" is not a parseable Guid,
        // so the controller's ResolveUserId() throws UnauthorizedAccessException.
        var token = CreateTokenWithMalformedSubClaim();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/notifications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var env = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        env.Should().NotBeNull();
        env!.Success.Should().BeFalse();
        env.Error!.Code.Should().Be("UNAUTHORIZED");
        env.Error.Message.Should().NotBeNullOrWhiteSpace();
    }

    private static string CreateTokenWithMalformedSubClaim()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtKey));
        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, "not-a-valid-guid"),
                new Claim(ClaimTypes.Role, "restaurant_owner")
            ]),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = TestIssuer,
            Audience = TestAudience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        });
    }
}

// Shared envelope types are in Infrastructure/ApiEnvelopes.cs
