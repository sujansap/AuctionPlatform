using AuctionService.Data;
using AuctionService.Dtos;
using AuctionService.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Contracts;
using MassTransit;

namespace AuctionService.Controllers;

[ApiController]
[Route("api/auctions")]
public class AuctionsController(AuctionDbContext db, IPublishEndpoint publish) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AuctionDto>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var items = await db.Auctions
            .OrderByDescending(a => a.CreatedAt)
            .Skip((Math.Max(page, 1) - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return items.Select(ToDto).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AuctionDto>> GetById(Guid id)
    {
        var auction = await db.Auctions.FindAsync(id);
        return auction is null ? NotFound() : ToDto(auction);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<AuctionDto>> Create(CreateAuctionDto dto)
    {
        
        if (dto.EndsAt <= DateTime.UtcNow)
            return BadRequest("EndsAt must be in the future.");

        var auction = new Auction
        {
            Id = Guid.NewGuid(),
            Title = dto.Title,
            Description = dto.Description,
            ReservePrice = dto.ReservePrice,
            EndsAt = dto.EndsAt.ToUniversalTime(),
            Seller = User.Identity!.Name!   // from the JWT, not hardcoded
        };

        db.Auctions.Add(auction);
        
        await publish.Publish(new AuctionCreated(
        auction.Id, auction.Seller, auction.ReservePrice, auction.EndsAt));

        await db.SaveChangesAsync();  
        return CreatedAtAction(nameof(GetById), new { id = auction.Id }, ToDto(auction));
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var auction = await db.Auctions.FindAsync(id);
        if (auction is null) return NotFound();

        if (auction.Seller != User.Identity!.Name)
            return Forbid();   // only the seller may delete

        db.Auctions.Remove(auction);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static AuctionDto ToDto(Auction a) => new(
        a.Id, a.Title, a.Description, a.Seller, a.ReservePrice,
        a.CurrentHighBid, a.CreatedAt, a.EndsAt, a.Status.ToString());
}