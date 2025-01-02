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
/// Définit les opérations disponibles pour le gestionnaire de suppression de branches Git.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GitDeleteBranchOperation>))]
public enum GitDeleteBranchOperation
{
    /// <summary>Supprime une branche du dépôt</summary>
    [Description("Deletes a branch from the repository")]
    [Parameters(
        "RepositoryPath: Path to the Git repository",
        "BranchName: Name of the branch to delete"
    )]
    Delete
}

/// <summary>
/// Classe de base pour les paramètres de l'outil Git Delete Branch
/// </summary>
public class GitDeleteBranchParameters
{
    /// <summary>
    /// Opération Git à effectuer
    /// </summary>
    public required GitDeleteBranchOperation Operation { get; init; }

    /// <summary>
    /// Chemin du dépôt Git
    /// </summary>
    public required string RepositoryPath { get; init; }

    /// <summary>
    /// Nom de la branche à supprimer
    /// </summary>
    public required string BranchName { get; init; }

    /// <summary>
    /// Retourne une représentation textuelle des paramètres.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (RepositoryPath != null) sb.Append($", RepositoryPath: {RepositoryPath}");
        if (BranchName != null) sb.Append($", BranchName: {BranchName}");
        return sb.ToString();
    }
}

/// <summary>
/// Contexte de sérialisation JSON pour les paramètres Git Delete Branch.
/// </summary>
[JsonSerializable(typeof(GitDeleteBranchParameters))]
public partial class GitDeleteBranchParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// Gestionnaire des opérations de suppression de branches Git implémentant l'interface outil du protocole MCP.
/// </summary>
public class GitDeleteBranchToolHandler : ToolHandlerBase<GitDeleteBranchParameters>
{
    private readonly ILogger<GitDeleteBranchToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public GitDeleteBranchToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<GitDeleteBranchToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "GitDeleteBranch",
        Description = typeof(GitDeleteBranchOperation).GenerateFullDescription(),
        InputSchema = GitDeleteBranchParametersJsonContext.Default.GitDeleteBranchParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        GitDeleteBranchParametersJsonContext.Default.GitDeleteBranchParameters;

    protected override async Task<CallToolResult> HandleAsync(
        GitDeleteBranchParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());
        _logger.LogInformation("Git Delete Branch Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                GitDeleteBranchOperation.Delete => await DeleteBranchAsync(parameters),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };

            _logger.LogInformation("Result: {content}", content);

            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Git Delete Branch operation");
            throw;
        }
    }

    private Task<string> DeleteBranchAsync(GitDeleteBranchParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.RepositoryPath))
            throw new ArgumentException("Repository path is required for Delete operation");

        if (string.IsNullOrEmpty(parameters.BranchName))
            throw new ArgumentException("Branch name is required for Delete operation");

        var validPath = _appConfig.ValidatePath(parameters.RepositoryPath);

        using (var repo = new Repository(validPath))
        {
            var branch = repo.Branches[parameters.BranchName]
                ?? throw new ArgumentException($"Branch '{parameters.BranchName}' not found");

            if (branch.IsCurrentRepositoryHead)
                throw new InvalidOperationException("Cannot delete the current HEAD branch");

            repo.Branches.Remove(branch);
            return Task.FromResult($"Successfully deleted branch '{parameters.BranchName}'");
        }
    }

    public Task<CallToolResult> TestHandleAsync(
        GitDeleteBranchParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}