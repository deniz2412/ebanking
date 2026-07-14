using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Shared.Extensions
{
    public class DevelopmentAuthenticationOptions : AuthenticationSchemeOptions
    {
    }

    public class DevelopmentAuthenticationHandler : AuthenticationHandler<DevelopmentAuthenticationOptions>
    {
        public DevelopmentAuthenticationHandler(
            IOptionsMonitor<DevelopmentAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        // Scopes granted to the fabricated dev principal. Each is emitted as its own
        // "scope" claim so RequireClaim("scope", "<value>") policies match in dev.
        private static readonly string[] DevScopes =
        {
            "read:accounts", "write:accounts", "write:transfers",
            "write:payments", "read:notifications", "read:audit"
        };

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            const string devUserId = "dev-user-id";

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, "dev-user"),
                new(ClaimTypes.NameIdentifier, devUserId),
                // Keycloak tokens carry the subject in "sub"; policies RequireClaim("sub").
                new("sub", devUserId),
                new(ClaimTypes.Role, "audit-admin")
            };
            claims.AddRange(DevScopes.Select(s => new Claim("scope", s)));

            var identity = new ClaimsIdentity(claims, "Development");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Development");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
