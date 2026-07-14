using EBanking.NotificationService.Services.Interfaces;
using WebPush;

namespace EBanking.NotificationService.Services;

public class WebPushService : IWebPushService
{
    private readonly WebPushClient _webPushClient;
    private readonly VapidDetails _vapidDetails;
    private readonly ILogger<WebPushService> _logger;

    public WebPushService(IConfiguration configuration, ILogger<WebPushService> logger)
    {
        _logger = logger;
        
        var publicKey = configuration["VAPID:PublicKey"] ?? 
            throw new InvalidOperationException("VAPID PublicKey not configured");
        var privateKey = configuration["VAPID:PrivateKey"] ?? 
            throw new InvalidOperationException("VAPID PrivateKey not configured");
        var subject = configuration["VAPID:Subject"] ?? "mailto:admin@ebank.local";

        _vapidDetails = new VapidDetails(subject, publicKey, privateKey);
        _webPushClient = new WebPushClient();
    }

    public async Task<bool> SendNotificationAsync(string endpoint, string p256dh, string auth, object payload)
    {
        try
        {
            var subscription = new PushSubscription(endpoint, p256dh, auth);
            var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);
            
            await _webPushClient.SendNotificationAsync(subscription, payloadJson, _vapidDetails);
            
            _logger.LogInformation("Web push notification sent successfully to endpoint: {Endpoint}", 
                MaskEndpoint(endpoint));
            
            return true;
        }
        catch (WebPushException ex)
        {
            _logger.LogWarning("Web push failed - Endpoint: {Endpoint}, Status: {StatusCode}, Message: {Message}",
                MaskEndpoint(endpoint), ex.StatusCode, ex.Message);
            
            // Handle specific error cases
            if (ex.StatusCode == System.Net.HttpStatusCode.Gone || 
                ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Subscription is no longer valid, should be removed
                _logger.LogInformation("Subscription expired for endpoint: {Endpoint}", MaskEndpoint(endpoint));
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending web push to endpoint: {Endpoint}", 
                MaskEndpoint(endpoint));
            return false;
        }
    }

    public async Task<bool> TestSubscriptionAsync(string endpoint, string p256dh, string auth)
    {
        var testPayload = new
        {
            title = "Test Notification",
            body = "This is a test notification from E-Banking",
            icon = "/icon-192x192.png",
            badge = "/badge-72x72.png",
            tag = "test",
            data = new { type = "test", timestamp = DateTime.UtcNow }
        };

        return await SendNotificationAsync(endpoint, p256dh, auth, testPayload);
    }

    private static string MaskEndpoint(string endpoint)
    {
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return $"{uri.Scheme}://{uri.Host}/****";
        }
        return "****";
    }
}
