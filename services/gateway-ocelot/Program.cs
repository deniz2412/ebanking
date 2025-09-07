using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

// Logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// CORS
var spaOrigin = builder.Configuration.GetValue<string>("Cors:SpaOrigin") ?? "https://ebank.local";
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("spa", p => p.WithOrigins(spaOrigin).AllowAnyHeader().AllowAnyMethod());
});

// Auth
var authority = builder.Configuration.GetValue<string>("Auth:Authority") ?? "https://ebank.local/auth/realms/ebanking";
var audience = builder.Configuration.GetValue<string>("Auth:Audience");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
    {
        o.Authority = authority;
        o.RequireHttpsMetadata = true;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = !string.IsNullOrWhiteSpace(audience),
            ValidAudience = audience
        };
    });

builder.Services.AddAuthorization();

// Rate limiting
builder.Services.AddRateLimiter(o =>
{
    o.AddPolicy("ip", ctx =>
        RateLimitPartition.GetIpPartition(ctx, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

// Ocelot
builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

// Security headers
app.Use((ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    ctx.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' data:; script-src 'self'; style-src 'self' 'unsafe-inline'";
    return next();
});

app.UseSerilogRequestLogging();
app.UseHsts();
app.UseHttpsRedirection();

app.UseCors("spa");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

await app.UseOcelot();

app.Run();
