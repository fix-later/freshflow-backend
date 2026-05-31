using FluentValidation;
using FreshFlow.Auth.Infrastructure;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Controllers ───────────────────────────────────────────────
builder.Services.AddControllers();

// ── OpenAPI (built-in ASP.NET Core 10) ───────────────────────
builder.Services.AddOpenApi();

// ── Database ──────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Module registrations ──────────────────────────────────────
builder.Services.AddAuthModule(builder.Configuration);
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
    app.MapOpenApi();
    app.MapScalarApiReference(opt => opt.WithTitle("FreshFlow API"));
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Expose for WebApplicationFactory in integration tests
public partial class Program;
