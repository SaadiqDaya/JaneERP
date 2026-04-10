using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using JaneERP.Models;

namespace JaneERP.Services
{
    public class ShopifyClient
    {
        private readonly HttpClient _http;
        private const string ApiVersion = "2024-10";

        public ShopifyClient(HttpClient? http = null)
        {
            _http = http ?? new HttpClient();
        }

        // If you have API key + password (legacy private app), pass them; otherwise pass nulls and provide the Admin access token.
        public async Task<List<Order>> GetOrdersAsync(
            string storeDomain,
            string accessToken,
            DateTime? createdAtMin = null,
            DateTime? createdAtMax = null,
            decimal? amountMin = null,
            decimal? amountMax = null,
            IProgress<string>? progress = null,
            string? apiKey = null,
            string? apiPassword = null)
        {
            if (string.IsNullOrWhiteSpace(storeDomain))
                throw new ArgumentException("storeDomain is required", nameof(storeDomain));

            var results = new List<Order>();
            // request currency and contact_email so we can show them
            var baseUrl = $"https://{storeDomain}/admin/api/{ApiVersion}/orders.json?limit=250&status=any&fields=id,name,order_number,created_at,total_price,currency,contact_email,shipping_lines";

            if (createdAtMin.HasValue)
                baseUrl += $"&created_at_min={Uri.EscapeDataString(createdAtMin.Value.ToString("o"))}";
            if (createdAtMax.HasValue)
                baseUrl += $"&created_at_max={Uri.EscapeDataString(createdAtMax.Value.ToString("o"))}";

            string? url = baseUrl;
            var retryDelay = 1000;
            var maxRetries = 5;

            while (!string.IsNullOrEmpty(url))
            {
                progress?.Report($"Requesting {url}");
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Accept.Clear();
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));


                // prefer Admin token header if provided
                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    req.Headers.Remove("X-Shopify-Access-Token");
                    req.Headers.Add("X-Shopify-Access-Token", accessToken);
                }
                else if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(apiPassword))
                {
                    var plain = $"{apiKey}:{apiPassword}";
                    var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(plain));
                    req.Headers.Authorization = new AuthenticationHeaderValue("Basic", b64);
                }
                else
                {
                    throw new ArgumentException("Either accessToken or apiKey+apiPassword must be provided for authentication.");
                }

                HttpResponseMessage resp;
                try
                {
                    resp = await _http.SendAsync(req).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Failed to contact Shopify API", ex);
                }

                // detailed handling for non-success so you can see the response
                // detailed handling for non-success so you can see the response
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var msg = $"Shopify API returned {(int)resp.StatusCode} {resp.ReasonPhrase}. Response body: {body}";
                    if (resp.StatusCode == HttpStatusCode.Unauthorized || resp.StatusCode == HttpStatusCode.Forbidden)
                    {
                        throw new UnauthorizedAccessException(msg);
                    }
                    if (resp.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        // exponential backoff on 429
                        await Task.Delay(retryDelay).ConfigureAwait(false);
                        retryDelay = Math.Min(retryDelay * 2, 16000);
                        if (maxRetries-- <= 0)
                            throw new InvalidOperationException("Too many 429 responses from Shopify API.");
                        continue;
                    }
                    throw new InvalidOperationException(msg);
                }

                var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("orders", out var ordersElem))
                {
                    foreach (var o in ordersElem.EnumerateArray())
                    {
                        try
                        {
                            var id = o.GetProperty("id").GetInt64();
                            var name = o.GetProperty("name").GetString();
                            var orderNumber = o.GetProperty("order_number").GetInt32();
                            var createdAt = o.GetProperty("created_at").GetDateTime();
                            var totalPriceString = o.GetProperty("total_price").GetString();
                            decimal totalPrice = 0m;
                            if (!string.IsNullOrEmpty(totalPriceString) &&
                                decimal.TryParse(totalPriceString, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var parsed))
                            {
                                totalPrice = parsed;
                            }

                            string? shippingMethod = null;
                            if (o.TryGetProperty("shipping_lines", out var shippingLines) && shippingLines.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var s in shippingLines.EnumerateArray())
                                {
                                    if (s.TryGetProperty("title", out var titleElem))
                                    {
                                        var title = titleElem.GetString();
                                        if (!string.IsNullOrEmpty(title))
                                        {
                                            shippingMethod = title;
                                            break;
                                        }
                                    }
                                }
                            }

                            // new fields
                            string? currency = null;
                            if (o.TryGetProperty("currency", out var currencyElem))
                                currency = currencyElem.GetString();

                            string? contactEmail = null;
                            if (o.TryGetProperty("contact_email", out var emailElem))
                                contactEmail = emailElem.GetString();
                            else if (o.TryGetProperty("email", out var altEmailElem))
                                contactEmail = altEmailElem.GetString();

                            var order = new Order
                            {
                                Id = id,
                                Name = name,
                                OrderNumber = orderNumber,
                                CreatedAt = createdAt,
                                TotalPrice = totalPrice,
                                ShippingMethod = shippingMethod,
                                Currency = currency,
                                ContactEmail = contactEmail
                            };

                            results.Add(order);
                        }
                        catch
                        {
                            // ignore malformed order entries
                        }
                    }
                }

                // pagination: look for rel="next" in Link header
                url = null;
                if (resp.Headers.TryGetValues("Link", out var links))
                {
                    var linkHeader = string.Join(",", links);
                    var match = Regex.Match(linkHeader, @"<([^>]+)>;\s*rel\s*=\s*""next""", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        url = match.Groups[1].Value;
                    }
                }
            }

            // client-side amount filters
            if (amountMin.HasValue)
                results = results.FindAll(o => o.TotalPrice >= amountMin.Value);
            if (amountMax.HasValue)
                results = results.FindAll(o => o.TotalPrice <= amountMax.Value);

            return results;
        }

        public static async Task SaveOrdersToFileAsync(IEnumerable<Order> orders, string filePath)
        {
            var opts = new JsonSerializerOptions { WriteIndented = true };
            await using var fs = File.Create(filePath);
            await JsonSerializer.SerializeAsync(fs, orders, opts).ConfigureAwait(false);
        }

        // Fetch a single order with full details
        public async Task<OrderDetails> GetOrderAsync(string storeDomain, string accessToken, long orderId, IProgress<string>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(storeDomain))
                throw new ArgumentException("storeDomain is required", nameof(storeDomain));

            var url = $"https://{storeDomain}/admin/api/{ApiVersion}/orders/{orderId}.json";
            progress?.Report($"Requesting {url}");

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Accept.Clear();
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                req.Headers.Remove("X-Shopify-Access-Token");
                req.Headers.Add("X-Shopify-Access-Token", accessToken);
            }
            else
            {
                throw new ArgumentException("accessToken is required for this call", nameof(accessToken));
            }

            HttpResponseMessage resp;
            try
            {
                resp = await _http.SendAsync(req).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to contact Shopify API", ex);
            }

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"Shopify API returned {(int)resp.StatusCode} {resp.ReasonPhrase}. Response body: {body}");
            }

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("order", out var o))
                throw new InvalidOperationException("Response did not contain order");

            var details = new OrderDetails();

            try
            {
                details.Id = o.GetProperty("id").GetInt64();
            }
            catch { details.Id = orderId; }

            if (o.TryGetProperty("name", out var nameElem)) details.Name = nameElem.GetString();
            if (o.TryGetProperty("order_number", out var numElem) && numElem.TryGetInt32(out var num)) details.OrderNumber = num;
            if (o.TryGetProperty("created_at", out var createdElem) && createdElem.ValueKind == JsonValueKind.String)
            {
                if (DateTime.TryParse(createdElem.GetString(), null, DateTimeStyles.RoundtripKind, out var dt)) details.CreatedAt = dt;
            }
            if (o.TryGetProperty("total_price", out var totalElem))
            {
                var s = totalElem.GetString();
                if (!string.IsNullOrEmpty(s) && decimal.TryParse(s, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var tp))
                    details.TotalPrice = tp;
            }
            if (o.TryGetProperty("currency", out var curElem)) details.Currency = curElem.GetString();
            if (o.TryGetProperty("contact_email", out var emailElem)) details.ContactEmail = emailElem.GetString();
            else if (o.TryGetProperty("email", out var altEmail)) details.ContactEmail = altEmail.GetString();

            // customer name
            if (o.TryGetProperty("customer", out var cust) && cust.ValueKind == JsonValueKind.Object)
            {
                var first = cust.TryGetProperty("first_name", out var fn) ? fn.GetString() : null;
                var last = cust.TryGetProperty("last_name", out var ln) ? ln.GetString() : null;
                details.CustomerName = string.Join(" ", new[] { first, last }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
            }

            // shipping address -> format into single string
            if (o.TryGetProperty("shipping_address", out var sa) && sa.ValueKind == JsonValueKind.Object)
            {
                var parts = new List<string>();
                if (sa.TryGetProperty("first_name", out var sfn)) parts.Add(sfn.GetString() ?? "");
                if (sa.TryGetProperty("last_name", out var sln)) parts.Add(sln.GetString() ?? "");
                if (sa.TryGetProperty("address1", out var a1)) parts.Add(a1.GetString() ?? "");
                if (sa.TryGetProperty("address2", out var a2) && !string.IsNullOrWhiteSpace(a2.GetString())) parts.Add(a2.GetString() ?? "");
                if (sa.TryGetProperty("city", out var city)) parts.Add(city.GetString() ?? "");
                if (sa.TryGetProperty("province", out var province)) parts.Add(province.GetString() ?? "");
                if (sa.TryGetProperty("zip", out var zip)) parts.Add(zip.GetString() ?? "");
                if (sa.TryGetProperty("country", out var country)) parts.Add(country.GetString() ?? "");
                details.ShippingAddress = string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            }

            // line items
            if (o.TryGetProperty("line_items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var li in items.EnumerateArray())
                {
                    try
                    {
                        var item = new LineItem();
                        if (li.TryGetProperty("id", out var liId) && liId.TryGetInt64(out var lid)) item.Id = lid;
                        if (li.TryGetProperty("title", out var title)) item.Title = title.GetString();
                        if (li.TryGetProperty("quantity", out var qty) && qty.TryGetInt32(out var q)) item.Quantity = q;
                        if (li.TryGetProperty("price", out var price) && decimal.TryParse(price.GetString(), NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var p)) item.Price = p;
                        if (li.TryGetProperty("sku", out var sku)) item.Sku = sku.GetString();
                        details.LineItems.Add(item);
                    }
                    catch { /* ignore single item parse errors */ }
                }
            }

            return details;
        }
    }
}
