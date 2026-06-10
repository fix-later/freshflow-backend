using FreshFlow.SharedKernel.Application;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Extensions;

public static class ErrorExtensions
{
    public static IActionResult ToActionResult(this Error error)
    {
        var body = Envelope(error.Code, error.Message);

        if (error.Code.EndsWith("_NOT_FOUND"))
            return new NotFoundObjectResult(body);

        if (error.Code is "EMAIL_ALREADY_EXISTS" or "PHONE_ALREADY_EXISTS"
                        or "REFRESH_TOKEN_REUSE" or "ALREADY_APPROVED"
                        or "CATEGORY_NAME_CONFLICT" or "UNIT_NAME_CONFLICT")
            return new ConflictObjectResult(body);

        if (error.Code is "UNAUTHORIZED" or "INVALID_CREDENTIALS" or "INVALID_CURRENT_PASSWORD"
            or "TOKEN_INVALID" or "REFRESH_TOKEN_EXPIRED" or "REFRESH_TOKEN_REVOKED")
            return new UnauthorizedObjectResult(body);

        if (error.Code is "FORBIDDEN")
            return new ObjectResult(body) { StatusCode = 403 };

        if (error.Code is "VALIDATION_ERROR" or "INVALID_ROLE" or "WEAK_PASSWORD")
            return new BadRequestObjectResult(body);

        if (error.Code is "ACCOUNT_LOCKED")
            return new ObjectResult(body) { StatusCode = 423 };

        if (error.Code is "RESET_TOKEN_INVALID" or "RESET_TOKEN_EXPIRED" or "OTP_INVALID")
            return new BadRequestObjectResult(body);

        if (error.Code is "CHANNEL_NOT_SUPPORTED" or "CANNOT_DEACTIVATE_SELF" or "INVALID_MARKET"
                        or "INVALID_ASSIGNMENT_TARGET" or "INVALID_UNIT" or "INVALID_CATEGORY"
            || error.Code.StartsWith("ACCOUNT_"))
            return new UnprocessableEntityObjectResult(body);

        if (error.Code is "ROLE_NOT_CONFIGURED")
            return new ObjectResult(body) { StatusCode = 500 };

        return new ObjectResult(body) { StatusCode = 500 };
    }

    private static object Envelope(string code, string message) =>
        new { success = false, error = new { code, message } };
}
