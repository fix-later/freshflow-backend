using System.ComponentModel.DataAnnotations;

namespace FreshFlow.Infrastructure.Media;

public sealed class CloudinaryOptions
{
    [Required]
    public string CloudName { get; set; } = string.Empty;

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    public string ApiSecret { get; set; } = string.Empty;
}
