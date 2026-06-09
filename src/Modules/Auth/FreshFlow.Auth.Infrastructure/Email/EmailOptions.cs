using System.ComponentModel.DataAnnotations;

namespace FreshFlow.Auth.Infrastructure.Email;

internal sealed class EmailOptions
{
    [Required]
    public string ResendApiKey { get; init; } = string.Empty;

    [Required]
    public string FromAddress { get; init; } = "no-reply@fishfix.vn";

    public string FromName { get; init; } = "FreshFlow";

    [Required]
    public string FrontendBaseUrl { get; init; } = string.Empty;
}
