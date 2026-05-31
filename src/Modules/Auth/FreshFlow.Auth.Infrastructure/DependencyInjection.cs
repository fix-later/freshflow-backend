using System.Reflection;
using System.Text;
using FluentValidation;
using FreshFlow.Auth.Application.Abstractions;
using FreshFlow.Auth.Application.Behaviors;
using FreshFlow.Auth.Infrastructure.CrossModule;
using FreshFlow.Auth.Infrastructure.Repositories;
using FreshFlow.Auth.Infrastructure.Seed;
using FreshFlow.Auth.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        // JWT Bearer authentication
        var jwtKey = config["JWT__Key"]
                     ?? config.GetSection("JWT")["Key"]
                     ?? throw new InvalidOperationException("JWT:Key configuration is required.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = config["JWT__Issuer"] ?? config.GetSection("JWT")["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = config["JWT__Audience"] ?? config.GetSection("JWT")["Audience"],
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ClockSkew = TimeSpan.Zero,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role
                };

                // SignalR: read token from query string
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var accessToken = ctx.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken))
                            ctx.Token = accessToken;
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorizationBuilder();

        // Application services
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // Cross-module services
        services.AddScoped<IRestaurantRepository, RestaurantRepository>();
        services.AddScoped<IDriverProfileCreator, DriverProfileCreator>();
        services.AddScoped<IMarketValidator, MarketValidator>();

        // Admin seed (idempotent hosted service)
        services.AddHostedService<AdminSeeder>();

        return services;
    }
}
