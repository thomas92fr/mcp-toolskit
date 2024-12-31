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
/// Définit les opérations disponibles pour le gestionnaire Git Create Branch.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GitCreateBranchOperation>))]
public enum GitCreateBranchOperation
{
    /// <summary>Création d'une nouvelle branche dans un dépôt Git</summary>
    [Description("Creates a new branch in a Git repository")]
    [Parameters(
        "RepositoryPath: Path to the Git repository",
        "Branch: Name for the new branch",
        "FromBranch: Optional: source branch to create from (defaults to the repository's default branch)"
    )]
    CreateBranch
}

/// <summary>
/// Classe de base pour les paramètres de l'outil Git Create Branch
/// </summary>
public class GitCreateBranchParameters
{
    /// <summary>
    /// Opération Git à effectuer
    /// </summary>
    public required GitCreateBranchOperation Operation { get; init; }

    /// <summary>
    /// Chemin du dépôt Git
    /// </summary>
    public required string RepositoryPath { get; init; }

    /// <summary>
    /// Nom de la nouvelle branche
    /// </summary>
    public required string Branch { get; init; }

    /// <summary>
    /// Branche source (optionnel, par défaut: branche par défaut du dépôt)
    /// </summary>
    public required string? FromBranch { get; init; }

    /// <summary>
    /// Retourne une représentation textuelle des paramètres.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (RepositoryPath != null) sb.Append($", RepositoryPath: {RepositoryPath}");
        if (Branch != null) sb.Append($", Branch: {Branch}");
        if (FromBranch != null) sb.Append($", FromBranch: {FromBranch}");
        return sb.ToString();
    }
}

/// <summary>
/// Contexte de sérialisation JSON pour les paramètres Git Create Branch.
/// </summary>
[JsonSerializable(typeof(GitCreateBranchParameters))]
public partial class GitCreateBranchParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// Gestionnaire des opérations Git Create Branch implémentant l'interface outil du protocole MCP.
/// </summary>
public class GitCreateBranchToolHandler : ToolHandlerBase<GitCreateBranchParameters>
{
    private readonly ILogger<GitCreateBranchToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public GitCreateBranchToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<GitCreateBranchToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "GitCreateBranch",
        Description = typeof(GitCreateBranchOperation).GenerateFullDescription(),
        InputSchema = GitCreateBranchParametersJsonContext.Default.GitCreateBranchParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        GitCreateBranchParametersJsonContext.Default.GitCreateBranchParameters;

    protected override async Task<CallToolResult> HandleAsync(
        GitCreateBranchParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Git Create Branch Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                GitCreateBranchOperation.CreateBranch => await CreateBranchAsync(parameters),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };
            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Git Create Branch operation");
            throw;
        }
    }

    private Task<string> CreateBranchAsync(GitCreateBranchParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.RepositoryPath))
            throw new ArgumentException("Repository path is required for Create Branch operation");

        if (string.IsNullOrEmpty(parameters.Branch))
            throw new ArgumentException("Branch name is required for Create Branch operation");

        var validPath = _appConfig.ValidatePath(parameters.RepositoryPath);

        using (var repo = new Repository(validPath))
        {
            // Obtenir la branche source
            var sourceBranch = !string.IsNullOrEmpty(parameters.FromBranch)
                ? repo.Branches[parameters.FromBranch]
                : repo.Head;

            if (sourceBranch == null)
                throw new ArgumentException($"Source branch '{parameters.FromBranch ?? "HEAD"}' not found");

            // Vérifier si la branche existe déjà
            if (repo.Branches[parameters.Branch] != null)
                throw new ArgumentException($"Branch '{parameters.Branch}' already exists");

            // Créer la nouvelle branche
            var newBranch = repo.CreateBranch(parameters.Branch, sourceBranch.Tip);

            return Task.FromResult(
                $"Successfully created branch '{parameters.Branch}' from '{sourceBranch.FriendlyName}'"
            );
        }
    }

    public Task<CallToolResult> TestHandleAsync(
        GitCreateBranchParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}