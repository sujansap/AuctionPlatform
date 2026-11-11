using Contracts;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using RealtimeService.Hubs;

namespace RealtimeService.Consumers;

public class BidPlacedConsumer(IHubContext<AuctionHub, IAuctionClient> hub) : IConsumer<BidPlaced>
{
    public Task Consume(ConsumeContext<BidPlaced> context)
    {
        var bid = context.Message;

        return hub.Clients
            .Group(AuctionHub.GroupName(bid.AuctionId))
            .BidPlaced(new BidNotification(bid.BidId, bid.AuctionId, bid.Bidder, bid.Amount, bid.PlacedAt));
    }
}