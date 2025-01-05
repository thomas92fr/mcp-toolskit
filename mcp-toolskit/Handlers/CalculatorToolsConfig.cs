using mcp_toolskit.Models;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.NET.Server.Builder;

namespace mcp_toolskit.Handlers
{
    public class CalculatorToolsConfig: IModuleConfiguration
    {
        public void ConfigureTools(IToolRegistry tools, AppConfig appConfig)
        {
            if (appConfig.ValidateTool("Calculator"))
                tools.AddHandler<CalculatorToolHandler>();

        }

        public void ConfigureServices(IServiceCollection services, AppConfig appConfig)
        {
            // Configuration des services spécifiques aux Tools Calculator
        }
    }
}
