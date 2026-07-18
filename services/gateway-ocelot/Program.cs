using Shared.Extensions;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Kubernetes;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Choose the Ocelot route file. OCELOT_CONFIG wins (compose sets ocelot.Docker.json with
// container hostnames); otherwise an environment-specific file (ocelot.Development.json →
// localhost dev ports); otherwise the default ocelot.json (k8s cluster DNS).
var ocelotFile = Environment.GetEnvironmentVariable("OCELOT_CONFIG");
if (string.IsNullOrWhiteSpace(ocelotFile) ||
    !File.Exists(Path.Combine(builder.Environment.ContentRootPath, ocelotFile)))
{
    ocelotFile = File.Exists(
        Path.Combine(builder.Environment.ContentRootPath, $"ocelot.{builder.Environment.EnvironmentName}.json"))
            ? $"ocelot.{builder.Environment.EnvironmentName}.json"
            : "ocelot.json";
}

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddJsonFile(ocelotFile, optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

// Logging
builder.AddSharedSerilog("gateway-ocelot");

// CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "https://ebank.local", "http://localhost:4200" };

builder.Services.AddCors(opts =>
{
    opts.AddPolicy("ebanking", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10)));
});

// Authentication
builder.Services.AddSharedAuthentication(builder.Configuration, builder.Environment, "api-gateway");

// Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("read:accounts", policy => policy.RequireAuthenticatedUser().RequireClaim("scope", "read:accounts"));
    options.AddPolicy("write:transfers", policy => policy.RequireAuthenticatedUser().RequireClaim("scope", "write:transfers"));
    options.AddPolicy("write:payments", policy => policy.RequireAuthenticatedUser().RequireClaim("scope", "write:payments"));
    options.AddPolicy("read:notifications", policy => policy.RequireAuthenticatedUser().RequireClaim("scope", "read:notifications"));
});

// Rate Limiting
builder.Services.AddSharedRateLimiting();

// Request size limits
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB limit
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB limit
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
});

// Ocelot. The Kubernetes service-discovery provider is only used in-cluster; locally the
// routes use explicit localhost DownstreamHostAndPorts, so skip it in Development.
var ocelotBuilder = builder.Services.AddOcelot(builder.Configuration);
if (!builder.Environment.IsDevelopment())
{
    ocelotBuilder.AddKubernetes();
}

builder.Services.AddHttpClient();
builder.Services.AddControllers();

var app = builder.Build();

// Middleware pipeline
app.UseRequestResponseLogging();
app.UseSecurityHeaders();

app.UseSerilogRequestLogging();

// TLS/HSTS is terminated at the ingress in production; locally the gateway serves plain
// HTTP so the SPA can call it without cert friction.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("ebanking");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/healthz", () => Results.Ok(new {
    status = "healthy",
    timestamp = DateTime.UtcNow,
    version = "1.0.0"
}));

await app.UseOcelot();

app.Run();
