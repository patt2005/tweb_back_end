namespace Domain.DTOs;

public class OnboardingTrendsRequestDto
{
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    /// <summary>RevenueCat app id (must match an app on your account and AppUser.AppId from webhooks).</summary>
    public string AppId { get; set; } = string.Empty;
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
