using EBanking.PaymentService.Data;
using EBanking.PaymentService.Services;
using EBanking.PaymentService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using FluentValidation;
using MediatR;
using System.Reflection;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.AddSharedSerilog("payment-service");

// Database
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Authentication
builder.Services.AddSharedAuthentication(builder.Configuration, builder.Environment, "payment-service");

// Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PaymentAccess", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("sub")
              .RequireClaim("scope", "write:payments"));
});

// Controllers
builder.Services.AddControllers();

// Services - Dependency Injection
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IPaymentTemplateService, PaymentTemplateService>();
builder.Services.AddScoped<IPaymentValidationService, PaymentValidationService>();
builder.Services.AddSingleton<IPaymentEventPublisher, PaymentEventPublisher>();

// E4 - Eventing Pattern - Removed (using direct Kafka for notifications only)
// builder.Services.AddEventingPattern<PaymentDbContext>(builder.Configuration);

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
        Title = "Payment Service API",
        Version = "v1",
        Description = "E-banking Payment Processing and Templates Management Service"
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
//    .AddDbContextCheck<PaymentDbContext>();

var app = builder.Build();

// Ensure the database schema exists. Dev uses EnsureCreated (no migration history);
// EF Core migrations are introduced in the hardening pass (M4).
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Payment Service API V1");
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

app.Run();
