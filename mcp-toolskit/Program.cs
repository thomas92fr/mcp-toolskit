using mcp_toolskit.Models;
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
                              outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {#if ExecutionId}[{ExecutionId}] {#end}{Message:lj}{NewLine}{Exception}",
                              rollingInterval: RollingInterval.Day, 
                              retainedFileCountLimit: 7)
                .Enrich.FromLogContext()
                .MinimumLevel.Debug()
                .CreateLogger();

            Log.Logger = seriLogger; //main 2

            // Récupération des modules (liste des tools)
            var modules = GetModuleConfigurations();

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
                  
                    // Configuration des services pour chaque module
                    foreach (var module in modules)
                    {
                        module.ConfigureServices(services, appConfig);
                    }

                    // Add configuration to DI
                    services.AddSingleton(appConfig);

                    
                })
                .ConfigureTools(tools => {                 
                    // Configuration des outils pour chaque module
                    foreach (var module in modules)
                    {
                        module.ConfigureTools(tools, appConfig);
                    }
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
        

        /// <summary>
        /// Recherche et retourne toutes les implémentations de IModuleConfiguration dans l'assembly courant
        /// </summary>
        private static IEnumerable<IModuleConfiguration> GetModuleConfigurations()
        {
            var moduleConfigurationType = typeof(IModuleConfiguration);

            return System.Reflection.Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => moduleConfigurationType.IsAssignableFrom(t) &&
                            !t.IsInterface &&
                            !t.IsAbstract)
                .Select(t => (IModuleConfiguration)Activator.CreateInstance(t)!)
                .Where(module => module != null);
        }
    }
}
