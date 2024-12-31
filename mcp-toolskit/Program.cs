using mcp_toolskit.Handlers;
using mcp_toolskit.Handlers.DotNet;
using mcp_toolskit.Handlers.Filesystem;
using mcp_toolskit.Handlers.BraveSearch;
using mcp_toolskit.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.NET.Core.Models.Protocol.Common;
using ModelContextProtocol.NET.Server.Builder;
using Polly.Extensions.Http;
using Polly;
using Serilog;
using mcp_toolskit.Handlers.BraveSearch.Helpers;
using mcp_toolskit.Handlers.Git;

namespace mcp_toolskit
{
    /// <summary>
    /// Classe principale du programme qui initialise et gère le serveur MCP (Model Context Protocol).
    /// </summary>
    internal class Program
    {
        public static string GetCurrentLogFileName(string logPath)
        {
            string baseFileName = "Logs";
            string date = DateTime.Now.ToString("yyyyMMdd");
            return Path.Combine(logPath, $"{baseFileName}{date}.txt");
        }


        /// <summary>
        /// Point d'entrée principal de l'application. Configure et démarre le serveur MCP.
        /// </summary>
        /// <param name="args">Arguments de ligne de commande (non utilisés)</param>
        /// <returns>Une tâche asynchrone représentant l'exécution du programme</returns>
        /// <remarks>
        /// Cette méthode :<br/>
        /// - Configure la journalisation avec Serilog<br/>
        /// - Initialise le serveur MCP avec les paramètres de base<br/>
        /// - Configure le transport stdio<br/>
        /// - Configure les services de journalisation<br/>
        /// - Ajoute le gestionnaire de calculatrice<br/>
        /// - Démarre le serveur et maintient son exécution
        /// </remarks>
        static async Task Main(string[] args)
        {
            // Chargement de la configuration
            string? errorMessage;
            var appConfig = AppConfig.GetConfiguration(args.Length > 0 ? args[0] : null, out errorMessage);

            if (errorMessage != null)
            {
                Console.Error.WriteLine(errorMessage);
            }

            Console.Error.WriteLine("Configuration chargée :");
            Console.Error.WriteLine(appConfig.ToString());

            // Create server info
            string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Version inconnue";
            var serverInfo = new Implementation { Name = "La boîte à outils de Toto", Version = version };
            var seriLogger = new LoggerConfiguration()
                .WriteTo.File(Path.Combine(appConfig.LogPath,"Logs.txt"), 
                              rollingInterval: RollingInterval.Day, 
                              retainedFileCountLimit: 7)
                .MinimumLevel.Debug()
                .CreateLogger();

            Log.Logger = seriLogger;

            // Configure and build server
            var server = new McpServerBuilder(serverInfo)
                .AddStdioTransport()
                .ConfigureLogging(logging => logging.AddSerilog(seriLogger).SetMinimumLevel(LogLevel.Trace))
                .ConfigureUserServices(services =>
                {
                    // Add logging
                    services.AddLogging(builder =>
                    {
                        builder.ClearProviders();
                        builder.AddSerilog(seriLogger, dispose: true);
                        builder.SetMinimumLevel(LogLevel.Debug);
                    });

                    // Add HttpClient with configuration
                    var BraveSearchHttpClient = services.AddHttpClient("BraveSearch")
                        .ConfigureHttpClient(client =>
                        {
                            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                            client.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
                        })
                        .SetHandlerLifetime(TimeSpan.FromMinutes(5));  // Définir la durée de vie du handler                       
                        
                    if(appConfig.BraveSearch.IgnoreSSLErrors)
                    {
                        BraveSearchHttpClient.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                        });
                    }

                    BraveSearchHttpClient.AddPolicyHandler(GetRetryPolicy());  // Optionnel : Ajouter une politique de retry
                    BraveSearchHttpClient.AddHttpMessageHandler<RetryHandler>();  



                    // Add configuration to DI
                    services.AddSingleton(appConfig);

                    services.AddTransient<RetryHandler>();
                })
                .ConfigureTools(tools => {
                    if (appConfig.ValidateTool("ListAllowedDirectories"))
                        tools.AddHandler<ListAllowedDirectoriesToolHandler>();                  
                    if (appConfig.ValidateTool("ReadMultipleFiles"))
                        tools.AddHandler<ReadMultipleFilesToolHandler>();
                    if (appConfig.ValidateTool("WriteFile"))
                        tools.AddHandler<WriteFileToolHandler>();
                    if (appConfig.ValidateTool("WriteFileAtPosition"))
                        tools.AddHandler<WriteFileAtPositionToolHandler>();
                    if (appConfig.ValidateTool("CreateDirectory"))
                        tools.AddHandler<CreateDirectoryToolHandler>();
                    if (appConfig.ValidateTool("ListDirectory"))
                        tools.AddHandler<ListDirectoryToolHandler>();
                    if (appConfig.ValidateTool("MoveFile"))
                        tools.AddHandler<MoveFileToolHandler>();
                    if (appConfig.ValidateTool("SearchFiles"))
                        tools.AddHandler<SearchFilesToolHandler>();
                    if (appConfig.ValidateTool("SearchPositionInFileWithRegex"))
                        tools.AddHandler<SearchPositionInFileWithRegexToolHandler>();
                    if (appConfig.ValidateTool("GetFileInfo"))
                        tools.AddHandler<GetFileInfoToolHandler>();
                    if (appConfig.ValidateTool("DeleteAtPosition"))
                        tools.AddHandler<DeleteAtPositionToolHandler>();
                    if (appConfig.ValidateTool("SearchAndReplace"))
                        tools.AddHandler<SearchAndReplaceToolHandler>();
                    if (appConfig.ValidateTool("DeleteFile"))
                        tools.AddHandler<DeleteFileToolHandler>();
                    if (appConfig.ValidateTool("DotNet"))
                        tools.AddHandler<DotNetToolHandler>();

                    if (appConfig.ValidateTool("Calculator"))
                        tools.AddHandler<CalculatorToolHandler>();

                    if (appConfig.ValidateTool("BraveWebSearch"))
                        tools.AddHandler<BraveWebSearchToolHandler>();
                    if (appConfig.ValidateTool("BraveLocalSearch"))
                        tools.AddHandler<BraveLocalSearchToolHandler>();

                    if (appConfig.ValidateTool("GitCommit"))
                        tools.AddHandler<GitCommitToolHandler>();
                    if (appConfig.ValidateTool("GitFetch"))
                        tools.AddHandler<GitFetchToolHandler>();
                    if (appConfig.ValidateTool("GitPull"))
                        tools.AddHandler<GitPullToolHandler>();
                    if (appConfig.ValidateTool("GitPush"))
                        tools.AddHandler<GitPushToolHandler>();
                    if (appConfig.ValidateTool("GitBranches"))
                        tools.AddHandler<GitBranchesToolHandler>();
                    if (appConfig.ValidateTool("GitCreateBranch"))
                        tools.AddHandler<GitCreateBranchToolHandler>();
                    if (appConfig.ValidateTool("GitCheckout"))
                        tools.AddHandler<GitCheckoutToolHandler>();
                    if (appConfig.ValidateTool("GitDeleteBranch"))
                        tools.AddHandler<GitDeleteBranchToolHandler>();
                    if (appConfig.ValidateTool("GitConflicts"))
                        tools.AddHandler<GitConflictsToolHandler>();
                    
                })
                .Build();

            try
            {
                server.Start();
                Console.Error.WriteLine($"Serveur MCP '{serverInfo.Name}' v{serverInfo.Version} s'exécutant sur stdio");
                string currentLogFile = GetCurrentLogFileName(appConfig.LogPath);
                Console.Error.WriteLine($"Fichier de log actuel : {currentLogFile}");
                await Task.Delay(-1); // Wait indefinitely
            }
            finally
            {
                server.Stop();
                await server.DisposeAsync();
                Log.CloseAndFlush(); // Important: flush any remaining logs
            }
        }
        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(3, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        }
    }
}
