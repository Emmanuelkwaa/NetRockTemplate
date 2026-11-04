using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using NetRockTemplate.Api.Tests.Fixtures;
using NetRockTemplate.Application.Cookies.Constants;
using NetRockTemplate.WebApi.Features.Users.Dtos;

namespace NetRockTemplate.Api.Tests.Controllers;

/// <summary>
/// Validates Keycloak access tokens end to end against the <see cref="TestIssuer"/> mock realm.
/// </summary>
public class UsersControllerTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private const string MeEndpoint = "/api/users/me";

    private readonly HttpClient _client;

    public UsersControllerTests(CustomWebApplicationFactory factory)
    {
        factory.ResetMocks();
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
    }

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task GetMe_ValidBearerToken_ReturnsClaims()
    {
        var response = await SendAsync(bearer: TestIssuer.CreateToken());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(user);
        Assert.Equal(Guid.Parse(TestIssuer.UserId), user.Id);
        Assert.Equal("jane", user.Username);
        Assert.Equal("jane@example.com", user.Email);
        Assert.Equal("Jane", user.FirstName);
        Assert.Equal("Doe", user.LastName);
    }

    [Fact]
    public async Task GetMe_TokenInCookie_ReturnsUser()
    {
        var response = await SendAsync(cookie: $"{CookieNames.AccessToken}={TestIssuer.CreateToken()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_NoToken_Returns401()
    {
        var response = await SendAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_DefaultKeycloakAudience_Returns401()
    {
        var response = await SendAsync(bearer: TestIssuer.CreateToken(audience: "account"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("https://keycloak.test/realms/other")]
    [InlineData("https://keycloak.test/realms/test/")]
    public async Task GetMe_IssuerMismatch_Returns401(string issuer)
    {
        var response = await SendAsync(bearer: TestIssuer.CreateToken(issuer: issuer));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_SubjectNotGuid_Returns401()
    {
        var response = await SendAsync(bearer: TestIssuer.CreateToken(sub: "service-account-123"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_ExpiredToken_Returns401()
    {
        var response = await SendAsync(bearer: TestIssuer.CreateToken(expiresIn: TimeSpan.FromMinutes(-5)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_UnknownSigningKey_Returns401()
    {
        var otherKey = new RsaSecurityKey(RSA.Create(2048)) { KeyId = "other-key" };

        var response = await SendAsync(bearer: TestIssuer.CreateToken(signingKey: otherKey));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private Task<HttpResponseMessage> SendAsync(string? bearer = null, string? cookie = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, MeEndpoint);
        if (bearer is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (cookie is not null) request.Headers.Add("Cookie", cookie);
        return _client.SendAsync(request);
    }
}
