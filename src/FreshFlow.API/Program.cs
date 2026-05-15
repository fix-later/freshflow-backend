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

// ── Health Checks ─────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ── Module registrations (added per sprint) ───────────────────
// services.AddAuthModule(config)
// services.AddPricingModule(config)
// ...

var app = builder.Build();

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
