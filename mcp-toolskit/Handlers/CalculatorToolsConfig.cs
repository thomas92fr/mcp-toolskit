using mcp_toolskit.Handlers.Filesystem;
using mcp_toolskit.Models;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.NET.Server.Builder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
