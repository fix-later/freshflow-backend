namespace FreshFlow.SharedKernel.Application;

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error NotFound(string resource, object id) =>
        new($"{resource.ToUpperInvariant()}_NOT_FOUND", $"{resource} '{id}' was not found.");

    public static Error Conflict(string code, string message) => new(code, message);

    public static Error Validation(string code, string message) => new(code, message);

    public static Error Unauthorized(string code = "UNAUTHORIZED", string message = "Access denied.") =>
        new(code, message);
}
