using System.ComponentModel.DataAnnotations;

namespace AuctionService.Dtos;

public record CreateAuctionDto(
    [Required, StringLength(200)] string Title,
    [Required] string Description,
    [Range(0, 10_000_000)] decimal ReservePrice,
    DateTime EndsAt);

public record AuctionDto(
    Guid Id, string Title, string Description, string Seller,
    decimal ReservePrice, decimal? CurrentHighBid,
    DateTime CreatedAt, DateTime EndsAt, string Status);