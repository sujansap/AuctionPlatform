using BiddingService.Data;
using BiddingService.Entities;
using Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace BiddingService.Consumers;

public class AuctionCreatedConsumer(BiddingDbContext db) : IConsumer<AuctionCreated>
{
    public async Task Consume(ConsumeContext<AuctionCreated> context)
    {
        var msg = context.Message;

        // idempotent: a duplicate delivery must not create a second row
        if (await db.AuctionStates.AnyAsync(a => a.AuctionId == msg.Id, context.CancellationToken))
            return;

        db.AuctionStates.Add(new AuctionState
        {
            AuctionId = msg.Id,
            Seller = msg.Seller,
            ReservePrice = msg.ReservePrice,
            EndsAt = msg.EndsAt
        });

        await db.SaveChangesAsync(context.CancellationToken);
    }
}