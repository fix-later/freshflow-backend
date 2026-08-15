using System.Net;
using FluentAssertions;
using FreshFlow.Auth.Infrastructure.Email;
using FreshFlow.Auth.Infrastructure.Services;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FreshFlow.Auth.UnitTests.Services;

[Trait("Category", "Unit")]
public sealed class ResendVerificationSenderTests
{
    private static readonly IOptions<EmailOptions> DefaultEmailOptions = Options.Create(new EmailOptions
    {
        ResendApiKey = "re_test_key",
        FromAddress = "no-reply@fishfix.vn",
        FromName = "FreshFlow"
    });

    private static (ResendVerificationSender sut, FakeHttpMessageHandler handler) BuildSut(
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var fakeHandler = new FakeHttpMessageHandler(statusCode);
        var httpClient = new HttpClient(fakeHandler) { BaseAddress = new Uri("https://api.resend.com/") };

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Resend").Returns(httpClient);

        return (new ResendVerificationSender(factory, DefaultEmailOptions), fakeHandler);
    }

    [Fact]
    public async Task SendVerificationCodeAsync_ValidRequest_PostsToEmailsEndpoint()
    {
        // Arrange
        var (sut, handler) = BuildSut(HttpStatusCode.OK);

        // Act
        await sut.SendVerificationCodeAsync("user@test.vn", "123456", default);

        // Assert — one POST to the Resend emails endpoint, no exception
        handler.RequestCount.Should().Be(1);
        handler.LastRequestUri.Should().Be("https://api.resend.com/emails");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task SendVerificationCodeAsync_ErrorResponse_ThrowsHttpRequestException(HttpStatusCode statusCode)
    {
        // Arrange
        var (sut, _) = BuildSut(statusCode);

        // Act
        var act = async () => await sut.SendVerificationCodeAsync("user@test.vn", "123456", default);

        // Assert — EnsureSuccessStatusCode propagates to caller
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task SendVerificationCodeAsync_CodeAppearsInSingleRequest()
    {
        // Arrange
        var (sut, handler) = BuildSut(HttpStatusCode.OK);

        // Act
        await sut.SendVerificationCodeAsync("user@test.vn", "654321", default);

        // Assert — exactly one email sent per code request
        handler.RequestCount.Should().Be(1);
    }
}
