using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NetRockTemplate.Application.Features.Authentication;
using NetRockTemplate.Application.Features.Authentication.Dtos;
using NetRockTemplate.Infrastructure.Features.Authentication.Options;
using NetRockTemplate.Shared;

namespace NetRockTemplate.Infrastructure.Features.Authentication.Services;

/// <summary>
/// Runs the Keycloak authorization code flow with PKCE (S256) and the refresh and logout calls.
/// Endpoints come from the realm's discovery document, shared with JWT bearer validation.
/// With <see cref="KeycloakOptions.InternalUrl"/> set, back-channel calls use the internal address.
/// </summary>
internal sealed class KeycloakAuthService(
    IHttpClientFactory httpClientFactory,
    IOptions<KeycloakOptions> options,
    IOptionsMonitor<JwtBearerOptions> jwtBearerOptions,
    ILogger<KeycloakAuthService> logger) : IKeycloakAuthService
{
    /// <summary>
    /// The named <see cref="HttpClient"/> used for calls to the realm.
    /// </summary>
    public const string HttpClientName = "Keycloak";

    private readonly KeycloakOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<Result<KeycloakAuthorizationRequest>> CreateAuthorizationRequestAsync(string redirectUri, CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        if (metadata is null || string.IsNullOrEmpty(metadata.AuthorizationEndpoint))
        {
            return Result<KeycloakAuthorizationRequest>.Failure(ErrorMessages.Keycloak.Unavailable, ErrorType.Unauthorized);
        }

        var state = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var codeVerifier = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var codeChallenge = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

        var url = QueryHelpers.AddQueryString(metadata.AuthorizationEndpoint, new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = _options.Scope,
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        });

        return Result<KeycloakAuthorizationRequest>.Success(new KeycloakAuthorizationRequest(url, state, codeVerifier));
    }

    /// <inheritdoc />
    public Task<Result<KeycloakTokens>> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken) =>
        RequestTokensAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["redirect_uri"] = redirectUri
        }, ErrorMessages.Keycloak.CodeExchangeFailed, cancellationToken);

    /// <inheritdoc />
    public Task<Result<KeycloakTokens>> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
        RequestTokensAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        }, ErrorMessages.Keycloak.RefreshFailed, cancellationToken);

    /// <inheritdoc />
    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);

        // Keycloak's logout endpoint accepts a back-channel POST with the refresh token and ends the
        // whole SSO session. Fall back to RFC 7009 revocation when the realm does not advertise it.
        var endpoint = metadata?.EndSessionEndpoint;
        var form = new Dictionary<string, string> { ["refresh_token"] = refreshToken };
        if (string.IsNullOrEmpty(endpoint) && metadata?.AdditionalData.TryGetValue("revocation_endpoint", out var revocation) == true)
        {
            endpoint = revocation?.ToString();
            form = new Dictionary<string, string> { ["token"] = refreshToken, ["token_type_hint"] = "refresh_token" };
        }

        if (string.IsNullOrEmpty(endpoint))
        {
            logger.LogWarning("Keycloak metadata has no logout or revocation endpoint; session not ended at the provider");
            return;
        }

        try
        {
            using var response = await PostAsync(endpoint, form, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Keycloak logout returned {StatusCode}", (int)response.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Keycloak logout request failed");
        }
    }

    private async Task<Result<KeycloakTokens>> RequestTokensAsync(
        Dictionary<string, string> form, string failureMessage, CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(cancellationToken);
        if (metadata is null || string.IsNullOrEmpty(metadata.TokenEndpoint))
        {
            return Result<KeycloakTokens>.Failure(ErrorMessages.Keycloak.Unavailable, ErrorType.Unauthorized);
        }

        try
        {
            using var response = await PostAsync(metadata.TokenEndpoint, form, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Keycloak puts OAuth error codes in the body; log the status only to keep tokens out of logs.
                logger.LogInformation("Keycloak token request ({GrantType}) returned {StatusCode}", form["grant_type"], (int)response.StatusCode);
                return Result<KeycloakTokens>.Failure(failureMessage, ErrorType.Unauthorized);
            }

            var body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
            if (body is not { AccessToken.Length: > 0, RefreshToken.Length: > 0 })
            {
                return Result<KeycloakTokens>.Failure(failureMessage, ErrorType.Unauthorized);
            }

            return Result<KeycloakTokens>.Success(new KeycloakTokens(
                body.AccessToken,
                body.RefreshToken,
                TimeSpan.FromSeconds(body.ExpiresIn),
                TimeSpan.FromSeconds(body.RefreshExpiresIn > 0 ? body.RefreshExpiresIn : body.ExpiresIn)));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
                                   || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(ex, "Keycloak token request ({GrantType}) failed", form["grant_type"]);
            return Result<KeycloakTokens>.Failure(ErrorMessages.Keycloak.Unavailable, ErrorType.Unauthorized);
        }
    }

    private Task<HttpResponseMessage> PostAsync(string endpoint, Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        form["client_id"] = _options.ClientId;
        form["client_secret"] = _options.ClientSecret;

        var client = httpClientFactory.CreateClient(HttpClientName);
        return client.PostAsync(ToBackchannel(endpoint), new FormUrlEncodedContent(form), cancellationToken);
    }

    // Keycloak advertises front-channel endpoints (such as logout) on the public hostname even when
    // discovery is fetched internally, so rewrite public realm URLs to the internal one.
    private string ToBackchannel(string endpoint)
    {
        var publicRealm = _options.Issuer + "/";
        return _options.BackchannelRealmUrl != _options.Issuer && endpoint.StartsWith(publicRealm, StringComparison.Ordinal)
            ? _options.BackchannelRealmUrl + "/" + endpoint[publicRealm.Length..]
            : endpoint;
    }

    private async Task<OpenIdConnectConfiguration?> GetMetadataAsync(CancellationToken cancellationToken)
    {
        var bearer = jwtBearerOptions.Get(JwtBearerDefaults.AuthenticationScheme);
        if (bearer.Configuration is not null)
        {
            return bearer.Configuration;
        }

        if (bearer.ConfigurationManager is null)
        {
            return null;
        }

        try
        {
            return await bearer.ConfigurationManager.GetConfigurationAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or IOException)
        {
            logger.LogWarning(ex, "Could not load Keycloak discovery metadata");
            return null;
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("refresh_expires_in")] int RefreshExpiresIn);
}
