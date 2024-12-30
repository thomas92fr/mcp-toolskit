using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mcp_toolskit.Handlers.BraveSearch.Helpers
{
    public class RateLimiter
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly int _maxParallelism;
        private readonly int _intervalMs;
        private DateTime _lastReleaseTime;
        private readonly object _lock = new();

        public RateLimiter(int maxParallelism = 1, int intervalMs = 1000)
        {
            _maxParallelism = maxParallelism;
            _intervalMs = intervalMs;
            _semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);
            _lastReleaseTime = DateTime.UtcNow;
        }

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
        {
            await _semaphore.WaitAsync();
            try
            {
                await EnsureIntervalAsync();
                return await action();
            }
            finally
            {
                lock (_lock)
                {
                    _lastReleaseTime = DateTime.UtcNow;
                }
                _semaphore.Release();
            }
        }

        private async Task EnsureIntervalAsync()
        {
            TimeSpan elapsedSinceLastRelease;
            lock (_lock)
            {
                elapsedSinceLastRelease = DateTime.UtcNow - _lastReleaseTime;
            }

            if (elapsedSinceLastRelease.TotalMilliseconds < _intervalMs)
            {
                await Task.Delay(_intervalMs - (int)elapsedSinceLastRelease.TotalMilliseconds);
            }
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }
    }
}
