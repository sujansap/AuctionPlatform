using BiddingService.Data;
using BiddingService.Entities;
using Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace BiddingService.Services;

public enum BidOutcome { Placed, AuctionNotFound, AuctionClosed, OwnAuction, TooLow, Conflict }

public record PlaceBidResult(BidOutcome Outcome, Bid? Bid = null, decimal? CurrentHighBid = null);

public class BidService(BiddingDbContext db, IPublishEndpoint publish)
{
    private const int MaxAttempts = 3;

    public async Task<PlaceBidResult> PlaceBidAsync(
        Guid auctionId, string bidder, decimal amount, CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var state = await db.AuctionStates.FirstOrDefaultAsync(a => a.AuctionId == auctionId, ct);

            if (state is null)
                return new(BidOutcome.AuctionNotFound);

            if (state.Seller == bidder)
                return new(BidOutcome.OwnAuction);

            if (state.EndsAt <= DateTime.UtcNow)
                return new(BidOutcome.AuctionClosed);

            if (amount <= (state.HighestBid ?? 0))
                return new(BidOutcome.TooLow, CurrentHighBid: state.HighestBid);

            var bid = new Bid
            {
                Id = Guid.NewGuid(),
                AuctionId = auctionId,
                Bidder = bidder,
                Amount = amount
            };

            state.HighestBid = amount;
            state.HighestBidder = bidder;
            db.Bids.Add(bid);

            // queued in the outbox, committed together with the bid below
            await publish.Publish(
                new BidPlaced(bid.Id, auctionId, bidder, amount, bid.PlacedAt), ct);

            try
            {
                await db.SaveChangesAsync(ct);
                return new(BidOutcome.Placed, bid, amount);
            }
            catch (DbUpdateConcurrencyException)
            {
                // someone else bid in between: discard our stale state (including the
                // queued outbox message), then re-read and re-check the rules
                db.ChangeTracker.Clear();
            }
        }

        return new(BidOutcome.Conflict);
    }
}