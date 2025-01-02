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
/// Définit les opérations disponibles pour le gestionnaire Git Pull.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GitPullOperation>))]
public enum GitPullOperation
{
    /// <summary>Récupère et fusionne les modifications depuis un dépôt distant</summary>
    [Description("Pulls changes from a remote repository")]
    [Parameters(
        "RepositoryPath: Path to the Git repository",
        "RemoteName: Name of the remote (default: origin)",
        "BranchName: Name of the branch to pull (default: current branch)"
    )]
    Pull
}

/// <summary>
/// Classe de base pour les paramètres de l'outil Git Pull
/// </summary>
public class GitPullParameters
{
    /// <summary>
    /// Opération Git à effectuer
    /// </summary>
    public required GitPullOperation Operation { get; init; }

    /// <summary>
    /// Chemin du dépôt Git
    /// </summary>
    public required string RepositoryPath { get; init; }

    /// <summary>
    /// Nom du remote (par défaut: "origin")
    /// </summary>
    public required string RemoteName { get; init; } = "origin";

    /// <summary>
    /// Nom de la branche (par défaut: branche courante)
    /// </summary>
    public string? BranchName { get; init; }

    /// <summary>
    /// Retourne une représentation textuelle des paramètres.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (RepositoryPath != null) sb.Append($", RepositoryPath: {RepositoryPath}");
        if (RemoteName != null) sb.Append($", RemoteName: {RemoteName}");
        if (BranchName != null) sb.Append($", BranchName: {BranchName}");
        return sb.ToString();
    }
}

/// <summary>
/// Contexte de sérialisation JSON pour les paramètres Git Pull.
/// </summary>
[JsonSerializable(typeof(GitPullParameters))]
public partial class GitPullParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// Gestionnaire des opérations Git Pull implémentant l'interface outil du protocole MCP.
/// </summary>
public class GitPullToolHandler : ToolHandlerBase<GitPullParameters>
{
    private readonly ILogger<GitPullToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public GitPullToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<GitPullToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "GitPull",
        Description = typeof(GitPullOperation).GenerateFullDescription(),
        InputSchema = GitPullParametersJsonContext.Default.GitPullParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        GitPullParametersJsonContext.Default.GitPullParameters;

    protected override async Task<CallToolResult> HandleAsync(
        GitPullParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());
        _logger.LogInformation("Git Pull Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                GitPullOperation.Pull => await PullFromRemoteAsync(parameters),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };
            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Git Pull operation");
            throw;
        }
    }

    private Task<string> PullFromRemoteAsync(GitPullParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.RepositoryPath))
            throw new ArgumentException("Repository path is required for Pull operation");

        var validPath = _appConfig.ValidatePath(parameters.RepositoryPath);

        using (var repo = new Repository(validPath))
        {
            var remote = repo.Network.Remotes[parameters.RemoteName]
                ?? throw new ArgumentException($"Remote '{parameters.RemoteName}' not found");

            // Obtention de la branche
            var branch = parameters.BranchName != null
                ? repo.Branches[parameters.BranchName]
                : repo.Head;

            if (branch == null)
                throw new ArgumentException("Specified branch not found");

            // Configuration des options de pull
            var options = new PullOptions
            {
                FetchOptions = new FetchOptions
                {
                    CredentialsProvider = (_url, _user, _cred) =>
                        new UsernamePasswordCredentials
                        {
                            Username = _appConfig.Git.UserName,
                            Password = _appConfig.Git.UserPassword
                        }
                },
                MergeOptions = new MergeOptions
                {
                    FastForwardStrategy = FastForwardStrategy.Default
                }
            };

            // Exécution du pull
            Commands.Pull(
                repo,
                new Signature(_appConfig.Git.UserName, _appConfig.Git.UserEmail, DateTimeOffset.Now),
                options
            );

            return Task.FromResult($"Successfully pulled from remote '{parameters.RemoteName}' for branch '{branch.FriendlyName}'");
        }
    }

    public Task<CallToolResult> TestHandleAsync(
        GitPullParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}