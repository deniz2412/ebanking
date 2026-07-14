using EBanking.NotificationService.DTOs;

namespace EBanking.NotificationService.Services.Interfaces;

public interface INotificationService
{
    Task<NotificationSubscriptionResponse> SubscribeAsync(string userId, SubscribeRequest request);
    Task<bool> UnsubscribeAsync(string userId, Guid subscriptionId);
    Task<IEnumerable<NotificationSubscriptionResponse>> GetUserSubscriptionsAsync(string userId);
    Task<NotificationMessageResponse> SendNotificationAsync(string userId, SendNotificationRequest request);
    Task<PagedResult<NotificationMessageResponse>> GetUserNotificationsAsync(string userId, int page, int pageSize, string? status = null);
    Task<bool> MarkAsReadAsync(string userId, Guid notificationId);
    Task<VapidKeysResponse> GetVapidPublicKeyAsync();
}

public interface IWebPushService
{
    Task<bool> SendNotificationAsync(string endpoint, string p256dh, string auth, object payload);
    Task<bool> TestSubscriptionAsync(string endpoint, string p256dh, string auth);
}

public interface INotificationTemplateService
{
    Task<string> RenderTitleAsync(string eventType, object data);
    Task<string> RenderBodyAsync(string eventType, object data);
    Task<string> GetPriorityAsync(string eventType);
    Task<string?> GetIconUrlAsync(string eventType);
}

// Kafka consumer interface removed due to architecture simplification
