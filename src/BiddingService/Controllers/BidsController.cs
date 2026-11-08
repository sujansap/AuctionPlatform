using BiddingService.Data;
using BiddingService.Dtos;
using BiddingService.Entities;
using BiddingService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BiddingService.Controllers;

[ApiController]
[Route("api/auctions/{auctionId:guid}/bids")]
public class BidsController(BidService bids, BiddingDbContext db) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> PlaceBid(Guid auctionId, PlaceBidDto dto, CancellationToken ct)
    {
        var result = await bids.PlaceBidAsync(auctionId, User.Identity!.Name!, dto.Amount, ct);

        return result.Outcome switch
        {
            BidOutcome.Placed => Ok(ToDto(result.Bid!)),
            BidOutcome.AuctionNotFound => NotFound(),
            BidOutcome.OwnAuction => Problem("You can't bid on your own auction.", statusCode: 403),
            BidOutcome.AuctionClosed => Problem("This auction has ended.", statusCode: 409),
            BidOutcome.TooLow => Problem(
                $"Your bid must be higher than the current highest bid ({result.CurrentHighBid}).",
                statusCode: 409),
            _ => Problem("Too many bids at once, please try again.", statusCode: 409)
        };
    }

    [HttpGet]
    public async Task<ActionResult<List<BidDto>>> GetBids(Guid auctionId)
    {
        var items = await db.Bids
            .Where(b => b.AuctionId == auctionId)
            .OrderByDescending(b => b.Amount)
            .ToListAsync();

        return items.Select(ToDto).ToList();
    }

    private static BidDto ToDto(Bid b) => new(b.Id, b.AuctionId, b.Bidder, b.Amount, b.PlacedAt);
}