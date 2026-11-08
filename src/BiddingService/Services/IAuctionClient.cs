namespace BiddingService.Services;

public record AuctionInfo(Guid Id, string Seller, decimal ReservePrice, DateTime EndsAt);

public interface IAuctionClient
{
    Task<AuctionInfo?> GetAuctionAsync(Guid id, CancellationToken ct = default);
}