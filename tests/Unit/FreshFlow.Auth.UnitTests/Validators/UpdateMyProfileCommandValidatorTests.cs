using FluentAssertions;
using FreshFlow.Auth.Application.Commands.UpdateMyProfile;

namespace FreshFlow.Auth.UnitTests.Validators;

[Trait("Category", "Unit")]
public sealed class UpdateMyProfileCommandValidatorTests
{
    private readonly UpdateMyProfileCommandValidator _sut = new();
    private static readonly Guid AnyUserId = Guid.NewGuid();

    private static UpdateMyProfileCommand Valid(
        string? fullName = "Nguyen Van A",
        string? phone = null,
        string? avatarUrl = null) =>
        new(AnyUserId, fullName, phone, avatarUrl);

    [Fact]
    public async Task Validate_ValidFullCommand_Passes()
    {
        var result = await _sut.ValidateAsync(
            new UpdateMyProfileCommand(
                AnyUserId,
                "Nguyen Van A",
                "+84901234567",
                "https://cdn.example.com/avatar.jpg"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_AllNulls_Passes()
    {
        var result = await _sut.ValidateAsync(new UpdateMyProfileCommand(AnyUserId, null, null, null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_FullNameTooLong_Fails()
    {
        var result = await _sut.ValidateAsync(Valid(fullName: new string('A', 256)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateMyProfileCommand.FullName));
    }

    [Fact]
    public async Task Validate_FullNameAt255Chars_Passes()
    {
        var result = await _sut.ValidateAsync(Valid(fullName: new string('A', 255)));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("not-a-phone")]
    public async Task Validate_InvalidPhone_Fails(string phone)
    {
        var result = await _sut.ValidateAsync(Valid(phone: phone));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateMyProfileCommand.Phone));
    }

    [Fact]
    public async Task Validate_PhoneTooLong_Fails()
    {
        var result = await _sut.ValidateAsync(Valid(phone: new string('1', 21)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateMyProfileCommand.Phone));
    }

    [Fact]
    public async Task Validate_ValidPhone_Passes()
    {
        var result = await _sut.ValidateAsync(Valid(phone: "+84901234567"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_AvatarUrlTooLong_Fails()
    {
        var longUrl = "https://example.com/" + new string('a', 500);
        var result = await _sut.ValidateAsync(Valid(avatarUrl: longUrl));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateMyProfileCommand.AvatarUrl));
    }

    [Fact]
    public async Task Validate_InvalidAvatarUrl_Fails()
    {
        var result = await _sut.ValidateAsync(Valid(avatarUrl: "not-a-url"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateMyProfileCommand.AvatarUrl));
    }

    [Theory]
    [InlineData("https://example.com/avatar.png")]
    [InlineData("http://cdn.test.vn/img/photo.jpg")]
    public async Task Validate_ValidAvatarUrl_Passes(string url)
    {
        var result = await _sut.ValidateAsync(Valid(avatarUrl: url));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    public async Task Validate_NonHttpAvatarUrl_Fails(string url)
    {
        var result = await _sut.ValidateAsync(Valid(avatarUrl: url));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(UpdateMyProfileCommand.AvatarUrl));
    }
}
