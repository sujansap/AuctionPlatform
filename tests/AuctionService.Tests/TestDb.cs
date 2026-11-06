using AuctionService.Data;
using Microsoft.EntityFrameworkCore;

namespace AuctionService.Tests;

public static class TestDb
{
    public static AuctionDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AuctionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AuctionDbContext(options);
    }
}