namespace FreshFlow.API;

/// <summary>
/// Factory for the standard API response envelope per docs/04 §1.3:
///   success  → { "success": true,  "data": T }
///   failure  → { "success": false, "error": { "code", "message" } }
/// </summary>
public static class ApiResponse
{
    /// <summary>Wraps a payload in a success envelope.</summary>
    public static object Ok<T>(T data) => new { success = true, data };

    /// <summary>Success envelope with null data (e.g. reset-password, verify).</summary>
    public static object OkEmpty() => new { success = true, data = (object?)null };

    /// <summary>
    /// Wraps a cursor-paginated response: { success, data, meta: { pageSize, nextCursor } }.
    /// </summary>
    public static object OkPaged<T>(IReadOnlyList<T> data, int pageSize, string? nextCursor) =>
        new { success = true, data, meta = new { pageSize, nextCursor } };

    /// <summary>Error envelope for inline controller returns (not via ErrorExtensions).</summary>
    internal static object Err(string code, string message) =>
        new { success = false, error = new { code, message } };
}
