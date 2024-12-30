using System.Text.Json.Serialization;

namespace mcp_toolskit.Handlers.BraveSearch.Models
{
    public class BraveWeb
    {
        [JsonPropertyName("web")]
        public WebResults? Web { get; set; }

        [JsonPropertyName("locations")]
        public LocationResults? Locations { get; set; }

        public class WebResults
        {
            [JsonPropertyName("results")]
            public List<WebResult>? Results { get; set; }
        }

        public class WebResult
        {
            [JsonPropertyName("title")]
            public string Title { get; set; } = string.Empty;

            [JsonPropertyName("description")]
            public string Description { get; set; } = string.Empty;

            [JsonPropertyName("url")]
            public string Url { get; set; } = string.Empty;

            [JsonPropertyName("language")]
            public string? Language { get; set; }

            [JsonPropertyName("published")]
            public string? Published { get; set; }

            [JsonPropertyName("rank")]
            public int? Rank { get; set; }
        }

        public class LocationResults
        {
            [JsonPropertyName("results")]
            public List<LocationResult>? Results { get; set; }
        }

        public class LocationResult
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("title")]
            public string? Title { get; set; }
        }
    }

    public class Address
    {
        [JsonPropertyName("streetAddress")]
        public string? StreetAddress { get; set; }

        [JsonPropertyName("addressLocality")]
        public string? AddressLocality { get; set; }

        [JsonPropertyName("addressRegion")]
        public string? AddressRegion { get; set; }

        [JsonPropertyName("postalCode")]
        public string? PostalCode { get; set; }
    }

    public class Coordinates
    {
        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }
    }

    public class Rating
    {
        [JsonPropertyName("ratingValue")]
        public double? RatingValue { get; set; }

        [JsonPropertyName("ratingCount")]
        public int? RatingCount { get; set; }
    }

    public class BraveLocation
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("address")]
        public Address Address { get; set; } = new();

        [JsonPropertyName("coordinates")]
        public Coordinates? Coordinates { get; set; }

        [JsonPropertyName("phone")]
        public string? Phone { get; set; }

        [JsonPropertyName("rating")]
        public Rating? Rating { get; set; }

        [JsonPropertyName("openingHours")]
        public List<string>? OpeningHours { get; set; }

        [JsonPropertyName("priceRange")]
        public string? PriceRange { get; set; }

    }

    public class BravePoiResponse
    {
        [JsonPropertyName("results")]
        public List<BraveLocation> Results { get; set; } = new();
    }

    public class BraveDescription
    {
        [JsonPropertyName("descriptions")]
        public Dictionary<string, string> Descriptions { get; set; } = new();
    }

    public class RateLimit
    {
        public int PerSecond { get; set; }
        public int PerMonth { get; set; }
    }

    public class RequestCount
    {
        public int Second { get; set; }
        public int Month { get; set; }
        public DateTime LastReset { get; set; }
    }
}