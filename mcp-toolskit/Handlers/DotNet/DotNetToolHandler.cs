using mcp_toolskit.Attributes;
using mcp_toolskit.Extentions;
using mcp_toolskit.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.NET.Core.Models.Protocol.Client.Responses;
using ModelContextProtocol.NET.Core.Models.Protocol.Common;
using ModelContextProtocol.NET.Core.Models.Protocol.Shared.Content;
using ModelContextProtocol.NET.Server.Contexts;
using ModelContextProtocol.NET.Server.Features.Tools;
using Serilog.Context;
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
        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());
        _logger.LogInformation("Running dotnet command: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                DotNetOperation.RunTests => await RunTestsAsync(parameters),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };

            _logger.LogInformation("Result: {content}", content);

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

        _logger.LogInformation("AllowedDirectories: {Directories}", string.Join(", ", _appConfig.AllowedDirectories));
        _logger.LogInformation("Checking path: {Path}", Path.GetDirectoryName(parameters.SolutionFile));

        var solutionDir = _appConfig.ValidatePath(Path.GetDirectoryName(parameters.SolutionFile)!);
        if (!Directory.Exists(solutionDir))
            throw new DirectoryNotFoundException($"Solution directory not found: {solutionDir}");

        var solutionPath = parameters.SolutionFile;
        if (!File.Exists(solutionPath))
            throw new FileNotFoundException($"Solution file not found: {solutionPath}");

        _logger.LogInformation("Running tests for solution: {SolutionPath}", solutionPath);

        var testResultsDir = Path.Combine(solutionDir, "TestResults");
        Directory.CreateDirectory(testResultsDir);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"test \"{solutionPath}\" --no-restore " +
                       $"--logger \"console;verbosity=detailed\" " +
                       $"--logger \"html;logfilename=TestResults.html\" " +
                       $"--results-directory \"{testResultsDir}\"",
            WorkingDirectory = solutionDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

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
                output.AppendLine($"ERROR: {e.Data}");
                _logger.LogError("Process error: {Error}", e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        var exitCode = process.ExitCode;
        _logger.LogInformation("Process completed with exit code: {ExitCode}", exitCode);

        // Ajouter le chemin du rapport HTML à la sortie
        output.AppendLine();
        output.AppendLine($"Test results HTML report: {Path.Combine(testResultsDir, "TestResults.html")}");
        
        return output.ToString();
    }
}