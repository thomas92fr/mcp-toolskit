using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace mcp_toolskit.Handlers.BraveSearch.Helpers
{
    public class RetryHandler : DelegatingHandler
    {
        private readonly ILogger<RetryHandler> _logger;
        private readonly int _maxRetries;
        private readonly int _initialDelayMs;

        public RetryHandler(ILogger<RetryHandler> logger, int maxRetries = 3, int initialDelayMs = 1000)
        {
            _logger = logger;
            _maxRetries = maxRetries;
            _initialDelayMs = initialDelayMs;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            for (int i = 0; i < _maxRetries; i++)
            {
                try
                {
                    var response = await base.SendAsync(request, cancellationToken);

                    if (response.StatusCode != HttpStatusCode.TooManyRequests)
                    {
                        return response;
                    }

                    var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Pow(2, i));
                    _logger.LogWarning("Rate limit exceeded. Waiting {RetryAfter} before retry {RetryCount}/{MaxRetries}",
                        retryAfter, i + 1, _maxRetries);

                    await Task.Delay(retryAfter, cancellationToken);
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (i == _maxRetries - 1) throw;

                    var delay = TimeSpan.FromMilliseconds(_initialDelayMs * Math.Pow(2, i));
                    _logger.LogWarning("Rate limit exceeded. Waiting {Delay}ms before retry {RetryCount}/{MaxRetries}",
                        delay.TotalMilliseconds, i + 1, _maxRetries);

                    await Task.Delay(delay, cancellationToken);
                }
            }

            throw new HttpRequestException("Max retries exceeded");
        }
    }
}
