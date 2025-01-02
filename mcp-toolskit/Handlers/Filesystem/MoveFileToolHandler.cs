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
/// Defines the available operations for the file manager.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MoveFileOperation>))]
public enum MoveFileOperation
{
    /// <summary>Move/rename a file</summary>
    [Description("Move or rename a file from one location to another")]
    [Parameters(
        "Source: Source path of the file",
        "Destination: Destination path for the file")]
    MoveFile
}

/// <summary>
/// Base class for filesystem tool parameters
/// </summary>
public class MoveFileParameters
{
    /// <summary>
    /// Operation to perform on the filesystem
    /// </summary>
    public required MoveFileOperation Operation { get; init; }

    /// <summary>
    /// Source path (for MoveFile operation)
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Destination path (for MoveFile operation)
    /// </summary>
    public required string Destination { get; init; }

    /// <summary>
    /// Returns a text representation of the parameters.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (Source != null) sb.Append($", Source: {Source}");
        if (Destination != null) sb.Append($", Destination: {Destination}");
        return sb.ToString();
    }
}

/// <summary>
/// JSON serialization context for filesystem parameters.
/// </summary>
[JsonSerializable(typeof(MoveFileParameters))]
public partial class MoveFileParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// Handler for filesystem operations implementing the MCP protocol tool interface.
/// </summary>
public class MoveFileToolHandler : ToolHandlerBase<MoveFileParameters>
{
    private readonly ILogger<MoveFileToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public MoveFileToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<MoveFileToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "MoveFile",
        Description = typeof(MoveFileOperation).GenerateFullDescription(),
        InputSchema = MoveFileParametersJsonContext.Default.MoveFileParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        MoveFileParametersJsonContext.Default.MoveFileParameters;

    protected override async Task<CallToolResult> HandleAsync(
        MoveFileParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());
        _logger.LogInformation("Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                MoveFileOperation.MoveFile => await MoveFileAsync(parameters),
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

    private Task<string> MoveFileAsync(MoveFileParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.Source))
            throw new ArgumentException("Source path is required for MoveFile operation");
        if (string.IsNullOrEmpty(parameters.Destination))
            throw new ArgumentException("Destination path is required for MoveFile operation");

        var validSourcePath = _appConfig.ValidatePath(parameters.Source);
        var validDestPath = _appConfig.ValidatePath(parameters.Destination);
       
        _logger.LogInformation("Original file source: {validSourcePath}", validSourcePath);
        _logger.LogInformation("file dest.: {validDestPath}", validDestPath);

        File.Move(validSourcePath, validDestPath);
        return Task.FromResult($"Successfully moved {parameters.Source} to {parameters.Destination}");
    }

    public Task<CallToolResult> TestHandleAsync(
        MoveFileParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}