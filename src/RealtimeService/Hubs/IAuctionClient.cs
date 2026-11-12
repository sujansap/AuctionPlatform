namespace RealtimeService.Hubs;

public record BidNotification(Guid BidId, Guid AuctionId, string Bidder, decimal Amount, DateTime PlacedAt);

public interface IAuctionClient
{
    Task BidPlaced(BidNotification bid);
}