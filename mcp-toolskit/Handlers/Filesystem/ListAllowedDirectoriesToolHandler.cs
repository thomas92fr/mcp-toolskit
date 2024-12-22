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
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace mcp_toolskit.Handlers.Filesystem;

/// <summary>
/// Définit les opérations disponibles pour le gestionnaire de liste des répertoires autorisés.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ListAllowedDirectoriesOperation>))]
public enum ListAllowedDirectoriesOperation
{
    /// <summary>Liste les répertoires autorisés</summary>
    [Description("List all directories authorized for file operations")]
    ListAllowedDirectories
}

/// <summary>
/// Classe de base pour les paramètres de l'outil de liste des répertoires autorisés
/// </summary>
public class ListAllowedDirectoriesParameters
{
    /// <summary>
    /// Opération à effectuer
    /// </summary>
    public required ListAllowedDirectoriesOperation Operation { get; init; }

    /// <summary>
    /// Retourne une représentation textuelle des paramètres.
    /// </summary>
    public override string ToString()
    {
        return $"Operation: {Operation}";
    }
}

/// <summary>
/// Contexte de sérialisation JSON pour les paramètres de liste des répertoires autorisés.
/// </summary>
[JsonSerializable(typeof(ListAllowedDirectoriesParameters))]
public partial class ListAllowedDirectoriesParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// Gestionnaire des opérations de liste des répertoires autorisés implémentant l'interface outil du protocole MCP.
/// </summary>
public class ListAllowedDirectoriesToolHandler : ToolHandlerBase<ListAllowedDirectoriesParameters>
{
    private readonly ILogger<ListAllowedDirectoriesToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public ListAllowedDirectoriesToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<ListAllowedDirectoriesToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "ListAllowedDirectories",
        Description = typeof(ListAllowedDirectoriesOperation).GenerateFullDescription(),
        InputSchema = ListAllowedDirectoriesParametersJsonContext.Default.ListAllowedDirectoriesParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        ListAllowedDirectoriesParametersJsonContext.Default.ListAllowedDirectoriesParameters;

    protected override Task<CallToolResult> HandleAsync(
        ListAllowedDirectoriesParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                ListAllowedDirectoriesOperation.ListAllowedDirectories => ListAllowedDirectories(),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };
            return Task.FromResult(new CallToolResult { Content = new Annotated[] { content } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in list allowed directories operation");
            throw;
        }
    }

    private string ListAllowedDirectories()
    {
        return $"Allowed directories:\n{string.Join("\n", _appConfig.AllowedDirectories)}";
    }

    public Task<CallToolResult> TestHandleAsync(
        ListAllowedDirectoriesParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}