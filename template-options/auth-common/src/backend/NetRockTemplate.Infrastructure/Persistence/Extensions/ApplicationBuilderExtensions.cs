using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetRockTemplate.Infrastructure.Persistence.Options;

namespace NetRockTemplate.Infrastructure.Persistence.Extensions;

/// <summary>
/// Extension methods for database initialization at startup.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Applies pending migrations in Development, or when <c>Database:MigrateOnStartup</c> is enabled.
    /// </summary>
    /// <param name="appBuilder">The application builder.</param>
    public static async Task InitializeDatabaseAsync(this IApplicationBuilder appBuilder)
    {
        using var scope = appBuilder.ApplicationServices.CreateScope();
        var services = scope.ServiceProvider;
        var isDevelopment = services.GetRequiredService<IHostEnvironment>().IsDevelopment();
        var databaseOptions = services.GetRequiredService<IConfiguration>()
            .GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();

        if (isDevelopment || databaseOptions.MigrateOnStartup)
        {
            var dbContext = services.GetRequiredService<NetRockTemplateDbContext>();
            await dbContext.Database.MigrateAsync();
        }
    }
}
