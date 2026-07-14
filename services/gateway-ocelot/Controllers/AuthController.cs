using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace GatewayOcelot.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        try
        {
            var token = GetTokenFromRequest();
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Logout attempted without token");
                return BadRequest(new { error = "No token provided" });
            }

            var userId = User.FindFirst("sub")?.Value;
            var sessionState = User.FindFirst("session_state")?.Value;

            _logger.LogInformation("User {UserId} initiating logout (session: {SessionState})", userId, sessionState);

            // Keycloak logout endpoint
            var authority = _configuration.GetValue<string>("Auth:Authority") ?? "https://ebank.local/auth/realms/ebanking";
            var logoutUrl = $"{authority}/protocol/openid-connect/logout";

            using var httpClient = _httpClientFactory.CreateClient();

            var logoutRequest = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("refresh_token", token),
                new KeyValuePair<string, string>("client_id", "api-gateway")
            });

            var response = await httpClient.PostAsync(logoutUrl, logoutRequest);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("User {UserId} logged out successfully", userId);
                return Ok(new { message = "Logged out successfully", timestamp = DateTime.UtcNow });
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Logout failed for user {UserId}: {Error}", userId, errorContent);
                return StatusCode(500, new { error = "Logout failed", details = errorContent });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during logout");
            return StatusCode(500, new { error = "Internal server error during logout" });
        }
    }

    [HttpPost("introspect")]
    [Authorize]
    public async Task<IActionResult> IntrospectToken()
    {
        try
        {
            var token = GetTokenFromRequest();
            if (string.IsNullOrEmpty(token))
            {
                return BadRequest(new { error = "No token provided" });
            }

            var authority = _configuration.GetValue<string>("Auth:Authority") ?? "https://ebank.local/auth/realms/ebanking";
            var introspectUrl = $"{authority}/protocol/openid-connect/token/introspect";

            using var httpClient = _httpClientFactory.CreateClient();

            var introspectRequest = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("token", token),
                new KeyValuePair<string, string>("client_id", "api-gateway")
            });

            var response = await httpClient.PostAsync(introspectUrl, introspectRequest);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var introspectionResult = JsonSerializer.Deserialize<JsonElement>(content);

                var isActive = introspectionResult.TryGetProperty("active", out var activeProp) && activeProp.GetBoolean();

                if (!isActive)
                {
                    _logger.LogWarning("Token introspection shows inactive token for user {UserId}",
                        User.FindFirst("sub")?.Value);
                    return Unauthorized(new { error = "Token is not active" });
                }

                return Ok(new {
                    active = isActive,
                    expires_at = introspectionResult.TryGetProperty("exp", out var exp) ? exp.GetInt64() : 0,
                    scope = introspectionResult.TryGetProperty("scope", out var scope) ? scope.GetString() : null,
                    client_id = introspectionResult.TryGetProperty("client_id", out var clientId) ? clientId.GetString() : null
                });
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Token introspection failed: {Error}", errorContent);
                return StatusCode(500, new { error = "Introspection failed" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during token introspection");
            return StatusCode(500, new { error = "Internal server error during introspection" });
        }
    }

    [HttpGet("user-info")]
    [Authorize]
    public IActionResult GetUserInfo()
    {
        var userId = User.FindFirst("sub")?.Value;
        var email = User.FindFirst("email")?.Value;
        var name = User.FindFirst("name")?.Value;
        var scope = User.FindFirst("scope")?.Value;
        var roles = User.FindAll("realm_access")?.Select(c => c.Value);

        return Ok(new
        {
            user_id = userId,
            email = email,
            name = name,
            scope = scope?.Split(' '),
            roles = roles,
            authenticated_at = DateTime.UtcNow
        });
    }

    private string? GetTokenFromRequest()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (authHeader?.StartsWith("Bearer ") == true)
        {
            return authHeader.Substring("Bearer ".Length).Trim();
        }
        return null;
    }
}
