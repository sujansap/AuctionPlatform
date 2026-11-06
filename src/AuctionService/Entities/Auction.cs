namespace AuctionService.Entities;

public enum AuctionStatus { Live, Finished, ReserveNotMet }

public class Auction
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Seller { get; set; } = default!;
    public decimal ReservePrice { get; set; }
    public decimal? CurrentHighBid { get; set; }
    public string? Winner { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime EndsAt { get; set; }
    public AuctionStatus Status { get; set; } = AuctionStatus.Live;
}