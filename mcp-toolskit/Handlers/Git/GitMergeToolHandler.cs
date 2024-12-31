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
/// Stratégies de résolution des conflits disponibles
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ConflictResolutionStrategy>))]
public enum ConflictResolutionStrategy
{
    /// <summary>Échoue en cas de conflits</summary>
    [Description("Fails when conflicts are detected")]
    Fail,
    
    /// <summary>Utilise les changements de la branche courante</summary>
    [Description("Keep changes from the current branch")]
    UseOurs,
    
    /// <summary>Utilise les changements de la branche à fusionner</summary>
    [Description("Keep changes from the branch being merged")]
    UseTheirs,
    
    /// <summary>Liste les conflits sans les résoudre</summary>
    [Description("Only report conflicts without resolving them")]
    ReportOnly
}

/// <summary>
/// Définit les opérations disponibles pour le gestionnaire Git Merge.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GitMergeOperation>))]
public enum GitMergeOperation
{
    /// <summary>Fusionne une branche dans la branche courante</summary>
    [Description("Merges specified branch into the current branch")]
    [Parameters(
        "RepositoryPath: Path to the Git repository",
        "BranchName: Name of the branch to merge",
        "CommitMessage: Optional commit message for the merge",
        "ConflictStrategy: Strategy for handling merge conflicts (Fail|UseOurs|UseTheirs|ReportOnly)",
        "AbortOnConflict: If true, aborts merge on conflict (default: true)"
    )]
    Merge
}

/// <summary>
/// Classe de base pour les paramètres de l'outil Git Merge
/// </summary>
public class GitMergeParameters
{
    /// <summary>
    /// Opération Git à effectuer
    /// </summary>
    public required GitMergeOperation Operation { get; init; }

    /// <summary>
    /// Chemin du dépôt Git
    /// </summary>
    public required string RepositoryPath { get; init; }

    /// <summary>
    /// Nom de la branche à fusionner
    /// </summary>
    public required string BranchName { get; init; }

    /// <summary>
    /// Message de commit pour la fusion (optionnel)
    /// </summary>
    public required string? CommitMessage { get; init; }

    /// <summary>
    /// Stratégie de résolution des conflits
    /// </summary>
    public required ConflictResolutionStrategy ConflictStrategy { get; init; } = ConflictResolutionStrategy.Fail;

    /// <summary>
    /// Si true, annule la fusion en cas de conflits
    /// </summary>
    public required bool AbortOnConflict { get; init; } = true;

    /// <summary>
    /// Retourne une représentation textuelle des paramètres.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (RepositoryPath != null) sb.Append($", RepositoryPath: {RepositoryPath}");
        if (BranchName != null) sb.Append($", BranchName: {BranchName}");
        if (CommitMessage != null) sb.Append($", CommitMessage: {CommitMessage}");
        sb.Append($", ConflictStrategy: {ConflictStrategy}");
        sb.Append($", AbortOnConflict: {AbortOnConflict}");
        return sb.ToString();
    }
}

/// <summary>
/// Contexte de sérialisation JSON pour les paramètres Git Merge.
/// </summary>
[JsonSerializable(typeof(GitMergeParameters))]
public partial class GitMergeParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// Gestionnaire des opérations Git Merge implémentant l'interface outil du protocole MCP.
/// </summary>
public class GitMergeToolHandler : ToolHandlerBase<GitMergeParameters>
{
    private readonly ILogger<GitMergeToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public GitMergeToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<GitMergeToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "GitMerge",
        Description = typeof(GitMergeOperation).GenerateFullDescription(),
        InputSchema = GitMergeParametersJsonContext.Default.GitMergeParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        GitMergeParametersJsonContext.Default.GitMergeParameters;

    protected override Task<CallToolResult> HandleAsync(
        GitMergeParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Git Merge Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                GitMergeOperation.Merge => MergeBranch(parameters),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };
            return Task.FromResult(new CallToolResult { Content = new Annotated[] { content } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Git Merge operation");
            throw;
        }
    }

    private string MergeBranch(GitMergeParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.RepositoryPath))
            throw new ArgumentException("Repository path is required for Merge operation");

        if (string.IsNullOrEmpty(parameters.BranchName))
            throw new ArgumentException("Branch name is required for Merge operation");

        var validPath = _appConfig.ValidatePath(parameters.RepositoryPath);

        using (var repo = new Repository(validPath))
        {
            // Recherche de la branche à fusionner
            var branchToMerge = repo.Branches[parameters.BranchName]
                ?? throw new ArgumentException($"Branch '{parameters.BranchName}' not found");

            // Configuration de la signature pour le commit de fusion
            var signature = new Signature(
                _appConfig.Git.UserName,
                _appConfig.Git.UserEmail,
                DateTimeOffset.Now
            );

            try
            {
                var mergeOptions = new MergeOptions
                {
                    FastForwardStrategy = FastForwardStrategy.Default,
                    FileConflictStrategy = parameters.ConflictStrategy switch
                    {
                        ConflictResolutionStrategy.UseOurs => CheckoutFileConflictStrategy.Ours,
                        ConflictResolutionStrategy.UseTheirs => CheckoutFileConflictStrategy.Theirs,
                        _ => CheckoutFileConflictStrategy.Normal
                    }
                };

                var mergeResult = repo.Merge(branchToMerge, signature, mergeOptions);

                if (mergeResult.Status == MergeStatus.Conflicts)
                {
                    var conflicts = repo.Index.Conflicts.ToList();
                    var conflictDetails = new StringBuilder();
                    conflictDetails.AppendLine($"Merge conflicts detected ({conflicts.Count} files):");
                    
                    foreach (var conflict in conflicts)
                    {
                        conflictDetails.AppendLine($"- {conflict.Ours.Path}:");
                        if (conflict.Ancestor != null)
                            conflictDetails.AppendLine($"  Base: {conflict.Ancestor.Id}");
                        conflictDetails.AppendLine($"  Ours: {conflict.Ours.Id}");
                        conflictDetails.AppendLine($"  Theirs: {conflict.Theirs.Id}");
                    }

                    switch (parameters.ConflictStrategy)
                    {
                        case ConflictResolutionStrategy.Fail:
                            if (parameters.AbortOnConflict)
                            {
                                repo.Reset(ResetMode.Hard);
                                return $"Merge aborted due to conflicts:\n{conflictDetails}";
                            }
                            return $"Merge halted due to conflicts:\n{conflictDetails}";

                        case ConflictResolutionStrategy.ReportOnly:
                            return conflictDetails.ToString();

                        case ConflictResolutionStrategy.UseOurs:
                        case ConflictResolutionStrategy.UseTheirs:
                            // Stage les fichiers résolus
                            foreach (var conflict in conflicts)
                            {
                                repo.Index.Add(conflict.Ours.Path);
                            }
                            
                            // Crée le commit de merge
                            var commitMessage = parameters.CommitMessage ?? 
                                $"Merge branch '{parameters.BranchName}' with {parameters.ConflictStrategy} conflict resolution";
                            repo.Commit(commitMessage, signature, signature);
                            
                            return $"Merge completed with automatic conflict resolution ({parameters.ConflictStrategy}):\n{conflictDetails}";

                        default:
                            throw new ArgumentException($"Unsupported conflict strategy: {parameters.ConflictStrategy}");
                    }
                }

                string message = mergeResult.Status switch
                {
                    MergeStatus.FastForward => $"Fast-forward merge of '{parameters.BranchName}' completed successfully",
                    MergeStatus.NonFastForward => $"Non-fast-forward merge of '{parameters.BranchName}' completed successfully",
                    MergeStatus.UpToDate => "Already up to date. No merge necessary.",
                    _ => $"Merge completed with status: {mergeResult.Status}"
                };

                return message;
            }
            catch (Exception ex)
            {
                // En cas d'erreur, tente d'annuler la fusion
                try
                {
                    repo.Reset(ResetMode.Hard);
                }
                catch (Exception resetEx)
                {
                    _logger.LogError(resetEx, "Failed to reset repository after merge error");
                }
                
                throw new Exception($"Merge failed: {ex.Message}", ex);
            }
        }
    }

    public Task<CallToolResult> TestHandleAsync(
        GitMergeParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}