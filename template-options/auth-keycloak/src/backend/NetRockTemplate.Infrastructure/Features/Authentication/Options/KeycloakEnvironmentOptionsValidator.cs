using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace NetRockTemplate.Infrastructure.Features.Authentication.Options;

/// <summary>
/// Requires an https <see cref="KeycloakOptions.Authority"/> outside Development, so a plain http realm URL
/// stops the app at startup instead of failing every request. <see cref="KeycloakOptions.InternalUrl"/> may
/// still use http, because it is only used on the internal network.
/// </summary>
/// <param name="environment">The host environment.</param>
internal sealed class KeycloakEnvironmentOptionsValidator(IHostEnvironment environment) : IValidateOptions<KeycloakOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, KeycloakOptions options)
    {
        if (environment.IsDevelopment()
            || !Uri.TryCreate(options.Authority, UriKind.Absolute, out var authority)
            || authority.Scheme != Uri.UriSchemeHttp)
        {
            // Missing or malformed values are reported by KeycloakOptions' own validation.
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            $"Authentication:Keycloak:Authority must use https outside Development (environment: {environment.EnvironmentName}). " +
            "It is the public realm URL that tokens carry as their issuer. Put Keycloak behind TLS; " +
            "only Authentication:Keycloak:InternalUrl may use plain http.");
    }
}
