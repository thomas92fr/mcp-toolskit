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
/// Définit les opérations disponibles pour le gestionnaire Git Merge.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GitMergeOperation>))]
public enum GitMergeOperation
{
    /// <summary>Fusionne une branche dans la branche courante</summary>
    [Description("Merges specified branch into the current branch, aborts merge on conflict")]
    [Parameters(
        "RepositoryPath: Path to the Git repository",
        "BranchName: Name of the branch to merge",
        "CommitMessage: Optional commit message for the merge"
    )]
    MergeAbortOnConflict,
    /// <summary>Fusionne une branche dans la branche courante</summary>
    [Description("Merges specified branch into the current branch, Only report conflicts without resolving them")]
    [Parameters(
        "RepositoryPath: Path to the Git repository",
        "BranchName: Name of the branch to merge",
        "CommitMessage: Optional commit message for the merge"
    )]
    MergeReportOnly
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
    /// Retourne une représentation textuelle des paramètres.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (RepositoryPath != null) sb.Append($", RepositoryPath: {RepositoryPath}");
        if (BranchName != null) sb.Append($", BranchName: {BranchName}");
        if (CommitMessage != null) sb.Append($", CommitMessage: {CommitMessage}");
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
                GitMergeOperation.MergeAbortOnConflict => MergeBranch(parameters, true),
                GitMergeOperation.MergeReportOnly => MergeBranch(parameters, false),
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

    private string MergeBranch(GitMergeParameters parameters, bool abortOnConflict)
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
                    FileConflictStrategy = CheckoutFileConflictStrategy.Normal
                };

                var mergeResult = repo.Merge(branchToMerge, signature, mergeOptions);

                if (mergeResult.Status == MergeStatus.Conflicts)
                {
                    var conflicts = repo.Index.Conflicts.ToList();
                    var conflictDetails = new StringBuilder();
                    conflictDetails.AppendLine($"Merge conflicts detected ({conflicts.Count} files):");

                    foreach (var conflict in conflicts)
                    {
                        conflictDetails.AppendLine($"\nFile: {conflict.Ours.Path}");

                        try
                        {
                            // On récupère les trees des branches
                            var currentBranch = repo.Head;
                            var targetBranch = repo.Branches[parameters.BranchName];

                            var oursTree = currentBranch.Tip.Tree;
                            var theirsTree = targetBranch.Tip.Tree;

                            // Liste des fichiers à comparer
                            var paths = new List<string> { conflict.Ours.Path };

                            // Création du patch
                            var patch = repo.Diff.Compare<Patch>(oursTree, theirsTree, paths);

                            conflictDetails.AppendLine("Changes:");
                            if (!string.IsNullOrEmpty(patch.Content))
                            {
                                var lines = patch.Content.Split('\n');
                                foreach (var line in lines)
                                {
                                    if (line.StartsWith("+") && !line.StartsWith("+++"))
                                        conflictDetails.AppendLine($"\u001b[32m{line}\u001b[0m"); // Vert pour les ajouts
                                    else if (line.StartsWith("-") && !line.StartsWith("---"))
                                        conflictDetails.AppendLine($"\u001b[31m{line}\u001b[0m"); // Rouge pour les suppressions
                                    else
                                        conflictDetails.AppendLine(line);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            conflictDetails.AppendLine($"Unable to compute detailed changes: {ex.Message}");
                        }

                        conflictDetails.AppendLine(new string('-', 80)); // Séparateur
                    }

                    // On annule la fusion
                    repo.Reset(ResetMode.Hard);

                    return abortOnConflict
                        ? $"Merge aborted due to conflicts:\n{conflictDetails}"
                        : $"Merge conflicts details:\n{conflictDetails}";
                }

                // Si pas de conflits, on peut créer le commit de merge
                if (mergeResult.Status == MergeStatus.NonFastForward)
                {
                    var commitMessage = parameters.CommitMessage ??
                        $"Merge branch '{parameters.BranchName}'";
                    repo.Commit(commitMessage, signature, signature);
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
    // Méthodes utilitaires
    private bool IsTextFile(string filePath)
    {
        // Extensions courantes de fichiers texte
        var textExtensions = new[] { ".txt", ".cs", ".js", ".html", ".css", ".xml", ".json", ".md", ".yml", ".yaml", ".config", ".pas", ".dfm", ".dpr", ".dproj", ".sln" , ".csproj" };
        return textExtensions.Any(ext => filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    private string ReadBlobContent(Blob blob)
    {
        using (var contentStream = blob.GetContentStream())
        using (var reader = new StreamReader(contentStream))
        {
            return reader.ReadToEnd();
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