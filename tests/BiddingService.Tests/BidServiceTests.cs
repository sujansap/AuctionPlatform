using BiddingService.Data;
using BiddingService.Services;
using Microsoft.EntityFrameworkCore;

namespace BiddingService.Tests;

public class FakeAuctionClient(params AuctionInfo[] auctions) : IAuctionClient
{
    public Task<AuctionInfo?> GetAuctionAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(auctions.FirstOrDefault(a => a.Id == id));
}

public class BidServiceTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task FirstBid_IsAccepted()
    {
        var auction = NewAuction();
        await using var db = pg.CreateDb();

        var result = await CreateSut(db, auction).PlaceBidAsync(auction.Id, "bob", 50m);

        Assert.Equal(BidOutcome.Placed, result.Outcome);
        Assert.Equal(50m, result.CurrentHighBid);
    }

    [Fact]
    public async Task HigherBid_ReplacesHighestBid()
    {
        var auction = NewAuction();
        await using var db = pg.CreateDb();
        var sut = CreateSut(db, auction);

        await sut.PlaceBidAsync(auction.Id, "bob", 50m);
        var result = await sut.PlaceBidAsync(auction.Id, "carol", 75m);

        Assert.Equal(BidOutcome.Placed, result.Outcome);
        var state = await db.AuctionStates.SingleAsync(a => a.AuctionId == auction.Id);
        Assert.Equal(75m, state.HighestBid);
        Assert.Equal("carol", state.HighestBidder);
    }

    [Theory]
    [InlineData(50)]  // equal
    [InlineData(40)]  // lower
    public async Task BidNotHigherThanCurrent_IsRejected(decimal amount)
    {
        var auction = NewAuction();
        await using var db = pg.CreateDb();
        var sut = CreateSut(db, auction);
        await sut.PlaceBidAsync(auction.Id, "bob", 50m);

        var result = await sut.PlaceBidAsync(auction.Id, "carol", amount);

        Assert.Equal(BidOutcome.TooLow, result.Outcome);
        Assert.Equal(50m, result.CurrentHighBid);
        Assert.Equal(1, await db.Bids.CountAsync(b => b.AuctionId == auction.Id));
    }

    [Fact]
    public async Task BidOnEndedAuction_IsRejected()
    {
        var auction = NewAuction(endsIn: TimeSpan.FromMinutes(-1));
        await using var db = pg.CreateDb();

        var result = await CreateSut(db, auction).PlaceBidAsync(auction.Id, "bob", 50m);

        Assert.Equal(BidOutcome.AuctionClosed, result.Outcome);
    }

    [Fact]
    public async Task SellerCannotBidOnOwnAuction()
    {
        var auction = NewAuction(seller: "alice");
        await using var db = pg.CreateDb();

        var result = await CreateSut(db, auction).PlaceBidAsync(auction.Id, "alice", 50m);

        Assert.Equal(BidOutcome.OwnAuction, result.Outcome);
    }

    [Fact]
    public async Task UnknownAuction_ReturnsNotFound()
    {
        await using var db = pg.CreateDb();

        var result = await CreateSut(db).PlaceBidAsync(Guid.NewGuid(), "bob", 50m);

        Assert.Equal(BidOutcome.AuctionNotFound, result.Outcome);
    }

    [Fact]
    public async Task ParallelBids_NeverLeaveTheHighestBidOutOfSync()
    {
        var auction = NewAuction();
        var amounts = Enumerable.Range(1, 15).Select(i => i * 10m).ToList();

        // like the real app: every request gets its own DbContext
        var results = await Task.WhenAll(amounts.Select(async amount =>
        {
            await using var db = pg.CreateDb();
            return await CreateSut(db, auction).PlaceBidAsync(auction.Id, $"user{amount}", amount);
        }));

        var placed = results.Where(r => r.Outcome == BidOutcome.Placed).ToList();
        Assert.NotEmpty(placed);
        Assert.All(results, r => Assert.Contains(r.Outcome,
            new[] { BidOutcome.Placed, BidOutcome.TooLow, BidOutcome.Conflict }));

        await using var check = pg.CreateDb();
        var state = await check.AuctionStates.SingleAsync(a => a.AuctionId == auction.Id);
        var stored = await check.Bids.Where(b => b.AuctionId == auction.Id).ToListAsync();

        Assert.Equal(placed.Count, stored.Count);                  // every "Placed" is really saved
        Assert.Equal(stored.Max(b => b.Amount), state.HighestBid); // state never disagrees with the bids
        Assert.Equal(placed.Max(r => r.Bid!.Amount), state.HighestBid);
    }

    private static AuctionInfo NewAuction(string seller = "seller", TimeSpan? endsIn = null) =>
        new(Guid.NewGuid(), seller, 10m, DateTime.UtcNow + (endsIn ?? TimeSpan.FromHours(1)));

    private static BidService CreateSut(BiddingDbContext db, params AuctionInfo[] known) =>
        new(db, new FakeAuctionClient(known));
}