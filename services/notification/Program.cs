using EBanking.NotificationService.Data;
using EBanking.NotificationService.Services;
using EBanking.NotificationService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using FluentValidation;
using MediatR;
using System.Reflection;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.AddSharedSerilog("notification-service");

// Database
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Authentication
builder.Services.AddSharedAuthentication(builder.Configuration, builder.Environment, "notification-service");

// Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("NotificationAccess", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("sub")
              .RequireClaim("scope", "read:notifications"));
});

// Controllers
builder.Services.AddControllers();

// Services - Dependency Injection
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IWebPushService, WebPushService>();
builder.Services.AddScoped<INotificationTemplateService, NotificationTemplateService>();

// E4 - Outbox & Idempotency Pattern - Disabled for notification service (Event consumer only)
// builder.Services.AddEventingPattern<NotificationDbContext>(builder.Configuration);

// Kafka consumer: turns transfer events into user notifications + Web Push.
builder.Services.AddHostedService<KafkaNotificationConsumer>();

// MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

// Validation
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

// CORS for web push
builder.Services.AddCors(options =>
{
    options.AddPolicy("WebPushPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// API Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Notification Service API",
        Version = "v1",
        Description = "E-banking Web Push Notification Service"
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
//    .AddDbContextCheck<NotificationDbContext>();

var app = builder.Build();

// Ensure the database schema exists (dev: EnsureCreated; migrations arrive in M4).
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification Service API V1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseCors("WebPushPolicy");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Health checks
app.MapHealthChecks("/health");

app.Run();
