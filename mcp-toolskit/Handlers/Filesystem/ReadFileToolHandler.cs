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
/// Définit les opérations disponibles pour le gestionnaire de fichiers.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReadFileOperation>))]
public enum ReadFileOperation
{
    /// <summary>Lecture du contenu d'un fichier</summary>
    [Description("Reads the content of a file from a specified path")]
    [Parameters("Path: Full path of the file to read")]
    ReadFile
}

/// <summary>
/// Classe de base pour les paramètres de l'outil système de fichiers
/// </summary>
public class ReadFileParameters
{
    /// <summary>
    /// Opération à effectuer sur le système de fichiers
    /// </summary>
    public required ReadFileOperation Operation { get; init; }

    /// <summary>
    /// Chemin du fichier ou du répertoire (pour les opérations nécessitant un seul chemin)
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Retourne une représentation textuelle des paramètres.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (Path != null) sb.Append($", Path: {Path}");      
        return sb.ToString();
    }
}


/// <summary>
/// Contexte de sérialisation JSON pour les paramètres du système de fichiers.
/// </summary>
[JsonSerializable(typeof(ReadFileParameters))]
public partial class ReadFileParametersJsonContext : JsonSerializerContext { }



/// <summary>
/// Gestionnaire des opérations du système de fichiers implémentant l'interface outil du protocole MCP.
/// </summary>
public class ReadFileToolHandler : ToolHandlerBase<ReadFileParameters>
{
    private readonly ILogger<ReadFileToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public ReadFileToolHandler(
    IServerContext serverContext,
    ISessionContext sessionContext,
    ILogger<ReadFileToolHandler> logger,
    AppConfig appConfig
) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "ReadFile",
        Description = typeof(ReadFileOperation).GenerateFullDescription(),
        InputSchema = ReadFileParametersJsonContext.Default.ReadFileParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
       ReadFileParametersJsonContext.Default.ReadFileParameters;

    protected override async Task<CallToolResult> HandleAsync(
        ReadFileParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                ReadFileOperation.ReadFile => await ReadFileAsync(parameters),             
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

    private async Task<string> ReadFileAsync(ReadFileParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.Path))
            throw new ArgumentException("Path is required for ReadFile operation");

        var validPath = _appConfig.ValidatePath(parameters.Path);
        return await File.ReadAllTextAsync(validPath);
    }

    public Task<CallToolResult> TestHandleAsync(
        ReadFileParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}