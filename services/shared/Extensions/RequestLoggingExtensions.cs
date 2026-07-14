using Microsoft.AspNetCore.Builder;
using Serilog;

namespace Shared.Extensions
{
    public static class RequestLoggingExtensions
    {
        public static IApplicationBuilder UseRequestResponseLogging(this IApplicationBuilder app)
        {
            return app.Use(async (context, next) =>
            {
                var requestId = Guid.NewGuid().ToString();
                context.Items["RequestId"] = requestId;

                var user = context.User?.FindFirst("sub")?.Value ?? "anonymous";
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var userAgent = context.Request.Headers.UserAgent.ToString();

                Log.Information("Request {RequestId}: {Method} {Path} from {User}@{IP} [{UserAgent}]",
                    requestId, context.Request.Method, context.Request.Path, user, ip, userAgent);

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    await next.Invoke();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Request {RequestId} failed with exception", requestId);
                    throw;
                }
                finally
                {
                    stopwatch.Stop();
                    Log.Information("Response {RequestId}: {StatusCode} in {ElapsedMs}ms",
                        requestId, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
                }
            });
        }
    }
}