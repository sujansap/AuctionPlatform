using System.Text;
using BiddingService.Data;
using BiddingService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using BiddingService.Consumers;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<BiddingDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));


builder.Services.AddMassTransit(x =>
{
    x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter("bidding", false));
    x.AddConsumersFromNamespaceContaining<AuctionCreatedConsumer>();

    x.AddEntityFrameworkOutbox<BiddingDbContext>(o =>
    {
        o.QueryDelay = TimeSpan.FromSeconds(1);
        o.UsePostgres();
        o.UseBusOutbox();
    });

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

builder.Services.AddScoped<BidService>();

var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            NameClaimType = "name"
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }