using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.NET.Core.Models.Protocol.Client.Responses;
using ModelContextProtocol.NET.Core.Models.Protocol.Common;
using ModelContextProtocol.NET.Core.Models.Protocol.Shared.Content;
using ModelContextProtocol.NET.Server.Contexts;
using ModelContextProtocol.NET.Server.Features.Tools;
using System.Text;
using mcp_toolskit.Models;

namespace mcp_toolskit.Handlers;

/// <summary>
/// Définit les opérations disponibles pour le gestionnaire de fichiers.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<FilesystemOperation>))]
public enum FilesystemOperation
{
    /// <summary>Lecture du contenu d'un fichier</summary>
    ReadFile,
    /// <summary>Lecture de plusieurs fichiers</summary>
    ReadMultipleFiles,
    /// <summary>Écriture dans un fichier</summary>
    WriteFile,
    /// <summary>Création d'un répertoire</summary>
    CreateDirectory,
    /// <summary>Liste le contenu d'un répertoire</summary>
    ListDirectory,
    /// <summary>Déplacement/renommage d'un fichier</summary>
    MoveFile,
    /// <summary>Recherche de fichiers</summary>
    SearchFiles,
    /// <summary>Obtention d'informations sur un fichier</summary>
    GetFileInfo,
    /// <summary>Liste les répertoires autorisés</summary>
    ListAllowedDirectories
}

/// <summary>
/// Classe de base pour les paramètres de l'outil système de fichiers
/// </summary>
public class FilesystemParameters
{
    /// <summary>
    /// Opération à effectuer sur le système de fichiers
    /// </summary>
    public required FilesystemOperation Operation { get; init; }
    
    /// <summary>
    /// Chemin du fichier ou du répertoire (pour les opérations nécessitant un seul chemin)
    /// </summary>
    public string? Path { get; init; }
    
    /// <summary>
    /// Liste des chemins (pour les opérations nécessitant plusieurs chemins)
    /// </summary>
    public List<string>? Paths { get; init; }
    
    /// <summary>
    /// Contenu à écrire (pour l'opération WriteFile)
    /// </summary>
    public string? Content { get; init; }
    
    /// <summary>
    /// Chemin source (pour l'opération MoveFile)
    /// </summary>
    public string? Source { get; init; }
    
    /// <summary>
    /// Chemin de destination (pour l'opération MoveFile)
    /// </summary>
    public string? Destination { get; init; }
    
    /// <summary>
    /// Motif de recherche (pour l'opération SearchFiles)
    /// </summary>
    public string? Pattern { get; init; }

    /// <summary>
    /// Retourne une représentation textuelle des paramètres.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder($"Operation: {Operation}");
        if (Path != null) sb.Append($", Path: {Path}");
        if (Paths != null) sb.Append($", Paths: [{string.Join(", ", Paths)}]");
        if (Source != null) sb.Append($", Source: {Source}");
        if (Destination != null) sb.Append($", Destination: {Destination}");
        if (Pattern != null) sb.Append($", Pattern: {Pattern}");
        return sb.ToString();
    }
}

/// <summary>
/// Contexte de sérialisation JSON pour les paramètres du système de fichiers.
/// </summary>
[JsonSerializable(typeof(FilesystemParameters))]
public partial class FilesystemParametersJsonContext : JsonSerializerContext { }

/// <summary>
/// Gestionnaire des opérations du système de fichiers implémentant l'interface outil du protocole MCP.
/// </summary>
public class FilesystemToolHandler : ToolHandlerBase<FilesystemParameters>
{
    private readonly ILogger<FilesystemToolHandler> _logger;
    private readonly string[] _allowedDirectories;

    public FilesystemToolHandler(
    IServerContext serverContext,
    ISessionContext sessionContext,
    ILogger<FilesystemToolHandler> logger,
    AppConfig appConfig
) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _allowedDirectories = appConfig.AllowedDirectories;
    }

    private static readonly Tool tool = new()
    {
        Name = "Filesystem",
        Description = "Provides access to filesystem operations within allowed directories",
        InputSchema = FilesystemParametersJsonContext.Default.FilesystemParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        FilesystemParametersJsonContext.Default.FilesystemParameters;

    /// <summary>
    /// Valide qu'un chemin est dans un répertoire autorisé
    /// </summary>
    private string ValidatePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!_allowedDirectories.Any(dir => fullPath.StartsWith(Path.GetFullPath(dir))))
        {
            throw new UnauthorizedAccessException($"Access denied - path outside allowed directories: {fullPath}");
        }
        return fullPath;
    }

    protected override async Task<CallToolResult> HandleAsync(
        FilesystemParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                FilesystemOperation.ReadFile => await ReadFileAsync(parameters),
                FilesystemOperation.ReadMultipleFiles => await ReadMultipleFilesAsync(parameters),
                FilesystemOperation.WriteFile => await WriteFileAsync(parameters),
                FilesystemOperation.CreateDirectory => await CreateDirectoryAsync(parameters),
                FilesystemOperation.ListDirectory => await ListDirectoryAsync(parameters),
                FilesystemOperation.MoveFile => await MoveFileAsync(parameters),
                FilesystemOperation.SearchFiles => await SearchFilesAsync(parameters),
                FilesystemOperation.GetFileInfo => await GetFileInfoAsync(parameters),
                FilesystemOperation.ListAllowedDirectories => ListAllowedDirectories(),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };
            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in filesystem operation");
            throw;
        }
    }

    private async Task<string> ReadFileAsync(FilesystemParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.Path))
            throw new ArgumentException("Path is required for ReadFile operation");

        var validPath = ValidatePath(parameters.Path);
        return await File.ReadAllTextAsync(validPath);
    }

    private async Task<string> ReadMultipleFilesAsync(FilesystemParameters parameters)
    {
        if (parameters.Paths == null || !parameters.Paths.Any())
            throw new ArgumentException("Paths are required for ReadMultipleFiles operation");

        var results = new List<string>();
        foreach (var path in parameters.Paths)
        {
            try
            {
                var validPath = ValidatePath(path);
                var content = await File.ReadAllTextAsync(validPath);
                results.Add($"{path}:\n{content}");
            }
            catch (Exception ex)
            {
                results.Add($"{path}: Error - {ex.Message}");
            }
        }
        return string.Join("\n---\n", results);
    }

    private async Task<string> WriteFileAsync(FilesystemParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.Path))
            throw new ArgumentException("Path is required for WriteFile operation");
        if (string.IsNullOrEmpty(parameters.Content))
            throw new ArgumentException("Content is required for WriteFile operation");

        var validPath = ValidatePath(parameters.Path);
        await File.WriteAllTextAsync(validPath, parameters.Content);
        return $"Successfully wrote to {parameters.Path}";
    }

    private Task<string> CreateDirectoryAsync(FilesystemParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.Path))
            throw new ArgumentException("Path is required for CreateDirectory operation");

        var validPath = ValidatePath(parameters.Path);
        Directory.CreateDirectory(validPath);
        return Task.FromResult($"Successfully created directory {parameters.Path}");
    }

    private  Task<string> ListDirectoryAsync(FilesystemParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.Path))
            throw new ArgumentException("Path is required for ListDirectory operation");

        var validPath = ValidatePath(parameters.Path);
        var entries = Directory.GetFileSystemEntries(validPath)
            .Select(entry =>
            {
                var isDirectory = Directory.Exists(entry);
                var name = Path.GetFileName(entry);
                return $"{(isDirectory ? "[DIR]" : "[FILE]")} {name}";
            });
        return Task.FromResult(string.Join("\n", entries));
    }

    private  Task<string> MoveFileAsync(FilesystemParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.Source))
            throw new ArgumentException("Source path is required for MoveFile operation");
        if (string.IsNullOrEmpty(parameters.Destination))
            throw new ArgumentException("Destination path is required for MoveFile operation");

        var validSourcePath = ValidatePath(parameters.Source);
        var validDestPath = ValidatePath(parameters.Destination);
        
        File.Move(validSourcePath, validDestPath);
        return Task.FromResult($"Successfully moved {parameters.Source} to {parameters.Destination}");
    }

    private  Task<string> SearchFilesAsync(FilesystemParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.Path))
            throw new ArgumentException("Path is required for SearchFiles operation");
        if (string.IsNullOrEmpty(parameters.Pattern))
            throw new ArgumentException("Pattern is required for SearchFiles operation");

        var validPath = ValidatePath(parameters.Path);
        var results = Directory.GetFiles(validPath, $"*{parameters.Pattern}*", SearchOption.AllDirectories)
            .Where(file =>
            {
                try
                {
                    ValidatePath(file);
                    return true;
                }
                catch
                {
                    return false;
                }
            });

        var fileList = results.ToList();
        return Task.FromResult(fileList.Any()
                ? string.Join("\n", fileList)
                : "No matches found");
    }

    private Task<string> GetFileInfoAsync(FilesystemParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.Path))
            throw new ArgumentException("Path is required for GetFileInfo operation");

        var validPath = ValidatePath(parameters.Path);
        var info = new FileInfo(validPath);
        
        var fileInfo = new Dictionary<string, string>
        {
            { "size", info.Length.ToString() },
            { "created", info.CreationTime.ToString() },
            { "modified", info.LastWriteTime.ToString() },
            { "accessed", info.LastAccessTime.ToString() },
            { "isDirectory", info.Attributes.HasFlag(FileAttributes.Directory).ToString() },
            { "isFile", (!info.Attributes.HasFlag(FileAttributes.Directory)).ToString() },
            { "permissions", Convert.ToString((int)(info.Attributes & FileAttributes.Archive), 8) }
        };

        return Task.FromResult(string.Join("\n", fileInfo.Select(kvp => $"{kvp.Key}: {kvp.Value}")));
    }

    private string ListAllowedDirectories()
    {
        return $"Allowed directories:\n{string.Join("\n", _allowedDirectories)}";
    }

    public Task<CallToolResult> TestHandleAsync(
        FilesystemParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        return HandleAsync(parameters, cancellationToken);
    }
}