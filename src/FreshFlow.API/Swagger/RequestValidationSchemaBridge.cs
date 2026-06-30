using System.Reflection;
using FluentValidation;
using FluentValidation.Results;

namespace FreshFlow.API.Swagger;

/// <summary>
/// Bridges API-layer <c>*Request</c> DTOs to the FluentValidation rules that live on the
/// corresponding Application-layer <c>*Command</c> / <c>*Query</c>.
///
/// Most controllers bind a <c>*Request</c> body and then construct the matching MediatR
/// <c>*Command</c> in code, so the validator targets the Command — a different CLR type from
/// the one Swashbuckle generates a schema for. MicroElements only attaches constraints when it
/// finds an <see cref="IValidator{T}"/> for the exact schema type, so those Request schemas come
/// out without <c>minLength</c>/<c>maxLength</c>/<c>pattern</c>/<c>required</c>.
///
/// This registers, for each <c>XxxRequest</c>, an <see cref="IValidator{TRequest}"/> adapter that
/// re-exposes the rule descriptor of <c>XxxCommand</c>'s validator. MicroElements then reflects the
/// Command's rules onto the Request schema — no rule duplication, validators stay on the Commands.
/// The adapter is used only by Swagger schema generation; nothing sends a <c>*Request</c> through
/// the MediatR pipeline, so it never runs as an actual validator.
/// </summary>
public static class RequestValidationSchemaBridge
{
    private const string RequestSuffix = "Request";
    private const string CommandSuffix = "Command";
    private const string QuerySuffix = "Query";

    /// <summary>
    /// Request DTOs whose name prefix does not match their Command's prefix.
    /// Keyed by Request type name → target Command type name.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> NameOverrides = new Dictionary<string, string>
    {
        ["ActivateRequest"] = "ActivateUserCommand",
        ["RefreshRequest"] = "RefreshTokenCommand",
        ["VerifyRequest"] = "VerifyEmailCommand",
        ["RecordActualQuantityRequest"] = "RecordOrderItemActualQuantityCommand",
        ["SetCreditLimitRequest"] = "SetRestaurantCreditLimitCommand",
        ["SettleCreditRequest"] = "SettleRestaurantCreditCommand",
        ["DeliveryAddressRequest"] = "AddDeliveryAddressCommand",
    };

    /// <summary>
    /// Registers Request→Command validator adapters. Call AFTER every module has registered its
    /// validators (so the Command/Query validators are already in the service collection).
    /// </summary>
    public static IServiceCollection AddRequestValidationSchemaBridge(this IServiceCollection services)
    {
        // Types that already have a FluentValidation validator registered (Commands, Queries,
        // and any Request that is validated directly, e.g. AssistantChatRequest).
        var validatedTypes = services
            .Where(d => d.ServiceType.IsGenericType
                        && d.ServiceType.GetGenericTypeDefinition() == typeof(IValidator<>))
            .Select(d => d.ServiceType.GetGenericArguments()[0])
            .Distinct()
            .ToList();

        var validatedByName = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var type in validatedTypes)
            validatedByName[type.Name] = type;

        var alreadyValidatedRequestNames = validatedTypes
            .Where(t => t.Name.EndsWith(RequestSuffix, StringComparison.Ordinal))
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        var requestTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && t.Name.EndsWith(RequestSuffix, StringComparison.Ordinal));

        foreach (var requestType in requestTypes)
        {
            // Skip Request DTOs that are validated directly (their schema already gets constraints).
            if (alreadyValidatedRequestNames.Contains(requestType.Name))
                continue;

            if (!TryResolveTarget(requestType.Name, validatedByName, out var commandType))
                continue;

            var requestValidatorService = typeof(IValidator<>).MakeGenericType(requestType);
            var commandValidatorService = typeof(IValidator<>).MakeGenericType(commandType);
            var adapterType = typeof(RequestRuleAdapter<>).MakeGenericType(requestType);

            services.AddScoped(requestValidatorService, sp =>
            {
                var inner = (IValidator)sp.GetRequiredService(commandValidatorService);
                return Activator.CreateInstance(adapterType, inner)!;
            });
        }

        return services;
    }

    private static bool TryResolveTarget(
        string requestName,
        IReadOnlyDictionary<string, Type> validatedByName,
        out Type commandType)
    {
        if (NameOverrides.TryGetValue(requestName, out var overrideName)
            && validatedByName.TryGetValue(overrideName, out commandType!))
            return true;

        var prefix = requestName[..^RequestSuffix.Length];
        return validatedByName.TryGetValue(prefix + CommandSuffix, out commandType!)
               || validatedByName.TryGetValue(prefix + QuerySuffix, out commandType!);
    }
}

/// <summary>
/// An <see cref="IValidator{TRequest}"/> that re-exposes another validator's rule descriptor.
/// Only <see cref="CreateDescriptor"/> / <see cref="CanValidateInstancesOfType"/> are consumed
/// (by MicroElements during Swagger schema generation); the validation members are inert.
/// </summary>
internal sealed class RequestRuleAdapter<TRequest>(IValidator inner) : IValidator<TRequest>
{
    public IValidatorDescriptor CreateDescriptor() => inner.CreateDescriptor();

    public bool CanValidateInstancesOfType(Type type) => type == typeof(TRequest);

    public ValidationResult Validate(IValidationContext context) => new();

    public Task<ValidationResult> ValidateAsync(IValidationContext context, CancellationToken cancellation = default)
        => Task.FromResult(new ValidationResult());

    public ValidationResult Validate(TRequest instance) => new();

    public Task<ValidationResult> ValidateAsync(TRequest instance, CancellationToken cancellation = default)
        => Task.FromResult(new ValidationResult());
}
