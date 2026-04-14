using Domain.DTOs;

namespace BusinessLogic.Interfaces;

public interface IOnboardingService
{
    Task<Dictionary<string, List<OnboardingVariantStatsDto>>> FetchUsersByAppAndOnboardingVariantAsync(CancellationToken ct = default);

    Task<OnboardingTrendsResponseDto?> GetOnboardingTrendsAsync(Guid userId, OnboardingTrendsRequestDto request, CancellationToken ct = default);
}
