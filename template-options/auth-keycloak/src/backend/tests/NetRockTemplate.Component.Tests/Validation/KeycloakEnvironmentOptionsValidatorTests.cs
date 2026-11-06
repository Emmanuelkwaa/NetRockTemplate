using Microsoft.Extensions.Hosting;
using NetRockTemplate.Infrastructure.Features.Authentication.Options;

namespace NetRockTemplate.Component.Tests.Validation;

public class KeycloakEnvironmentOptionsValidatorTests
{
    private static KeycloakEnvironmentOptionsValidator Validator(string environmentName)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return new KeycloakEnvironmentOptionsValidator(environment);
    }

    private static KeycloakOptions Options(string authority, string? internalUrl = null) => new()
    {
        Authority = authority,
        ClientId = "app",
        ClientSecret = "secret",
        InternalUrl = internalUrl
    };

    [Fact]
    public void Development_HttpAuthority_Succeeds()
    {
        var result = Validator(Environments.Development).Validate(null, Options("http://localhost:13009/realms/app"));

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public void OutsideDevelopment_HttpAuthority_FailsNamingTheSetting(string environmentName)
    {
        var result = Validator(environmentName).Validate(null, Options("http://auth.example.com/realms/app"));

        Assert.True(result.Failed);
        Assert.Contains("Authentication:Keycloak:Authority must use https outside Development", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_HttpsAuthority_Succeeds()
    {
        var result = Validator(Environments.Production).Validate(null, Options("https://auth.example.com/realms/app"));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Production_HttpsAuthorityWithHttpInternalUrl_Succeeds()
    {
        var result = Validator(Environments.Production).Validate(null,
            Options("https://auth.example.com/realms/app", internalUrl: "http://keycloak:8080/realms/app"));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Production_HttpAuthorityWithHttpInternalUrl_Fails()
    {
        var result = Validator(Environments.Production).Validate(null,
            Options("http://auth.example.com/realms/app", internalUrl: "http://keycloak:8080/realms/app"));

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    public void Production_MissingOrMalformedAuthority_LeftToDataAnnotations(string authority)
    {
        var result = Validator(Environments.Production).Validate(null, Options(authority));

        Assert.True(result.Succeeded);
    }
}
