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

[JsonConverter(typeof(JsonStringEnumConverter<SearchInFilesOperation>))]
public enum SearchInFilesOperation
{
    [Description("Search in files content using a regex pattern")]
    [Parameters(
        "Path: Directory to search in",
        "Pattern: Regex pattern to search in file contents",
        "FileExtensions: Optional array of file extensions to filter (e.g. [\".txt\", \".cs\"])")]
    SearchInFiles
}

public class SearchInFilesParameters
{
    public required SearchInFilesOperation Operation { get; init; }
    public required string Path { get; init; }
    public required string Pattern { get; init; }

    public required string[] FileExtensions { get; init; }  

    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (Path != null) sb.Append($", Path: {Path}");
        if (Pattern != null) sb.Append($", Pattern: {Pattern}");
        if (FileExtensions != null) sb.Append($", Extensions: {string.Join(",", FileExtensions)}");
        return sb.ToString();
    }
}

[JsonSerializable(typeof(SearchInFilesParameters))]
public partial class SearchInFilesParametersJsonContext : JsonSerializerContext { }

public class SearchInFilesToolHandler : ToolHandlerBase<SearchInFilesParameters>
{
    private readonly ILogger<SearchInFilesToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public SearchInFilesToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<SearchInFilesToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "SearchInFiles",
        Description = typeof(SearchInFilesOperation).GenerateFullDescription(),
        InputSchema = SearchInFilesParametersJsonContext.Default.SearchInFilesParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        SearchInFilesParametersJsonContext.Default.SearchInFilesParameters;

    protected override async Task<CallToolResult> HandleAsync(
        SearchInFilesParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());
        _logger.LogInformation("Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                SearchInFilesOperation.SearchInFiles => await SearchInFilesAsync(parameters, cancellationToken),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };
            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in search in files operation");
            throw;
        }
    }

    private async Task<string> SearchInFilesAsync(SearchInFilesParameters parameters, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(parameters.Path))
            throw new ArgumentException("Path is required for SearchInFiles operation");
        if (string.IsNullOrEmpty(parameters.Pattern))
            throw new ArgumentException("Pattern is required for SearchInFiles operation");

        var validPath = _appConfig.ValidatePath(parameters.Path);
        var regex = new Regex(parameters.Pattern, RegexOptions.Compiled);
        var results = new List<string>();

        // Get all files
        var allFiles = Directory.GetFiles(validPath, "*.*", SearchOption.AllDirectories);

        // Filter files by extensions if specified
        var filesToProcess = parameters.FileExtensions != null && parameters.FileExtensions.Length > 0
            ? allFiles.Where(f => parameters.FileExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            : allFiles;

        foreach (var file in filesToProcess)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var filePath = _appConfig.ValidatePath(file);
                var content = await File.ReadAllTextAsync(filePath, cancellationToken);

                if (regex.IsMatch(content))
                {
                    results.Add($"Found match in: {filePath}");

                    // Extract matching lines with context
                    var lines = content.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (regex.IsMatch(lines[i]))
                        {
                            var lineNum = i + 1;
                            results.Add($"  Line {lineNum}: {lines[i].Trim()}");
                        }
                    }
                    results.Add(string.Empty); // Add blank line between files
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing file: {file}", file);
                continue;
            }
        }

        return results.Any()
            ? string.Join("\n", results)
            : "No matches found";
    }

    public Task<CallToolResult> TestHandleAsync(
        SearchInFilesParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}