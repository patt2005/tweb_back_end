using System.Globalization;
using System.Security.Claims;
using BusinessLogic.Interfaces;
using Domain.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OnboardingController : ControllerBase
{
    private readonly IOnboardingService _onboardingService;

    public OnboardingController(IOnboardingService onboardingService)
    {
        _onboardingService = onboardingService;
    }

    [HttpGet("fetch-users")]
    public async Task<IActionResult> FetchUsers(CancellationToken ct)
    {
        var data = await _onboardingService.FetchUsersByAppAndOnboardingVariantAsync(ct);
        return Ok(data);
    }

    [HttpPost("trends")]
    public async Task<IActionResult> GetTrends([FromBody] OnboardingTrendsRequestDto request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { message = "User is not authenticated." });

        if (request == null
            || string.IsNullOrWhiteSpace(request.StartDate)
            || string.IsNullOrWhiteSpace(request.EndDate)
            || string.IsNullOrWhiteSpace(request.AppId))
            return BadRequest(new { message = "startDate, endDate, and appId are required." });

        if (!DateTime.TryParse(request.StartDate, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var startParsed)
            || !DateTime.TryParse(request.EndDate, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var endParsed))
            return BadRequest(new { message = "startDate and endDate must be valid dates (e.g. YYYY-MM-DD)." });

        if (startParsed.Date > endParsed.Date)
            return BadRequest(new { message = "startDate must be on or before endDate." });

        if ((endParsed.Date - startParsed.Date).TotalDays > 366)
            return BadRequest(new { message = "Date range must be at most 366 days." });

        var trend = await _onboardingService.GetOnboardingTrendsAsync(userId.Value, request, ct);
        if (trend == null)
            return BadRequest(new { message = "App not found for this account, or request is invalid." });

        return Ok(trend);
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
