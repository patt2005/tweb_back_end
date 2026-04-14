using System.Globalization;
using BusinessLogic.Database;
using BusinessLogic.Interfaces;
using Domain.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Services;

public class OnboardingService : IOnboardingService
{
    private const int MaxTrendRangeDays = 366;

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

    public async Task<OnboardingTrendsResponseDto?> GetOnboardingTrendsAsync(Guid userId, OnboardingTrendsRequestDto request, CancellationToken ct = default)
    {
        if (request == null
            || string.IsNullOrWhiteSpace(request.StartDate)
            || string.IsNullOrWhiteSpace(request.EndDate)
            || string.IsNullOrWhiteSpace(request.AppId))
            return null;

        if (!TryParseUtcDateRange(request.StartDate, request.EndDate, out var startUtc, out var endUtc, out var startDay, out var endDay))
            return null;

        if (startDay > endDay)
            return null;

        if ((endDay - startDay).TotalDays > MaxTrendRangeDays)
            return null;

        var requestedAppId = request.AppId.Trim();
        var app = await _db.Apps
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .FirstOrDefaultAsync(a => a.RevenueCatId.Trim() == requestedAppId, ct);

        if (app == null)
            return null;

        var revenueCatAppId = app.RevenueCatId.Trim();

        var countryCodes = (request.CountryCodes ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        var aggregates = await _db.AppUsers
            .AsNoTracking()
            .Where(u =>
                u.AppId == revenueCatAppId
                && !string.IsNullOrWhiteSpace(u.OnboardingVariant)
                && u.InstallDate >= startUtc
                && u.InstallDate <= endUtc
                && (countryCodes.Count == 0 || countryCodes.Contains(u.CountryCode.ToUpper())))
            .GroupBy(u => new { Date = u.InstallDate.Date, u.OnboardingVariant })
            .Select(g => new
            {
                g.Key.Date,
                g.Key.OnboardingVariant,
                UsersCount = g.Count(),
                TotalRevenue = g.Sum(u => u.TotalRevenue)
            })
            .ToListAsync(ct);

        var variants = aggregates
            .Select(a => a.OnboardingVariant)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();

        var byDayAndVariant = aggregates.ToDictionary(
            a => (a.Date, a.OnboardingVariant),
            a => (a.UsersCount, a.TotalRevenue));

        var days = new List<OnboardingTrendDayDto>();
        for (var d = startDay; d <= endDay; d = d.AddDays(1))
        {
            var row = new OnboardingTrendDayDto
            {
                Date = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Variants = variants
                    .Select(variant =>
                    {
                        if (byDayAndVariant.TryGetValue((d, variant), out var stats))
                        {
                            return new OnboardingVariantDayStatsDto
                            {
                                OnboardingVariant = variant,
                                UsersCount = stats.UsersCount,
                                TotalRevenue = stats.TotalRevenue
                            };
                        }

                        return new OnboardingVariantDayStatsDto
                        {
                            OnboardingVariant = variant,
                            UsersCount = 0,
                            TotalRevenue = 0
                        };
                    })
                    .ToList()
            };
            days.Add(row);
        }

        return new OnboardingTrendsResponseDto
        {
            AppId = revenueCatAppId,
            StartDate = startDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            EndDate = endDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Days = days
        };
    }

    private static bool TryParseUtcDateRange(
        string startDate,
        string endDate,
        out DateTime startUtc,
        out DateTime endUtc,
        out DateTime startDay,
        out DateTime endDay)
    {
        startUtc = default;
        endUtc = default;
        startDay = default;
        endDay = default;

        if (!DateTime.TryParse(startDate, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var startParsed))
            return false;
        if (!DateTime.TryParse(endDate, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var endParsed))
            return false;

        startDay = startParsed.Date;
        endDay = endParsed.Date;
        startUtc = startDay;
        endUtc = endDay.AddDays(1).AddTicks(-1);
        return true;
    }
}
