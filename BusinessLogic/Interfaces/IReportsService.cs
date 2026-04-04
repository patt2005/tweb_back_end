using Domain.DTOs;

namespace BusinessLogic.Interfaces;

public interface IReportsService
{
    Task<CampaignReportResponseDto?> GetCampaignReportAsync(Guid userId, CampaignReportRequestDto request, CancellationToken ct = default);

    Task<PerformanceTrendsResponseDto?> GetPerformanceTrendsAsync(Guid userId, PerformanceTrendsRequestDto request, CancellationToken ct = default);
}