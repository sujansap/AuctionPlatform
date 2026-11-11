using MassTransit;
using RealtimeService.Consumers;
using RealtimeService.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

builder.Services.AddMassTransit(x =>
{
    x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter("realtime", false));
    x.AddConsumer<BidPlacedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbit = builder.Configuration.GetSection("RabbitMq");
        cfg.Host(
            rabbit["Host"] ?? throw new InvalidOperationException("RabbitMq:Host is not configured."),
            "/",
            h =>
            {
                h.Username(rabbit["Username"] ?? throw new InvalidOperationException("RabbitMq:Username is not configured."));
                h.Password(rabbit["Password"] ?? throw new InvalidOperationException("RabbitMq:Password is not configured."));
            });
        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHub<AuctionHub>("/hubs/auctions");

app.Run();

public partial class Program { }