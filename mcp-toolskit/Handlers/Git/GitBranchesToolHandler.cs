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

namespace mcp_toolskit.Handlers.Git;

/// <summary>
/// Définit les opérations disponibles pour le gestionnaire Git Branches.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GitBranchesOperation>))]
public enum GitBranchesOperation
{
    /// <summary>Liste toutes les branches d'un dépôt</summary>
    [Description("Lists all branches in a repository")]
    [Parameters(
        "RepositoryPath: Path to the Git repository",
        "IncludeRemote: Include remote branches (default: true)"
    )]
    ListBranches
}

/// <summary>
/// Classe de base pour les paramètres de l'outil Git Branches
/// </summary>
public class GitBranchesParameters
{
    /// <summary>
    /// Opération Git à effectuer
    /// </summary>
    public required GitBranchesOperation Operation { get; init; }

    /// <summary>
    /// Chemin du dépôt Git
    /// </summary>
    public required string RepositoryPath { get; init; }

    /// <summary>
    /// Inclure les branches distantes
    /// </summary>
    public bool IncludeRemote { get; init; } = true;

    /// <summary>
    /// Retourne une représentation textuelle des paramètres.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (RepositoryPath != null) sb.Append($", RepositoryPath: {RepositoryPath}");
        sb.Append($", IncludeRemote: {IncludeRemote}");
        return sb.ToString();
    }
}

/// <summary>
/// Contexte de sérialisation JSON pour les paramètres Git Branches.
/// </summary>
[JsonSerializable(typeof(GitBranchesParameters))]
public partial class GitBranchesParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// Gestionnaire des opérations Git Branches implémentant l'interface outil du protocole MCP.
/// </summary>
public class GitBranchesToolHandler : ToolHandlerBase<GitBranchesParameters>
{
    private readonly ILogger<GitBranchesToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public GitBranchesToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<GitBranchesToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "GitBranches",
        Description = typeof(GitBranchesOperation).GenerateFullDescription(),
        InputSchema = GitBranchesParametersJsonContext.Default.GitBranchesParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        GitBranchesParametersJsonContext.Default.GitBranchesParameters;

    protected override async Task<CallToolResult> HandleAsync(
        GitBranchesParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Git Branches Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                GitBranchesOperation.ListBranches => await ListBranchesAsync(parameters),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };
            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Git Branches operation");
            throw;
        }
    }

    private Task<string> ListBranchesAsync(GitBranchesParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.RepositoryPath))
            throw new ArgumentException("Repository path is required for ListBranches operation");

        var validPath = _appConfig.ValidatePath(parameters.RepositoryPath);

        using (var repo = new Repository(validPath))
        {
            var branches = new StringBuilder();
            branches.AppendLine("Branches in repository:");

            // Récupération des branches locales
            branches.AppendLine("\nLocal branches:");
            foreach (var branch in repo.Branches.Where(b => !b.IsRemote))
            {
                branches.AppendLine($"- {branch.FriendlyName} ({branch.Tip?.Sha[..7] ?? "No commits"})");
                if (branch.IsCurrentRepositoryHead)
                {
                    branches.AppendLine("  * Current HEAD");
                }
            }

            // Récupération des branches distantes si demandé
            if (parameters.IncludeRemote)
            {
                branches.AppendLine("\nRemote branches:");
                foreach (var branch in repo.Branches.Where(b => b.IsRemote))
                {
                    branches.AppendLine($"- {branch.FriendlyName} ({branch.Tip?.Sha[..7] ?? "No commits"})");
                }
            }

            return Task.FromResult(branches.ToString());
        }
    }

    public Task<CallToolResult> TestHandleAsync(
        GitBranchesParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}