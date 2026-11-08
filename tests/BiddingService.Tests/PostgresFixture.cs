using BiddingService.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace BiddingService.Tests;

public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateDb();
        await db.Database.MigrateAsync(); // runs the real migrations, so they get tested too
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public BiddingDbContext CreateDb() => new(
        new DbContextOptionsBuilder<BiddingDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options);
}