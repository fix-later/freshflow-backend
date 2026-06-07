using FreshFlow.SharedKernel.Application;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Extensions;

public static class ErrorExtensions
{
    public static IActionResult ToActionResult(this Error error)
    {
        if (error.Code.EndsWith("_NOT_FOUND"))
            return new NotFoundObjectResult(new { code = error.Code, message = error.Message });

        if (error.Code is "EMAIL_ALREADY_EXISTS" or "PHONE_ALREADY_EXISTS"
                        or "REFRESH_TOKEN_REUSE" or "ALREADY_APPROVED")
            return new ConflictObjectResult(new { code = error.Code, message = error.Message });

        if (error.Code is "UNAUTHORIZED" or "INVALID_CREDENTIALS" or "INVALID_CURRENT_PASSWORD"
            or "TOKEN_INVALID" or "TOKEN_REUSE_DETECTED"
            or "REFRESH_TOKEN_EXPIRED" or "REFRESH_TOKEN_REVOKED")
            return new UnauthorizedObjectResult(new { code = error.Code, message = error.Message });

        if (error.Code is "FORBIDDEN")
            return new ObjectResult(new { code = error.Code, message = error.Message }) { StatusCode = 403 };

        if (error.Code is "VALIDATION_ERROR" or "INVALID_ROLE" or "WEAK_PASSWORD")
            return new BadRequestObjectResult(new { code = error.Code, message = error.Message });

        if (error.Code is "ACCOUNT_LOCKED")
            return new ObjectResult(new { code = error.Code, message = error.Message }) { StatusCode = 423 };

        if (error.Code is "RESET_TOKEN_INVALID" or "RESET_TOKEN_EXPIRED" or "OTP_INVALID")
            return new BadRequestObjectResult(new { code = error.Code, message = error.Message });

        if (error.Code is "CHANNEL_NOT_SUPPORTED" or "CANNOT_DEACTIVATE_SELF" or "INVALID_MARKET"
            || error.Code.StartsWith("ACCOUNT_"))
            return new UnprocessableEntityObjectResult(new { code = error.Code, message = error.Message });

        if (error.Code is "ROLE_NOT_CONFIGURED")
            return new ObjectResult(new { code = error.Code, message = error.Message }) { StatusCode = 500 };

        return new ObjectResult(new { code = error.Code, message = error.Message }) { StatusCode = 500 };
    }
}
