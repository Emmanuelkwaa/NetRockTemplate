namespace NetRockTemplate.Application.Features.Authentication.Dtos;

/// <summary>
/// Tokens issued by Keycloak.
/// </summary>
/// <param name="AccessToken">The access token (JWT) for API calls.</param>
/// <param name="RefreshToken">The refresh token for obtaining new access tokens.</param>
/// <param name="AccessTokenLifetime">How long the access token is valid.</param>
/// <param name="RefreshTokenLifetime">How long the refresh token is valid.</param>
public sealed record KeycloakTokens(
    string AccessToken,
    string RefreshToken,
    TimeSpan AccessTokenLifetime,
    TimeSpan RefreshTokenLifetime);
