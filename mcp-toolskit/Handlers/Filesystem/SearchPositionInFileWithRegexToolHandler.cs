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
using System.Text.RegularExpressions;

namespace mcp_toolskit.Handlers.Filesystem;

/// <summary>
/// Defines available operations for the regex search handler.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SearchPositionInFileWithRegexOperation>))]
public enum SearchPositionInFileWithRegexOperation
{
    /// <summary>Search regex pattern positions in file</summary>
    [Description("Searches for positions of regex pattern matches in a file")]
    [Parameters(
        "Path: Full path of the file to examine",
        "Regex: Regular expression pattern to search for")]
    SearchPositionInFileWithRegex
}

/// <summary>
/// Base class for regex search tool parameters
/// </summary>
public class SearchPositionInFileWithRegexParameters
{
    /// <summary>
    /// Operation to perform
    /// </summary>
    public required SearchPositionInFileWithRegexOperation Operation { get; init; }

    /// <summary>
    /// Path to the file to examine
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Regular expression pattern to search for
    /// </summary>
    public required string Regex { get; init; }

    /// <summary>
    /// Returns a string representation of the parameters.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (Path != null) sb.Append($", Path: {Path}");
        if (Regex != null) sb.Append($", Regex: {Regex}");
        return sb.ToString();
    }
}

/// <summary>
/// JSON serialization context for regex search parameters.
/// </summary>
[JsonSerializable(typeof(SearchPositionInFileWithRegexParameters))]
public partial class SearchPositionInFileWithRegexParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// Handler for regex search operations implementing the MCP protocol tool interface.
/// </summary>
public class SearchPositionInFileWithRegexToolHandler : ToolHandlerBase<SearchPositionInFileWithRegexParameters>
{
    private readonly ILogger<SearchPositionInFileWithRegexToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public SearchPositionInFileWithRegexToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<SearchPositionInFileWithRegexToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "SearchPositionInFileWithRegex",
        Description = typeof(SearchPositionInFileWithRegexOperation).GenerateFullDescription(),
        InputSchema = SearchPositionInFileWithRegexParametersJsonContext.Default.SearchPositionInFileWithRegexParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        SearchPositionInFileWithRegexParametersJsonContext.Default.SearchPositionInFileWithRegexParameters;

    protected override async Task<CallToolResult> HandleAsync(
        SearchPositionInFileWithRegexParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());
        _logger.LogInformation("Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                SearchPositionInFileWithRegexOperation.SearchPositionInFileWithRegex => await SearchPositionInFileWithRegexAsync(parameters),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };

            _logger.LogInformation("Result: {content}", content);

            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in regex search operation");
            throw;
        }
    }

    private async Task<string> SearchPositionInFileWithRegexAsync(SearchPositionInFileWithRegexParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.Path))
            throw new ArgumentException("Path is required for SearchPositionInFileWithRegex operation");
        if (string.IsNullOrEmpty(parameters.Regex))
            throw new ArgumentException("Regex pattern is required for SearchPositionInFileWithRegex operation");

        var validPath = _appConfig.ValidatePath(parameters.Path);

        // Read file content
        var content = await File.ReadAllTextAsync(validPath);

        // Create and execute regex
        var regex = new Regex(parameters.Regex);
        var matches = regex.Matches(content);

        if (!matches.Any())
            return "No matches found";

        // Build result with positions of each occurrence
        var results = matches.Select(match => new
        {
            Position = match.Index,
            Length = match.Length,
            Value = match.Value
        });

        return string.Join("\n", results.Select(r =>
            $"Position: {r.Position}, Length: {r.Length}, Value: {r.Value}"));
    }

    public Task<CallToolResult> TestHandleAsync(
        SearchPositionInFileWithRegexParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}