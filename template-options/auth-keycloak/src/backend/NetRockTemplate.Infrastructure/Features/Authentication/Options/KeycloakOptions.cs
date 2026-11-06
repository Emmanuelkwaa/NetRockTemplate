using System.ComponentModel.DataAnnotations;

namespace NetRockTemplate.Infrastructure.Features.Authentication.Options;

/// <summary>
/// Connection settings for the external Keycloak realm.
/// Bound from <c>Authentication:Keycloak</c> (e.g. <c>Authentication__Keycloak__ClientSecret</c>).
/// </summary>
public sealed class KeycloakOptions : IValidatableObject
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Authentication:Keycloak";

    /// <summary>
    /// Gets the realm URL, e.g. <c>https://auth.example.com/realms/my-realm</c>.
    /// Must match the <c>iss</c> claim Keycloak writes into tokens exactly.
    /// </summary>
    [Required]
    public string Authority { get; init; } = string.Empty;

    /// <summary>
    /// Gets the confidential client ID registered in the realm.
    /// </summary>
    [Required]
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the client secret. Supply it through user secrets or the environment, never a committed file.
    /// </summary>
    [Required]
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>
    /// Gets the audience access tokens must carry. Defaults to <see cref="ClientId"/>, which needs an
    /// audience mapper on the client (Keycloak's default audience is <c>account</c>).
    /// </summary>
    public string? Audience { get; init; }

    /// <summary>
    /// Gets the public base URL that Keycloak redirects back to (<c>{CallbackBaseUrl}/api/auth/keycloak/callback</c>).
    /// Defaults to <c>Email:FrontendBaseUrl</c>, which is right when the frontend proxies <c>/api</c>.
    /// Set it to the API's own URL when browsers reach the API directly, for example next to a Blazor frontend.
    /// </summary>
    public string? CallbackBaseUrl { get; init; }

    /// <summary>
    /// Gets an optional realm URL the API uses to reach Keycloak on an internal network, for example
    /// <c>http://keycloak:8080/realms/my-realm</c>, when the public <see cref="Authority"/> is not reachable
    /// from the API container. Discovery, signing keys, token and logout calls go here; tokens are still
    /// validated against <see cref="Authority"/>. Plain HTTP is allowed for this address only.
    /// Keycloak must run with <c>KC_HOSTNAME_BACKCHANNEL_DYNAMIC=true</c>.
    /// </summary>
    public string? InternalUrl { get; init; }

    /// <summary>
    /// Gets the scopes requested in the authorization code flow.
    /// </summary>
    public string Scope { get; init; } = "openid profile email";

    /// <summary>
    /// Gets how long the login state cookie (state, PKCE verifier, return URL) stays valid.
    /// </summary>
    public TimeSpan LoginStateLifetime { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets the issuer without a trailing slash.
    /// </summary>
    public string Issuer => Authority.TrimEnd('/');

    /// <summary>
    /// Gets the audience that access tokens are validated against.
    /// </summary>
    public string ValidAudience => string.IsNullOrWhiteSpace(Audience) ? ClientId : Audience;

    /// <summary>
    /// Gets the realm URL used for back-channel calls: <see cref="InternalUrl"/> when set, otherwise <see cref="Issuer"/>.
    /// </summary>
    public string BackchannelRealmUrl => string.IsNullOrWhiteSpace(InternalUrl) ? Issuer : InternalUrl.TrimEnd('/');

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Uri.TryCreate(Authority, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
        {
            yield return new ValidationResult("Authority must be an absolute http(s) URL of the Keycloak realm.", [nameof(Authority)]);
        }

        if (!string.IsNullOrWhiteSpace(CallbackBaseUrl)
            && (!Uri.TryCreate(CallbackBaseUrl, UriKind.Absolute, out var callback) || callback.Scheme is not ("https" or "http")))
        {
            yield return new ValidationResult("CallbackBaseUrl must be an absolute http(s) URL when set.", [nameof(CallbackBaseUrl)]);
        }

        if (!string.IsNullOrWhiteSpace(InternalUrl)
            && (!Uri.TryCreate(InternalUrl, UriKind.Absolute, out var internalUri) || internalUri.Scheme is not ("https" or "http")))
        {
            yield return new ValidationResult("InternalUrl must be an absolute http(s) URL of the Keycloak realm when set.", [nameof(InternalUrl)]);
        }

        if (LoginStateLifetime <= TimeSpan.Zero)
        {
            yield return new ValidationResult("LoginStateLifetime must be greater than zero.", [nameof(LoginStateLifetime)]);
        }
    }
}
