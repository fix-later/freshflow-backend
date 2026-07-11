using System.Threading.RateLimiting;
using FluentValidation;
using FreshFlow.API.Assistant;
using FreshFlow.API.Swagger;
using FreshFlow.Auth.Infrastructure;
using FreshFlow.Catalog.Infrastructure;
using FreshFlow.Hub.Infrastructure;
using FreshFlow.Infrastructure.Media;
using FreshFlow.Infrastructure.Persistence;
using FreshFlow.Logistics.Infrastructure;
using FreshFlow.Notifications.Infrastructure;
using FreshFlow.Orders.Infrastructure;
using FreshFlow.Orders.Infrastructure.Realtime;
using FreshFlow.Pricing.Infrastructure;
using FreshFlow.Pricing.Infrastructure.Realtime;
using MicroElements.Swashbuckle.FluentValidation.AspNetCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Controllers ───────────────────────────────────────────────
// SuppressAsyncSuffixInActionNames = false: keep "Async" in action names so
// CreatedAtAction(nameof(GetXxxAsync), ...) resolves correctly without stripping.
builder.Services.AddControllers(options =>
    options.SuppressAsyncSuffixInActionNames = false);

// ── Swagger / OpenAPI ─────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FreshFlow API",
        Version = "v1",
        Description = "Wholesale market food procurement & logistics platform"
    });

    var bearerScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token returned by POST /api/v1/auth/login"
    };

    options.AddSecurityDefinition("Bearer", bearerScheme);
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>()
        }
    });
});

// Reflect FluentValidation rules (MinimumLength, MaximumLength, NotEmpty, Matches, etc.)
// into the generated OpenAPI schema so the FE can read field constraints directly.
// Must run after AddSwaggerGen and after the module validators are registered in DI.
builder.Services.AddFluentValidationRulesToSwagger();

// ── Database ──────────────────────────────────────────────────
// Registers AppDbContext + DomainEventDispatchInterceptor (post-save domain event dispatch).
builder.Services.AddPersistence(builder.Configuration);

// ── CORS ─────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var origins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ── Forwarded headers — trust X-Forwarded-For / X-Forwarded-Proto from proxies ──
// Without this, Connection.RemoteIpAddress is always the proxy IP, which makes the
// rate limiter partition key meaningless and would throttle all users together (DoS).
// KnownNetworks/KnownProxies are cleared so we accept forwarded headers from any upstream;
// restrict to specific proxy CIDRs in production if the infrastructure allows it.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ── Rate limiting — "auth" policy: fixed window per remote IP ───
// Limit and window are configurable via "RateLimiting:Auth:*" so integration
// tests can override to a high value and avoid accidentally hitting the cap.
// Config is read lazily from IConfiguration at request time so WebApplicationFactory
// ConfigureAppConfiguration overrides are respected (startup-time capture misses them).
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", context =>
    {
        var cfg = context.RequestServices.GetRequiredService<IConfiguration>();
        var limit = cfg.GetValue("RateLimiting:Auth:PermitLimit", 10);
        var window = cfg.GetValue("RateLimiting:Auth:WindowMinutes", 1);
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(window),
                PermitLimit = limit,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
    // "orders" policy: fixed window per authenticated restaurant (JWT NameIdentifier claim).
    // Falls back to remote IP if unauthenticated (defence in depth — endpoints require auth).
    options.AddPolicy("orders", context =>
    {
        var cfg = context.RequestServices.GetRequiredService<IConfiguration>();
        var limit = cfg.GetValue("RateLimiting:Orders:PermitLimit", 30);
        var window = cfg.GetValue("RateLimiting:Orders:WindowMinutes", 1);
        var partitionKey = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: partitionKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(window),
                PermitLimit = limit,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    // "assistant" policy: fixed window per authenticated user (JWT NameIdentifier claim). The AI
    // assistant fans out to the LLM + several MediatR calls per request, so it gets its own, tighter
    // budget than the plain order endpoints. Falls back to remote IP if unauthenticated.
    options.AddPolicy("assistant", context =>
    {
        var cfg = context.RequestServices.GetRequiredService<IConfiguration>();
        var limit = cfg.GetValue("RateLimiting:Assistant:PermitLimit", 15);
        var window = cfg.GetValue("RateLimiting:Assistant:WindowMinutes", 1);
        var partitionKey = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: partitionKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(window),
                PermitLimit = limit,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Return the standard API error envelope so all 429 responses are machine-readable.
    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsJsonAsync(new
        {
            success = false,
            error = new
            {
                code = "TOO_MANY_REQUESTS",
                message = "Too many requests. Please slow down and try again shortly."
            }
        }, ct);
    };
});

// ── SignalR ───────────────────────────────────────────────────
// Must be registered before modules so IHubContext<PricingHub> is available
// when PricingBroadcastService is resolved.
builder.Services.AddSignalR();

// ── Module registrations ──────────────────────────────────────
builder.Services.AddAuthModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddPricingModule(builder.Configuration);
builder.Services.AddOrdersModule(builder.Configuration);
builder.Services.AddLogisticsModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);
builder.Services.AddHubModule(builder.Configuration);
builder.Services.AddMediaModule(builder.Configuration);

// ── AI Shopping Assistant (Tầng 2 — host-layer orchestration) ──
builder.Services.AddAssistant(builder.Configuration);

// Reflect Command/Query FluentValidation rules onto the matching *Request DTO schemas in OpenAPI.
// Must run after all modules have registered their validators.
builder.Services.AddRequestValidationSchemaBridge();

// ── Health Checks ─────────────────────────────────────────────
builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Global exception handler (converts ValidationException → 400) ──
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async ctx =>
    {
        var feature = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        if (feature?.Error is ValidationException ve)
        {
            ctx.Response.StatusCode = 400;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsJsonAsync(new
            {
                success = false,
                error = new
                {
                    code = "VALIDATION_ERROR",
                    message = "One or more fields failed validation.",
                    details = ve.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage })
                }
            });
            return;
        }

        if (feature?.Error is UnauthorizedAccessException)
        {
            ctx.Response.StatusCode = 401;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsJsonAsync(new
            {
                success = false,
                error = new { code = "UNAUTHORIZED", message = "Authentication is required or the provided credentials are invalid." }
            });
            return;
        }

        // Log non-validation errors with request context — do NOT expose details in response (M8-host).
        if (feature?.Error is not null)
        {
            var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(
                feature.Error,
                "Unhandled exception on {Method} {Path}: {ExceptionType}",
                ctx.Request.Method,
                ctx.Request.Path,
                feature.Error.GetType().Name);
        }

        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new
        {
            success = false,
            error = new { code = "INTERNAL_ERROR", message = "An unexpected error occurred." }
        });
    });
});

// ── HSTS — only outside Development (browsers ignore for non-prod origins) ──
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// ── API docs (Development only) ──────────────────────────────
if (app.Environment.IsDevelopment())
{
    // Swashbuckle generates spec at /swagger/v1/swagger.json
    app.UseSwagger();

    // Swagger UI at /swagger
    app.UseSwaggerUI(opt =>
    {
        opt.SwaggerEndpoint("/swagger/v1/swagger.json", "FreshFlow API v1");
        opt.RoutePrefix = "swagger";
        opt.EnablePersistAuthorization();
    });

    // Scalar reads the same Swashbuckle spec
    app.MapScalarApiReference(opt => opt
        .WithTitle("FreshFlow API")
        .WithOpenApiRoutePattern("/swagger/v1/swagger.json")
        .WithPreferredScheme("Bearer")
        .WithHttpBearerAuthentication(bearer => { bearer.Token = ""; }));
}

app.UseForwardedHeaders();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapHub<PricingHub>("/hubs/pricing");
app.MapHub<OrderHub>("/hubs/orders");

app.Run();

// Expose for WebApplicationFactory in integration tests
public partial class Program;
