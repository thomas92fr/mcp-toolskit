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
/// Defines available operations for the replace function handler.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReplaceFunctionOperation>))]
public enum ReplaceFunctionOperation
{
    /// <summary>Replace function content in source code</summary>
    [Description("Replaces function content in source code using signature and markers")]
    [Parameters(
        "Path: Full path of the file to modify",
        "FunctionSignature: Signature of the function to replace",
        "StartMarkers: Array of possible function start markers",
        "EndMarkers: Array of possible function end markers",
        "NewFunctionCode: New function code content")]
    ReplaceFunction
}

/// <summary>
/// Base class for replace function tool parameters
/// </summary>
public class ReplaceFunctionParameters
{
    /// <summary>
    /// Operation to perform
    /// </summary>
    public required ReplaceFunctionOperation Operation { get; init; }

    /// <summary>
    /// Path of the file to modify
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Function signature to find the function to replace
    /// </summary>
    public required string FunctionSignature { get; init; }

    /// <summary>
    /// Array of possible function start markers
    /// </summary>
    public required string[] StartMarkers { get; init; }

    /// <summary>
    /// Array of possible function end markers
    /// </summary>
    public required string[] EndMarkers { get; init; }

    /// <summary>
    /// New function code content
    /// </summary>
    public required string NewFunctionCode { get; init; }

    /// <summary>
    /// Returns a string representation of the parameters.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (Path != null) sb.Append($", Path: {Path}");
        if (FunctionSignature != null) sb.Append($", FunctionSignature: {FunctionSignature}");
        if (StartMarkers != null) sb.Append($", StartMarkers: [{string.Join(", ", StartMarkers)}]");
        if (EndMarkers != null) sb.Append($", EndMarkers: [{string.Join(", ", EndMarkers)}]");
        if (NewFunctionCode != null) sb.Append($", NewFunctionCode length: {NewFunctionCode.Length}");
        return sb.ToString();
    }
}

/// <summary>
/// JSON serialization context for replace function parameters.
/// </summary>
[JsonSerializable(typeof(ReplaceFunctionParameters))]
public partial class ReplaceFunctionParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// Handler for replace function operations implementing the MCP tool interface.
/// </summary>
public class ReplaceFunctionToolHandler : ToolHandlerBase<ReplaceFunctionParameters>
{
    private readonly ILogger<ReplaceFunctionToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public ReplaceFunctionToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<ReplaceFunctionToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "ReplaceFunction",
        Description = typeof(ReplaceFunctionOperation).GenerateFullDescription(),
        InputSchema = ReplaceFunctionParametersJsonContext.Default.ReplaceFunctionParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        ReplaceFunctionParametersJsonContext.Default.ReplaceFunctionParameters;

    protected override async Task<CallToolResult> HandleAsync(
        ReplaceFunctionParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());
        _logger.LogInformation("Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                ReplaceFunctionOperation.ReplaceFunction => await ReplaceFunctionAsync(parameters),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };

            _logger.LogInformation("Result: {content}", content);

            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in replace function operation");
            throw;
        }
    }

    private async Task<string> ReplaceFunctionAsync(ReplaceFunctionParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.Path))
            throw new ArgumentException("Path is required");
        if (string.IsNullOrEmpty(parameters.FunctionSignature))
            throw new ArgumentException("Function signature is required");
        if (parameters.StartMarkers == null || parameters.StartMarkers.Length == 0)
            throw new ArgumentException("At least one start marker is required");
        if (parameters.EndMarkers == null || parameters.EndMarkers.Length == 0)
            throw new ArgumentException("At least one end marker is required");
        if (string.IsNullOrEmpty(parameters.NewFunctionCode))
            throw new ArgumentException("New function code is required");

        var validPath = _appConfig.ValidatePath(parameters.Path);

        // Read existing content
        var sourceCode = await File.ReadAllTextAsync(validPath);
        _logger.LogInformation("Original file content:\n{content}", sourceCode);

        // Trouver la position de la signature
        var signaturePos = sourceCode.IndexOf(parameters.FunctionSignature);
        if (signaturePos == -1)
        {
            return "Function signature not found";
        }

        // Chercher le marqueur de début le plus proche avant la signature
        var startPositions = parameters.StartMarkers
            .Select(marker => sourceCode.LastIndexOf(marker, signaturePos))
            .Where(pos => pos != -1)
            .ToList();

        if (!startPositions.Any())
        {
            return "Start marker not found";
        }

        var startPos = startPositions.Max();

        // Chercher le marqueur de fin le plus proche après la signature
        var endPositions = parameters.EndMarkers
            .Select(marker => sourceCode.IndexOf(marker, signaturePos))
            .Where(pos => pos != -1)
            .ToList();

        if (!endPositions.Any())
        {
            return "End marker not found";
        }

        var endPos = endPositions.Min();

        // Calculer la position finale du marqueur de fin
        var endMarker = parameters.EndMarkers
            .First(marker => sourceCode.IndexOf(marker, signaturePos) == endPos);
        var finalEndPos = endPos + endMarker.Length;

        // Remplacer le contenu
        var newSourceCode = sourceCode.Substring(0, startPos) + 
                           parameters.NewFunctionCode + 
                           sourceCode.Substring(finalEndPos);

        await File.WriteAllTextAsync(validPath, newSourceCode);

        _logger.LogInformation("New file content:\n{content}", newSourceCode);

        return $"Successfully replaced function in {parameters.Path}";
    }

    public Task<CallToolResult> TestHandleAsync(
        ReplaceFunctionParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}