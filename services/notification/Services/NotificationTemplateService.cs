using EBanking.NotificationService.Data;
using EBanking.NotificationService.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace EBanking.NotificationService.Services;

public class NotificationTemplateService : INotificationTemplateService
{
    private readonly NotificationDbContext _context;
    private readonly ILogger<NotificationTemplateService> _logger;

    public NotificationTemplateService(NotificationDbContext context, ILogger<NotificationTemplateService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> RenderTitleAsync(string eventType, object data)
    {
        var template = await GetTemplateAsync(eventType);
        if (template == null)
        {
            return GetDefaultTitle(eventType);
        }

        return RenderTemplate(template.TitleTemplate, data);
    }

    public async Task<string> RenderBodyAsync(string eventType, object data)
    {
        var template = await GetTemplateAsync(eventType);
        if (template == null)
        {
            return GetDefaultBody(eventType, data);
        }

        return RenderTemplate(template.BodyTemplate, data);
    }

    public async Task<string> GetPriorityAsync(string eventType)
    {
        var template = await GetTemplateAsync(eventType);
        if (template == null)
        {
            return GetDefaultPriority(eventType);
        }

        return template.Priority;
    }

    public async Task<string?> GetIconUrlAsync(string eventType)
    {
        var template = await GetTemplateAsync(eventType);
        return template?.IconUrl ?? GetDefaultIcon(eventType);
    }

    private async Task<Models.NotificationTemplate?> GetTemplateAsync(string eventType)
    {
        return await _context.NotificationTemplates
            .FirstOrDefaultAsync(t => t.EventType == eventType && t.IsActive);
    }

    private static string RenderTemplate(string template, object data)
    {
        if (data == null) return template;

        var result = template;
        var properties = data.GetType().GetProperties();

        foreach (var prop in properties)
        {
            var value = prop.GetValue(data)?.ToString() ?? "";
            var pattern = $@"\{{\{{\s*{prop.Name}\s*\}}\}}";
            result = Regex.Replace(result, pattern, value, RegexOptions.IgnoreCase);
        }

        return result;
    }

    private static string GetDefaultTitle(string eventType)
    {
        return eventType.ToLowerInvariant() switch
        {
            "transfer.created" => "Transfer Initiated",
            "transfer.completed" => "Transfer Completed",
            "transfer.failed" => "Transfer Failed",
            "payment.created" => "Payment Initiated",
            "payment.completed" => "Payment Completed",
            "payment.failed" => "Payment Failed",
            "account.low_balance" => "Low Balance Alert",
            "account.statement_ready" => "Statement Ready",
            "security.login" => "New Login Detected",
            "security.password_changed" => "Password Changed",
            _ => "E-Banking Notification"
        };
    }

    private static string GetDefaultBody(string eventType, object data)
    {
        return eventType.ToLowerInvariant() switch
        {
            "transfer.created" => "Your transfer has been initiated and is being processed.",
            "transfer.completed" => "Your transfer has been completed successfully.",
            "transfer.failed" => "Your transfer could not be completed. Please contact support.",
            "payment.created" => "Your payment has been initiated and is being processed.",
            "payment.completed" => "Your payment has been completed successfully.",
            "payment.failed" => "Your payment could not be completed. Please try again.",
            "account.low_balance" => "Your account balance is running low. Consider adding funds.",
            "account.statement_ready" => "Your monthly statement is ready for download.",
            "security.login" => "A new login to your account was detected. If this wasn't you, please contact us immediately.",
            "security.password_changed" => "Your password has been changed successfully.",
            _ => "You have a new notification from E-Banking."
        };
    }

    private static string GetDefaultPriority(string eventType)
    {
        return eventType.ToLowerInvariant() switch
        {
            "security.login" => "HIGH",
            "security.password_changed" => "HIGH",
            "transfer.failed" => "HIGH",
            "payment.failed" => "HIGH",
            "account.low_balance" => "NORMAL",
            "transfer.completed" => "NORMAL",
            "payment.completed" => "NORMAL",
            _ => "NORMAL"
        };
    }

    private static string GetDefaultIcon(string eventType)
    {
        return eventType.ToLowerInvariant() switch
        {
            var e when e.StartsWith("transfer") => "/icons/transfer.png",
            var e when e.StartsWith("payment") => "/icons/payment.png",
            var e when e.StartsWith("account") => "/icons/account.png",
            var e when e.StartsWith("security") => "/icons/security.png",
            _ => "/icons/notification.png"
        };
    }
}
