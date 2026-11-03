using System.ComponentModel.DataAnnotations;
using NetRockTemplate.Infrastructure.Features.Authentication.Options;

namespace NetRockTemplate.Component.Tests.Validation;

public class KeycloakOptionsValidationTests
{
    private static List<ValidationResult> Validate(object instance)
    {
        var context = new ValidationContext(instance);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, context, results, validateAllProperties: true);
        return results;
    }

    private static KeycloakOptions Valid(string authority = "https://auth.example.com/realms/app", string clientSecret = "secret") => new()
    {
        Authority = authority,
        ClientId = "app",
        ClientSecret = clientSecret
    };

    [Fact]
    public void ValidOptions_NoErrors()
    {
        Assert.Empty(Validate(Valid()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("ftp://auth.example.com/realms/app")]
    public void Authority_Invalid_ReturnsError(string authority)
    {
        var results = Validate(Valid(authority: authority));

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(KeycloakOptions.Authority)));
    }

    [Fact]
    public void ClientSecret_Missing_ReturnsError()
    {
        var results = Validate(Valid(clientSecret: ""));

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(KeycloakOptions.ClientSecret)));
    }

    [Fact]
    public void Issuer_TrimsTrailingSlash()
    {
        Assert.Equal("https://auth.example.com/realms/app", Valid(authority: "https://auth.example.com/realms/app/").Issuer);
    }

    [Theory]
    [InlineData(null, "app")]
    [InlineData("", "app")]
    [InlineData("api-audience", "api-audience")]
    public void ValidAudience_DefaultsToClientId(string? audience, string expected)
    {
        var options = new KeycloakOptions { Authority = "https://a.example.com/realms/x", ClientId = "app", ClientSecret = "s", Audience = audience };

        Assert.Equal(expected, options.ValidAudience);
    }
}
