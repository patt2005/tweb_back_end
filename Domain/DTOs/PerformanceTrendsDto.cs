using System.Text.Json.Serialization;

namespace Domain.DTOs;

public class PerformanceTrendsRequestDto
{
    [JsonPropertyName("startTime")]
    public string? StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public string? EndTime { get; set; }

    [JsonPropertyName("timeZone")]
    public string? TimeZone { get; set; }

    /// <summary>
    /// Apple Search Ads app ids (Adam IDs). Must match app rows for the user; revenue uses the mapped RevenueCat app id on each app.
    /// </summary>
    [JsonPropertyName("appleSearchAdsAppIds")]
    public List<long>? AppleSearchAdsAppIds { get; set; }
}

public class PerformanceTrendsResponseDto
{
    [JsonPropertyName("granularity")]
    public string Granularity { get; set; } = "DAILY";

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("days")]
    public List<PerformanceTrendDayDto> Days { get; set; } = new();
}

public class PerformanceTrendDayDto
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("spend")]
    public decimal Spend { get; set; }

    [JsonPropertyName("revenue")]
    public double Revenue { get; set; }

    [JsonPropertyName("roas")]
    public decimal Roas { get; set; }

    [JsonPropertyName("ttr")]
    public double Ttr { get; set; }

    [JsonPropertyName("cr")]
    public double Cr { get; set; }
}
