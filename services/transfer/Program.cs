using EBanking.TransferService.Data;
using EBanking.TransferService.Services;
using EBanking.TransferService.Services.Interfaces;
using EBanking.TransferService.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using FluentValidation;
using MediatR;
using System.Reflection;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.AddSharedSerilog("transfer-service");

// Database
builder.Services.AddDbContext<TransferDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Authentication
builder.Services.AddSharedAuthentication(builder.Configuration, builder.Environment, "transfer-service");

// Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TransferAccess", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("sub")
              .RequireClaim("scope", "write:transfers"));
});

// Controllers
builder.Services.AddControllers();

// Services - Dependency Injection
builder.Services.AddScoped<ITransferService, TransferService>();
builder.Services.AddScoped<IEventPublisher, KafkaEventPublisher>();
builder.Services.AddScoped<IKafkaEventPublisher, KafkaEventPublisher>();
builder.Services.AddScoped<IIdempotencyService, IdempotencyService>();
builder.Services.AddScoped<IValidationService, ValidationService>();

// E4 - Eventing Pattern - Removed (using direct Kafka for notifications only)
// builder.Services.AddEventingPattern<TransferDbContext>(builder.Configuration);

// External dependencies
builder.Services.AddSingleton<IbanNet.IIbanValidator, IbanNet.IbanValidator>();

// MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

// Validation
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

// API Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Transfer Service API",
        Version = "v1",
        Description = "E-banking Transfer and Standing Orders Management Service"
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
//.AddDbContextCheck<TransferDbContext>();

var app = builder.Build();

// Ensure the database schema exists. Dev uses EnsureCreated (no migration history);
// EF Core migrations are introduced in the hardening pass (M4).
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TransferDbContext>();
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Transfer Service API V1");
        c.RoutePrefix = string.Empty;
    });
}

// Middleware
app.UseMiddleware<IdempotencyMiddleware>();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Health checks
app.MapHealthChecks("/health");

app.Run();
