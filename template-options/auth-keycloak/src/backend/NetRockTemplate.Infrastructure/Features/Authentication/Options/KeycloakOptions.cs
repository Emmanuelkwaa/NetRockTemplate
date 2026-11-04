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

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Uri.TryCreate(Authority, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
        {
            yield return new ValidationResult("Authority must be an absolute http(s) URL of the Keycloak realm.", [nameof(Authority)]);
        }

        if (LoginStateLifetime <= TimeSpan.Zero)
        {
            yield return new ValidationResult("LoginStateLifetime must be greater than zero.", [nameof(LoginStateLifetime)]);
        }
    }
}
