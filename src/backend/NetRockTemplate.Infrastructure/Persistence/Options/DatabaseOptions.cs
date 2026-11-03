namespace NetRockTemplate.Infrastructure.Persistence.Options;

/// <summary>
/// Configuration options for database startup behavior.
/// Bound from the <c>Database</c> configuration section (e.g. <c>Database__MigrateOnStartup</c>).
/// </summary>
internal sealed class DatabaseOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Database";

    /// <summary>
    /// Gets a value indicating whether pending EF Core migrations are applied when the API starts
    /// outside Development. Development always applies them.
    /// </summary>
    public bool MigrateOnStartup { get; init; }
}
