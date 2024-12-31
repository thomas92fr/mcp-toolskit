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
/// Définit les opérations disponibles pour le gestionnaire Git Checkout.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GitCheckoutOperation>))]
public enum GitCheckoutOperation
{
    /// <summary>Bascule vers une branche spécifiée</summary>
    [Description("Switches to a specified branch")]
    [Parameters(
        "RepositoryPath: Path to the Git repository",
        "BranchName: Name of the branch to checkout"
    )]
    Checkout
}

/// <summary>
/// Classe de base pour les paramètres de l'outil Git Checkout
/// </summary>
public class GitCheckoutParameters
{
    /// <summary>
    /// Opération Git à effectuer
    /// </summary>
    public required GitCheckoutOperation Operation { get; init; }

    /// <summary>
    /// Chemin du dépôt Git
    /// </summary>
    public required string RepositoryPath { get; init; }

    /// <summary>
    /// Nom de la branche à checkout
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
/// Contexte de sérialisation JSON pour les paramètres Git Checkout.
/// </summary>
[JsonSerializable(typeof(GitCheckoutParameters))]
public partial class GitCheckoutParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// Gestionnaire des opérations Git Checkout implémentant l'interface outil du protocole MCP.
/// </summary>
public class GitCheckoutToolHandler : ToolHandlerBase<GitCheckoutParameters>
{
    private readonly ILogger<GitCheckoutToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public GitCheckoutToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<GitCheckoutToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "GitCheckout",
        Description = typeof(GitCheckoutOperation).GenerateFullDescription(),
        InputSchema = GitCheckoutParametersJsonContext.Default.GitCheckoutParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        GitCheckoutParametersJsonContext.Default.GitCheckoutParameters;

    protected override async Task<CallToolResult> HandleAsync(
        GitCheckoutParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Git Checkout Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                GitCheckoutOperation.Checkout => await CheckoutBranchAsync(parameters),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };
            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Git Checkout operation");
            throw;
        }
    }

    private Task<string> CheckoutBranchAsync(GitCheckoutParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.RepositoryPath))
            throw new ArgumentException("Repository path is required for Checkout operation");

        if (string.IsNullOrEmpty(parameters.BranchName))
            throw new ArgumentException("Branch name is required for Checkout operation");

        var validPath = _appConfig.ValidatePath(parameters.RepositoryPath);

        using (var repo = new Repository(validPath))
        {
            var branch = repo.Branches[parameters.BranchName]
                ?? throw new ArgumentException($"Branch '{parameters.BranchName}' not found");

            // Configure checkout options
            var options = new CheckoutOptions
            {
                CheckoutModifiers = CheckoutModifiers.Force
            };

            // Exécution du checkout
            Commands.Checkout(repo, branch, options);

            return Task.FromResult($"Successfully checked out branch '{parameters.BranchName}'");
        }
    }

    public Task<CallToolResult> TestHandleAsync(
        GitCheckoutParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}