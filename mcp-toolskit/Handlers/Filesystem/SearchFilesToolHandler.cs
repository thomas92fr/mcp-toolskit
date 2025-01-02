using mcp_toolskit.Attributes;
using mcp_toolskit.Extentions;
using mcp_toolskit.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.NET.Core.Models.Protocol.Client.Responses;
using ModelContextProtocol.NET.Core.Models.Protocol.Common;
using ModelContextProtocol.NET.Core.Models.Protocol.Shared.Content;
using ModelContextProtocol.NET.Server.Contexts;
using ModelContextProtocol.NET.Server.Features.Tools;
using Serilog.Context;
using System.ComponentModel;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;


namespace mcp_toolskit.Handlers.Filesystem;


/// <summary>
/// Defines the available operations for the search files handler.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SearchFilesOperation>))]
public enum SearchFilesOperation
{
    /// <summary>Search for files</summary>
    [Description("Search for files matching a pattern in a directory")]
    [Parameters(
        "Path: Directory to search in",
        "Pattern: Search pattern for files")]
    SearchFiles
}

/// <summary>
/// Base class for search files tool parameters
/// </summary>
public class SearchFilesParameters
{
    /// <summary>
    /// Operation to perform with the search files tool
    /// </summary>
    public required SearchFilesOperation Operation { get; init; }

    /// <summary>
    /// Directory path to search in
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Search pattern to match files
    /// </summary>
    public required string Pattern { get; init; }

    /// <summary>
    /// Returns a string representation of the parameters.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (Path != null) sb.Append($", Path: {Path}");
        if (Pattern != null) sb.Append($", Pattern: {Pattern}");
        return sb.ToString();
    }
}


/// <summary>
/// JSON serialization context for search files parameters.
/// </summary>
[JsonSerializable(typeof(SearchFilesParameters))]
public partial class SearchFilesParametersJsonContext : JsonSerializerContext { }


/// <summary>
/// Handler for search files operations implementing the MCP tool interface.
/// </summary>
public class SearchFilesToolHandler : ToolHandlerBase<SearchFilesParameters>
{
    private readonly ILogger<SearchFilesToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public SearchFilesToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<SearchFilesToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "SearchFiles",
        Description = typeof(SearchFilesOperation).GenerateFullDescription(),
        InputSchema = SearchFilesParametersJsonContext.Default.SearchFilesParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        SearchFilesParametersJsonContext.Default.SearchFilesParameters;

    protected override async Task<CallToolResult> HandleAsync(
        SearchFilesParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());
        _logger.LogInformation("Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                SearchFilesOperation.SearchFiles => await SearchFilesAsync(parameters),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };
            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in search files operation");
            throw;
        }
    }

    private Task<string> SearchFilesAsync(SearchFilesParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.Path))
            throw new ArgumentException("Path is required for SearchFiles operation");
        if (string.IsNullOrEmpty(parameters.Pattern))
            throw new ArgumentException("Pattern is required for SearchFiles operation");

        var validPath = _appConfig.ValidatePath(parameters.Path);
        var results = Directory.GetFiles(validPath, $"*{parameters.Pattern}*", SearchOption.AllDirectories)
            .Where(file =>
            {
                try
                {
                    _appConfig.ValidatePath(file);
                    return true;
                }
                catch
                {
                    return false;
                }
            });

        var fileList = results.ToList();
        return Task.FromResult(fileList.Any()
            ? string.Join("\n", fileList)
            : "No matches found");
    }

    public Task<CallToolResult> TestHandleAsync(
        SearchFilesParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}