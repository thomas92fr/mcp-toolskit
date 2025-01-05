using mcp_toolskit.Attributes;
using mcp_toolskit.Extentions;
using mcp_toolskit.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.NET.Core.Models.Protocol.Client.Responses;
using ModelContextProtocol.NET.Core.Models.Protocol.Common;
using ModelContextProtocol.NET.Core.Models.Protocol.Shared.Content;
using ModelContextProtocol.NET.Server.Contexts;
using ModelContextProtocol.NET.Server.Features.Tools;
using Serilog.Context;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace mcp_toolskit.Handlers.Systems;

[JsonConverter(typeof(JsonStringEnumConverter<SystemInfoOperation>))]
public enum SystemInfoOperation
{
    [Description("Returns geographical location and current date and time")]
    GetLocationAndTime
}

public class SystemInfoParameters
{
    public required SystemInfoOperation Operation { get; init; }

    public override string ToString()
    {
        return $"Operation: {Operation}";
    }
}

[JsonSerializable(typeof(SystemInfoParameters))]
public partial class SystemInfoParametersJsonContext : JsonSerializerContext { }

public class SystemInfoToolHandler : ToolHandlerBase<SystemInfoParameters>
{
    private readonly ILogger<SystemInfoToolHandler> _logger;
    private readonly HttpClient _httpClient;

    public SystemInfoToolHandler(
        IServerContext serverContext,
        ISessionContext sessionContext,
        ILogger<SystemInfoToolHandler> logger,
        IHttpClientFactory httpClientFactory
    ) : base(tool, serverContext, sessionContext)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("SystemInfo");
    }

    private static readonly Tool tool = new()
    {
        Name = "SystemInfo",
        Description = typeof(SystemInfoOperation).GenerateFullDescription(),
        InputSchema = SystemInfoParametersJsonContext.Default.SystemInfoParameters.GetToolSchema()!
    };

    public override JsonTypeInfo JsonTypeInfo =>
        SystemInfoParametersJsonContext.Default.SystemInfoParameters;

    protected override async Task<CallToolResult> HandleAsync(
        SystemInfoParameters parameters,
        CancellationToken cancellationToken = default
    )
    {
        using var _ = LogContext.PushProperty("ExecutionId", Guid.NewGuid());
        _logger.LogInformation("Query: {parameters}", parameters.ToString());

        try
        {
            string result = parameters.Operation switch
            {
                SystemInfoOperation.GetLocationAndTime => await GetLocationAndTimeAsync(cancellationToken),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };

            var content = new TextContent { Text = result };
            _logger.LogInformation("Result: {content}", content);

            return new CallToolResult { Content = new Annotated[] { content } };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in system info operation");
            throw;
        }
    }

    private async Task<string> GetLocationAndTimeAsync(CancellationToken cancellationToken)
    {
        var currentTime = new
        {
            DateTime = DateTime.Now.ToString("F"),
            Timezone = TimeZoneInfo.Local.Id,
            UtcOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).ToString()
        };

        try
        {
            // Obtenir la localisation via ip-api.com
            var response = await _httpClient.GetStringAsync("http://ip-api.com/json/?fields=status,message,city,regionName,country,query,lat,lon", cancellationToken);
            var locationInfo = JsonSerializer.Deserialize<JsonElement>(response);

            // Vérifier le statut de la réponse
            var status = locationInfo.GetProperty("status").GetString();
            if (status != "success")
            {
                throw new Exception(locationInfo.GetProperty("message").GetString() ?? "Unknown error from ip-api.com");
            }

            var location = new
            {
                currentTime.DateTime,
                currentTime.Timezone,
                currentTime.UtcOffset,
                City = locationInfo.GetProperty("city").GetString(),
                Region = locationInfo.GetProperty("regionName").GetString(),
                Country = locationInfo.GetProperty("country").GetString(),
                IP = locationInfo.GetProperty("query").GetString(),
                Latitude = locationInfo.GetProperty("lat").GetDouble(),
                Longitude = locationInfo.GetProperty("lon").GetDouble()
            };

            return JsonSerializer.Serialize(location, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération des informations système");

            var fallbackLocation = new
            {
                currentTime.DateTime,
                currentTime.Timezone,
                currentTime.UtcOffset,
                Location = "Position unknown",
                Error = ex.Message
            };

            return JsonSerializer.Serialize(fallbackLocation, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
    }
}