using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using NetRockTemplate.Application.Cookies.Constants;
using NetRockTemplate.Application.Features.Authentication;
using NetRockTemplate.Infrastructure.Features.Authentication.Options;
using NetRockTemplate.Infrastructure.Features.Authentication.Services;

namespace NetRockTemplate.Infrastructure.Features.Authentication.Extensions;

/// <summary>
/// Extension methods for registering Keycloak authentication.
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers JWT bearer validation against the Keycloak realm (discovery metadata and signing keys)
        /// and the service that runs the authorization code flow.
        /// Tokens are read from the <c>Authorization</c> header first, then from the access token cookie.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="environment">The host environment. HTTPS metadata is required outside Development.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddKeycloakAuthentication(IConfiguration configuration, IHostEnvironment environment)
        {
            services.AddOptions<KeycloakOptions>()
                .BindConfiguration(KeycloakOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            var keycloak = configuration.GetSection(KeycloakOptions.SectionName).Get<KeycloakOptions>() ?? new KeycloakOptions();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(opt =>
                {
                    opt.Authority = keycloak.Issuer;
                    opt.Audience = keycloak.ValidAudience;
                    opt.RequireHttpsMetadata = !environment.IsDevelopment();
                    opt.MapInboundClaims = false;
                    opt.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = keycloak.Issuer,
                        ValidateAudience = true,
                        ValidAudience = keycloak.ValidAudience,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ClockSkew = TimeSpan.FromSeconds(30),
                        NameClaimType = "preferred_username",
                        RoleClaimType = "roles"
                    };

                    opt.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            // The Authorization header wins; web clients fall back to the HttpOnly cookie.
                            var authHeader = context.Request.Headers.Authorization.ToString();
                            if ((string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                                && context.Request.Cookies.TryGetValue(CookieNames.AccessToken, out var accessToken))
                            {
                                context.Token = accessToken;
                            }

                            return Task.CompletedTask;
                        },
                        OnTokenValidated = context =>
                        {
                            // UserContext and auditing use Guid user IDs; Keycloak user IDs are UUIDs.
                            if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out _))
                            {
                                context.Fail("The token's sub claim is not a valid user ID.");
                            }

                            return Task.CompletedTask;
                        }
                    };
                });

            services.AddHttpClient(KeycloakAuthService.HttpClientName);
            services.AddScoped<IKeycloakAuthService, KeycloakAuthService>();

            return services;
        }
    }
}
