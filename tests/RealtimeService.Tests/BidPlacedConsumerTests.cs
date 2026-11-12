using Contracts;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using RealtimeService.Consumers;
using RealtimeService.Hubs;

namespace RealtimeService.Tests;

public class BidPlacedConsumerTests
{
    private readonly IHubClients<IAuctionClient> _clients = Substitute.For<IHubClients<IAuctionClient>>();
    private readonly BidPlacedConsumer _sut;

    public BidPlacedConsumerTests()
    {
        var hub = Substitute.For<IHubContext<AuctionHub, IAuctionClient>>();
        hub.Clients.Returns(_clients);
        _sut = new BidPlacedConsumer(hub);
    }

    [Fact]
    public async Task Consume_SendsBidToTheGroupOfThatAuction()
    {
        var auctionId = Guid.NewGuid();
        var watchers = Substitute.For<IAuctionClient>();
        _clients.Group(AuctionHub.GroupName(auctionId)).Returns(watchers);

        await _sut.Consume(ContextFor(new BidPlaced(
            Guid.NewGuid(), auctionId, "bob", 120m, DateTime.UtcNow)));

        await watchers.Received(1).BidPlaced(
            Arg.Is<BidNotification>(n => n.AuctionId == auctionId && n.Bidder == "bob" && n.Amount == 120m));
    }

    [Fact]
    public async Task Consume_DoesNotSendToOtherAuctionsGroups()
    {
        var watchedAuction = Guid.NewGuid();
        var otherAuction = Guid.NewGuid();
        var others = Substitute.For<IAuctionClient>();
        _clients.Group(AuctionHub.GroupName(otherAuction)).Returns(others);
        _clients.Group(AuctionHub.GroupName(watchedAuction)).Returns(Substitute.For<IAuctionClient>());

        await _sut.Consume(ContextFor(new BidPlaced(
            Guid.NewGuid(), watchedAuction, "bob", 120m, DateTime.UtcNow)));

        await others.DidNotReceive().BidPlaced(Arg.Any<BidNotification>());
    }

    private static ConsumeContext<BidPlaced> ContextFor(BidPlaced message)
    {
        var context = Substitute.For<ConsumeContext<BidPlaced>>();
        context.Message.Returns(message);
        return context;
    }
}