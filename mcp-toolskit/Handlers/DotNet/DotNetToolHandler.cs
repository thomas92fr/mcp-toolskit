using mcp_toolskit.Attributes;
using mcp_toolskit.Extentions;
using mcp_toolskit.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.NET.Core.Models.Protocol.Client.Responses;
using ModelContextProtocol.NET.Core.Models.Protocol.Common;
using ModelContextProtocol.NET.Core.Models.Protocol.Shared.Content;
using ModelContextProtocol.NET.Server.Contexts;
using ModelContextProtocol.NET.Server.Features.Tools;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace mcp_toolskit.Handlers.DotNet;

[JsonConverter(typeof(JsonStringEnumConverter<DotNetOperation>))]
public enum DotNetOperation
{
    [Description("Executes unit tests for a .NET solution")]
    [Parameters("SolutionFile: Full path of the solution file")]
    RunTests
}

public class DotNetParameters
{
    public required DotNetOperation Operation { get; init; }

    public required string SolutionFile { get; init; }

    public override string ToString()
    {
        return $"Operation: {Operation}, SolutionFile: {SolutionFile}";
    }
}

[JsonSerializable(typeof(DotNetParameters))]
public partial class DotNetParametersJsonContext : JsonSerializerContext { }

public class DotNetToolHandler : ToolHandlerBase<DotNetParameters>
{
    private readonly ILogger<DotNetToolHandler> _logger;
    private readonly AppConfig _appConfig;

    public DotNetToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<DotNetToolHandler> logger,
        AppConfig appConfig
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _appConfig = appConfig;
    }

    private static readonly Tool tool = new()
    {
        Name = "DotNet",
        Description = typeof(DotNetOperation).GenerateFullDescription(),
        InputSchema = DotNetParametersJsonContext.Default.DotNetParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        DotNetParametersJsonContext.Default.DotNetParameters;

    protected override async Task<CallToolResult> HandleAsync(
        DotNetParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Running dotnet command: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                DotNetOperation.RunTests => await RunTestsAsync(parameters),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };
            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running dotnet command");
            throw;
        }
    }

    private async Task<string> RunTestsAsync(DotNetParameters parameters)
    {
        if (string.IsNullOrEmpty(parameters.SolutionFile))
            throw new ArgumentException("SolutionFile is required for RunTests operation");

        // Ajout des logs de débogage pour vérifier la configuration
        foreach(var env in Environment.GetEnvironmentVariables().Cast<DictionaryEntry>())
        {
            _logger.LogDebug("Environment variable: {Key} = {Value}", env.Key, env.Value);
        }
        
        _logger.LogInformation("AllowedDirectories: {Directories}", string.Join(", ", _appConfig.AllowedDirectories));
        _logger.LogInformation("Checking path: {Path}", Path.GetDirectoryName(parameters.SolutionFile));

        var solutionDir = _appConfig.ValidatePath(Path.GetDirectoryName(parameters.SolutionFile)!);
        if (!Directory.Exists(solutionDir))
            throw new DirectoryNotFoundException($"Solution directory not found: {solutionDir}");

        // Ensure we have the full path to the solution file
        var solutionPath = parameters.SolutionFile;
        if (!File.Exists(solutionPath))
            throw new FileNotFoundException($"Solution file not found: {solutionPath}");

        _logger.LogInformation("Running tests for solution: {SolutionPath}", solutionPath);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"test \"{solutionPath}\" --no-restore --logger \"trx;LogFileName=TestResults.trx\"",
            WorkingDirectory = solutionDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _logger.LogInformation("Starting process with: FileName = {FileName}, Arguments = {Arguments}, WorkingDirectory = {WorkingDirectory}",
            processStartInfo.FileName,
            processStartInfo.Arguments,
            processStartInfo.WorkingDirectory);

        using var process = new Process { StartInfo = processStartInfo };
        var output = new StringBuilder();
        
        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                output.AppendLine(e.Data);
                _logger.LogInformation("Process output: {Output}", e.Data);
            }
        };
        
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                var errorMessage = $"ERROR: {e.Data}";
                output.AppendLine(errorMessage);
                _logger.LogError("Process error: {Error}", e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        var exitCode = process.ExitCode;
        _logger.LogInformation("Process completed with exit code: {ExitCode}", exitCode);

        return output.ToString();
    }
}