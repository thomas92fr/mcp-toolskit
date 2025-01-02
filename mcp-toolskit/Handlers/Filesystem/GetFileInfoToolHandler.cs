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
/// Defines operations available for the file info handler.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GetFileInfoOperation>))]
public enum GetFileInfoOperation
{
    /// <summary>Get file or directory metadata</summary>
    [Description("Retrieve detailed metadata about a file or directory")]
    [Parameters("Path: Full path of the file or directory")]
    GetFileInfo
}

/// <summary>
/// Base class for file info tool parameters
/// </summary>
public class GetFileInfoParameters
{
    /// <summary>
    /// Operation to perform on the filesystem
    /// </summary>
    public required GetFileInfoOperation Operation { get; init; }

    /// <summary>
    /// Path of the file or directory
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Returns a string representation of the parameters.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (Path != null) sb.Append($", Path: {Path}");
        return sb.ToString();
    }
}

/// <summary>
/// JSON serialization context for file info parameters.
/// </summary>
[JsonSerializable(typeof(GetFileInfoParameters))]
public partial class GetFileInfoParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// File system operation handler implementing the MCP protocol tool interface.
/// </summary>
public class GetFileInfoToolHandler : ToolHandlerBase<GetFileInfoParameters>
{
    private readonly ILogger<GetFileInfoToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public GetFileInfoToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<GetFileInfoToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "GetFileInfo",
        Description = typeof(GetFileInfoOperation).GenerateFullDescription(),
        InputSchema = GetFileInfoParametersJsonContext.Default.GetFileInfoParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        GetFileInfoParametersJsonContext.Default.GetFileInfoParameters;

    protected override async Task<CallToolResult> HandleAsync(
        GetFileInfoParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());
        _logger.LogInformation("Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                GetFileInfoOperation.GetFileInfo => await GetFileInfoAsync(parameters),
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

    private Task<string> GetFileInfoAsync(GetFileInfoParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.Path))
            throw new ArgumentException("Path is required for GetFileInfo operation");

        var validPath = _appConfig.ValidatePath(parameters.Path);
        var info = new FileInfo(validPath);
        
        var fileInfo = new Dictionary<string, string>
        {
            { "size", info.Length.ToString() },
            { "created", info.CreationTime.ToString() },
            { "modified", info.LastWriteTime.ToString() },
            { "accessed", info.LastAccessTime.ToString() },
            { "isDirectory", info.Attributes.HasFlag(FileAttributes.Directory).ToString() },
            { "isFile", (!info.Attributes.HasFlag(FileAttributes.Directory)).ToString() },
            { "permissions", Convert.ToString((int)(info.Attributes & FileAttributes.Archive), 8) }
        };

        return Task.FromResult(string.Join("\n", fileInfo.Select(kvp => $"{kvp.Key}: {kvp.Value}")));
    }

    public Task<CallToolResult> TestHandleAsync(
        GetFileInfoParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}