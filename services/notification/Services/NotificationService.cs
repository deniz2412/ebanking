using EBanking.NotificationService.Data;
using EBanking.NotificationService.DTOs;
using EBanking.NotificationService.Models;
using EBanking.NotificationService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EBanking.NotificationService.Services;

public class NotificationService : INotificationService
{
    private readonly NotificationDbContext _context;
    private readonly IWebPushService _webPushService;
    private readonly INotificationTemplateService _templateService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        NotificationDbContext context,
        IWebPushService webPushService,
        INotificationTemplateService templateService,
        IConfiguration configuration,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _webPushService = webPushService;
        _templateService = templateService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<NotificationSubscriptionResponse> SubscribeAsync(string userId, SubscribeRequest request)
    {
        // Check if subscription already exists
        var existingSubscription = await _context.NotificationSubscriptions
            .FirstOrDefaultAsync(s => s.Endpoint == request.Endpoint);

        if (existingSubscription != null)
        {
            // Update existing subscription
            existingSubscription.UserId = userId;
            existingSubscription.P256dh = request.P256dh;
            existingSubscription.Auth = request.Auth;
            existingSubscription.UserAgent = request.UserAgent;
            existingSubscription.IsActive = true;
            existingSubscription.LastUsedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated existing notification subscription for user: {UserId}", userId);
            return MapToResponse(existingSubscription);
        }

        // Create new subscription
        var subscription = new NotificationSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Endpoint = request.Endpoint,
            P256dh = request.P256dh,
            Auth = request.Auth,
            UserAgent = request.UserAgent,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        // Test the subscription
        var testResult = await _webPushService.TestSubscriptionAsync(
            subscription.Endpoint, subscription.P256dh, subscription.Auth);

        if (!testResult)
        {
            throw new InvalidOperationException("Failed to verify push subscription");
        }

        _context.NotificationSubscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created new notification subscription for user: {UserId}", userId);
        return MapToResponse(subscription);
    }

    public async Task<bool> UnsubscribeAsync(string userId, Guid subscriptionId)
    {
        var subscription = await _context.NotificationSubscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.UserId == userId);

        if (subscription == null)
            return false;

        subscription.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Unsubscribed notification subscription: {SubscriptionId} for user: {UserId}",
            subscriptionId, userId);

        return true;
    }

    public async Task<IEnumerable<NotificationSubscriptionResponse>> GetUserSubscriptionsAsync(string userId)
    {
        var subscriptions = await _context.NotificationSubscriptions
            .Where(s => s.UserId == userId && s.IsActive)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return subscriptions.Select(MapToResponse);
    }

    public async Task<NotificationMessageResponse> SendNotificationAsync(string userId, SendNotificationRequest request)
    {
        // Create notification message
        var notification = new NotificationMessage
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = request.Title,
            Body = request.Body,
            Type = request.CorrelationId,
            Priority = request.Priority,
            Icon = request.Icon,
            Badge = request.Badge,
            Data = request.Data != null ? JsonSerializer.Serialize(request.Data) : null,
            CreatedAt = DateTime.UtcNow,
            Status = "PENDING"
        };

        _context.NotificationMessages.Add(notification);
        await _context.SaveChangesAsync();

        // Send to all active subscriptions for the user
        await SendToUserSubscriptionsAsync(notification);

        return MapToResponse(notification);
    }

    public async Task<PagedResult<NotificationMessageResponse>> GetUserNotificationsAsync(
        string userId, int page, int pageSize, string? status = null)
    {
        var query = _context.NotificationMessages.Where(n => n.UserId == userId);

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(n => n.Status == status.ToUpperInvariant());
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var responses = notifications.Select(MapToResponse);

        return new PagedResult<NotificationMessageResponse>(responses, totalCount, page, pageSize, totalPages);
    }

    public async Task<bool> MarkAsReadAsync(string userId, Guid notificationId)
    {
        var notification = await _context.NotificationMessages
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null)
            return false;

        notification.ReadAt = DateTime.UtcNow;
        if (notification.Status == "SENT" || notification.Status == "DELIVERED")
        {
            notification.Status = "READ";
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<VapidKeysResponse> GetVapidPublicKeyAsync()
    {
        var publicKey = _configuration["VAPID:PublicKey"] ??
            throw new InvalidOperationException("VAPID PublicKey not configured");

        return new VapidKeysResponse(publicKey);
    }

    private async Task SendToUserSubscriptionsAsync(NotificationMessage notification)
    {
        var subscriptions = await _context.NotificationSubscriptions
            .Where(s => s.UserId == notification.UserId && s.IsActive)
            .ToListAsync();

        if (!subscriptions.Any())
        {
            notification.Status = "FAILED";
            notification.FailureReason = "No active subscriptions found";
            await _context.SaveChangesAsync();
            return;
        }

        var payload = new
        {
            title = notification.Title,
            body = notification.Body,
            icon = notification.Icon ?? "/icon-192x192.png",
            badge = notification.Badge ?? "/badge-72x72.png",
            tag = notification.Type.ToLowerInvariant(),
            data = new
            {
                id = notification.Id,
                type = notification.Type,
                timestamp = notification.CreatedAt,
                customData = notification.Data != null ? JsonSerializer.Deserialize<object>(notification.Data) : null
            }
        };

        var successCount = 0;
        var failedEndpoints = new List<string>();

        foreach (var subscription in subscriptions)
        {
            var success = await _webPushService.SendNotificationAsync(
                subscription.Endpoint, subscription.P256dh, subscription.Auth, payload);

            if (success)
            {
                successCount++;
                subscription.LastUsedAt = DateTime.UtcNow;
            }
            else
            {
                failedEndpoints.Add(subscription.Endpoint);
                // Mark subscription as inactive if it failed
                subscription.IsActive = false;
            }
        }

        // Update notification status
        if (successCount > 0)
        {
            notification.Status = "SENT";
            notification.SentAt = DateTime.UtcNow;
        }
        else
        {
            notification.Status = "FAILED";
            notification.FailureReason = $"Failed to send to all {subscriptions.Count} subscriptions";
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Sent notification {NotificationId} to {SuccessCount}/{TotalCount} subscriptions",
            notification.Id, successCount, subscriptions.Count);
    }

    private static NotificationSubscriptionResponse MapToResponse(NotificationSubscription subscription)
    {
        return new NotificationSubscriptionResponse(
            subscription.Id,
            subscription.Endpoint,
            subscription.CreatedAt,
            subscription.LastUsedAt,
            subscription.IsActive
        );
    }

    private static NotificationMessageResponse MapToResponse(NotificationMessage notification)
    {
        return new NotificationMessageResponse(
            notification.Id,
            notification.Title,
            notification.Body,
            notification.Type,
            notification.Priority,
            notification.Status,
            notification.CreatedAt,
            notification.SentAt,
            notification.ReadAt,
            notification.FailureReason
        );
    }
}
