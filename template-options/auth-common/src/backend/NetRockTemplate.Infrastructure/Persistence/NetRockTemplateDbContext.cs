using Microsoft.EntityFrameworkCore;
using NetRockTemplate.Infrastructure.Features.Jobs.Models;
using NetRockTemplate.Infrastructure.Persistence.Extensions;

namespace NetRockTemplate.Infrastructure.Persistence;

/// <summary>
/// Application database context with background job state and custom model configuration.
/// </summary>
internal class NetRockTemplateDbContext(DbContextOptions<NetRockTemplateDbContext> options)
    : DbContext(options)
{
    /// <summary>
    /// Gets or sets the paused jobs table for persisting pause state across restarts.
    /// </summary>
    public DbSet<PausedJob> PausedJobs { get; set; }

    /// <summary>
    /// Configures the model by applying all <see cref="IEntityTypeConfiguration{TEntity}"/> from this assembly
    /// and fuzzy search extensions.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NetRockTemplateDbContext).Assembly);
        modelBuilder.ApplyFuzzySearch();
    }
}
