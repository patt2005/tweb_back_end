using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BusinessLogic.Database;
using BusinessLogic.Interfaces;
using Domain.DTOs;
using Domain.Entities.App;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Services;

public class RevenuecatUserService : IRevenuecatUserService
{
    private const string AttrCampaign = "$campaign";
    private const string AttrKeyword = "$keyword";
    private const string AttrAdGroup = "$adGroup";
    private const string AttrInstallDate = "installDate";
    private const string AttrOnboardingVariant = "onboardingVariant";

    private readonly AppDbContext _db;

    public RevenuecatUserService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AppUser?> SetUserAsync(AddRevenuecatUserDto dto, CancellationToken ct = default)
    {
        var evt = dto.Event;
        var appUserId = evt?.AppUserId;
        if (string.IsNullOrWhiteSpace(appUserId))
            return null;

        var attrs = evt?.SubscriberAttributes;
        var installDate = ParseInstallDate(attrs) ?? DateTime.UtcNow;
        var campaignId = ParseLong(attrs, AttrCampaign);
        var keywordId = ParseLong(attrs, AttrKeyword);
        var adGroupId = ParseLong(attrs, AttrAdGroup);
        var onboardingVariant = GetAttrValue(attrs, AttrOnboardingVariant) ?? string.Empty;
        var appId = evt?.AppId ?? string.Empty;
        var countryCode = evt?.CountryCode ?? string.Empty;

        var subscriberKey = SubscriberKeyFromRevenueCatId(appUserId);
        var existing = await _db.AppUsers
            .FirstOrDefaultAsync(u => u.Id == subscriberKey, ct);

        if (existing != null)
        {
            existing.CountryCode = countryCode;
            existing.InstallDate = installDate;
            existing.CampaignId = campaignId;
            existing.KeywordId = keywordId;
            existing.AdGroupId = adGroupId;
            existing.OnboardingVariant = onboardingVariant;
            existing.AppId = appId;
            ApplyEventToUser(existing, evt);
            await _db.SaveChangesAsync(ct);
            return existing;
        }

        var appUser = new AppUser
        {
            Id = subscriberKey,
            CountryCode = countryCode,
            InstallDate = installDate,
            TotalRevenue = 0,
            CampaignId = campaignId,
            KeywordId = keywordId,
            AdGroupId = adGroupId,
            OnboardingVariant = onboardingVariant,
            AppId = appId
        };
        ApplyEventToUser(appUser, evt);

        _db.AppUsers.Add(appUser);
        await _db.SaveChangesAsync(ct);
        return appUser;
    }

    private static Guid SubscriberKeyFromRevenueCatId(string appUserId)
    {
        if (Guid.TryParse(appUserId, out var g))
            return g;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(appUserId));
        Span<byte> slice = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(slice);
        return new Guid(slice);
    }

    private static bool IsCorrectEvent(string type)
    {
        return string.Equals(type, "RENEWAL", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(type, "INITIAL_PURCHASE", StringComparison.OrdinalIgnoreCase) || 
               string.Equals(type, "NON_RENEWING_PURCHASE", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyEventToUser(AppUser user, RevenueCatSetUserEventDto? evt)
    {
        if (evt == null) return;

        if (string.Equals(evt.PeriodType, "TRIAL", StringComparison.OrdinalIgnoreCase))
            user.HasTrial = true;

        var type = evt.Type ?? string.Empty;
        if (IsCorrectEvent(type) && evt.Price.HasValue && evt.Price.Value > 0)
        {
            user.TotalRevenue += (double)evt.Price.Value;
        }
    }

    private static DateTime? ParseInstallDate(Dictionary<string, RevenueCatSubscriberAttributeItemDto>? attrs)
    {
        var raw = GetAttrValue(attrs, AttrInstallDate);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d) ? d : null;
    }

    private static long? ParseLong(Dictionary<string, RevenueCatSubscriberAttributeItemDto>? attrs, string key)
    {
        var raw = GetAttrValue(attrs, key);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return long.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static string? GetAttrValue(Dictionary<string, RevenueCatSubscriberAttributeItemDto>? attrs, string key)
    {
        if (attrs == null || !attrs.TryGetValue(key, out var item)) return null;
        return item?.Value;
    }
}
