using System.Net;

namespace BiddingService.Services;

public class AuctionClient(HttpClient http) : IAuctionClient
{
    public async Task<AuctionInfo?> GetAuctionAsync(Guid id, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"api/auctions/{id}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AuctionInfo>(ct);
    }
}