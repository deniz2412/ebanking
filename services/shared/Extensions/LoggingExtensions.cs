using Microsoft.AspNetCore.Builder;
using Serilog;

namespace Shared.Extensions
{
    public static class LoggingExtensions
    {
        public static void AddSharedSerilog(this WebApplicationBuilder builder, string serviceName)
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", serviceName)
                .WriteTo.Console()
                .WriteTo.File($"logs/{serviceName}-.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            builder.Host.UseSerilog();
        }
    }
}
