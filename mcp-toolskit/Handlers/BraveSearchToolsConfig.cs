using mcp_toolskit.Handlers.BraveSearch;
using mcp_toolskit.Handlers.BraveSearch.Helpers;
using mcp_toolskit.Models;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.NET.Server.Builder;
using Polly.Extensions.Http;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mcp_toolskit.Handlers
{
    public class BraveSearchToolsConfig: IModuleConfiguration
    {
        public void ConfigureTools(IToolRegistry tools, AppConfig appConfig)
        {
            if (appConfig.ValidateTool("BraveWebSearch"))
                tools.AddHandler<BraveWebSearchToolHandler>();
            if (appConfig.ValidateTool("BraveLocalSearch"))
                tools.AddHandler<BraveLocalSearchToolHandler>();

        }

        public void ConfigureServices(IServiceCollection services, AppConfig appConfig)
        {
            // Add HttpClient with configuration
            var BraveSearchHttpClient = services.AddHttpClient("BraveSearch")
                .ConfigureHttpClient(client =>
                {
                    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
                })
                .SetHandlerLifetime(TimeSpan.FromMinutes(5));  // Définir la durée de vie du handler                       

            if (appConfig.BraveSearch.IgnoreSSLErrors)
            {
                BraveSearchHttpClient.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                });
            }

            BraveSearchHttpClient.AddPolicyHandler(GetRetryPolicy());  
            BraveSearchHttpClient.AddHttpMessageHandler<RetryHandler>();

            services.AddTransient<RetryHandler>();
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
