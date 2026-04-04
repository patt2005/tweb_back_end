using BusinessLogic.Database;
using BusinessLogic.Interfaces;
using Domain.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Services;

public class OnboardingService : IOnboardingService
{
    private readonly AppDbContext _db;

    public OnboardingService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Dictionary<string, List<OnboardingVariantStatsDto>>> FetchUsersByAppAndOnboardingVariantAsync(CancellationToken ct = default)
    {
        var groupedStats = await _db.AppUsers
            .AsNoTracking()
            .Where(u => !string.IsNullOrWhiteSpace(u.OnboardingVariant))
            .GroupBy(u => new { u.AppId, u.OnboardingVariant })
            .Select(g => new OnboardingVariantStatsDto
            {
                AppId = g.Key.AppId,
                OnboardingVariant = g.Key.OnboardingVariant,
                UsersCount = g.Count(),
                TotalRevenue = g.Sum(u => u.TotalRevenue)
            })
            .OrderBy(x => x.AppId)
            .ThenBy(x => x.OnboardingVariant)
            .ToListAsync(ct);

        var allVariants = groupedStats
            .Select(x => x.OnboardingVariant)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        return groupedStats
            .GroupBy(x => x.AppId)
            .ToDictionary(
                appGroup => appGroup.Key,
                appGroup => allVariants
                    .Select(variant =>
                    {
                        var existing = appGroup.FirstOrDefault(x => x.OnboardingVariant == variant);
                        return existing ?? new OnboardingVariantStatsDto
                        {
                            AppId = appGroup.Key,
                            OnboardingVariant = variant,
                            UsersCount = 0,
                            TotalRevenue = 0
                        };
                    })
                    .ToList()
            );
    }
}
