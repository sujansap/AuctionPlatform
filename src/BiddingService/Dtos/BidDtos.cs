using System.ComponentModel.DataAnnotations;

namespace BiddingService.Dtos;

public record PlaceBidDto([Range(0.01, 10_000_000)] decimal Amount);

public record BidDto(Guid Id, Guid AuctionId, string Bidder, decimal Amount, DateTime PlacedAt);