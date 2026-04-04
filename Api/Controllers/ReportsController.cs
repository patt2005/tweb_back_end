using System.Globalization;
using System.Security.Claims;
using BusinessLogic.Interfaces;
using Domain.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IKeywordService _keywordService;
    private readonly IReportsService _reportsService;

    public ReportsController(IKeywordService keywordService, IReportsService reportsService)
    {
        _keywordService = keywordService;
        _reportsService = reportsService;
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    [HttpPost("keywords/{campaignId:long}")]
    public async Task<IActionResult> GetKeywordReport(long campaignId, [FromBody] KeywordReportRequestDto request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(new { message = "User is not authenticated." });

        var report = await _keywordService.GetKeywordReportAsync(campaignId, userId.Value, request, ct);
        if (report == null)
            return BadRequest(new { message = "Failed to fetch keyword report. Check Apple Search Ads credentials and request body." });

        return Ok(report);
    }

    [HttpPost("campaigns")]
    public async Task<IActionResult> GetCampaignReportList([FromBody] CampaignReportRequestDto request, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(new { message = "User is not authenticated." });

        var report = await _reportsService.GetCampaignReportAsync(userId.Value, request, ct);
        if (report == null)
            return BadRequest(new { message = "Failed to fetch campaign report. Check Apple Search Ads credentials and request body." });

        return Ok(report);
    }

    [HttpPost("performance-trends")]
    public async Task<IActionResult> GetPerformanceTrends([FromBody] PerformanceTrendsRequestDto request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(new { message = "User is not authenticated." });

        if (string.IsNullOrWhiteSpace(request.StartTime) || string.IsNullOrWhiteSpace(request.EndTime))
            return BadRequest(new { message = "startTime and endTime are required (YYYY-MM-DD)." });

        if (!DateTime.TryParse(request.StartTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var start))
            return BadRequest(new { message = "startTime must be a valid date (YYYY-MM-DD)." });

        if (!DateTime.TryParse(request.EndTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var end))
            return BadRequest(new { message = "endTime must be a valid date (YYYY-MM-DD)." });

        if (start.Date > end.Date)
            return BadRequest(new { message = "startTime must be on or before endTime." });

        if ((end.Date - start.Date).TotalDays > 90)
            return BadRequest(new { message = "Date range must be at most 90 days for DAILY reports (Apple Search Ads limit)." });

        var trend = await _reportsService.GetPerformanceTrendsAsync(userId.Value, request, ct);
        if (trend == null)
            return BadRequest(new { message = "Failed to fetch performance trends. Check Apple Search Ads credentials and request body." });

        return Ok(trend);
    }
}
