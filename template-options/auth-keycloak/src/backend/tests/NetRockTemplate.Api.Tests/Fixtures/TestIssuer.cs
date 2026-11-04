using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace NetRockTemplate.Api.Tests.Fixtures;

/// <summary>
/// An in-process stand-in for the Keycloak realm configured in <c>appsettings.Testing.json</c>:
/// discovery metadata plus an RSA signing key for minting access tokens.
/// </summary>
internal static class TestIssuer
{
    public const string Authority = "https://keycloak.test/realms/test";
    public const string ClientId = "test-client";
    public const string UserId = "6f1c1d7e-3b7a-4f0e-9a58-2f0d7c1e9b11";

    private static readonly RsaSecurityKey SigningKey = new(RSA.Create(2048)) { KeyId = "test-signing-key" };

    public static OpenIdConnectConfiguration Configuration { get; } = CreateConfiguration();

    /// <summary>
    /// Creates a signed access token. Defaults produce a token the API accepts.
    /// </summary>
    public static string CreateToken(
        string sub = UserId,
        string audience = ClientId,
        string issuer = Authority,
        TimeSpan? expiresIn = null,
        SecurityKey? signingKey = null)
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var expires = now.Add(expiresIn ?? TimeSpan.FromMinutes(5));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = expires.AddMinutes(-10),
            NotBefore = expires.AddMinutes(-10),
            Expires = expires,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = sub,
                ["preferred_username"] = "jane",
                ["email"] = "jane@example.com",
                ["given_name"] = "Jane",
                ["family_name"] = "Doe"
            },
            SigningCredentials = new SigningCredentials(signingKey ?? SigningKey, SecurityAlgorithms.RsaSha256)
        });
    }

    private static OpenIdConnectConfiguration CreateConfiguration()
    {
        var configuration = new OpenIdConnectConfiguration
        {
            Issuer = Authority,
            AuthorizationEndpoint = $"{Authority}/protocol/openid-connect/auth",
            TokenEndpoint = $"{Authority}/protocol/openid-connect/token",
            EndSessionEndpoint = $"{Authority}/protocol/openid-connect/logout"
        };
        configuration.SigningKeys.Add(SigningKey);
        return configuration;
    }
}
