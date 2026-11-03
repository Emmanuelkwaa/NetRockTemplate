using NetRockTemplate.Application.Features.Authentication.Dtos;
using NetRockTemplate.Shared;

namespace NetRockTemplate.Application.Features.Authentication;

/// <summary>
/// Talks to the Keycloak realm for the authorization code flow with PKCE, token refresh and logout.
/// </summary>
public interface IKeycloakAuthService
{
    /// <summary>
    /// Creates the Keycloak authorization URL with a fresh state value and PKCE code verifier.
    /// </summary>
    /// <param name="redirectUri">The callback URI registered on the Keycloak client.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The authorization request, or a failure when the realm metadata is unavailable.</returns>
    Task<Result<KeycloakAuthorizationRequest>> CreateAuthorizationRequestAsync(string redirectUri, CancellationToken cancellationToken);

    /// <summary>
    /// Exchanges an authorization code for tokens.
    /// </summary>
    /// <param name="code">The authorization code from the callback.</param>
    /// <param name="codeVerifier">The PKCE code verifier created with the authorization request.</param>
    /// <param name="redirectUri">The same callback URI used for the authorization request.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The issued tokens, or a failure when Keycloak rejects the exchange.</returns>
    Task<Result<KeycloakTokens>> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken);

    /// <summary>
    /// Exchanges a refresh token for a new token pair.
    /// </summary>
    /// <param name="refreshToken">The current refresh token.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The rotated tokens, or a failure when the refresh token is no longer valid.</returns>
    Task<Result<KeycloakTokens>> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>
    /// Ends the Keycloak session that owns the refresh token. Failures are logged and not returned,
    /// so local logout always completes.
    /// </summary>
    /// <param name="refreshToken">The refresh token of the session to end.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);
}
