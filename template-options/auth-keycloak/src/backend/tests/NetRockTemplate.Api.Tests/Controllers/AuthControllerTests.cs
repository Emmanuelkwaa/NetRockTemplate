using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NetRockTemplate.Api.Tests.Fixtures;
using NetRockTemplate.Application.Cookies.Constants;
using NetRockTemplate.Application.Features.Authentication;
using NetRockTemplate.Application.Features.Authentication.Dtos;
using NetRockTemplate.Shared;
using NetRockTemplate.WebApi.Features.Authentication;
using NetRockTemplate.WebApi.Features.Authentication.Dtos;

namespace NetRockTemplate.Api.Tests.Controllers;

/// <summary>
/// Covers the Keycloak authorization code flow endpoints with a mocked <see cref="IKeycloakAuthService"/>.
/// </summary>
public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const string RedirectUri = "https://test.example.com/api/auth/keycloak/callback";
    private const string State = "state-123";
    private const string CodeVerifier = "verifier-123";
    private const string AuthorizationUrl = TestIssuer.Authority + "/protocol/openid-connect/auth?client_id=test-client";

    private static readonly KeycloakTokens Tokens = new("new-access", "new-refresh", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30));

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
        _factory.KeycloakAuthService.CreateAuthorizationRequestAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<KeycloakAuthorizationRequest>.Success(new KeycloakAuthorizationRequest(AuthorizationUrl, State, CodeVerifier)));
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
    }

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Login_RedirectsToKeycloakWithRegisteredCallback()
    {
        var response = await _client.GetAsync("/api/auth/keycloak/login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(AuthorizationUrl, response.Headers.Location?.ToString());
        await _factory.KeycloakAuthService.Received(1).CreateAuthorizationRequestAsync(RedirectUri, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_SetsShortLivedHttpOnlyLoginStateCookie()
    {
        var response = await _client.GetAsync("/api/auth/keycloak/login");

        var cookie = Assert.Single(SetCookies(response), c => c.StartsWith(AuthController.LoginStateCookie + "=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth/keycloak", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=600", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(CodeVerifier, cookie, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://evil.example.com/")]
    [InlineData("//evil.example.com")]
    [InlineData("/\\evil.example.com")]
    public async Task Login_NonLocalReturnUrl_Returns400(string returnUrl)
    {
        var response = await _client.GetAsync($"/api/auth/keycloak/login?returnUrl={Uri.EscapeDataString(returnUrl)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await _factory.KeycloakAuthService.DidNotReceive().CreateAuthorizationRequestAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_ProviderUnavailable_ReturnsProblem()
    {
        _factory.KeycloakAuthService.CreateAuthorizationRequestAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<KeycloakAuthorizationRequest>.Failure(ErrorMessages.Keycloak.Unavailable, ErrorType.Unauthorized));

        var response = await _client.GetAsync("/api/auth/keycloak/login");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Callback_ValidState_ExchangesCodeSetsCookiesAndRedirects()
    {
        _factory.KeycloakAuthService.ExchangeCodeAsync("the-code", CodeVerifier, RedirectUri, Arg.Any<CancellationToken>())
            .Returns(Result<KeycloakTokens>.Success(Tokens));
        var loginCookie = await LoginCookieAsync("/dashboard?tab=1");

        var response = await CallbackAsync($"code=the-code&state={State}", loginCookie);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/dashboard?tab=1", response.Headers.Location?.ToString());
        var cookies = SetCookies(response);
        Assert.Contains(cookies, c => c.StartsWith($"{CookieNames.AccessToken}=new-access", StringComparison.Ordinal));
        Assert.Contains(cookies, c => c.StartsWith($"{CookieNames.RefreshToken}=new-refresh", StringComparison.Ordinal));
        Assert.Contains(cookies, c => c.StartsWith($"{AuthController.LoginStateCookie}=;", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Callback_StateMismatch_Returns400WithoutExchange()
    {
        var loginCookie = await LoginCookieAsync("/");

        var response = await CallbackAsync("code=the-code&state=forged", loginCookie);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await _factory.KeycloakAuthService.DidNotReceive().ExchangeCodeAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Callback_MissingLoginStateCookie_Returns400()
    {
        var response = await CallbackAsync($"code=the-code&state={State}", cookie: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Callback_TamperedLoginStateCookie_Returns400()
    {
        var response = await CallbackAsync($"code=the-code&state={State}", $"{AuthController.LoginStateCookie}=not-protected");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Callback_KeycloakError_Returns401()
    {
        var loginCookie = await LoginCookieAsync("/");

        var response = await CallbackAsync($"error=access_denied&state={State}", loginCookie);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Callback_ExchangeFails_Returns401WithoutCookies()
    {
        _factory.KeycloakAuthService.ExchangeCodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<KeycloakTokens>.Failure(ErrorMessages.Keycloak.CodeExchangeFailed, ErrorType.Unauthorized));
        var loginCookie = await LoginCookieAsync("/");

        var response = await CallbackAsync($"code=the-code&state={State}", loginCookie);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(SetCookies(response), c => c.StartsWith(CookieNames.AccessToken + "=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Refresh_WithCookie_RotatesCookiesAndReturns204()
    {
        _factory.KeycloakAuthService.RefreshAsync("old-refresh", Arg.Any<CancellationToken>())
            .Returns(Result<KeycloakTokens>.Success(Tokens));

        var response = await PostAsync("/api/auth/refresh?useCookies=true", $"{CookieNames.RefreshToken}=old-refresh");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains(SetCookies(response), c => c.StartsWith($"{CookieNames.RefreshToken}=new-refresh", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Refresh_WithBody_ReturnsTokens()
    {
        _factory.KeycloakAuthService.RefreshAsync("body-refresh", Arg.Any<CancellationToken>())
            .Returns(Result<KeycloakTokens>.Success(Tokens));

        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "body-refresh" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.Equal("new-access", body?.AccessToken);
        Assert.Equal(300, body?.ExpiresIn);
        Assert.Empty(SetCookies(response));
    }

    [Fact]
    public async Task Refresh_NoToken_Returns401()
    {
        var response = await PostAsync("/api/auth/refresh?useCookies=true", cookie: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_Rejected_ClearsCookiesAndReturns401()
    {
        _factory.KeycloakAuthService.RefreshAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<KeycloakTokens>.Failure(ErrorMessages.Keycloak.RefreshFailed, ErrorType.Unauthorized));

        var response = await PostAsync("/api/auth/refresh?useCookies=true", $"{CookieNames.RefreshToken}=stale");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(SetCookies(response), c => c.StartsWith($"{CookieNames.AccessToken}=;", StringComparison.Ordinal));
        Assert.Contains(SetCookies(response), c => c.StartsWith($"{CookieNames.RefreshToken}=;", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Logout_RevokesSessionAndClearsCookies()
    {
        var response = await PostAsync("/api/auth/logout", $"{CookieNames.RefreshToken}=current-refresh");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await _factory.KeycloakAuthService.Received(1).RevokeAsync("current-refresh", Arg.Any<CancellationToken>());
        Assert.Contains(SetCookies(response), c => c.StartsWith($"{CookieNames.RefreshToken}=;", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Logout_WithoutSession_StillReturns204()
    {
        var response = await PostAsync("/api/auth/logout", cookie: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await _factory.KeycloakAuthService.DidNotReceive().RevokeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private async Task<string> LoginCookieAsync(string returnUrl)
    {
        var response = await _client.GetAsync($"/api/auth/keycloak/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        var cookie = SetCookies(response).Single(c => c.StartsWith(AuthController.LoginStateCookie + "=", StringComparison.Ordinal));
        return cookie.Split(';')[0];
    }

    private Task<HttpResponseMessage> CallbackAsync(string query, string? cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/auth/keycloak/callback?{query}");
        if (cookie is not null) request.Headers.Add("Cookie", cookie);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostAsync(string url, string? cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (cookie is not null) request.Headers.Add("Cookie", cookie);
        return _client.SendAsync(request);
    }

    private static List<string> SetCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToList() : [];
}
