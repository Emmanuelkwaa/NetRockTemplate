using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using NetRockTemplate.Application.Cookies;
using NetRockTemplate.Application.Cookies.Constants;
using NetRockTemplate.Application.Features.Authentication;
using NetRockTemplate.Application.Features.Authentication.Dtos;
using NetRockTemplate.Infrastructure.Features.Authentication.Options;
using NetRockTemplate.Infrastructure.Features.Email.Options;
using NetRockTemplate.Shared;
using NetRockTemplate.WebApi.Features.Authentication.Dtos;
using NetRockTemplate.WebApi.Shared;

namespace NetRockTemplate.WebApi.Features.Authentication;

/// <summary>
/// Sign-in through the external Keycloak realm (authorization code flow with PKCE),
/// plus cookie refresh and logout. Bearer clients can also use Keycloak tokens directly.
/// </summary>
[ApiController]
[Route("api/auth")]
[Tags("Auth")]
public class AuthController(
    IKeycloakAuthService keycloakAuthService,
    ICookieService cookieService,
    IDataProtectionProvider dataProtectionProvider,
    IOptions<KeycloakOptions> keycloakOptions,
    IOptions<EmailOptions> emailOptions,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>
    /// Name of the short-lived cookie that carries the protected login state between login and callback.
    /// </summary>
    public const string LoginStateCookie = "__Secure-KEYCLOAK-LOGIN";

    private const string CallbackPath = "/api/auth/keycloak/callback";
    private const string LoginStateCookiePath = "/api/auth/keycloak";

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("Keycloak.LoginState");

    /// <summary>
    /// Redirects to Keycloak to sign in. Register <c>{FrontendBaseUrl}/api/auth/keycloak/callback</c>
    /// as a valid redirect URI on the client.
    /// </summary>
    /// <param name="returnUrl">Local path to return to after sign-in. Defaults to <c>/</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="302">Redirect to the Keycloak login page.</response>
    /// <response code="400">The return URL is not a local path.</response>
    [HttpGet("keycloak/login")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> Login([FromQuery] string? returnUrl, CancellationToken cancellationToken)
    {
        returnUrl = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl;
        if (!Url.IsLocalUrl(returnUrl))
        {
            return ProblemFactory.Create(ErrorMessages.Keycloak.InvalidReturnUrl, ErrorType.Validation);
        }

        var result = await keycloakAuthService.CreateAuthorizationRequestAsync(RedirectUri, cancellationToken);
        if (!result.IsSuccess)
        {
            return ProblemFactory.Create(result.Error, result.ErrorType);
        }

        var lifetime = keycloakOptions.Value.LoginStateLifetime;
        var state = new LoginState(result.Value.State, result.Value.CodeVerifier, returnUrl, timeProvider.GetUtcNow().Add(lifetime));
        Response.Cookies.Append(LoginStateCookie, _protector.Protect(JsonSerializer.Serialize(state)), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            // Lax so the cookie comes back on the top-level redirect from Keycloak.
            SameSite = SameSiteMode.Lax,
            Path = LoginStateCookiePath,
            MaxAge = lifetime
        });

        return Redirect(result.Value.AuthorizationUrl);
    }

    /// <summary>
    /// Completes sign-in: validates the state, exchanges the code, sets the auth cookies and
    /// redirects to the return URL.
    /// </summary>
    /// <param name="code">The authorization code from Keycloak.</param>
    /// <param name="state">The state value from Keycloak.</param>
    /// <param name="error">The OAuth error code when Keycloak did not sign the user in.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="302">Signed in; redirect to the return URL.</response>
    /// <response code="400">The login state is missing, expired or does not match.</response>
    /// <response code="401">Keycloak refused the sign-in or the code exchange.</response>
    [HttpGet("keycloak/callback")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        var loginState = ReadLoginState();
        Response.Cookies.Delete(LoginStateCookie, new CookieOptions { Secure = true, HttpOnly = true, Path = LoginStateCookiePath });

        if (!string.IsNullOrEmpty(error))
        {
            return ProblemFactory.Create(ErrorMessages.Keycloak.SignInRefused, ErrorType.Unauthorized);
        }

        if (loginState is null || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(loginState.State)))
        {
            return ProblemFactory.Create(ErrorMessages.Keycloak.LoginStateInvalid, ErrorType.Validation);
        }

        var result = await keycloakAuthService.ExchangeCodeAsync(code, loginState.CodeVerifier, RedirectUri, cancellationToken);
        if (!result.IsSuccess)
        {
            return ProblemFactory.Create(result.Error, result.ErrorType);
        }

        SetAuthCookies(result.Value);
        return LocalRedirect(loginState.ReturnUrl);
    }

    /// <summary>
    /// Refreshes the session with Keycloak and rotates the tokens.
    /// Web clients send <c>?useCookies=true</c> and get rotated cookies and an empty body;
    /// other clients send the refresh token in the body and get the new tokens back.
    /// </summary>
    /// <param name="request">Optional body carrying the refresh token.</param>
    /// <param name="useCookies">Whether to read and write the auth cookies.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="200">New tokens (when <paramref name="useCookies"/> is false).</response>
    /// <response code="204">Cookies rotated.</response>
    /// <response code="401">The refresh token is missing or no longer valid.</response>
    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<TokenResponse>> Refresh(
        [FromBody] RefreshRequest? request,
        [FromQuery] bool useCookies = false,
        CancellationToken cancellationToken = default)
    {
        var refreshToken = request?.RefreshToken;
        if (string.IsNullOrEmpty(refreshToken) && useCookies)
        {
            refreshToken = cookieService.GetCookie(CookieNames.RefreshToken);
        }

        if (string.IsNullOrEmpty(refreshToken))
        {
            return ProblemFactory.Create(ErrorMessages.Keycloak.TokenMissing, ErrorType.Unauthorized);
        }

        var result = await keycloakAuthService.RefreshAsync(refreshToken, cancellationToken);
        if (!result.IsSuccess)
        {
            if (useCookies)
            {
                ClearAuthCookies();
            }

            return ProblemFactory.Create(result.Error, result.ErrorType);
        }

        if (useCookies)
        {
            SetAuthCookies(result.Value);
            return NoContent();
        }

        return Ok(new TokenResponse
        {
            AccessToken = result.Value.AccessToken,
            RefreshToken = result.Value.RefreshToken,
            ExpiresIn = (int)result.Value.AccessTokenLifetime.TotalSeconds
        });
    }

    /// <summary>
    /// Ends the Keycloak session and clears the auth cookies.
    /// </summary>
    /// <param name="request">Optional body carrying the refresh token (non-cookie clients).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <response code="204">Signed out.</response>
    [HttpPost("logout")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> Logout([FromBody] RefreshRequest? request, CancellationToken cancellationToken)
    {
        var refreshToken = request?.RefreshToken ?? cookieService.GetCookie(CookieNames.RefreshToken);
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await keycloakAuthService.RevokeAsync(refreshToken, cancellationToken);
        }

        ClearAuthCookies();
        return NoContent();
    }

    private string RedirectUri
    {
        get
        {
            var baseUrl = keycloakOptions.Value.CallbackBaseUrl;
            return (string.IsNullOrWhiteSpace(baseUrl) ? emailOptions.Value.FrontendBaseUrl : baseUrl).TrimEnd('/') + CallbackPath;
        }
    }

    private LoginState? ReadLoginState()
    {
        if (!Request.Cookies.TryGetValue(LoginStateCookie, out var protectedState) || string.IsNullOrEmpty(protectedState))
        {
            return null;
        }

        try
        {
            var state = JsonSerializer.Deserialize<LoginState>(_protector.Unprotect(protectedState));
            return state is not null && state.ExpiresAt > timeProvider.GetUtcNow() && Url.IsLocalUrl(state.ReturnUrl) ? state : null;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            return null;
        }
    }

    private void SetAuthCookies(KeycloakTokens tokens)
    {
        var now = timeProvider.GetUtcNow();
        cookieService.SetSecureCookie(CookieNames.AccessToken, tokens.AccessToken, now.Add(tokens.AccessTokenLifetime));
        cookieService.SetSecureCookie(CookieNames.RefreshToken, tokens.RefreshToken, now.Add(tokens.RefreshTokenLifetime));
    }

    private void ClearAuthCookies()
    {
        cookieService.DeleteCookie(CookieNames.AccessToken);
        cookieService.DeleteCookie(CookieNames.RefreshToken);
    }

    private sealed record LoginState(string State, string CodeVerifier, string ReturnUrl, DateTimeOffset ExpiresAt);
}
