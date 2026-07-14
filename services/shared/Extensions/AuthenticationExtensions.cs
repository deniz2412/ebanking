using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace Shared.Extensions
{
    public static class AuthenticationExtensions
    {
        /// <summary>
        /// Registers authentication for a service or the gateway.
        ///
        /// Modes (config key <c>Auth:DevBypass</c>):
        ///  - <c>true</c>  → a fake <see cref="DevelopmentAuthenticationHandler"/> that authenticates
        ///    every request as a dev principal. Convenient for local work without Keycloak. Only
        ///    honoured when the environment is Development.
        ///  - <c>false</c> (default outside Development) → real OIDC JWT validation against Keycloak.
        ///
        /// Real-mode config:
        ///  - <c>Auth:Authority</c>            Keycloak realm URL (issuer).
        ///  - <c>Auth:Audience</c>             expected <c>aud</c>; empty ⇒ audience not validated
        ///                                     (used by the gateway, which validates issuer only).
        ///  - <c>Auth:RequireHttpsMetadata</c> false for local http Keycloak.
        /// </summary>
        public static IServiceCollection AddSharedAuthentication(
            this IServiceCollection services,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            string audience)
        {
            var devBypass = environment.IsDevelopment()
                && configuration.GetValue("Auth:DevBypass", false);

            if (devBypass)
            {
                services.AddAuthentication("Development")
                    .AddScheme<DevelopmentAuthenticationOptions, DevelopmentAuthenticationHandler>(
                        "Development", _ => { });
                return services;
            }

            var authority = configuration.GetValue<string>("Auth:Authority")
                ?? "https://ebank.local/auth/realms/ebanking";
            // Explicit config wins; otherwise fall back to the caller-provided audience.
            var expectedAudience = configuration.GetValue<string>("Auth:Audience") ?? audience;
            var validateAudience = !string.IsNullOrWhiteSpace(expectedAudience);
            var requireHttps = configuration.GetValue("Auth:RequireHttpsMetadata", true);

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.Authority = authority;
                    options.RequireHttpsMetadata = requireHttps;
                    options.Audience = validateAudience ? expectedAudience : null;
                    // Keep raw claim types ("sub", "scope") instead of the legacy SOAP mappings
                    // so RouteClaimsRequirement["sub"] and RequireClaim("sub") match the token.
                    options.MapInboundClaims = false;

                    // RS256/ES256 signature validated against Keycloak's JWKS, plus
                    // iss/aud/exp all enforced with minimal clock skew.
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = authority,
                        ValidateAudience = validateAudience,
                        ValidAudience = validateAudience ? expectedAudience : null,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(2),
                        NameClaimType = "preferred_username",
                        RoleClaimType = ClaimTypes.Role
                    };
                    options.Events = new JwtBearerEvents
                    {
                        // Keycloak emits scopes as one space-delimited "scope" claim. Split it into
                        // individual "scope" claims so RequireClaim("scope", x) and Ocelot's
                        // AllowedScopes both work.
                        OnTokenValidated = context =>
                        {
                            if (context.Principal?.Identity is ClaimsIdentity identity)
                            {
                                var scopeClaim = identity.FindFirst("scope");
                                if (scopeClaim is not null && scopeClaim.Value.Contains(' '))
                                {
                                    identity.RemoveClaim(scopeClaim);
                                    foreach (var scope in scopeClaim.Value.Split(
                                        ' ', StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        identity.AddClaim(new Claim("scope", scope));
                                    }
                                }

                                // Mirror the Keycloak "sub" into NameIdentifier so controllers
                                // that read User.FindFirst(ClaimTypes.NameIdentifier) resolve the
                                // user id (parity with the dev-bypass handler).
                                var sub = identity.FindFirst("sub")?.Value;
                                if (!string.IsNullOrEmpty(sub)
                                    && identity.FindFirst(ClaimTypes.NameIdentifier) is null)
                                {
                                    identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, sub));
                                }
                            }

                            var userId = context.Principal?.FindFirst("sub")?.Value;
                            Log.Information("JWT validated for user {UserId}", userId);
                            return Task.CompletedTask;
                        },
                        OnAuthenticationFailed = context =>
                        {
                            Log.Warning("JWT authentication failed: {Error} for {Path}",
                                context.Exception.Message, context.Request.Path);
                            return Task.CompletedTask;
                        },
                        OnChallenge = context =>
                        {
                            Log.Warning("JWT challenge for {Path}: {Error}",
                                context.Request.Path, context.ErrorDescription);
                            return Task.CompletedTask;
                        }
                    };
                });

            return services;
        }
    }
}
