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
/// Définit les opérations disponibles pour le gestionnaire de suppression de fichiers.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<DeleteFileOperation>))]
public enum DeleteFileOperation
{
    /// <summary>Suppression d'un fichier</summary>
    [Description("Deletes a file at the specified path")]
    [Parameters("Path: Full path of the file to delete")]
    DeleteFile
}

/// <summary>
/// Classe de base pour les paramètres de l'outil de suppression de fichiers
/// </summary>
public class DeleteFileParameters
{
    /// <summary>
    /// Opération à effectuer sur le système de fichiers
    /// </summary>
    public required DeleteFileOperation Operation { get; init; }

    /// <summary>
    /// Chemin du fichier à supprimer
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
/// Contexte de sérialisation JSON pour les paramètres de suppression de fichiers.
/// </summary>
[JsonSerializable(typeof(DeleteFileParameters))]
public partial class DeleteFileParametersJsonContext : JsonSerializerContext { }


/// <summary>
/// Gestionnaire des opérations de suppression de fichiers implémentant l'interface outil du protocole MCP.
/// </summary>
public class DeleteFileToolHandler : ToolHandlerBase<DeleteFileParameters>
{
    private readonly ILogger<DeleteFileToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public DeleteFileToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<DeleteFileToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "DeleteFile",
        Description = typeof(DeleteFileOperation).GenerateFullDescription(),
        InputSchema = DeleteFileParametersJsonContext.Default.DeleteFileParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        DeleteFileParametersJsonContext.Default.DeleteFileParameters;

    protected override async Task<CallToolResult> HandleAsync(
        DeleteFileParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());
        _logger.LogInformation("Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                DeleteFileOperation.DeleteFile => await DeleteFileAsync(parameters),             
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

    private async Task<string> DeleteFileAsync(DeleteFileParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.Path))
            throw new ArgumentException("Path is required for DeleteFile operation");

        var validPath = _appConfig.ValidatePath(parameters.Path);
        
        if (!File.Exists(validPath))
            throw new FileNotFoundException($"The file {validPath} does not exist.");

        // Read existing content
        var content = await File.ReadAllTextAsync(validPath);
        _logger.LogInformation("Original file content:\n{content}", content);

        await Task.Run(() => File.Delete(validPath));
        return $"File {validPath} has been successfully deleted.";
    }

    public Task<CallToolResult> TestHandleAsync(
        DeleteFileParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}