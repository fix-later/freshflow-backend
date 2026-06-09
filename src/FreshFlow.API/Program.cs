using FluentValidation;
using FreshFlow.Auth.Infrastructure;
using FreshFlow.Catalog.Infrastructure;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Controllers ───────────────────────────────────────────────
builder.Services.AddControllers();

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
                code = "VALIDATION_ERROR",
                errors = ve.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage })
            });
            return;
        }
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new { code = "INTERNAL_ERROR", message = "An unexpected error occurred." });
    });
});

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

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Expose for WebApplicationFactory in integration tests
public partial class Program;
