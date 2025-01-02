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
/// Définit les opérations disponibles pour le gestionnaire d'écriture de fichier à une position spécifique.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<WriteFileAtPositionOperation>))]
public enum WriteFileAtPositionOperation
{
    /// <summary>Écriture dans un fichier à une position spécifique</summary>
    [Description("Insert content into a file at a specific position")]
    [Parameters(
        "Path: Full path of the file to modify",
        "Content: Content to insert",
        "Position: Insertion position in the file")]
    WriteFileAtPosition
}

/// <summary>
/// Classe de base pour les paramètres de l'outil d'écriture de fichier à une position spécifique
/// </summary>
public class WriteFileAtPositionParameters
{
    /// <summary>
    /// Opération à effectuer
    /// </summary>
    public required WriteFileAtPositionOperation Operation { get; init; }

    /// <summary>
    /// Chemin du fichier à modifier
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Contenu à insérer
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Position d'insertion dans le fichier
    /// </summary>
    public required int Position { get; init; }

    /// <summary>
    /// Retourne une représentation textuelle des paramètres.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        sb.Append($", Path: {Path}");
        sb.Append($", Content Length: {Content?.Length ?? 0}");
        sb.Append($", Position: {Position}");
        return sb.ToString();
    }
}

/// <summary>
/// Contexte de sérialisation JSON pour les paramètres d'écriture de fichier à une position spécifique.
/// </summary>
[JsonSerializable(typeof(WriteFileAtPositionParameters))]
public partial class WriteFileAtPositionParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// Gestionnaire des opérations d'écriture de fichier à une position spécifique implémentant l'interface outil du protocole MCP.
/// </summary>
public class WriteFileAtPositionToolHandler : ToolHandlerBase<WriteFileAtPositionParameters>
{
    private readonly ILogger<WriteFileAtPositionToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public WriteFileAtPositionToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<WriteFileAtPositionToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "WriteFileAtPosition",
        Description = typeof(WriteFileAtPositionOperation).GenerateFullDescription(),
        InputSchema = WriteFileAtPositionParametersJsonContext.Default.WriteFileAtPositionParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        WriteFileAtPositionParametersJsonContext.Default.WriteFileAtPositionParameters;

    protected override async Task<CallToolResult> HandleAsync(
        WriteFileAtPositionParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());
        _logger.LogInformation("Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                WriteFileAtPositionOperation.WriteFileAtPosition => await WriteFileAtPositionAsync(parameters),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };

            _logger.LogInformation("Result: {content}", content);

            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in write file at position operation");
            throw;
        }
    }

   
    private async Task<string> WriteFileAtPositionAsync(WriteFileAtPositionParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.Path))
            throw new ArgumentException("Path is required for WriteFileAtPosition operation");
        if (string.IsNullOrEmpty(parameters.Content))
            throw new ArgumentException("Content is required for WriteFileAtPosition operation");
        if (parameters.Position < 0)
            throw new ArgumentException("Position cannot be negative");

        var validPath = _appConfig.ValidatePath(parameters.Path);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(validPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string existingContent = "";
        if (File.Exists(validPath))
        {
            existingContent = await File.ReadAllTextAsync(validPath);                    
            _logger.LogInformation("Original file content:\n{content}", existingContent);
        }

        string newContent;

        if (parameters.Position > existingContent.Length)
        {
            // Si la position est au-delà de la fin du contenu, on ajoute des espaces
            newContent = existingContent.PadRight(parameters.Position) + parameters.Content;
        }
        else if (parameters.Position == 0)
        {
            // Remplacer le début du texte, si nécessaire
            if (existingContent.StartsWith(parameters.Content))
            {
                newContent = existingContent;
            }
            else
            {
                //newContent = parameters.Content + existingContent.Substring(parameters.Content.Length);
                newContent = parameters.Content +
                     (existingContent.Length > parameters.Content.Length
                         ? existingContent.Substring(parameters.Content.Length)
                         : "");
            }
        }
        else
        {
            // Pour toute autre position, insérer ou remplacer une partie
            newContent = existingContent.Substring(0, parameters.Position) +
                         parameters.Content +
                         existingContent.Substring(parameters.Position);
        }

        await File.WriteAllTextAsync(validPath, newContent);

        _logger.LogInformation("New file content:\n{content}", newContent);

        return $"Successfully wrote content at position {parameters.Position} in {parameters.Path}";
    }




    public Task<CallToolResult> TestHandleAsync(
        WriteFileAtPositionParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}