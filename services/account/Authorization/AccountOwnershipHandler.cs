using Microsoft.AspNetCore.Authorization;
using EBanking.AccountService.Data;
using Microsoft.EntityFrameworkCore;

namespace EBanking.AccountService.Authorization;

public class AccountOwnershipRequirement : IAuthorizationRequirement
{
    public string AccountIdParameter { get; set; } = "accountId";
}

public class AccountOwnershipHandler : AuthorizationHandler<AccountOwnershipRequirement>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<AccountOwnershipHandler> _logger;

    public AccountOwnershipHandler(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<AccountOwnershipHandler> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AccountOwnershipRequirement requirement)
    {
        var userId = context.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("No user ID found in token");
            context.Fail();
            return;
        }

        // For /accounts/me/* endpoints, we just verify the user is authenticated with a valid sub claim
        if (context.Resource is HttpContext httpContext)
        {
            var path = httpContext.Request.Path.Value?.ToLower();
            if (path?.Contains("/accounts/me") == true)
            {
                _logger.LogInformation("Allowing access to /accounts/me for user {UserId}", userId);
                context.Succeed(requirement);
                return;
            }

            // For specific account ID endpoints (future use)
            var accountIdString = httpContext.Request.RouteValues[requirement.AccountIdParameter]?.ToString();
            if (!string.IsNullOrEmpty(accountIdString) && int.TryParse(accountIdString, out var accountId))
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();

                var account = await dbContext.Accounts
                    .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

                if (account != null)
                {
                    _logger.LogInformation("User {UserId} authorized for account {AccountId}", userId, accountId);
                    context.Succeed(requirement);
                    return;
                }
                else
                {
                    _logger.LogWarning("User {UserId} attempted to access account {AccountId} - access denied", userId, accountId);
                }
            }
        }

        context.Fail();
    }
}
