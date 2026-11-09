namespace Contracts;

public record AuctionCreated(Guid Id, string Seller, decimal ReservePrice, DateTime EndsAt);