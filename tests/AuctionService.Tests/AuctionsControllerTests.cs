using AuctionService.Controllers;
using AuctionService.Data;
using AuctionService.Dtos;
using AuctionService.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AuctionService.Tests;

public class AuctionsControllerTests : IDisposable
{
    private readonly AuctionDbContext _db = TestDb.Create();
    private readonly AuctionsController _sut;

    public AuctionsControllerTests()
    {
        _sut = new AuctionsController(_db);
    }

    public void Dispose() => _db.Dispose();

    // ---------- Create ----------

    [Fact]
    public async Task Create_ValidAuction_ReturnsCreatedAndPersists()
    {
        var dto = new CreateAuctionDto("Vintage camera", "Works great", 100m,
            DateTime.UtcNow.AddDays(1));

        var result = await _sut.Create(dto);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var body = Assert.IsType<AuctionDto>(created.Value);
        Assert.Equal("Vintage camera", body.Title);
        Assert.Equal("Live", body.Status);
        Assert.Single(_db.Auctions);
    }

    [Fact]
    public async Task Create_EndDateInThePast_ReturnsBadRequest()
    {
        var dto = new CreateAuctionDto("Old item", "Too late", 10m,
            DateTime.UtcNow.AddMinutes(-5));

        var result = await _sut.Create(dto);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(_db.Auctions);
    }

    // ---------- GetById ----------

    [Fact]
    public async Task GetById_ExistingAuction_ReturnsIt()
    {
        var auction = await SeedAuction("Lamp");

        var result = await _sut.GetById(auction.Id);

        Assert.Equal(auction.Id, result.Value!.Id);
        Assert.Equal("Lamp", result.Value.Title);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        var result = await _sut.GetById(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ---------- GetAll ----------

    [Fact]
    public async Task GetAll_ReturnsNewestFirst()
    {
        await SeedAuction("Oldest", createdAt: DateTime.UtcNow.AddDays(-2));
        await SeedAuction("Newest", createdAt: DateTime.UtcNow);
        await SeedAuction("Middle", createdAt: DateTime.UtcNow.AddDays(-1));

        var result = await _sut.GetAll();

        Assert.Equal(["Newest", "Middle", "Oldest"],
            result.Value!.Select(a => a.Title).ToArray());
    }

    [Fact]
    public async Task GetAll_PaginatesResults()
    {
        for (var i = 0; i < 5; i++)
            await SeedAuction($"Item {i}", createdAt: DateTime.UtcNow.AddMinutes(-i));

        var page2 = await _sut.GetAll(page: 2, pageSize: 2);

        Assert.Equal(2, page2.Value!.Count);
        Assert.Equal("Item 2", page2.Value[0].Title);
    }

    [Fact]
    public async Task GetAll_PageSizeAboveMax_IsClampedTo100()
    {
        for (var i = 0; i < 120; i++)
            await SeedAuction($"Item {i}");

        var result = await _sut.GetAll(page: 1, pageSize: 5000);

        Assert.Equal(100, result.Value!.Count);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_ExistingAuction_RemovesItAndReturnsNoContent()
    {
        var auction = await SeedAuction("To delete");

        var result = await _sut.Delete(auction.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(_db.Auctions);
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        var result = await _sut.Delete(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    // ---------- Helpers ----------

    private async Task<Auction> SeedAuction(string title, DateTime? createdAt = null)
    {
        var auction = new Auction
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = "Seeded",
            Seller = "tester",
            ReservePrice = 50m,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            EndsAt = DateTime.UtcNow.AddDays(1)
        };
        _db.Auctions.Add(auction);
        await _db.SaveChangesAsync();
        return auction;
    }
}