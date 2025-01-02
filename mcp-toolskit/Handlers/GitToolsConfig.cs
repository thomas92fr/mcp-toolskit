using mcp_toolskit.Handlers.Filesystem;
using mcp_toolskit.Handlers.Git;
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
    public class GitToolsConfig : IModuleConfiguration
    {
        public void ConfigureTools(IToolRegistry tools, AppConfig appConfig)
        {
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

        }

        public void ConfigureServices(IServiceCollection services, AppConfig appConfig)
        {
            // Configuration des services spécifiques aux Tools Git
        }
    }
}
