namespace NetRockTemplate.Application.Features.Authentication.Dtos;

/// <summary>
/// A prepared Keycloak authorization request.
/// </summary>
/// <param name="AuthorizationUrl">The URL to redirect the browser to.</param>
/// <param name="State">The opaque state value that must come back on the callback.</param>
/// <param name="CodeVerifier">The PKCE code verifier to send with the code exchange.</param>
public sealed record KeycloakAuthorizationRequest(string AuthorizationUrl, string State, string CodeVerifier);
