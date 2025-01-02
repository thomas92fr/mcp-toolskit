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
    public class FileSystemToolsConfig : IModuleConfiguration
    {
        public void ConfigureTools(IToolRegistry tools, AppConfig appConfig)
        {
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
            if (appConfig.ValidateTool("SearchInFiles"))
                tools.AddHandler<SearchInFilesToolHandler>();
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

            

        }

        public void ConfigureServices(IServiceCollection services, AppConfig appConfig)
        {
            // Configuration des services spécifiques aux Tools FileSystem
        }
    }
}
