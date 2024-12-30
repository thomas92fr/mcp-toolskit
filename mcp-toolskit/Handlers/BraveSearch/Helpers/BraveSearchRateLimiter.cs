using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mcp_toolskit.Handlers.BraveSearch.Helpers
{
    public static class BraveSearchRateLimiter
    {
        public static readonly RateLimiter Instance = new RateLimiter(1, 1000);
    }
}
