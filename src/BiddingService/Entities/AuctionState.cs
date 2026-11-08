namespace BiddingService.Entities;

public class AuctionState
{
    public Guid AuctionId { get; set; }
    public string Seller { get; set; } = default!;
    public decimal ReservePrice { get; set; }
    public DateTime EndsAt { get; set; }
    public decimal? HighestBid { get; set; }
    public string? HighestBidder { get; set; }

    // Postgres xmin: changes on every update, used as the concurrency token
    public uint Version { get; set; }
}