using System.Reflection;
using System.Text;
using System.Text.Json;
using FluentValidation;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Behaviors;
using FreshFlow.Auth.Infrastructure.CrossModule;
using FreshFlow.Auth.Infrastructure.Repositories;
using FreshFlow.Auth.Infrastructure.Seed;
using FreshFlow.Auth.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FreshFlow.Auth.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAuthModule(
        this IServiceCollection services,
        IConfiguration config)
    {
        // MediatR — scan Application assembly for handlers and behaviours
        var applicationAssembly = Assembly.Load("FreshFlow.Auth.Application");
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(applicationAssembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // FluentValidation — auto-register all validators from Application
        services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);

        // JWT configuration + validation
        services.AddOptions<JwtSettings>()
            .Bind(config.GetSection("JWT"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // JWT Bearer authentication — options configured lazily from IOptions<JwtSettings>
        // so WebApplicationFactory's ConfigureAppConfiguration values are visible at resolve time.
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((bearerOptions, settingsOptions) =>
            {
                var s = settingsOptions.Value;
                bearerOptions.MapInboundClaims = false;
                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = s.Issuer,
                    ValidateAudience = true,
                    ValidAudience = s.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(s.Key)),
                    ClockSkew = TimeSpan.Zero,
                    // Tokens carry the short "role" claim; tell ASP.NET Core to use it for
                    // IsInRole() checks and [Authorize(Roles = ...)] attributes.
                    RoleClaimType = "role",
                    NameClaimType = "sub"
                };

                // SignalR: read token from query string.
                // OnChallenge: return JSON { code, message } instead of the default
                // WWW-Authenticate plain-text response (FR-AUTH-008 / UC-AUTH-08).
                bearerOptions.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var accessToken = ctx.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken))
                            ctx.Token = accessToken;
                        return Task.CompletedTask;
                    },

                    OnChallenge = async ctx =>
                    {
                        // Suppress the default WWW-Authenticate challenge response.
                        ctx.HandleResponse();

                        var isExpired = ctx.AuthenticateFailure is SecurityTokenExpiredException;
                        var code = isExpired ? "TOKEN_EXPIRED" : "UNAUTHORIZED";
                        var message = isExpired
                            ? "The access token has expired."
                            : "Authentication is required. Provide a valid Bearer token.";

                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsync(
                            JsonSerializer.Serialize(new { code, message }),
                            ctx.HttpContext.RequestAborted);
                    },

                    OnForbidden = async ctx =>
                    {
                        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                        ctx.Response.ContentType = "application/json";
                        await ctx.Response.WriteAsync(
                            JsonSerializer.Serialize(new
                            {
                                code = "FORBIDDEN",
                                message = "You do not have permission to access this resource."
                            }),
                            ctx.HttpContext.RequestAborted);
                    }
                };
            });

        services.AddAuthorizationBuilder();

        // Application services
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IPasswordResetSender, NoOpPasswordResetSender>();

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IVerificationCodeRepository, VerificationCodeRepository>();
        services.AddScoped<IVerificationSender, NoOpVerificationSender>();

        services.AddScoped<IUserMarketAssignmentRepository, UserMarketAssignmentRepository>();

        // Cross-module services
        services.AddScoped<IRestaurantRepository, RestaurantRepository>();
        services.AddScoped<IDriverProfileCreator, DriverProfileCreator>();
        services.AddScoped<IMarketValidator, MarketValidator>();

        // Admin seed (idempotent hosted service)
        services.AddHostedService<AdminSeeder>();

        return services;
    }
}
