using EBanking.AuditService.Data;
using EBanking.AuditService.Services;
using EBanking.AuditService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using FluentValidation;
using MediatR;
using System.Reflection;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.AddSharedSerilog("audit-service");

// Database
builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Authentication
builder.Services.AddSharedAuthentication(builder.Configuration, builder.Environment, "audit-service");

// Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AuditAccess", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("sub")
              .RequireClaim("scope", "read:audit"));
});

// Controllers
builder.Services.AddControllers();

// Services - Dependency Injection
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IHashService, HashService>();
builder.Services.AddScoped<IIntegrityService, IntegrityService>();

// E4 - Outbox & Idempotency Pattern - Disabled for audit service (Event consumer only)
// builder.Services.AddEventingPattern<AuditDbContext>(builder.Configuration);

// Kafka consumer: appends domain events to the immutable hash-chained audit log.
builder.Services.AddHostedService<KafkaAuditConsumer>();
builder.Services.AddHostedService<IntegrityVerificationService>();

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
        Title = "Audit Service API",
        Version = "v1",
        Description = "E-banking Audit Logging and Integrity Verification Service"
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
//    .AddDbContextCheck<AuditDbContext>();

var app = builder.Build();

// Ensure the database schema exists (dev: EnsureCreated; migrations arrive in M4).
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Audit Service API V1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Health checks
app.MapHealthChecks("/health");

app.Run();
