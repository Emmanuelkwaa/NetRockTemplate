using Microsoft.AspNetCore.Hosting;
using NetRockTemplate.Api.Tests.Fixtures;

namespace NetRockTemplate.Api.Tests.Startup;

/// <summary>
/// The host must refuse to start (not fail per request) when the realm URL is plain http outside Development.
/// The validation message is covered by <c>KeycloakStartupValidatorTests</c> in Component.Tests.
/// </summary>
public class KeycloakStartupValidationTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public void HttpAuthorityOutsideDevelopment_HostFailsToStart()
    {
        // Program logs the startup failure as fatal and disposes the host, so the client cannot be created.
        using var app = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Authentication:Keycloak:Authority", "http://keycloak.test/realms/test"));

        Assert.ThrowsAny<Exception>(() => app.CreateClient());
    }

    [Fact]
    public async Task HttpInternalUrlWithHttpsAuthority_HostStarts()
    {
        using var app = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Authentication:Keycloak:InternalUrl", "http://keycloak:8080/realms/test"));
        using var client = app.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.True(response.IsSuccessStatusCode, $"/health/live returned {(int)response.StatusCode}");
    }
}
