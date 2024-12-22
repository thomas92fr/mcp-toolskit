using mcp_toolskit.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.NET.Core.Models.Protocol.Common;
using ModelContextProtocol.NET.Server.Builder;
using Serilog;

namespace mcp_toolskit
{
    /// <summary>
    /// Classe principale du programme qui initialise et gère le serveur MCP (Model Context Protocol).
    /// </summary>
    internal class Program
    {
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
            // Create server info
            string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Version inconnue"; ;
            string logPath = Path.Combine(AppContext.BaseDirectory, "log.txt");
            var serverInfo = new Implementation { Name = "La boîte à outils de Toto", Version = version };
            var seriLogger = new LoggerConfiguration()
                .WriteTo.File(logPath)
                .MinimumLevel.Debug()
                .CreateLogger();

            Log.Logger = seriLogger;

            // Configure and build server
            var server = new McpServerBuilder(serverInfo)
                .AddStdioTransport()
                .ConfigureLogging(logging => logging.AddSerilog(seriLogger).SetMinimumLevel(LogLevel.Trace))
                .ConfigureUserServices(services =>
                    // Add logging
                    services.AddLogging(builder =>
                    {
                        builder.ClearProviders(); // Clear existing providers
                        builder.AddSerilog(seriLogger, dispose: true);
                        builder.SetMinimumLevel(LogLevel.Debug);
                    })
                )
                .ConfigureTools(tools => tools.AddHandler<CalculatorToolHandler>())
                .Build();

            try
            {
                server.Start();
                Console.Error.WriteLine($"Serveur MCP '{serverInfo.Name}' s'exécutant sur stdio");
                Console.Error.WriteLine($"Les logs seront écrits dans : {logPath}");

                await Task.Delay(-1); // Wait indefinitely
            }
            finally
            {
                server.Stop();
                await server.DisposeAsync();
                Log.CloseAndFlush(); // Important: flush any remaining logs
            }
        }
    }
}
