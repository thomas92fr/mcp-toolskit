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
/// Defines available operations for the search and replace handler.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SearchAndReplaceOperation>))]
public enum SearchAndReplaceOperation
{
    /// <summary>Search and replace content using regex</summary>
    [Description("Replaces occurrences of a regular expression with replacement content")]
    [Parameters(
        "Path: Full path of the file to modify",
        "Regex: Regular expression pattern to search for",
        "Replacement: Replacement content",
        "PreserveLength: Option to preserve file length")]
    SearchAndReplace
}

/// <summary>
/// Base class for search and replace tool parameters
/// </summary>
public class SearchAndReplaceParameters
{
    /// <summary>
    /// Operation to perform
    /// </summary>
    public required SearchAndReplaceOperation Operation { get; init; }

    /// <summary>
    /// Path of the file to modify
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Regular expression pattern to search for
    /// </summary>
    public required string Regex { get; init; }

    /// <summary>
    /// Replacement content
    /// </summary>
    public required string Replacement { get; init; }

    /// <summary>
    /// Option to preserve file length by padding replacements
    /// </summary>
    public bool PreserveLength { get; init; }

    /// <summary>
    /// Returns a string representation of the parameters.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (Path != null) sb.Append($", Path: {Path}");
        if (Regex != null) sb.Append($", Regex: {Regex}");
        if (Replacement != null) sb.Append($", Replacement: {Replacement}");
        sb.Append($", PreserveLength: {PreserveLength}");
        return sb.ToString();
    }
}


/// <summary>
/// JSON serialization context for search and replace parameters.
/// </summary>
[JsonSerializable(typeof(SearchAndReplaceParameters))]
public partial class SearchAndReplaceParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// Handler for search and replace operations implementing the MCP tool interface.
/// </summary>
public class SearchAndReplaceToolHandler : ToolHandlerBase<SearchAndReplaceParameters>
{
    private readonly ILogger<SearchAndReplaceToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public SearchAndReplaceToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<SearchAndReplaceToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "SearchAndReplace",
        Description = typeof(SearchAndReplaceOperation).GenerateFullDescription(),
        InputSchema = SearchAndReplaceParametersJsonContext.Default.SearchAndReplaceParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        SearchAndReplaceParametersJsonContext.Default.SearchAndReplaceParameters;

    protected override async Task<CallToolResult> HandleAsync(
        SearchAndReplaceParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());
        _logger.LogInformation("Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                SearchAndReplaceOperation.SearchAndReplace => await SearchAndReplaceAsync(parameters),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };

            _logger.LogInformation("Result: {content}", content);

            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in search and replace operation");
            throw;
        }
    }

    private async Task<string> SearchAndReplaceAsync(SearchAndReplaceParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.Path))
            throw new ArgumentException("Path is required for SearchAndReplace operation");
        if (string.IsNullOrEmpty(parameters.Regex))
            throw new ArgumentException("Regex pattern is required for SearchAndReplace operation");
        if (string.IsNullOrEmpty(parameters.Replacement))
            throw new ArgumentException("Replacement content is required for SearchAndReplace operation");

        var validPath = _appConfig.ValidatePath(parameters.Path);

        // Read existing content
        var content = await File.ReadAllTextAsync(validPath);
        _logger.LogInformation("Original file content:\n{content}", content);

        var regex = new System.Text.RegularExpressions.Regex(parameters.Regex);
        var matches = regex.Matches(content);

        if (!matches.Any())
            return "No matches found";

        // Perform replacements
        string newContent;
        if (parameters.PreserveLength)
        {
            // Length preservation mode
            newContent = content;
            foreach (System.Text.RegularExpressions.Match match in matches.Reverse()) // Start from end to avoid shifting positions
            {
                var replacement = parameters.Replacement.PadRight(match.Length);
                if (replacement.Length > match.Length)
                    replacement = replacement.Substring(0, match.Length);

                newContent = newContent.Remove(match.Index, match.Length)
                                     .Insert(match.Index, replacement);
            }
        }
        else
        {
            // Normal replacement mode
            newContent = regex.Replace(content, parameters.Replacement);
        }

        await File.WriteAllTextAsync(validPath, newContent);

        _logger.LogInformation("New file content:\n{content}", newContent);

        return $"Successfully replaced {matches.Count} occurrences in {parameters.Path}";
    }

    public Task<CallToolResult> TestHandleAsync(
        SearchAndReplaceParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}