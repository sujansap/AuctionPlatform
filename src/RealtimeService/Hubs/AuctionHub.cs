using Microsoft.AspNetCore.SignalR;

namespace RealtimeService.Hubs;

public class AuctionHub : Hub<IAuctionClient>
{
    public Task JoinAuction(Guid auctionId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(auctionId));

    public Task LeaveAuction(Guid auctionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(auctionId));

    public static string GroupName(Guid auctionId) => $"auction-{auctionId}";
}