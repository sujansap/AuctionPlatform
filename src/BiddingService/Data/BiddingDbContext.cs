using BiddingService.Entities;
using Microsoft.EntityFrameworkCore;
using MassTransit;
namespace BiddingService.Data;

public class BiddingDbContext(DbContextOptions<BiddingDbContext> options) : DbContext(options)
{
    public DbSet<AuctionState> AuctionStates => Set<AuctionState>();
    public DbSet<Bid> Bids => Set<Bid>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AuctionState>(e =>
        {
            e.HasKey(a => a.AuctionId);
            e.Property(a => a.AuctionId).ValueGeneratedNever();
            e.Property(a => a.Seller).HasMaxLength(256).IsRequired();
            e.Property(a => a.ReservePrice).HasPrecision(18, 2);
            e.Property(a => a.HighestBid).HasPrecision(18, 2);
            e.Property(a => a.Version).IsRowVersion(); // maps to xmin with Npgsql
        });

        b.Entity<Bid>(e =>
        {
            e.Property(x => x.Bidder).HasMaxLength(256).IsRequired();
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasIndex(x => new { x.AuctionId, x.Amount });
            e.HasOne<AuctionState>().WithMany().HasForeignKey(x => x.AuctionId);
        });

        b.AddInboxStateEntity();
        b.AddOutboxMessageEntity();
        b.AddOutboxStateEntity();
    }
}