using AuctionService.Data;
using Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace AuctionService.Consumers;

public class BidPlacedConsumer(AuctionDbContext db) : IConsumer<BidPlaced>
{
    public async Task Consume(ConsumeContext<BidPlaced> context)
    {
        var bid = context.Message;

        // One atomic UPDATE that only ever moves the high bid upwards.
        // Safe against duplicates, out-of-order delivery, and two consumers racing.
        await db.Auctions
            .Where(a => a.Id == bid.AuctionId
                     && (a.CurrentHighBid == null || a.CurrentHighBid < bid.Amount))
            .ExecuteUpdateAsync(
                s => s.SetProperty(a => a.CurrentHighBid, bid.Amount),
                context.CancellationToken);
    }
}