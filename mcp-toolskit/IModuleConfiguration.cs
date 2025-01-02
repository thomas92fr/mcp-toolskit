using mcp_toolskit.Models;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.NET.Server.Builder;

namespace mcp_toolskit
{
    /// <summary>
    /// Interface définissant la configuration d'un module de l'application.
    /// Permet d'enregistrer les outils et services spécifiques au module.
    /// </summary>
    public interface IModuleConfiguration
    {
        /// <summary>
        /// Configure les outils spécifiques au module.
        /// </summary>
        /// <param name="tools">Registre des outils où enregistrer les handlers</param>
        /// <param name="appConfig">Configuration de l'application</param>
        void ConfigureTools(IToolRegistry tools, AppConfig appConfig);

        /// <summary>
        /// Configure les services spécifiques au module.
        /// </summary>
        /// <param name="services">Collection de services pour l'injection de dépendances</param>
        /// <param name="appConfig">Configuration de l'application</param>
        void ConfigureServices(IServiceCollection services, AppConfig appConfig);
    }
}
