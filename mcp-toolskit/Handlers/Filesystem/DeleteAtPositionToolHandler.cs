using mcp_toolskit.Attributes;
using mcp_toolskit.Extentions;
using mcp_toolskit.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.NET.Core.Models.Protocol.Client.Responses;
using ModelContextProtocol.NET.Core.Models.Protocol.Common;
using ModelContextProtocol.NET.Core.Models.Protocol.Shared.Content;
using ModelContextProtocol.NET.Server.Contexts;
using ModelContextProtocol.NET.Server.Features.Tools;
using System.ComponentModel;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace mcp_toolskit.Handlers.Filesystem;

/// <summary>
/// Defines available operations for the delete at position handler.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<DeleteAtPositionOperation>))]
public enum DeleteAtPositionOperation
{
    /// <summary>Delete content at a specific position</summary>
    [Description("Deletes a specific number of characters at the given position")]
    [Parameters(
        "Path: Full path of the file to modify",
        "Position: Starting position for deletion",
        "Length: Number of characters to delete",
        "PreserveLength: Option to replace with spaces")]
    DeleteAtPosition
}

/// <summary>
/// Base class for delete at position tool parameters
/// </summary>
public class DeleteAtPositionParameters
{
    /// <summary>
    /// Operation to perform
    /// </summary>
    public required DeleteAtPositionOperation Operation { get; init; }

    /// <summary>
    /// File path
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Position in the file where to start deletion
    /// </summary>
    public required int Position { get; init; }

    /// <summary>
    /// Number of characters to delete
    /// </summary>
    public required int Length { get; init; }

    /// <summary>
    /// Option to preserve file length by replacing with spaces
    /// </summary>
    public bool PreserveLength { get; init; }

    /// <summary>
    /// Returns a string representation of the parameters.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (Path != null) sb.Append($", Path: {Path}");
        sb.Append($", Position: {Position}");
        sb.Append($", Length: {Length}");
        sb.Append($", PreserveLength: {PreserveLength}");
        return sb.ToString();
    }
}

/// <summary>
/// JSON serialization context for delete at position parameters.
/// </summary>
[JsonSerializable(typeof(DeleteAtPositionParameters))]
public partial class DeleteAtPositionParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// Handler for delete at position operations implementing the MCP protocol tool interface.
/// </summary>
public class DeleteAtPositionToolHandler : ToolHandlerBase<DeleteAtPositionParameters>
{
    private readonly ILogger<DeleteAtPositionToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public DeleteAtPositionToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<DeleteAtPositionToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "DeleteAtPosition",
        Description = typeof(DeleteAtPositionOperation).GenerateFullDescription(),
        InputSchema = DeleteAtPositionParametersJsonContext.Default.DeleteAtPositionParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        DeleteAtPositionParametersJsonContext.Default.DeleteAtPositionParameters;

    protected override async Task<CallToolResult> HandleAsync(
        DeleteAtPositionParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                DeleteAtPositionOperation.DeleteAtPosition => await DeleteAtPositionAsync(parameters),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };
            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in delete at position operation");
            throw;
        }
    }

    private async Task<string> DeleteAtPositionAsync(DeleteAtPositionParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.Path))
            throw new ArgumentException("Path is required for DeleteAtPosition operation");
        if (parameters.Length <= 0)
            throw new ArgumentException("Length must be positive for DeleteAtPosition operation");
        if (parameters.Position < 0)
            throw new ArgumentException("Position cannot be negative");

        var validPath = _appConfig.ValidatePath(parameters.Path);

        // Read existing content
        var content = await File.ReadAllTextAsync(validPath);

        if (parameters.Position >= content.Length)
            throw new ArgumentException("Position is beyond end of file");

        // Calculate effective length to delete
        var effectiveLength = Math.Min(parameters.Length, content.Length - parameters.Position);

        string newContent;
        if (parameters.PreserveLength)
        {
            // Replace with spaces if we need to preserve length
            var spaces = new string(' ', effectiveLength);
            newContent = content.Remove(parameters.Position, effectiveLength)
                              .Insert(parameters.Position, spaces);
        }
        else
        {
            // Otherwise, simply delete the content
            newContent = content.Remove(parameters.Position, effectiveLength);
        }

        await File.WriteAllTextAsync(validPath, newContent);

        return $"Successfully deleted {effectiveLength} characters at position {parameters.Position} in {parameters.Path}";
    }

    public Task<CallToolResult> TestHandleAsync(
        DeleteAtPositionParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}