using mcp_toolskit.Attributes;
using mcp_toolskit.Extentions;
using mcp_toolskit.Handlers.BraveSearch.Models;
using mcp_toolskit.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.NET.Core.Models.Protocol.Client.Responses;
using ModelContextProtocol.NET.Core.Models.Protocol.Common;
using ModelContextProtocol.NET.Core.Models.Protocol.Shared.Content;
using ModelContextProtocol.NET.Server.Contexts;
using ModelContextProtocol.NET.Server.Features.Tools;
using Serilog.Context;
using Serilog.Core;
using System.ComponentModel;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace mcp_toolskit.Handlers.BraveSearch;

[JsonConverter(typeof(JsonStringEnumConverter<BraveWebSearchOperation>))]
public enum BraveWebSearchOperation
{
    [Description("Performs a web search using the Brave Search API")]
    [Parameters("Query: Search query (max 400 chars, 50 words)\nCount: Number of results (1-20, default 10)\nOffset: Pagination offset (max 9, default 0)")]
    BraveWebSearch
}

public class BraveWebSearchParameters
{
    public required BraveWebSearchOperation Operation { get; init; }
    public required string Query { get; init; }
    public required int? Count { get; init; }
    public required int? Offset { get; init; }

    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (Query != null) sb.Append($", Query: {Query}");
        if (Count.HasValue) sb.Append($", Count: {Count}");
        if (Offset.HasValue) sb.Append($", Offset: {Offset}");
        return sb.ToString();
    }
}

[JsonSerializable(typeof(BraveWebSearchParameters))]
public partial class BraveWebSearchParametersJsonContext : JsonSerializerContext { }

public class BraveWebSearchToolHandler : ToolHandlerBase<BraveWebSearchParameters>
{
    private readonly ILogger<BraveWebSearchToolHandler> _logger;
    private readonly AppConfig _appConfig;
    private readonly IHttpClientFactory _httpClientFactory;

    public BraveWebSearchToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<BraveWebSearchToolHandler> logger,
        AppConfig appConfig,
        IHttpClientFactory httpClientFactory
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
        _httpClientFactory = httpClientFactory;
    }

    private static readonly Tool tool = new()
    {
        Name = "BraveWebSearch",
        Description = typeof(BraveWebSearchOperation).GenerateFullDescription(),
        InputSchema = BraveWebSearchParametersJsonContext.Default.BraveWebSearchParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        BraveWebSearchParametersJsonContext.Default.BraveWebSearchParameters;

    protected override async Task<CallToolResult> HandleAsync(
        BraveWebSearchParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());
        _logger.LogInformation("Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                BraveWebSearchOperation.BraveWebSearch =>
                    await PerformWebSearchAsync(parameters, cancellationToken),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };

            _logger.LogInformation("Result: {content}", content);

            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Brave web search operation");
            throw;
        }
    }

    private async Task<string> PerformWebSearchAsync(BraveWebSearchParameters parameters, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient("BraveSearch");
        httpClient.DefaultRequestHeaders.Add("X-Subscription-Token", _appConfig.BraveSearch.ApiKey);

        var url = new UriBuilder("https://api.search.brave.com/res/v1/web/search");
        var queryParameters = System.Web.HttpUtility.ParseQueryString(string.Empty);
        queryParameters["q"] = parameters.Query;
        queryParameters["count"] = (parameters.Count ?? 10).ToString();
        queryParameters["offset"] = (parameters.Offset ?? 0).ToString();
        url.Query = queryParameters.ToString();

        _logger.LogDebug("Sending request to Brave API: {Url}", url.Uri);

        using var request = new HttpRequestMessage(HttpMethod.Get, url.Uri);
        // Ajout de l'en-tête Accept-Encoding pour indiquer qu'on accepte la compression
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        _logger.LogDebug("Response status: {StatusCode}", response.StatusCode);

        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        _logger.LogDebug("Raw response length: {Length} bytes", bytes.Length);

        string jsonString;

        // Vérification si la réponse est compressée en GZIP
        if (bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
        {
            _logger.LogDebug("Response is GZIP compressed, decompressing...");
            using var memoryStream = new MemoryStream(bytes);
            using var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
            using var decompressedStream = new MemoryStream();
            await gzipStream.CopyToAsync(decompressedStream, cancellationToken);
            jsonString = Encoding.UTF8.GetString(decompressedStream.ToArray());
        }
        else
        {
            jsonString = Encoding.UTF8.GetString(bytes);
        }

        _logger.LogDebug("Decompressed JSON content (first 500 chars): {Content}",
            jsonString.Length > 500 ? jsonString[..500] + "..." : jsonString);

        try
        {
            var options = new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNameCaseInsensitive = true
            };

            var braveWeb = JsonSerializer.Deserialize<BraveWeb>(jsonString, options);

            if (braveWeb?.Web?.Results == null || !braveWeb.Web.Results.Any())
            {
                return "No results found.";
            }

            return string.Join("\n\n", braveWeb.Web.Results.Select(result =>
                $"Title: {result.Title}\nDescription: {result.Description}\nURL: {result.Url}"));
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "JSON deserialization failed. Content-Type: {ContentType}, Content-Length: {ContentLength}",
                response.Content.Headers.ContentType,
                jsonString.Length);
            throw;
        }
    }

    public Task<CallToolResult> TestHandleAsync(
        BraveWebSearchParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}