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
/// Définit les opérations disponibles pour le gestionnaire Git Push.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GitPushOperation>))]
public enum GitPushOperation
{
    /// <summary>Pousse les modifications vers un dépôt distant</summary>
    [Description("Pushes changes to a remote repository")]
    [Parameters(
        "RepositoryPath: Path to the Git repository",
        "RemoteName: Name of the remote (default: origin)",
        "BranchName: Name of the branch to push (default: current branch)"
    )]
    Push
}

/// <summary>
/// Classe de base pour les paramètres de l'outil Git Push
/// </summary>
public class GitPushParameters
{
    /// <summary>
    /// Opération Git à effectuer
    /// </summary>
    public required GitPushOperation Operation { get; init; }

    /// <summary>
    /// Chemin du dépôt Git
    /// </summary>
    public required string RepositoryPath { get; init; }

    /// <summary>
    /// Nom du remote, par défaut: "origin"
    /// </summary>
    public required string RemoteName { get; init; } = "origin";

    /// <summary>
    /// Nom de la branche à pousser (optionnel, par défaut: branche courante)
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
/// Contexte de sérialisation JSON pour les paramètres Git Push.
/// </summary>
[JsonSerializable(typeof(GitPushParameters))]
public partial class GitPushParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// Gestionnaire des opérations Git Push implémentant l'interface outil du protocole MCP.
/// </summary>
public class GitPushToolHandler : ToolHandlerBase<GitPushParameters>
{
    private readonly ILogger<GitPushToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public GitPushToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<GitPushToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "GitPush",
        Description = typeof(GitPushOperation).GenerateFullDescription(),
        InputSchema = GitPushParametersJsonContext.Default.GitPushParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        GitPushParametersJsonContext.Default.GitPushParameters;

    protected override async Task<CallToolResult> HandleAsync(
        GitPushParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Git Push Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                GitPushOperation.Push => await PushToRemoteAsync(parameters),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };
            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Git Push operation");
            throw;
        }
    }

    private Task<string> PushToRemoteAsync(GitPushParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.RepositoryPath))
            throw new ArgumentException("Repository path is required for Push operation");

        var validPath = _appConfig.ValidatePath(parameters.RepositoryPath);

        using (var repo = new Repository(validPath))
        {
            var remote = repo.Network.Remotes[parameters.RemoteName]
                ?? throw new ArgumentException($"Remote '{parameters.RemoteName}' not found");

            var branch = parameters.BranchName != null
                ? repo.Branches[parameters.BranchName]
                : repo.Head;

            if (branch == null)
                throw new ArgumentException($"Branch '{parameters.BranchName ?? "HEAD"}' not found");

            // Configuration des options de push
            var options = new PushOptions
            {
                CredentialsProvider = (_url, _user, _cred) =>
                    new UsernamePasswordCredentials
                    {
                        Username = _appConfig.Git.UserName,
                        Password = _appConfig.Git.UserPassword
                    }
            };

            // Exécution du push
            repo.Network.Push(remote, branch.CanonicalName, options);

            return Task.FromResult($"Successfully pushed '{branch.FriendlyName}' to remote '{parameters.RemoteName}'");
        }
    }

    public Task<CallToolResult> TestHandleAsync(
        GitPushParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}