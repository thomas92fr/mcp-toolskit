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
/// Définit les opérations disponibles pour le gestionnaire de fichiers.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<WriteFileOperation>))]
public enum WriteFileOperation
{
    /// <summary>Écriture dans un fichier</summary>
    [Description("Writes content to a file, overwriting existing content")]
    [Parameters(
        "Path: Complete path of the file to write",
        "Content: Content to write to the file")]
    WriteFile
}

/// <summary>
/// Classe de base pour les paramètres de l'outil système de fichiers
/// </summary>
public class WriteFileParameters
{
    /// <summary>
    /// Opération à effectuer sur le système de fichiers
    /// </summary>
    public required WriteFileOperation Operation { get; init; }

    /// <summary>
    /// Chemin du fichier ou du répertoire (pour les opérations nécessitant un seul chemin)
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Contenu à écrire dans le fichier
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Retourne une représentation textuelle des paramètres.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (Path != null) sb.Append($", Path: {Path}");
        if (Content != null) sb.Append($", Content length: {Content.Length}");
        return sb.ToString();
    }
}

/// <summary>
/// Contexte de sérialisation JSON pour les paramètres du système de fichiers.
/// </summary>
[JsonSerializable(typeof(WriteFileParameters))]
public partial class WriteFileParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// Gestionnaire des opérations du système de fichiers implémentant l'interface outil du protocole MCP.
/// </summary>
public class WriteFileToolHandler : ToolHandlerBase<WriteFileParameters>
{
    private readonly ILogger<WriteFileToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public WriteFileToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<WriteFileToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "WriteFile",
        Description = typeof(WriteFileOperation).GenerateFullDescription(),
        InputSchema = WriteFileParametersJsonContext.Default.WriteFileParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        WriteFileParametersJsonContext.Default.WriteFileParameters;

    protected override async Task<CallToolResult> HandleAsync(
        WriteFileParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());

        _logger.LogInformation("Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                WriteFileOperation.WriteFile => await WriteFileAsync(parameters),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };
            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in filesystem operation");
            throw;
        }
        
        
    }

    private async Task<string> WriteFileAsync(WriteFileParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.Path))
            throw new ArgumentException("Path is required for WriteFile operation");
        if (string.IsNullOrEmpty(parameters.Content))
            throw new ArgumentException("Content is required for WriteFile operation");

        var validPath = _appConfig.ValidatePath(parameters.Path);

        // Read existing content
        var content = await File.ReadAllTextAsync(validPath);
        _logger.LogInformation("Original file content:\n{content}", content);

        await File.WriteAllTextAsync(validPath, parameters.Content);
        _logger.LogInformation("New file content:\n{content}", parameters.Content);
        return $"Successfully wrote to {parameters.Path}";
    }

    public Task<CallToolResult> TestHandleAsync(
        WriteFileParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}