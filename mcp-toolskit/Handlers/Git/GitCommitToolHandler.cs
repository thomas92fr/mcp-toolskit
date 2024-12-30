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
/// Définit les opérations disponibles pour le gestionnaire Git.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GitCommitOperation>))]
public enum GitCommitOperation
{
    /// <summary>Création d'un commit</summary>
    [Description("Creates a new commit with the specified message")]
    [Parameters(
        "RepositoryPath: Path to the Git repository",
        "Message: Commit message"
        )]
    Commit
}

/// <summary>
/// Classe de base pour les paramètres de l'outil Git
/// </summary>
public class GitCommitParameters
{
    /// <summary>
    /// Opération Git à effectuer
    /// </summary>
    public required GitCommitOperation Operation { get; init; }

    /// <summary>
    /// Chemin du dépôt Git
    /// </summary>
    public required string RepositoryPath { get; init; }

    /// <summary>
    /// Message du commit
    /// </summary>
    public required string Message { get; init; }

    

    /// <summary>
    /// Retourne une représentation textuelle des paramètres.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (RepositoryPath != null) sb.Append($", RepositoryPath: {RepositoryPath}");
        if (Message != null) sb.Append($", Message: {Message}");     
        return sb.ToString();
    }
}

/// <summary>
/// Contexte de sérialisation JSON pour les paramètres Git.
/// </summary>
[JsonSerializable(typeof(GitCommitParameters))]
public partial class GitCommitParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// Gestionnaire des opérations Git implémentant l'interface outil du protocole MCP.
/// </summary>
public class GitCommitToolHandler : ToolHandlerBase<GitCommitParameters>
{
    private readonly ILogger<GitCommitToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public GitCommitToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<GitCommitToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "GitCommit",
        Description = typeof(GitCommitOperation).GenerateFullDescription(),
        InputSchema = GitCommitParametersJsonContext.Default.GitCommitParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        GitCommitParametersJsonContext.Default.GitCommitParameters;

    protected override async Task<CallToolResult> HandleAsync(
        GitCommitParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Git Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                GitCommitOperation.Commit => await CreateCommitAsync(parameters),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };
            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Git operation");
            throw;
        }
    }

    private Task<string> CreateCommitAsync(GitCommitParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.RepositoryPath))
            throw new ArgumentException("Repository path is required for Commit operation");
        if (string.IsNullOrEmpty(parameters.Message))
            throw new ArgumentException("Commit message is required");

        var validPath = _appConfig.ValidatePath(parameters.RepositoryPath);

        using (var repo = new Repository(validPath))
        {
            // Création de la signature de l'auteur
            var signature = new Signature(_appConfig.Git.UserName, _appConfig.Git.UserEmail, DateTimeOffset.Now);

            // Stage tous les fichiers modifiés
            Commands.Stage(repo, "*");

            // Création du commit
            var commit = repo.Commit(parameters.Message, signature, signature);

            return Task.FromResult($"Successfully created commit {commit.Sha} with message: {parameters.Message}");
        }
    }

    public Task<CallToolResult> TestHandleAsync(
        GitCommitParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}