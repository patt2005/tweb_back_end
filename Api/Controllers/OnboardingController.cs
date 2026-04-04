using BusinessLogic.Interfaces;
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
}
