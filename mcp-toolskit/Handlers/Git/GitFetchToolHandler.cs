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
/// Définit les opérations disponibles pour le gestionnaire Git Fetch.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GitFetchOperation>))]
public enum GitFetchOperation
{
    /// <summary>Récupération des modifications depuis un dépôt distant</summary>
    [Description("Fetches changes from a remote repository")]
    [Parameters(
        "RepositoryPath: Path to the Git repository",
        "RemoteName: Name of the remote (default: origin)"
    )]
    Fetch
}

/// <summary>
/// Classe de base pour les paramètres de l'outil Git Fetch
/// </summary>
public class GitFetchParameters
{
    /// <summary>
    /// Opération Git à effectuer
    /// </summary>
    public required GitFetchOperation Operation { get; init; }

    /// <summary>
    /// Chemin du dépôt Git
    /// </summary>
    public required string RepositoryPath { get; init; }

    /// <summary>
    /// Nom du remote, par défaut: "origin")
    /// </summary>
    public required string RemoteName { get; init; } = "origin";

    /// <summary>
    /// Retourne une représentation textuelle des paramètres.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (RepositoryPath != null) sb.Append($", RepositoryPath: {RepositoryPath}");
        if (RemoteName != null) sb.Append($", RemoteName: {RemoteName}");
        return sb.ToString();
    }
}

/// <summary>
/// Contexte de sérialisation JSON pour les paramètres Git Fetch.
/// </summary>
[JsonSerializable(typeof(GitFetchParameters))]
public partial class GitFetchParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// Gestionnaire des opérations Git Fetch implémentant l'interface outil du protocole MCP.
/// </summary>
public class GitFetchToolHandler : ToolHandlerBase<GitFetchParameters>
{
    private readonly ILogger<GitFetchToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public GitFetchToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<GitFetchToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "GitFetch",
        Description = typeof(GitFetchOperation).GenerateFullDescription(),
        InputSchema = GitFetchParametersJsonContext.Default.GitFetchParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        GitFetchParametersJsonContext.Default.GitFetchParameters;

    protected override async Task<CallToolResult> HandleAsync(
        GitFetchParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());
        _logger.LogInformation("Git Fetch Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                GitFetchOperation.Fetch => await FetchFromRemoteAsync(parameters),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };

            _logger.LogInformation("Result: {content}", content);

            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Git Fetch operation");
            throw;
        }
    }

    private Task<string> FetchFromRemoteAsync(GitFetchParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.RepositoryPath))
            throw new ArgumentException("Repository path is required for Fetch operation");

        var validPath = _appConfig.ValidatePath(parameters.RepositoryPath);

        using (var repo = new Repository(validPath))
        {
            var remote = repo.Network.Remotes[parameters.RemoteName]
                ?? throw new ArgumentException($"Remote '{parameters.RemoteName}' not found");

            // Configuration des options de fetch
            var options = new FetchOptions
            {
                CredentialsProvider = (_url, _user, _cred) =>
                    new UsernamePasswordCredentials
                    {
                        Username = _appConfig.Git.UserName,
                        Password = _appConfig.Git.UserPassword
                    }
            };

            // Exécution du fetch
            var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification).ToList();
            Commands.Fetch(repo, remote.Name, refSpecs, options, "Fetch executed via GitFetchToolHandler");

            return Task.FromResult($"Successfully fetched from remote '{parameters.RemoteName}'");
        }
    }

    public Task<CallToolResult> TestHandleAsync(
        GitFetchParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}