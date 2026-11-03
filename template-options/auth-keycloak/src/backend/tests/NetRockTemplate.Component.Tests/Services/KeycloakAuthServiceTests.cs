using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NetRockTemplate.Infrastructure.Features.Authentication.Options;
using NetRockTemplate.Infrastructure.Features.Authentication.Services;
using NetRockTemplate.Shared;

namespace NetRockTemplate.Component.Tests.Services;

public class KeycloakAuthServiceTests
{
    private const string Realm = "https://keycloak.test/realms/test";
    private const string RedirectUri = "https://app.example.com/api/auth/keycloak/callback";

    private readonly CapturingHandler _handler = new();
    private readonly JwtBearerOptions _bearerOptions = new()
    {
        Configuration = new OpenIdConnectConfiguration
        {
            Issuer = Realm,
            AuthorizationEndpoint = $"{Realm}/protocol/openid-connect/auth",
            TokenEndpoint = $"{Realm}/protocol/openid-connect/token",
            EndSessionEndpoint = $"{Realm}/protocol/openid-connect/logout"
        }
    };

    private KeycloakAuthService CreateService()
    {
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(KeycloakAuthService.HttpClientName).Returns(_ => new HttpClient(_handler, disposeHandler: false));

        var bearerMonitor = Substitute.For<IOptionsMonitor<JwtBearerOptions>>();
        bearerMonitor.Get(JwtBearerDefaults.AuthenticationScheme).Returns(_bearerOptions);

        var options = Options.Create(new KeycloakOptions
        {
            Authority = Realm,
            ClientId = "my-client",
            ClientSecret = "my-secret"
        });

        return new KeycloakAuthService(httpClientFactory, options, bearerMonitor, NullLogger<KeycloakAuthService>.Instance);
    }

    [Fact]
    public async Task CreateAuthorizationRequest_BuildsCodeFlowUrlWithPkceS256()
    {
        var result = await CreateService().CreateAuthorizationRequestAsync(RedirectUri, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var uri = new Uri(result.Value.AuthorizationUrl);
        Assert.Equal($"{Realm}/protocol/openid-connect/auth", uri.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(uri.Query);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("my-client", query["client_id"]);
        Assert.Equal(RedirectUri, query["redirect_uri"]);
        Assert.Equal("openid profile email", query["scope"]);
        Assert.Equal(result.Value.State, query["state"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        var expectedChallenge = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(result.Value.CodeVerifier)));
        Assert.Equal(expectedChallenge, query["code_challenge"]);
        Assert.False(query.ContainsKey("client_secret"));
    }

    [Fact]
    public async Task CreateAuthorizationRequest_GeneratesFreshStateAndVerifier()
    {
        var service = CreateService();

        var first = await service.CreateAuthorizationRequestAsync(RedirectUri, CancellationToken.None);
        var second = await service.CreateAuthorizationRequestAsync(RedirectUri, CancellationToken.None);

        Assert.NotEqual(first.Value.State, second.Value.State);
        Assert.NotEqual(first.Value.CodeVerifier, second.Value.CodeVerifier);
        Assert.True(first.Value.CodeVerifier.Length >= 43);
    }

    [Fact]
    public async Task CreateAuthorizationRequest_NoMetadata_ReturnsUnavailable()
    {
        _bearerOptions.Configuration = null;

        var result = await CreateService().CreateAuthorizationRequestAsync(RedirectUri, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.Keycloak.Unavailable, result.Error);
    }

    [Fact]
    public async Task ExchangeCode_PostsCodeVerifierAndClientCredentials()
    {
        _handler.Respond(HttpStatusCode.OK, """{"access_token":"at","refresh_token":"rt","expires_in":300,"refresh_expires_in":1800}""");

        var result = await CreateService().ExchangeCodeAsync("the-code", "the-verifier", RedirectUri, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("at", result.Value.AccessToken);
        Assert.Equal("rt", result.Value.RefreshToken);
        Assert.Equal(TimeSpan.FromMinutes(5), result.Value.AccessTokenLifetime);
        Assert.Equal(TimeSpan.FromMinutes(30), result.Value.RefreshTokenLifetime);

        Assert.Equal($"{Realm}/protocol/openid-connect/token", _handler.LastUri?.ToString());
        var form = _handler.LastForm;
        Assert.Equal("authorization_code", form["grant_type"]);
        Assert.Equal("the-code", form["code"]);
        Assert.Equal("the-verifier", form["code_verifier"]);
        Assert.Equal(RedirectUri, form["redirect_uri"]);
        Assert.Equal("my-client", form["client_id"]);
        Assert.Equal("my-secret", form["client_secret"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task ExchangeCode_Rejected_ReturnsUnauthorizedFailure(HttpStatusCode status)
    {
        _handler.Respond(status, """{"error":"invalid_grant"}""");

        var result = await CreateService().ExchangeCodeAsync("bad", "verifier", RedirectUri, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.ErrorType);
        Assert.Equal(ErrorMessages.Keycloak.CodeExchangeFailed, result.Error);
    }

    [Fact]
    public async Task ExchangeCode_NetworkFailure_ReturnsUnavailable()
    {
        _handler.Throw(new HttpRequestException("connection refused"));

        var result = await CreateService().ExchangeCodeAsync("code", "verifier", RedirectUri, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.Keycloak.Unavailable, result.Error);
    }

    [Fact]
    public async Task Refresh_PostsRefreshTokenGrant()
    {
        _handler.Respond(HttpStatusCode.OK, """{"access_token":"at2","refresh_token":"rt2","expires_in":60,"refresh_expires_in":600}""");

        var result = await CreateService().RefreshAsync("rt1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("rt2", result.Value.RefreshToken);
        Assert.Equal("refresh_token", _handler.LastForm["grant_type"]);
        Assert.Equal("rt1", _handler.LastForm["refresh_token"]);
    }

    [Fact]
    public async Task Refresh_ResponseWithoutTokens_ReturnsFailure()
    {
        _handler.Respond(HttpStatusCode.OK, """{"access_token":"","refresh_token":""}""");

        var result = await CreateService().RefreshAsync("rt1", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.Keycloak.RefreshFailed, result.Error);
    }

    [Fact]
    public async Task Revoke_PostsRefreshTokenToLogoutEndpoint()
    {
        _handler.Respond(HttpStatusCode.NoContent, "");

        await CreateService().RevokeAsync("rt1", CancellationToken.None);

        Assert.Equal($"{Realm}/protocol/openid-connect/logout", _handler.LastUri?.ToString());
        Assert.Equal("rt1", _handler.LastForm["refresh_token"]);
        Assert.Equal("my-secret", _handler.LastForm["client_secret"]);
    }

    [Fact]
    public async Task Revoke_ProviderFailure_DoesNotThrow()
    {
        _handler.Throw(new HttpRequestException("down"));

        var exception = await Record.ExceptionAsync(() => CreateService().RevokeAsync("rt1", CancellationToken.None));

        Assert.Null(exception);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private HttpStatusCode _status = HttpStatusCode.OK;
        private string _body = "{}";
        private Exception? _exception;

        public Uri? LastUri { get; private set; }

        public Dictionary<string, string> LastForm { get; private set; } = [];

        public void Respond(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
            _exception = null;
        }

        public void Throw(Exception exception) => _exception = exception;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            if (request.Content is not null)
            {
                var content = await request.Content.ReadAsStringAsync(cancellationToken);
                LastForm = QueryHelpers.ParseQuery(content).ToDictionary(p => p.Key, p => p.Value.ToString());
            }

            if (_exception is not null)
            {
                throw _exception;
            }

            return new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") };
        }
    }
}
