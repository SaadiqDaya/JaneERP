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

            var res = await SendWithRetryAsync(req);
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

    // Retries the request on 429 Rate Limited responses with exponential backoff.
    // HttpRequestMessage can only be sent once, so requests are cloned on retry.
    private async Task<HttpResponseMessage> SendWithRetryAsync(HttpRequestMessage request, int maxRetries = 3)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            var req = attempt == 0 ? request : await CloneRequestAsync(request);
            var response = await _http.SendAsync(req);

            if ((int)response.StatusCode == 429)
            {
                if (attempt == maxRetries) return response; // give up, caller will EnsureSuccessStatusCode
                int waitSeconds = 2;
                if (response.Headers.TryGetValues("Retry-After", out var vals) &&
                    int.TryParse(vals.FirstOrDefault(), out var ra))
                    waitSeconds = ra;
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(waitSeconds, 30)));
                continue;
            }
            return response;
        }
        throw new InvalidOperationException("Retry loop exited unexpectedly");
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage req)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri);
        foreach (var header in req.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        if (req.Content != null)
        {
            var body = await req.Content.ReadAsStringAsync();
            clone.Content = new StringContent(body, System.Text.Encoding.UTF8,
                req.Content.Headers.ContentType?.MediaType ?? "application/json");
        }
        return clone;
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
