namespace Domain.DTOs;

public class OnboardingTrendsRequestDto
{
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// When non-empty, only users whose country code is in this list are included (case-insensitive).
    /// When null or empty, no country filter is applied.
    /// </summary>
    public List<string>? CountryCodes { get; set; }
}

public class OnboardingTrendsResponseDto
{
    public string AppId { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public List<OnboardingTrendDayDto> Days { get; set; } = [];
}

public class OnboardingTrendDayDto
{
    public string Date { get; set; } = string.Empty;
    public List<OnboardingVariantDayStatsDto> Variants { get; set; } = [];
}

public class OnboardingVariantDayStatsDto
{
    public string OnboardingVariant { get; set; } = string.Empty;
    public int UsersCount { get; set; }
    public double TotalRevenue { get; set; }
}
