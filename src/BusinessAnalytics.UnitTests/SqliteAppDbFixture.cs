using System.Threading.Tasks;
using BusinessAnalytics.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BusinessAnalytics.UnitTests;

public sealed class SqliteAppDbFixture : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    public AppDbContext Db { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Single in-memory SQLite connection for this fixture
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)             // ✅ this is why we need Microsoft.EntityFrameworkCore.Sqlite
            .Options;

        Db = new AppDbContext(options);

        // Build schema from your model
        await Db.Database.EnsureCreatedAsync();
        // If you prefer migrations: await Db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

