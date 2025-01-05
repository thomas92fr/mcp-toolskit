using mcp_toolskit.Models;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.NET.Server.Builder;
using Polly.Extensions.Http;
using Polly;
using mcp_toolskit.Handlers.Systems;

namespace mcp_toolskit.Handlers
{
    public class SystemInfoToolsConfig : IModuleConfiguration
    {
        public void ConfigureTools(IToolRegistry tools, AppConfig appConfig)
        {
            if (appConfig.ValidateTool("SystemInfo"))
                tools.AddHandler<SystemInfoToolHandler>();
        }

        public void ConfigureServices(IServiceCollection services, AppConfig appConfig)
        {
            var systemInfoHttpClient = services.AddHttpClient("SystemInfo")
                .ConfigureHttpClient(client =>
                {
                    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                })
                .SetHandlerLifetime(TimeSpan.FromMinutes(5));

            if (appConfig.BraveSearch.IgnoreSSLErrors)
            {
                systemInfoHttpClient.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                });
            }

            systemInfoHttpClient.AddPolicyHandler(GetRetryPolicy());
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
