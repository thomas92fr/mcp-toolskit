using mcp_toolskit.Handlers.DotNet;
using mcp_toolskit.Models;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.NET.Server.Builder;

namespace mcp_toolskit.Handlers
{
    public class DotNetToolsConfig: IModuleConfiguration
    {
        public void ConfigureTools(IToolRegistry tools, AppConfig appConfig)
        {
            if (appConfig.ValidateTool("DotNet"))
                tools.AddHandler<DotNetToolHandler>();
        }

        public void ConfigureServices(IServiceCollection services, AppConfig appConfig)
        {
            // Configuration des services spécifiques aux Tools DotNet
        }
    }
}
