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
/// Defines the available operations for the directory listing handler.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ListDirectoryOperation>))]
public enum ListDirectoryOperation
{
    /// <summary>Lists the contents of a directory</summary>
    [Description("Lists all files and subdirectories in a specified directory")]
    [Parameters("Path: Full path of the directory to list")]
    ListDirectory
}

/// <summary>
/// Base class for directory listing tool parameters
/// </summary>
public class ListDirectoryParameters
{
    /// <summary>
    /// Operation to perform on the filesystem
    /// </summary>
    public required ListDirectoryOperation Operation { get; init; }

    /// <summary>
    /// Path of the directory to list
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Returns a textual representation of the parameters.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (Path != null) sb.Append($", Path: {Path}");      
        return sb.ToString();
    }
}

/// <summary>
/// JSON serialization context for directory listing parameters.
/// </summary>
[JsonSerializable(typeof(ListDirectoryParameters))]
public partial class ListDirectoryParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// Handler for directory listing operations implementing the MCP protocol tool interface.
/// </summary>
public class ListDirectoryToolHandler : ToolHandlerBase<ListDirectoryParameters>
{
    private readonly ILogger<ListDirectoryToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public ListDirectoryToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<ListDirectoryToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "ListDirectory",
        Description = typeof(ListDirectoryOperation).GenerateFullDescription(),
        InputSchema = ListDirectoryParametersJsonContext.Default.ListDirectoryParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        ListDirectoryParametersJsonContext.Default.ListDirectoryParameters;

    protected override async Task<CallToolResult> HandleAsync(
        ListDirectoryParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());
        _logger.LogInformation("Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                ListDirectoryOperation.ListDirectory => await ListDirectoryAsync(parameters),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };

            _logger.LogInformation("Result: {content}", content);

            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in filesystem operation");
            throw;
        }
    }

    private Task<string> ListDirectoryAsync(ListDirectoryParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.Path))
            throw new ArgumentException("Path is required for ListDirectory operation");

        var validPath = _appConfig.ValidatePath(parameters.Path);
        
        var entries = Directory.GetFileSystemEntries(validPath)
            .Select(entry =>
            {
                var isDirectory = Directory.Exists(entry);
                var name = Path.GetFileName(entry);
                return $"{(isDirectory ? "[DIR]" : "[FILE]")} {name}";
            });
            
        return Task.FromResult(string.Join("\n", entries));
    }

    public Task<CallToolResult> TestHandleAsync(
        ListDirectoryParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}