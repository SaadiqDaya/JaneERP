using System.Net.Http.Headers;
using System.Text.Json;
using JaneERP.Api.Models;

namespace JaneERP.Api.Services;

public class ApiShopifyClient
{
    private readonly HttpClient _http;
    private const string ApiVersion = "2024-10";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiShopifyClient(HttpClient http) => _http = http;

    public async Task<List<ShopifyApiOrder>> GetOrdersAsync(
        string storeDomain,
        string accessToken,
        DateTime? createdAtMin = null)
    {
        var results = new List<ShopifyApiOrder>();
        var url = $"https://{storeDomain}/admin/api/{ApiVersion}/orders.json?limit=250&status=any";
        if (createdAtMin.HasValue)
            url += $"&created_at_min={Uri.EscapeDataString(createdAtMin.Value.ToString("o"))}";

        while (!string.IsNullOrEmpty(url))
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("X-Shopify-Access-Token", accessToken);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var res = await _http.SendAsync(req);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadAsStringAsync();
            var page = JsonSerializer.Deserialize<ShopifyOrdersResponse>(json, JsonOpts);
            if (page?.Orders != null)
                results.AddRange(page.Orders);

            // Follow pagination via Link header
            url = null;
            if (res.Headers.TryGetValues("Link", out var linkValues))
                url = ParseNextLink(linkValues.FirstOrDefault());
        }

        return results;
    }

    // Parses: <https://...>; rel="next", <https://...>; rel="previous"
    private static string? ParseNextLink(string? linkHeader)
    {
        if (string.IsNullOrEmpty(linkHeader)) return null;
        foreach (var part in linkHeader.Split(','))
        {
            var trimmed = part.Trim();
            if (!trimmed.Contains("rel=\"next\"")) continue;
            return trimmed.Split(';')[0].Trim().TrimStart('<').TrimEnd('>');
        }
        return null;
    }
}
