using System.Security.Claims;
using BusinessLogic.Interfaces;
using Domain.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AppsController : ControllerBase
{
    private readonly IAppService _appService;

    public AppsController(IAppService appService)
    {
        _appService = appService;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { message = "User is not authenticated." });

        var items = await _appService.ListAsync(userId.Value, ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { message = "User is not authenticated." });

        var app = await _appService.GetByIdAsync(id, userId.Value, ct);
        if (app == null)
            return NotFound(new { message = "App not found." });

        return Ok(app);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAppDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { message = "User is not authenticated." });

        var created = await _appService.CreateAsync(dto, userId.Value, ct);
        if (created == null)
            return BadRequest(new { message = "Name is required." });

        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAppDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { message = "User is not authenticated." });

        var updated = await _appService.UpdateAsync(id, userId.Value, dto, ct);
        if (updated == null)
            return NotFound(new { message = "App not found, or name is invalid." });

        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized(new { message = "User is not authenticated." });

        var deleted = await _appService.DeleteAsync(id, userId.Value, ct);
        if (!deleted)
            return NotFound(new { message = "App not found." });

        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
