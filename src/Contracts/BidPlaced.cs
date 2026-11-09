namespace Contracts;

public record BidPlaced(Guid BidId, Guid AuctionId, string Bidder, decimal Amount, DateTime PlacedAt);