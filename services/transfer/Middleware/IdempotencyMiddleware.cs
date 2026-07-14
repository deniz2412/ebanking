using EBanking.TransferService.Services.Interfaces;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EBanking.TransferService.Middleware;

public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IIdempotencyService idempotencyService)
    {
        // Only apply to POST requests to transfers and standing-orders
        if (context.Request.Method != "POST" || 
            (!context.Request.Path.StartsWithSegments("/api/transfers") && 
             !context.Request.Path.StartsWithSegments("/api/standing-orders")))
        {
            await _next(context);
            return;
        }

        var idempotencyKey = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { 
                error = "Idempotency-Key header is required for POST requests" 
            }));
            return;
        }

        var userId = context.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            await _next(context);
            return;
        }

        // Read request body
        context.Request.EnableBuffering();
        var requestBody = await ReadRequestBodyAsync(context.Request);
        context.Request.Body.Position = 0;

        // Generate request hash
        var requestHash = GenerateRequestHash(context.Request.Method, context.Request.Path, requestBody);

        // Check if this request was already processed
        var existingRecord = await idempotencyService.GetIdempotencyRecordAsync(idempotencyKey, userId);
        
        if (existingRecord != null)
        {
            if (existingRecord.RequestHash == requestHash)
            {
                // Same request - return cached response
                _logger.LogInformation("Returning cached response for idempotency key {IdempotencyKey}", idempotencyKey);
                
                context.Response.StatusCode = existingRecord.ResponseStatusCode ?? 200;
                if (!string.IsNullOrEmpty(existingRecord.ResponseData))
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(existingRecord.ResponseData);
                }
                return;
            }
            else
            {
                // Different request with same key - conflict
                _logger.LogWarning("Idempotency key conflict for key {IdempotencyKey}, user {UserId}", idempotencyKey, userId);
                context.Response.StatusCode = 409;
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { 
                    error = "Idempotency key already used with different request data" 
                }));
                return;
            }
        }

        // Store the idempotency key and hash for this request
        context.Items["IdempotencyKey"] = idempotencyKey;
        context.Items["RequestHash"] = requestHash;
        context.Items["UserId"] = userId;

        // Capture response
        var originalResponseBodyStream = context.Response.Body;
        using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        try
        {
            await _next(context);

            // Save the response for future idempotent requests
            responseBodyStream.Position = 0;
            var responseBody = await new StreamReader(responseBodyStream).ReadToEndAsync();
            
            await idempotencyService.SaveIdempotencyRecordAsync(
                idempotencyKey, 
                userId, 
                requestHash, 
                responseBody, 
                context.Response.StatusCode);

            // Copy response back to original stream
            responseBodyStream.Position = 0;
            await responseBodyStream.CopyToAsync(originalResponseBodyStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request with idempotency key {IdempotencyKey}", idempotencyKey);
            
            // Save error response
            var errorResponse = JsonSerializer.Serialize(new { error = "Internal server error" });
            await idempotencyService.SaveIdempotencyRecordAsync(
                idempotencyKey, 
                userId, 
                requestHash, 
                errorResponse, 
                500);
            
            throw;
        }
        finally
        {
            context.Response.Body = originalResponseBodyStream;
        }
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }

    private static string GenerateRequestHash(string method, PathString path, string body)
    {
        var input = $"{method}:{path}:{body}";
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hashBytes);
    }
}
