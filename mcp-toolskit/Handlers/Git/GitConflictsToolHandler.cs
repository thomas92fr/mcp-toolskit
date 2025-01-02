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
using LibGit2Sharp;
using Serilog.Context;

namespace mcp_toolskit.Handlers.Git;

/// <summary>
/// Définit les opérations disponibles pour le gestionnaire Git Conflicts.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GitConflictsOperation>))]
public enum GitConflictsOperation
{
    /// <summary>Liste les conflits dans l'index du dépôt</summary>
    [Description("Lists conflicts in the repository index")]
    [Parameters(
        "RepositoryPath: Path to the Git repository"
    )]
    ListConflicts
}

/// <summary>
/// Classe de base pour les paramètres de l'outil Git Conflicts
/// </summary>
public class GitConflictsParameters
{
    /// <summary>
    /// Opération Git à effectuer
    /// </summary>
    public required GitConflictsOperation Operation { get; init; }

    /// <summary>
    /// Chemin du dépôt Git
    /// </summary>
    public required string RepositoryPath { get; init; }

    /// <summary>
    /// Retourne une représentation textuelle des paramètres.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (RepositoryPath != null) sb.Append($", RepositoryPath: {RepositoryPath}");
        return sb.ToString();
    }
}

/// <summary>
/// Contexte de sérialisation JSON pour les paramètres Git Conflicts.
/// </summary>
[JsonSerializable(typeof(GitConflictsParameters))]
public partial class GitConflictsParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// Gestionnaire des opérations Git Conflicts implémentant l'interface outil du protocole MCP.
/// </summary>
public class GitConflictsToolHandler : ToolHandlerBase<GitConflictsParameters>
{
    private readonly ILogger<GitConflictsToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public GitConflictsToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<GitConflictsToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "GitConflicts",
        Description = typeof(GitConflictsOperation).GenerateFullDescription(),
        InputSchema = GitConflictsParametersJsonContext.Default.GitConflictsParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        GitConflictsParametersJsonContext.Default.GitConflictsParameters;

    protected override async Task<CallToolResult> HandleAsync(
        GitConflictsParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());
        _logger.LogInformation("Git Conflicts Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                GitConflictsOperation.ListConflicts => await ListRepositoryConflictsAsync(parameters),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };
            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Git Conflicts operation");
            throw;
        }
    }

    private Task<string> ListRepositoryConflictsAsync(GitConflictsParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.RepositoryPath))
            throw new ArgumentException("Repository path is required for ListConflicts operation");

        var validPath = _appConfig.ValidatePath(parameters.RepositoryPath);

        using (var repo = new Repository(validPath))
        {
            var conflicts = repo.Index.Conflicts;

            if (conflicts == null || !conflicts.Any())
            {
                return Task.FromResult("No conflicts found in the repository.");
            }

            var sb = new StringBuilder("Found the following conflicts:\n\n");

            foreach (var conflict in conflicts)
            {
                sb.AppendLine($"File path: {conflict.Ancestor.Path}");
                sb.AppendLine($"  Ancestor version: {conflict.Ancestor.Id}");
                sb.AppendLine($"  Ours version: {conflict.Ours.Id}");
                sb.AppendLine($"  Theirs version: {conflict.Theirs.Id}");
                sb.AppendLine();
            }

            return Task.FromResult(sb.ToString());
        }
    }

    public Task<CallToolResult> TestHandleAsync(
        GitConflictsParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}