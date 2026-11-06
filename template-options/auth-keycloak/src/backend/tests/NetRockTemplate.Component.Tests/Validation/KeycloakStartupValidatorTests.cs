using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NetRockTemplate.Infrastructure.Features.Authentication.Extensions;

namespace NetRockTemplate.Component.Tests.Validation;

/// <summary>
/// Runs the same startup validation the host runs (ValidateOnStart) for the Keycloak options.
/// </summary>
public class KeycloakStartupValidatorTests
{
    private static void Validate(string environmentName, string authority, string? internalUrl = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Authentication:Keycloak:Authority"] = authority,
            ["Authentication:Keycloak:ClientId"] = "app",
            ["Authentication:Keycloak:ClientSecret"] = "secret",
            ["Authentication:Keycloak:InternalUrl"] = internalUrl
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddKeycloakAuthentication(configuration, environment);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IStartupValidator>().Validate();
    }

    [Fact]
    public void Development_HttpAuthority_Starts()
    {
        Validate(Environments.Development, "http://localhost:13009/realms/app");
    }

    [Fact]
    public void Production_HttpAuthority_FailsAtStartupNamingTheSetting()
    {
        var exception = Assert.Throws<OptionsValidationException>(() => Validate(Environments.Production, "http://auth.example.com/realms/app"));

        Assert.Contains("Authentication:Keycloak:Authority must use https outside Development", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_HttpsAuthority_Starts()
    {
        Validate(Environments.Production, "https://auth.example.com/realms/app");
    }

    [Fact]
    public void Production_HttpsAuthorityWithHttpInternalUrl_Starts()
    {
        Validate(Environments.Production, "https://auth.example.com/realms/app", internalUrl: "http://keycloak:8080/realms/app");
    }
}
