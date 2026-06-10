using System.Threading.RateLimiting;
using FluentValidation;
using FreshFlow.Auth.Infrastructure;
using FreshFlow.Catalog.Infrastructure;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
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
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── Database ──────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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
              .AllowAnyMethod();
    });
});

// ── Rate limiting — "auth" policy: fixed window per remote IP ───
// Limit and window are configurable via "RateLimiting:Auth:*" so integration
// tests can override to a high value and avoid accidentally hitting the cap.
var authPermitLimit = builder.Configuration.GetValue("RateLimiting:Auth:PermitLimit", defaultValue: 10);
var authWindowMinutes = builder.Configuration.GetValue("RateLimiting:Auth:WindowMinutes", defaultValue: 1);

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(authWindowMinutes),
                PermitLimit = authPermitLimit,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
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

// ── Module registrations ──────────────────────────────────────
builder.Services.AddAuthModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);
// builder.Services.AddPricingModule(builder.Configuration);
// builder.Services.AddOrdersModule(builder.Configuration);

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

// ── HTTPS redirect — no-op when no HTTPS port is configured (e.g. tests) ──
app.UseHttpsRedirection();

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

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Expose for WebApplicationFactory in integration tests
public partial class Program;
