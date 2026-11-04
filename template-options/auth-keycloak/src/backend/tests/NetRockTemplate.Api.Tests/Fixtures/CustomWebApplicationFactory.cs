using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using NetRockTemplate.Application.Features.Authentication;
using NetRockTemplate.Application.Features.Email;
using NetRockTemplate.Infrastructure.Persistence;
using NSubstitute.ClearExtensions;

namespace NetRockTemplate.Api.Tests.Fixtures;

/// <summary>
/// Test server factory that replaces PostgreSQL with an in-memory database,
/// disables Hangfire, substitutes external services with mocks, and validates bearer tokens
/// against the in-process <see cref="TestIssuer"/> instead of a real Keycloak realm.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "TestDb_" + Guid.NewGuid();

    /// <summary>Gets the mocked Keycloak client.</summary>
    public IKeycloakAuthService KeycloakAuthService { get; } = Substitute.For<IKeycloakAuthService>();

    /// <summary>Gets the mocked email service.</summary>
    public IEmailService EmailService { get; } = Substitute.For<IEmailService>();

    /// <summary>Gets the mocked hybrid cache.</summary>
    public HybridCache HybridCache { get; } = Substitute.For<HybridCache>();

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Use Testing environment - loads appsettings.Testing.json which disables
        // Hangfire, and provides a dummy DB connection string.
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Remove Hangfire hosted services in case config override didn't prevent registration
            var hangfireDescriptors = services
                .Where(d => d.ServiceType == typeof(IHostedService) &&
                            (d.ImplementationType?.FullName?.Contains("Hangfire") == true ||
                             d.ImplementationFactory?.Method.DeclaringType?.FullName?.Contains("Hangfire") == true))
                .ToList();
            foreach (var descriptor in hangfireDescriptors)
            {
                services.Remove(descriptor);
            }

            // Remove ALL EF Core / DbContext registrations to avoid dual-provider conflict
            // (Npgsql registered by app + InMemory registered by tests)
            services.RemoveAll<DbContextOptions<NetRockTemplateDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<NetRockTemplateDbContext>();

            services.AddScoped(_ =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<NetRockTemplateDbContext>();
                optionsBuilder.UseInMemoryDatabase(_dbName);
                return optionsBuilder.Options;
            });

            services.AddScoped<DbContextOptions>(sp =>
                sp.GetRequiredService<DbContextOptions<NetRockTemplateDbContext>>());

            services.AddScoped<NetRockTemplateDbContext>();

            services.RemoveAll<IEmailService>();
            services.AddSingleton(EmailService);

            services.RemoveAll<IKeycloakAuthService>();
            services.AddSingleton(KeycloakAuthService);

            // Validate tokens with the test issuer's metadata and signing key - no network calls.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Configuration = TestIssuer.Configuration;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(TestIssuer.Configuration);
            });

            services.RemoveAll<HybridCache>();
            services.AddSingleton(HybridCache);
        });
    }

    /// <summary>
    /// Clears all recorded calls and configured returns on the mocks.
    /// </summary>
    public void ResetMocks()
    {
        KeycloakAuthService.ClearSubstitute(ClearOptions.All);
        EmailService.ClearSubstitute(ClearOptions.All);
        HybridCache.ClearSubstitute(ClearOptions.All);
    }
}
