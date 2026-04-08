using BusinessLogic.Database;
using BusinessLogic.Interfaces;
using Domain.DTOs;
using Domain.Entities.App;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogic.Services;

public class AppService : IAppService
{
    private readonly AppDbContext _dbContext;

    public AppService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AppResponseDto>> ListAsync(Guid userId, CancellationToken ct)
    {
        return await _dbContext.Apps
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.Name)
            .Select(a => Map(a))
            .ToListAsync(ct);
    }

    public async Task<AppResponseDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var app = await _dbContext.Apps
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        return app == null ? null : Map(app);
    }

    public async Task<AppResponseDto?> CreateAsync(CreateAppDto dto, Guid userId, CancellationToken ct)
    {
        var name = dto.Name.Trim();
        if (string.IsNullOrEmpty(name))
            return null;

        var app = new App
        {
            Id = Guid.NewGuid(),
            Name = name,
            UserId = userId,
            RevenueCatId = dto.RevenueCatId.Trim(),
            AppleSearchAdsId = dto.AppleSearchAdsId
        };

        _dbContext.Apps.Add(app);
        await _dbContext.SaveChangesAsync(ct);

        return Map(app);
    }

    public async Task<AppResponseDto?> UpdateAsync(Guid id, Guid userId, UpdateAppDto dto, CancellationToken ct)
    {
        var name = dto.Name.Trim();
        if (string.IsNullOrEmpty(name))
            return null;

        var app = await _dbContext.Apps.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        if (app == null)
            return null;

        app.Name = name;
        app.RevenueCatId = dto.RevenueCatId.Trim();
        app.AppleSearchAdsId = dto.AppleSearchAdsId;

        await _dbContext.SaveChangesAsync(ct);

        return Map(app);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var app = await _dbContext.Apps.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        if (app == null)
            return false;

        _dbContext.Apps.Remove(app);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    private static AppResponseDto Map(App a) => new()
    {
        Id = a.Id,
        Name = a.Name,
        RevenueCatId = a.RevenueCatId,
        AppleSearchAdsId = a.AppleSearchAdsId
    };
}
