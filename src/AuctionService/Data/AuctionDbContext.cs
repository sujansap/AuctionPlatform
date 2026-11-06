using AuctionService.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuctionService.Data;

public class AuctionDbContext(DbContextOptions<AuctionDbContext> options) : DbContext(options)
{
    public DbSet<Auction> Auctions => Set<Auction>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Auction>(e =>
        {
            e.Property(a => a.Title).HasMaxLength(200).IsRequired();
            e.Property(a => a.ReservePrice).HasPrecision(18, 2);
            e.Property(a => a.CurrentHighBid).HasPrecision(18, 2);
            e.Property(a => a.Status).HasConversion<string>();
        });
    }
}