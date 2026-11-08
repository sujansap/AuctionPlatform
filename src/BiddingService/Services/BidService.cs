using BiddingService.Data;
using BiddingService.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BiddingService.Services;

public enum BidOutcome { Placed, AuctionNotFound, AuctionClosed, OwnAuction, TooLow, Conflict }

public record PlaceBidResult(BidOutcome Outcome, Bid? Bid = null, decimal? CurrentHighBid = null);

public class BidService(BiddingDbContext db, IAuctionClient auctions)
{
    private const int MaxAttempts = 3;

    public async Task<PlaceBidResult> PlaceBidAsync(
        Guid auctionId, string bidder, decimal amount, CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var state = await db.AuctionStates.FirstOrDefaultAsync(a => a.AuctionId == auctionId, ct);
            var isNew = state is null;

            if (state is null)
            {
                var info = await auctions.GetAuctionAsync(auctionId, ct);
                if (info is null) return new(BidOutcome.AuctionNotFound);

                state = new AuctionState
                {
                    AuctionId = info.Id,
                    Seller = info.Seller,
                    ReservePrice = info.ReservePrice,
                    EndsAt = info.EndsAt
                };
            }

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

            if (isNew) db.AuctionStates.Add(state);
            db.Bids.Add(bid);

            try
            {
                await db.SaveChangesAsync(ct);
                return new(BidOutcome.Placed, bid, amount);
            }
            catch (Exception ex) when (IsConflict(ex))
            {
                // someone else changed this auction between our read and our write:
                // throw away our stale view, then re-read and re-check the rules
                db.ChangeTracker.Clear();
            }
        }

        return new(BidOutcome.Conflict);
    }

    private static bool IsConflict(Exception ex) =>
        ex is DbUpdateConcurrencyException
        || ex is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } };
}