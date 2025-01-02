using mcp_toolskit.Attributes;
using mcp_toolskit.Extentions;
using mcp_toolskit.Handlers.BraveSearch.Helpers;
using mcp_toolskit.Handlers.BraveSearch.Models;
using mcp_toolskit.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.NET.Core.Models.Protocol.Client.Responses;
using ModelContextProtocol.NET.Core.Models.Protocol.Common;
using ModelContextProtocol.NET.Core.Models.Protocol.Shared.Content;
using ModelContextProtocol.NET.Server.Contexts;
using ModelContextProtocol.NET.Server.Features.Tools;
using Serilog.Context;
using System.ComponentModel;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace mcp_toolskit.Handlers.BraveSearch;

[JsonConverter(typeof(JsonStringEnumConverter<BraveLocalSearchOperation>))]
public enum BraveLocalSearchOperation
{
    [Description("Searches for local businesses and places using Brave's Local Search API")]
    [Parameters("Query: Local search query (e.g. 'pizza near Central Park')\nCount: Number of results (1-20, default 5)")]
    BraveLocalSearch
}

public class BraveLocalSearchParameters
{
    public required BraveLocalSearchOperation Operation { get; init; }
    public required string Query { get; init; }
    public required int? Count { get; init; }

    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (Query != null) sb.Append($", Query: {Query}");
        if (Count.HasValue) sb.Append($", Count: {Count}");
        return sb.ToString();
    }
}

[JsonSerializable(typeof(BraveLocalSearchParameters))]
public partial class BraveLocalSearchParametersJsonContext : JsonSerializerContext { }

public class BraveLocalSearchToolHandler : ToolHandlerBase<BraveLocalSearchParameters>
{
    private readonly ILogger<BraveLocalSearchToolHandler> _logger;
    private readonly ILogger<BraveWebSearchToolHandler> _weblogger;
    private readonly AppConfig _appConfig;
    private readonly IHttpClientFactory _httpClientFactory;
    

    public BraveLocalSearchToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<BraveLocalSearchToolHandler> logger,
        ILogger<BraveWebSearchToolHandler> weblogger,
        AppConfig appConfig,
        IHttpClientFactory httpClientFactory
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _weblogger = weblogger;
        _appConfig = appConfig;
        _httpClientFactory = httpClientFactory;
    }

    private static readonly Tool tool = new()
    {
        Name = "BraveLocalSearch",
        Description = typeof(BraveLocalSearchOperation).GenerateFullDescription(),
        InputSchema = BraveLocalSearchParametersJsonContext.Default.BraveLocalSearchParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        BraveLocalSearchParametersJsonContext.Default.BraveLocalSearchParameters;

    protected override async Task<CallToolResult> HandleAsync(
        BraveLocalSearchParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());
        _logger.LogInformation("Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                BraveLocalSearchOperation.BraveLocalSearch => await PerformLocalSearchAsync(parameters, cancellationToken),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };

            _logger.LogInformation("Result: {content}", content);

            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Brave local search operation");
            throw;
        }
    }

    private async Task<string> PerformLocalSearchAsync(BraveLocalSearchParameters parameters, CancellationToken cancellationToken)
    {
        return await BraveSearchRateLimiter.Instance.ExecuteAsync(async () =>
        {
            var httpClient = _httpClientFactory.CreateClient("BraveSearch");
            httpClient.DefaultRequestHeaders.Add("X-Subscription-Token", _appConfig.BraveSearch.ApiKey);

            var webUrl = new UriBuilder("https://api.search.brave.com/res/v1/web/search");
            var webQueryParams = System.Web.HttpUtility.ParseQueryString(string.Empty);
            webQueryParams["q"] = parameters.Query;
            webQueryParams["search_lang"] = "en";
            webQueryParams["result_filter"] = "locations";
            webQueryParams["count"] = (parameters.Count ?? 5).ToString();
            webUrl.Query = webQueryParams.ToString();

            _logger.LogDebug("Sending request to Brave API: {Url}", webUrl.Uri);

            using var request = new HttpRequestMessage(HttpMethod.Get, webUrl.Uri);
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

            using var webResponse = await httpClient.SendAsync(request, cancellationToken);
            _logger.LogDebug("Response status: {StatusCode}", webResponse.StatusCode);

            webResponse.EnsureSuccessStatusCode();

            var bytes = await webResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            string webContent;

            if (bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
            {
                _logger.LogDebug("Response is GZIP compressed, decompressing...");
                using var memoryStream = new MemoryStream(bytes);
                using var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
                using var decompressedStream = new MemoryStream();
                await gzipStream.CopyToAsync(decompressedStream, cancellationToken);
                webContent = Encoding.UTF8.GetString(decompressedStream.ToArray());
            }
            else
            {
                webContent = Encoding.UTF8.GetString(bytes);
            }

            _logger.LogDebug("Decompressed content: {Content}",
                webContent.Length > 500 ? webContent[..500] + "..." : webContent);

            var braveWeb = JsonSerializer.Deserialize<BraveWeb>(webContent);

            var locationIds = braveWeb?.Locations?.Results?
                .Where(r => !string.IsNullOrEmpty(r.Id))
                .Select(r => r.Id)
                .ToList() ?? new List<string>();

            if (!locationIds.Any())
            {
                return await new BraveWebSearchToolHandler(
                    ServerContext,
                    SessionContext,
                    _weblogger,
                    _appConfig,
                    _httpClientFactory
                ).TestHandleAsync(new BraveWebSearchParameters
                {
                    Operation = BraveWebSearchOperation.BraveWebSearch,
                    Query = parameters.Query,
                    Count = parameters.Count ?? 5,
                    Offset = 0
                }, cancellationToken).ContinueWith(t =>
                    ((TextContent)t.Result.Content.First()).Text
                );
            }

            var poisTask = BraveSearchRateLimiter.Instance.ExecuteAsync(() => GetPoisDataAsync(locationIds, cancellationToken));
            var descriptionsTask = BraveSearchRateLimiter.Instance.ExecuteAsync(() => GetDescriptionsDataAsync(locationIds, cancellationToken));

            await Task.WhenAll(poisTask, descriptionsTask);
            return FormatLocalResults(await poisTask, await descriptionsTask);
        });
    }

    private async Task<BravePoiResponse> GetPoisDataAsync(List<string> ids, CancellationToken cancellationToken)
    {
        if (!ids.Any()) return new BravePoiResponse();

        var httpClient = _httpClientFactory.CreateClient("BraveSearch");
        httpClient.DefaultRequestHeaders.Add("X-Subscription-Token", _appConfig.BraveSearch.ApiKey);

        var url = new UriBuilder("https://api.search.brave.com/res/v1/local/pois");
        var queryParams = System.Web.HttpUtility.ParseQueryString(string.Empty);
        foreach (var id in ids)
        {
            queryParams.Add("ids", id);
        }
        url.Query = queryParams.ToString();

        _logger.LogDebug("Sending POIs request to: {Url}", url.Uri);

        using var request = new HttpRequestMessage(HttpMethod.Get, url.Uri);
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

        var response = await httpClient.SendAsync(request, cancellationToken);
        _logger.LogDebug("POIs response status: {StatusCode}", response.StatusCode);

        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        string content;

        if (bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
        {
            _logger.LogDebug("POIs response is GZIP compressed, decompressing...");
            using var memoryStream = new MemoryStream(bytes);
            using var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
            using var decompressedStream = new MemoryStream();
            await gzipStream.CopyToAsync(decompressedStream, cancellationToken);
            content = Encoding.UTF8.GetString(decompressedStream.ToArray());
        }
        else
        {
            content = Encoding.UTF8.GetString(bytes);
        }

        _logger.LogDebug("POIs decompressed content: {Content}",
            content.Length > 500 ? content[..500] + "..." : content);

        return JsonSerializer.Deserialize<BravePoiResponse>(content) ?? new BravePoiResponse();
    }

    private async Task<BraveDescription> GetDescriptionsDataAsync(List<string> ids, CancellationToken cancellationToken)
    {
        if (!ids.Any()) return new BraveDescription();

        var httpClient = _httpClientFactory.CreateClient("BraveSearch");
        httpClient.DefaultRequestHeaders.Add("X-Subscription-Token", _appConfig.BraveSearch.ApiKey);

        var url = new UriBuilder("https://api.search.brave.com/res/v1/local/descriptions");
        var queryParams = System.Web.HttpUtility.ParseQueryString(string.Empty);
        foreach (var id in ids)
        {
            queryParams.Add("ids", id);
        }
        url.Query = queryParams.ToString();

        _logger.LogDebug("Sending descriptions request to: {Url}", url.Uri);

        using var request = new HttpRequestMessage(HttpMethod.Get, url.Uri);
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

        var response = await httpClient.SendAsync(request, cancellationToken);
        _logger.LogDebug("Descriptions response status: {StatusCode}", response.StatusCode);

        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        string content;

        if (bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
        {
            _logger.LogDebug("Descriptions response is GZIP compressed, decompressing...");
            using var memoryStream = new MemoryStream(bytes);
            using var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
            using var decompressedStream = new MemoryStream();
            await gzipStream.CopyToAsync(decompressedStream, cancellationToken);
            content = Encoding.UTF8.GetString(decompressedStream.ToArray());
        }
        else
        {
            content = Encoding.UTF8.GetString(bytes);
        }

        _logger.LogDebug("Descriptions decompressed content: {Content}",
            content.Length > 500 ? content[..500] + "..." : content);

        return JsonSerializer.Deserialize<BraveDescription>(content) ?? new BraveDescription();
    }

    private string FormatLocalResults(BravePoiResponse poisData, BraveDescription descData)
    {
        if (poisData.Results.Count == 0)
        {
            return "No local results found";
        }

        return string.Join("\n---\n", poisData.Results.Select(poi =>
        {
            var address = new[]
            {
                poi.Address?.StreetAddress,
                poi.Address?.AddressLocality,
                poi.Address?.AddressRegion,
                poi.Address?.PostalCode
            }
            .Where(part => !string.IsNullOrEmpty(part))
            .ToList();

            return $@"Name: {poi.Name}
Address: {(address.Any() ? string.Join(", ", address) : "N/A")}
Phone: {poi.Phone ?? "N/A"}
Rating: {poi.Rating?.RatingValue?.ToString() ?? "N/A"} ({poi.Rating?.RatingCount?.ToString() ?? "0"} reviews)
Price Range: {poi.PriceRange ?? "N/A"}
Hours: {(poi.OpeningHours?.Any() == true ? string.Join(", ", poi.OpeningHours) : "N/A")}
Description: {(descData.Descriptions.TryGetValue(poi.Id, out var description) ? description : "No description available")}";
        }));
    }

    public Task<CallToolResult> TestHandleAsync(
        BraveLocalSearchParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}