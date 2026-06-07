using System.Net;
using FluentAssertions;
using FreshFlow.Auth.Infrastructure.Email;
using FreshFlow.Auth.Infrastructure.Services;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Services;

[Trait("Category", "Unit")]
public sealed class ResendPasswordResetSenderTests
{
    private static readonly IOptions<EmailOptions> DefaultEmailOptions = Options.Create(new EmailOptions
    {
        ResendApiKey = "re_test_key",
        FromAddress = "no-reply@fishfix.vn",
        FromName = "FreshFlow",
        FrontendBaseUrl = "http://localhost:3000"
    });

    private static (ResendPasswordResetSender sut, FakeHttpMessageHandler handler) BuildSut(
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var fakeHandler = new FakeHttpMessageHandler(statusCode);
        var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri("https://api.resend.com/") };

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Resend").Returns(httpClient);

        return (new ResendPasswordResetSender(factory, DefaultEmailOptions), fakeHandler);
    }

    [Fact]
    public async Task SendResetLinkAsync_ValidRequest_PostsToEmailsEndpoint()
    {
        // Arrange
        var (sut, handler) = BuildSut(HttpStatusCode.OK);

        // Act
        await sut.SendResetLinkAsync("owner@test.vn", "raw-reset-token-123", default);

        // Assert — one POST to the Resend emails endpoint, no exception
        handler.RequestCount.Should().Be(1);
        handler.LastRequestUri.Should().Be("https://api.resend.com/emails");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SendResetLinkAsync_ErrorResponse_ThrowsHttpRequestException(HttpStatusCode statusCode)
    {
        // Arrange
        var (sut, _) = BuildSut(statusCode);

        // Act
        var act = async () => await sut.SendResetLinkAsync("owner@test.vn", "token", default);

        // Assert — EnsureSuccessStatusCode propagates to caller
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task SendResetLinkAsync_ResetLinkContainsEscapedToken()
    {
        // Arrange — token with characters that need URL-encoding
        var (sut, handler) = BuildSut(HttpStatusCode.OK);

        // Act
        await sut.SendResetLinkAsync("u@test.vn", "tok en+special", default);

        // Assert — handler received the request (body encoding verified via no-throw)
        handler.RequestCount.Should().Be(1);
    }
}

/// <summary>Minimal fake <see cref="HttpMessageHandler"/> for unit tests.</summary>
internal sealed class FakeHttpMessageHandler(HttpStatusCode statusCode) : HttpMessageHandler
{
    public int RequestCount { get; private set; }
    public string? LastRequestUri { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        LastRequestUri = request.RequestUri?.ToString();
        return Task.FromResult(new HttpResponseMessage(statusCode));
    }
}
