using CloudinaryDotNet;
using FluentAssertions;
using FreshFlow.Infrastructure.Media;
using FreshFlow.SharedKernel.Application;
using Microsoft.Extensions.Options;

namespace FreshFlow.Infrastructure.Media.UnitTests;

[Trait("Category", "Unit")]
public sealed class CloudinarySignatureServiceTests
{
    private static readonly CloudinaryOptions Settings = new()
    {
        CloudName = "demo-cloud",
        ApiKey = "123456789012345",
        ApiSecret = "s3cr3t-api-key-value"
    };

    [Fact]
    public void Sign_SameInputsAndTimestamp_ProducesDeterministicSignature()
    {
        var sut = CreateSut(new FixedTimeProvider(new DateTimeOffset(2026, 7, 10, 8, 0, 0, TimeSpan.Zero)));

        var first = sut.Sign(new CloudinarySignatureRequest("avatars"));
        var second = sut.Sign(new CloudinarySignatureRequest("avatars"));

        first.Signature.Should().Be(second.Signature);
        first.Signature.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Sign_MatchesCloudinarySdkComputationForSameParameters()
    {
        var timestamp = new DateTimeOffset(2026, 7, 10, 8, 0, 0, TimeSpan.Zero);
        var sut = CreateSut(new FixedTimeProvider(timestamp));

        var result = sut.Sign(new CloudinarySignatureRequest("avatars"));

        var cloudinary = new Cloudinary(new Account(Settings.CloudName, Settings.ApiKey, Settings.ApiSecret));
        var expectedSignature = cloudinary.Api.SignParameters(new Dictionary<string, object>
        {
            ["timestamp"] = timestamp.ToUnixTimeSeconds(),
            ["folder"] = "avatars"
        });

        result.Signature.Should().Be(expectedSignature);
    }

    [Fact]
    public void Sign_DifferentFolder_ProducesDifferentSignature()
    {
        var sut = CreateSut(new FixedTimeProvider(new DateTimeOffset(2026, 7, 10, 8, 0, 0, TimeSpan.Zero)));

        var avatars = sut.Sign(new CloudinarySignatureRequest("avatars"));
        var licenses = sut.Sign(new CloudinarySignatureRequest("licenses"));

        avatars.Signature.Should().NotBe(licenses.Signature);
    }

    [Fact]
    public void Sign_ExtraParameters_ChangeSignature()
    {
        var sut = CreateSut(new FixedTimeProvider(new DateTimeOffset(2026, 7, 10, 8, 0, 0, TimeSpan.Zero)));

        var withoutExtra = sut.Sign(new CloudinarySignatureRequest("avatars"));
        var withExtra = sut.Sign(new CloudinarySignatureRequest(
            "avatars",
            new Dictionary<string, object> { ["public_id"] = "user-123" }));

        withoutExtra.Signature.Should().NotBe(withExtra.Signature);
    }

    [Fact]
    public void Sign_ReturnsTimestampFromTimeProvider()
    {
        var timestamp = new DateTimeOffset(2026, 7, 10, 8, 0, 0, TimeSpan.Zero);
        var sut = CreateSut(new FixedTimeProvider(timestamp));

        var result = sut.Sign(new CloudinarySignatureRequest("avatars"));

        result.Timestamp.Should().Be(timestamp.ToUnixTimeSeconds());
        result.CloudName.Should().Be(Settings.CloudName);
        result.ApiKey.Should().Be(Settings.ApiKey);
        result.Folder.Should().Be("avatars");
    }

    [Fact]
    public void Sign_Result_DoesNotExposeApiSecret()
    {
        var sut = CreateSut(new FixedTimeProvider(new DateTimeOffset(2026, 7, 10, 8, 0, 0, TimeSpan.Zero)));

        var result = sut.Sign(new CloudinarySignatureRequest("avatars"));

        typeof(CloudinarySignatureResult).GetProperties()
            .Should().NotContain(property => property.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
        result.ToString().Should().NotContain(Settings.ApiSecret);
    }

    private static CloudinarySignatureService CreateSut(TimeProvider timeProvider) =>
        new(Options.Create(Settings), timeProvider);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
