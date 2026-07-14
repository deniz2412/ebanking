using EBanking.AccountService.Data;
using EBanking.AccountService.Services;
using EBanking.AccountService.Services.Interfaces;
using EBanking.AccountService.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using FluentValidation;
using System.Reflection;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.AddSharedSerilog("account-service");

// Database
builder.Services.AddDbContext<AccountDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Authentication
builder.Services.AddSharedAuthentication(builder.Configuration, builder.Environment, "account-service");

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AccountOwner", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("sub")
              .Requirements.Add(new AccountOwnershipRequirement()));

    options.AddPolicy("AccountAccess", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("sub")
              .RequireClaim("scope", "read:accounts"));

    options.AddPolicy("AccountWrite", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("sub")
              .RequireClaim("scope", "write:accounts"));
});

// Authorization handlers
builder.Services.AddSingleton<IAuthorizationHandler, AccountOwnershipHandler>();

// Controllers
builder.Services.AddControllers();

// Services - Dependency Injection
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddScoped<DatabaseSeeder>();

// E4 - Eventing Pattern - Removed (using direct Kafka for notifications only)
// builder.Services.AddEventingPattern<AccountDbContext>(builder.Configuration);

// Validation
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

// API Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Account Service API",
        Version = "v1",
        Description = "E-banking Account Management Service"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Health checks
builder.Services.AddHealthChecks();
var app = builder.Build();

// Ensure the database schema exists. Dev uses EnsureCreated (no migration history);
// EF Core migrations are introduced in the hardening pass (M4).
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Ensuring database schema...");
        await context.Database.EnsureCreatedAsync();
        logger.LogInformation("Database schema ready.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while ensuring the database schema.");
        throw;
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Account Service API V1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Health checks
app.MapHealthChecks("/health");

// Database seeding in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync();
}

app.Run();
