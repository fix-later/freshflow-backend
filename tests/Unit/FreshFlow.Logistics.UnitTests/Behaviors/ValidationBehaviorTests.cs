using FluentAssertions;
using FluentValidation;
using FreshFlow.Logistics.Application.Behaviors;
using MediatR;

namespace FreshFlow.Logistics.UnitTests.Behaviors;

[Trait("Category", "Unit")]
public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_NoValidators_CallsNextAsync()
    {
        var sut = new ValidationBehavior<TestRequest, int>([]);

        var result = await sut.Handle(new TestRequest("ok"), _ => Task.FromResult(42), default);

        result.Should().Be(42);
    }

    [Fact]
    public async Task Handle_ValidRequestWithValidator_CallsNextAsync()
    {
        var validator = new InlineValidator<TestRequest>();
        validator.RuleFor(r => r.Value).NotEmpty();
        var sut = new ValidationBehavior<TestRequest, int>([validator]);

        var result = await sut.Handle(new TestRequest("ok"), _ => Task.FromResult(42), default);

        result.Should().Be(42);
    }

    [Fact]
    public async Task Handle_InvalidRequest_ThrowsValidationExceptionAsync()
    {
        var validator = new InlineValidator<TestRequest>();
        validator.RuleFor(r => r.Value).NotEmpty();
        var sut = new ValidationBehavior<TestRequest, int>([validator]);

        var act = () => sut.Handle(new TestRequest(string.Empty), _ => Task.FromResult(42), default);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.Any(e => e.PropertyName == nameof(TestRequest.Value)));
    }

    private sealed record TestRequest(string Value) : IRequest<int>;
}
