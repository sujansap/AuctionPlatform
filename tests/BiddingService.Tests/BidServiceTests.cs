using BiddingService.Data;
using BiddingService.Entities;
using BiddingService.Services;
using Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace BiddingService.Tests;

public class BidServiceTests(PostgresFixture pg) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task FirstBid_IsAcceptedAndPublishesBidPlaced()
    {
        var auctionId = await SeedAuction();
        var publish = Substitute.For<IPublishEndpoint>();
        await using var db = pg.CreateDb();

        var result = await new BidService(db, publish).PlaceBidAsync(auctionId, "bob", 50m);

        Assert.Equal(BidOutcome.Placed, result.Outcome);
        Assert.Equal(50m, result.CurrentHighBid);
        await publish.Received(1).Publish(
            Arg.Is<BidPlaced>(e => e.AuctionId == auctionId && e.Bidder == "bob" && e.Amount == 50m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HigherBid_ReplacesHighestBid()
    {
        var auctionId = await SeedAuction();
        await using var db = pg.CreateDb();
        var sut = CreateSut(db);

        await sut.PlaceBidAsync(auctionId, "bob", 50m);
        var result = await sut.PlaceBidAsync(auctionId, "carol", 75m);

        Assert.Equal(BidOutcome.Placed, result.Outcome);
        var state = await db.AuctionStates.SingleAsync(a => a.AuctionId == auctionId);
        Assert.Equal(75m, state.HighestBid);
        Assert.Equal("carol", state.HighestBidder);
    }

    [Theory]
    [InlineData(50)]  // equal
    [InlineData(40)]  // lower
    public async Task BidNotHigherThanCurrent_IsRejectedAndNotPublished(decimal amount)
    {
        var auctionId = await SeedAuction();
        var publish = Substitute.For<IPublishEndpoint>();
        await using var db = pg.CreateDb();
        var sut = new BidService(db, publish);
        await sut.PlaceBidAsync(auctionId, "bob", 50m);
        publish.ClearReceivedCalls();

        var result = await sut.PlaceBidAsync(auctionId, "carol", amount);

        Assert.Equal(BidOutcome.TooLow, result.Outcome);
        Assert.Equal(50m, result.CurrentHighBid);
        Assert.Equal(1, await db.Bids.CountAsync(b => b.AuctionId == auctionId));
        await publish.DidNotReceive().Publish(
            Arg.Any<BidPlaced>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BidOnEndedAuction_IsRejected()
    {
        var auctionId = await SeedAuction(endsIn: TimeSpan.FromMinutes(-1));
        await using var db = pg.CreateDb();

        var result = await CreateSut(db).PlaceBidAsync(auctionId, "bob", 50m);

        Assert.Equal(BidOutcome.AuctionClosed, result.Outcome);
    }

    [Fact]
    public async Task SellerCannotBidOnOwnAuction()
    {
        var auctionId = await SeedAuction(seller: "alice");
        await using var db = pg.CreateDb();

        var result = await CreateSut(db).PlaceBidAsync(auctionId, "alice", 50m);

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
        var auctionId = await SeedAuction();
        var amounts = Enumerable.Range(1, 15).Select(i => i * 10m).ToList();

        // like the real app: every request gets its own DbContext
        var results = await Task.WhenAll(amounts.Select(async amount =>
        {
            await using var db = pg.CreateDb();
            return await CreateSut(db).PlaceBidAsync(auctionId, $"user{amount}", amount);
        }));

        var placed = results.Where(r => r.Outcome == BidOutcome.Placed).ToList();
        Assert.NotEmpty(placed);
        Assert.All(results, r => Assert.Contains(r.Outcome,
            new[] { BidOutcome.Placed, BidOutcome.TooLow, BidOutcome.Conflict }));

        await using var check = pg.CreateDb();
        var state = await check.AuctionStates.SingleAsync(a => a.AuctionId == auctionId);
        var stored = await check.Bids.Where(b => b.AuctionId == auctionId).ToListAsync();

        Assert.Equal(placed.Count, stored.Count);
        Assert.Equal(stored.Max(b => b.Amount), state.HighestBid);
        Assert.Equal(placed.Max(r => r.Bid!.Amount), state.HighestBid);
    }

    private async Task<Guid> SeedAuction(string seller = "seller", TimeSpan? endsIn = null)
    {
        await using var db = pg.CreateDb();
        var state = new AuctionState
        {
            AuctionId = Guid.NewGuid(),
            Seller = seller,
            ReservePrice = 10m,
            EndsAt = DateTime.UtcNow + (endsIn ?? TimeSpan.FromHours(1))
        };
        db.AuctionStates.Add(state);
        await db.SaveChangesAsync();
        return state.AuctionId;
    }

    private static BidService CreateSut(BiddingDbContext db) =>
        new(db, Substitute.For<IPublishEndpoint>());
}