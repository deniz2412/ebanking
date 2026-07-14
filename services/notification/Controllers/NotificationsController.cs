using EBanking.NotificationService.DTOs;
using EBanking.NotificationService.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EBanking.NotificationService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "NotificationAccess")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(INotificationService notificationService, ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Get VAPID public key for web push subscription
    /// </summary>
    [HttpGet("vapid-key")]
    [AllowAnonymous]
    public async Task<ActionResult<VapidKeysResponse>> GetVapidPublicKey()
    {
        try
        {
            var result = await _notificationService.GetVapidPublicKeyAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving VAPID public key");
            return StatusCode(500, "An error occurred while retrieving VAPID key");
        }
    }

    /// <summary>
    /// Subscribe to push notifications
    /// </summary>
    [HttpPost("subscribe")]
    public async Task<ActionResult<NotificationSubscriptionResponse>> Subscribe([FromBody] SubscribeRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var subscription = await _notificationService.SubscribeAsync(userId, request);
            
            _logger.LogInformation("User subscribed to notifications - User: {UserId}, Subscription: {SubscriptionId}",
                userId, subscription.Id);
            
            return Ok(subscription);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Subscription failed - User: {UserId}, Error: {Error}", userId, ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during subscription for user: {UserId}", userId);
            return StatusCode(500, "An error occurred while creating subscription");
        }
    }

    /// <summary>
    /// Unsubscribe from push notifications
    /// </summary>
    [HttpDelete("subscribe/{subscriptionId}")]
    public async Task<ActionResult> Unsubscribe(Guid subscriptionId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var success = await _notificationService.UnsubscribeAsync(userId, subscriptionId);
            
            if (!success)
            {
                return NotFound($"Subscription with ID {subscriptionId} not found");
            }

            _logger.LogInformation("User unsubscribed from notifications - User: {UserId}, Subscription: {SubscriptionId}",
                userId, subscriptionId);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsubscribing user {UserId} from subscription {SubscriptionId}", 
                userId, subscriptionId);
            return StatusCode(500, "An error occurred while removing subscription");
        }
    }

    /// <summary>
    /// Get user's notification subscriptions
    /// </summary>
    [HttpGet("subscriptions")]
    public async Task<ActionResult<IEnumerable<NotificationSubscriptionResponse>>> GetSubscriptions()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var subscriptions = await _notificationService.GetUserSubscriptionsAsync(userId);
            return Ok(subscriptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving subscriptions for user {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving subscriptions");
        }
    }

    /// <summary>
    /// Send a notification (for testing)
    /// </summary>
    [HttpPost("send")]
    public async Task<ActionResult<NotificationMessageResponse>> SendNotification([FromBody] SendNotificationRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var notification = await _notificationService.SendNotificationAsync(userId, request);
            
            _logger.LogInformation("Manual notification sent - User: {UserId}, Notification: {NotificationId}, Type: {Type}",
                userId, notification.Id, notification.Type);
            
            return Ok(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending manual notification for user: {UserId}", userId);
            return StatusCode(500, "An error occurred while sending notification");
        }
    }

    /// <summary>
    /// Get user's notifications with pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<NotificationMessageResponse>>> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var notifications = await _notificationService.GetUserNotificationsAsync(userId, page, pageSize, status);
            return Ok(notifications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notifications for user {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving notifications");
        }
    }

    /// <summary>
    /// Mark notification as read
    /// </summary>
    [HttpPut("{notificationId}/read")]
    public async Task<ActionResult> MarkAsRead(Guid notificationId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var success = await _notificationService.MarkAsReadAsync(userId, notificationId);
            
            if (!success)
            {
                return NotFound($"Notification with ID {notificationId} not found");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {NotificationId} as read for user {UserId}", 
                notificationId, userId);
            return StatusCode(500, "An error occurred while updating notification");
        }
    }
}
