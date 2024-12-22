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
[JsonConverter(typeof(JsonStringEnumConverter<ReadMultipleFilesOperation>))]
public enum ReadMultipleFilesOperation
{
    /// <summary>Lecture de plusieurs fichiers</summary>
    [Description("Reads the contents of multiple files from specified paths")]
    [Parameters("Paths: List of full paths of files to read")]
    ReadMultipleFiles
}

/// <summary>
/// Classe de base pour les paramètres de l'outil système de fichiers
/// </summary>
public class ReadMultipleFilesParameters
{
    /// <summary>
    /// Opération à effectuer sur le système de fichiers
    /// </summary>
    public required ReadMultipleFilesOperation Operation { get; init; }

    /// <summary>
    /// Liste des chemins de fichiers à lire
    /// </summary>
    public required List<string> Paths { get; init; }

    /// <summary>
    /// Retourne une représentation textuelle des paramètres.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (Paths != null) sb.Append($", Paths: [{string.Join(", ", Paths)}]");
        return sb.ToString();
    }
}


/// <summary>
/// Contexte de sérialisation JSON pour les paramètres du système de fichiers.
/// </summary>
[JsonSerializable(typeof(ReadMultipleFilesParameters))]
public partial class ReadMultipleFilesParametersJsonContext : JsonSerializerContext { }



/// <summary>
/// Gestionnaire des opérations du système de fichiers implémentant l'interface outil du protocole MCP.
/// </summary>
public class ReadMultipleFilesToolHandler : ToolHandlerBase<ReadMultipleFilesParameters>
{
    private readonly ILogger<ReadMultipleFilesToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public ReadMultipleFilesToolHandler(
    IServerContext serverContext,
    ISessionContext sessionContext,
    ILogger<ReadMultipleFilesToolHandler> logger,
    AppConfig appConfig
) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "ReadMultipleFiles",
        Description = typeof(ReadMultipleFilesOperation).GenerateFullDescription(),
        InputSchema = ReadMultipleFilesParametersJsonContext.Default.ReadMultipleFilesParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
       ReadMultipleFilesParametersJsonContext.Default.ReadMultipleFilesParameters;

    protected override async Task<CallToolResult> HandleAsync(
        ReadMultipleFilesParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                ReadMultipleFilesOperation.ReadMultipleFiles => await ReadMultipleFilesAsync(parameters),             
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

    private async Task<string> ReadMultipleFilesAsync(ReadMultipleFilesParameters parameters)
    {
        if (parameters.Paths == null || !parameters.Paths.Any())
            throw new ArgumentException("Paths are required for ReadMultipleFiles operation");

        var results = new List<string>();
        foreach (var path in parameters.Paths)
        {
            try
            {
                var validPath = _appConfig.ValidatePath(path);
                var content = await File.ReadAllTextAsync(validPath);
                results.Add($"{path}:\n{content}");
            }
            catch (Exception ex)
            {
                results.Add($"{path}: Error - {ex.Message}");
            }
        }
        return string.Join("\n---\n", results);
    }

    public Task<CallToolResult> TestHandleAsync(
        ReadMultipleFilesParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}