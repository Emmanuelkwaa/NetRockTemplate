using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NetRockTemplate.Infrastructure.Persistence;

namespace NetRockTemplate.Component.Tests.Fixtures;

internal static class TestDbContextFactory
{
    public static NetRockTemplateDbContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<NetRockTemplateDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new NetRockTemplateDbContext(options);
    }
}
