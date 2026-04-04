namespace Domain.DTOs;

public class OnboardingVariantStatsDto
{
    public string AppId { get; set; } = string.Empty;
    public string OnboardingVariant { get; set; } = string.Empty;
    public int UsersCount { get; set; }
    public double TotalRevenue { get; set; }
}
