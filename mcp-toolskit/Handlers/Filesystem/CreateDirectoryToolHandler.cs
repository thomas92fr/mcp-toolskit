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
/// Defines the available operations for the directory creation handler.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CreateDirectoryOperation>))]
public enum CreateDirectoryOperation
{
    /// <summary>Creates a new directory</summary>
    [Description("Creates a new directory at a specified path")]
    [Parameters("Path: Full path of the directory to create")]
    CreateDirectory
}

/// <summary>
/// Base class for file system tool parameters
/// </summary>
public class CreateDirectoryParameters
{
    /// <summary>
    /// Operation to perform on the file system
    /// </summary>
    public required CreateDirectoryOperation Operation { get; init; }

    /// <summary>
    /// Path of the file or directory (for operations requiring a single path)
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Returns a text representation of the parameters.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (Path != null) sb.Append($", Path: {Path}");      
        return sb.ToString();
    }
}

/// <summary>
/// JSON serialization context for file system parameters.
/// </summary>
[JsonSerializable(typeof(CreateDirectoryParameters))]
public partial class CreateDirectoryParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// File system operations handler implementing the MCP protocol tool interface.
/// </summary>
public class CreateDirectoryToolHandler : ToolHandlerBase<CreateDirectoryParameters>
{
    private readonly ILogger<CreateDirectoryToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public CreateDirectoryToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<CreateDirectoryToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "CreateDirectory",
        Description = typeof(CreateDirectoryOperation).GenerateFullDescription(),
        InputSchema = CreateDirectoryParametersJsonContext.Default.CreateDirectoryParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        CreateDirectoryParametersJsonContext.Default.CreateDirectoryParameters;

    protected override async Task<CallToolResult> HandleAsync(
        CreateDirectoryParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());
        _logger.LogInformation("Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                CreateDirectoryOperation.CreateDirectory => await CreateDirectoryAsync(parameters),             
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

    private async Task<string> CreateDirectoryAsync(CreateDirectoryParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.Path))
            throw new ArgumentException("Path is required for CreateDirectory operation");

        var validPath = _appConfig.ValidatePath(parameters.Path);
        var parentDirectory = Path.GetDirectoryName(validPath);
        
        if (!string.IsNullOrEmpty(parentDirectory))
        {
            // Valider également le dossier parent
            _appConfig.ValidatePath(parentDirectory);
            // Créer les dossiers parents si nécessaire
            Directory.CreateDirectory(parentDirectory);
        }
        
        Directory.CreateDirectory(validPath);
        return await Task.FromResult($"Successfully created directory {parameters.Path}");
    
    }

    public Task<CallToolResult> TestHandleAsync(
        CreateDirectoryParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}