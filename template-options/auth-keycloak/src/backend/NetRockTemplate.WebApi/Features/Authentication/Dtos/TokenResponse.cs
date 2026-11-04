using JetBrains.Annotations;

namespace NetRockTemplate.WebApi.Features.Authentication.Dtos;

/// <summary>
/// Tokens returned to non-cookie clients after a refresh.
/// </summary>
public class TokenResponse
{
    /// <summary>
    /// The Keycloak access token. Send it as <c>Authorization: Bearer {accessToken}</c>.
    /// </summary>
    public string AccessToken { [UsedImplicitly] get; init; } = string.Empty;

    /// <summary>
    /// The rotated Keycloak refresh token.
    /// </summary>
    public string RefreshToken { [UsedImplicitly] get; init; } = string.Empty;

    /// <summary>
    /// Access token lifetime in seconds.
    /// </summary>
    public int ExpiresIn { [UsedImplicitly] get; init; }
}
