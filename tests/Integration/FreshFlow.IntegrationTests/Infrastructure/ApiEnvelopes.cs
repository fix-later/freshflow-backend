namespace FreshFlow.IntegrationTests.Infrastructure;

/// <summary>
/// Shared DTOs for deserialising the standard API response envelope
/// per docs/04 §1.3: { success, data } / { success, error }.
/// </summary>

/// <summary>Generic success envelope: { "success": true, "data": T }.</summary>
public sealed record Envelope<T>(bool Success, T? Data);

/// <summary>Error envelope: { "success": false, "error": { "code", "message" } }.</summary>
public sealed record ErrorEnvelope(bool Success, ErrorInfo? Error);

/// <summary>Error payload nested under the "error" key.</summary>
public sealed record ErrorInfo(string Code, string Message);

/// <summary>Token pair returned by login / refresh.</summary>
public sealed record TokenBody(string AccessToken, string RefreshToken, int ExpiresIn);
