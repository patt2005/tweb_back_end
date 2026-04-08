using System.Globalization;
using System.Linq;
using System.Text.Json;
using BusinessLogic.Database;
using BusinessLogic.Interfaces;
using Domain.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Services;

public class ReportsService : IReportsService
{
    private const int AppleCampaignReportPageSize = 200;

    private readonly IAppleSearchAdsApiClient _apiClient;
    private readonly AppDbContext _db;

    private sealed class DayRollup
    {
        public decimal Spend;
        public string? Currency;
        public long Impressions;
        public long Taps;
        public long Installs;
    }

    public ReportsService(IAppleSearchAdsApiClient apiClient, AppDbContext db)
    {
        _apiClient = apiClient;
        _db = db;
    }

    public async Task<CampaignReportResponseDto?> GetCampaignReportAsync(Guid userId, CampaignReportRequestDto request, CancellationToken ct = default)
    {
        try
        {
            var report = await FetchCampaignReportFromAppleAsync(userId, request, ct);
            if (report?.Data?.ReportingDataResponse?.Row == null)
                return report;

            if (!TryParseReportDateRange(request.StartTime, request.EndTime, out var startUtc, out var endUtc))
                return report;

            foreach (var row in report.Data.ReportingDataResponse.Row)
            {
                var campaignId = row.Metadata?.CampaignId;
                if (!campaignId.HasValue)
                    continue;

                var (revenue, userCount, trialsCount, payingUserCount) = await GetRevenueAndUserCountsForCampaignInRangeAsync(campaignId.Value, startUtc, endUtc, ct);
                row.Revenue = (decimal)revenue;
                row.TrialsCount = trialsCount;

                GetAggregatedSpendAndInstalls(row, out var localSpendAmount, out var totalInstallsForRow);

                row.Arpu = totalInstallsForRow > 0 ? (decimal)revenue / totalInstallsForRow : 0;

                row.Trial2PaidConversionRate = trialsCount > 0 ? (double)payingUserCount / trialsCount * 100.0 : 0;
                row.Install2TrialConversionRate = totalInstallsForRow > 0 ? (double)trialsCount / totalInstallsForRow * 100.0 : 0;
                row.Install2PaidConversionRate = totalInstallsForRow > 0 ? (double)payingUserCount / totalInstallsForRow * 100.0 : 0;

                if (localSpendAmount > 0)
                {
                    row.Roas = row.Revenue / localSpendAmount;
                    row.Cac = payingUserCount > 0 ? localSpendAmount / payingUserCount : 0;
                    row.CostPerTrial = trialsCount > 0 ? localSpendAmount / trialsCount : 0;
                }
                else
                {
                    row.Roas = 0;
                    row.Cac = 0;
                    row.CostPerTrial = 0;
                }
            }

            return report;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error while fetching campaign report:");
            Console.WriteLine(ex.Message);
            return null;
        }
    }

    public async Task<PerformanceTrendsResponseDto?> GetPerformanceTrendsAsync(Guid userId, PerformanceTrendsRequestDto request, CancellationToken ct = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.StartTime) || string.IsNullOrWhiteSpace(request.EndTime))
            return null;

        if (!TryParseReportDateRange(request.StartTime, request.EndTime, out var startUtc, out var endUtc))
            return null;

        if (request.AppleSearchAdsAppIds is not { Count: > 0 })
            return null;

        var asaIdSet = request.AppleSearchAdsAppIds.Distinct().ToHashSet();
        var appRows = await _db.Apps
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.AppleSearchAdsId != null && asaIdSet.Contains(a.AppleSearchAdsId.Value))
            .ToListAsync(ct);

        foreach (var id in asaIdSet)
        {
            if (!appRows.Any(a => a.AppleSearchAdsId == id))
                return null;
        }

        var revenueCatIds = appRows
            .Select(a => a.RevenueCatId.Trim())
            .Where(rc => rc.Length > 0)
            .Distinct()
            .ToHashSet(StringComparer.Ordinal);

        var timeZone = string.IsNullOrWhiteSpace(request.TimeZone) ? "UTC" : request.TimeZone;
        var rollupByDay = new Dictionary<string, DayRollup>(StringComparer.Ordinal);

        var adamConditionValues = asaIdSet
            .Select(id => id.ToString(CultureInfo.InvariantCulture))
            .ToList();

        for (var offset = 0; ; offset += AppleCampaignReportPageSize)
        {
            var appleRequest = new CampaignReportRequestDto
            {
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                TimeZone = timeZone,
                Granularity = "DAILY",
                ReturnRowTotals = false,
                ReturnGrandTotals = false,
                Selector = new CampaignReportSelectorDto
                {
                    OrderBy = new List<CampaignReportOrderByDto>
                    {
                        new() { Field = "localSpend", SortOrder = "DESCENDING" }
                    },
                    Conditions = new List<CampaignReportConditionDto>
                    {
                        new()
                        {
                            Field = "adamId",
                            Operator = "IN",
                            Values = adamConditionValues
                        }
                    },
                    Pagination = new CampaignReportPaginationDto { Offset = offset, Limit = AppleCampaignReportPageSize }
                }
            };

            var report = await FetchCampaignReportFromAppleAsync(userId, appleRequest, ct);
            if (report?.Data?.ReportingDataResponse == null)
            {
                if (offset == 0)
                    return null;
                break;
            }

            var rows = report.Data.ReportingDataResponse.Row ?? new List<CampaignReportRowDto>();
            if (rows.Count == 0)
                break;

            foreach (var row in rows)
            {
                if (row.Granularity is { Count: > 0 } buckets)
                {
                    foreach (var g in buckets)
                    {
                        var dayKey = NormalizeReportDateString(g.Date);
                        if (dayKey == null)
                            continue;

                        if (!rollupByDay.TryGetValue(dayKey, out var rollup))
                        {
                            rollup = new DayRollup();
                            rollupByDay[dayKey] = rollup;
                        }

                        rollup.Spend += ParseAmount(g.LocalSpend?.Amount) ?? 0;
                        rollup.Currency ??= g.LocalSpend?.Currency;
                        rollup.Impressions += g.Impressions ?? 0;
                        rollup.Taps += g.Taps ?? 0;
                        rollup.Installs += g.TotalInstalls ?? 0;
                    }
                }
                else
                {
                    var dayKey = ResolveCampaignReportRowDate(row);
                    if (dayKey == null)
                        continue;

                    if (!rollupByDay.TryGetValue(dayKey, out var rollup))
                    {
                        rollup = new DayRollup();
                        rollupByDay[dayKey] = rollup;
                    }

                    rollup.Spend += ParseAmount(row.Total?.LocalSpend?.Amount) ?? 0;
                    rollup.Currency ??= row.Total?.LocalSpend?.Currency;
                    rollup.Impressions += row.Total?.Impressions ?? 0;
                    rollup.Taps += row.Total?.Taps ?? 0;
                    rollup.Installs += row.Total?.TotalInstalls ?? 0;
                }
            }

            if (rows.Count < AppleCampaignReportPageSize)
                break;
        }

        var dbAggQuery = _db.AppUsers
            .AsNoTracking()
            .Where(u => u.InstallDate >= startUtc && u.InstallDate <= endUtc);

        dbAggQuery = revenueCatIds.Count > 0
            ? dbAggQuery.Where(u => revenueCatIds.Contains(u.AppId))
            : dbAggQuery.Where(u => false);

        var dbAgg = await dbAggQuery
            .GroupBy(u => u.InstallDate.Date)
            .Select(g => new { Day = g.Key, Revenue = g.Sum(u => u.TotalRevenue) })
            .ToListAsync(ct);

        var revenueByDay = dbAgg.ToDictionary(
            x => x.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            x => x.Revenue);

        var startDate = startUtc.Date;
        var endDate = endUtc.Date;
        var days = new List<PerformanceTrendDayDto>();
        string? firstCurrency = null;
        for (var d = startDate; d <= endDate; d = d.AddDays(1))
        {
            var key = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            rollupByDay.TryGetValue(key, out var rollup);
            revenueByDay.TryGetValue(key, out var revenue);
            if (firstCurrency == null && rollup?.Currency != null)
                firstCurrency = rollup.Currency;

            var impressions = rollup?.Impressions ?? 0;
            var taps = rollup?.Taps ?? 0;
            var installs = rollup?.Installs ?? 0;
            var ttr = impressions > 0 ? (double)taps / impressions : 0;
            var cr = taps > 0 ? (double)installs / taps : 0;

            var spend = rollup?.Spend ?? 0;
            var roas = spend > 0 ? (decimal)revenue / spend : 0;

            days.Add(new PerformanceTrendDayDto
            {
                Date = key,
                Spend = spend,
                Revenue = revenue,
                Roas = roas,
                Ttr = ttr,
                Cr = cr
            });
        }

        return new PerformanceTrendsResponseDto
        {
            Granularity = "DAILY",
            Currency = firstCurrency,
            Days = days
        };
    }

    private async Task<CampaignReportResponseDto?> FetchCampaignReportFromAppleAsync(Guid userId, CampaignReportRequestDto request, CancellationToken ct)
    {
        var response = await _apiClient.PostAsJsonAsync(userId, $"{AppleSearchAdsApiClientService.BaseUrl}/reports/campaigns", request, ct);

        if (response == null || !response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        response.Dispose();
        return JsonSerializer.Deserialize<CampaignReportResponseDto>(json);
    }

    private static string? ResolveCampaignReportRowDate(CampaignReportRowDto row)
    {
        return NormalizeReportDateString(row.Date ?? row.Total?.Date);
    }

    private static string? NormalizeReportDateString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            return dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return raw.Length >= 10 && raw[4] == '-' && raw[7] == '-'
            ? raw[..10]
            : null;
    }

    private static void GetAggregatedSpendAndInstalls(CampaignReportRowDto row, out decimal spend, out int totalInstalls)
    {
        spend = 0;
        totalInstalls = 0;

        if (row.Granularity is { Count: > 0 } buckets &&
            buckets.Any(static g =>
                g.LocalSpend != null
                || (g.Impressions ?? 0) != 0
                || (g.Taps ?? 0) != 0
                || (g.TotalInstalls ?? 0) != 0))
        {
            foreach (var g in buckets)
            {
                spend += ParseAmount(g.LocalSpend?.Amount) ?? 0;
                totalInstalls += g.TotalInstalls ?? 0;
            }

            return;
        }

        spend = ParseAmount(row.Total?.LocalSpend?.Amount) ?? 0;
        totalInstalls = row.Total?.TotalInstalls ?? 0;
    }

    private static bool TryParseReportDateRange(string? startTime, string? endTime, out DateTime startUtc, out DateTime endUtc)
    {
        startUtc = default;
        endUtc = default;
        if (string.IsNullOrWhiteSpace(startTime) || string.IsNullOrWhiteSpace(endTime))
            return false;
        if (!DateTime.TryParse(startTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out startUtc))
            return false;
        if (!DateTime.TryParse(endTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out endUtc))
            return false;
        startUtc = startUtc.Date;
        endUtc = endUtc.Date.AddDays(1).AddTicks(-1);
        return true;
    }

    private async Task<(double Revenue, int UserCount, int TrialsCount, int PayingUserCount)> GetRevenueAndUserCountsForCampaignInRangeAsync(long campaignId, DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        var query = _db.AppUsers
            .AsNoTracking()
            .Where(u => u.CampaignId == campaignId && u.InstallDate >= startUtc && u.InstallDate <= endUtc);

        var revenue = await query.SumAsync(u => u.TotalRevenue, ct);
        var userCount = await query.CountAsync(ct);
        var trialsCount = await query.CountAsync(u => u.HasTrial, ct);
        var payingUserCount = await query.CountAsync(u => u.TotalRevenue > 0, ct);
        return (revenue, userCount, trialsCount, payingUserCount);
    }

    private static decimal? ParseAmount(string? amount)
    {
        if (string.IsNullOrWhiteSpace(amount))
            return null;
        return decimal.TryParse(amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
