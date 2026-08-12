using Client.Extensions;
using Client.Filters;
using Finance.Application.Dtos;
using Finance.Application.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Client.Controllers;

/// <summary>
/// Repeating costs — the agreement, not the individual bills.
///
/// A schedule posts nothing. It says which charges should exist; the forecast every screen draws
/// is <c>occurrences</c>, and a charge is written only when somebody acts on one.
/// </summary>
[ApiController]
[Authorize]
[RequireGroupMembership]
[Route("api/finance/schedules")]
public sealed class ChargeSchedulesController(IChargeScheduleManager schedules) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListMine(CancellationToken ct = default)
        => Ok(await schedules.ListForUserAsync(User.GetUserId().Value, ct));

    [HttpGet("/api/finance/groups/{groupId:guid}/schedules")]
    public async Task<IActionResult> ListForGroup(Guid groupId, CancellationToken ct = default)
        => Ok(await schedules.ListForGroupAsync(groupId, ct));

    [HttpPost]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Create([FromBody] CreateChargeScheduleCommand request, CancellationToken ct = default)
    {
        var result = await schedules.CreateAsync(request with { UserId = User.GetUserId().Value }, ct);
        return CreatedAtAction(nameof(ListMine), new { }, result);
    }

    [HttpPut("{scheduleId:guid}")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Amend(Guid scheduleId, [FromBody] AmendChargeScheduleCommand request, CancellationToken ct = default)
    {
        var result = await schedules.AmendAsync(
            request with { ScheduleId = scheduleId, CallerId = User.GetUserId().Value }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{scheduleId:guid}")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Deactivate(Guid scheduleId, CancellationToken ct = default)
        => await schedules.DeactivateAsync(scheduleId, User.GetUserId().Value, ct) ? NoContent() : NotFound();

    /// <summary>
    /// The dates this schedule places a charge on, and the charge for each where one exists.
    /// Most will be null — that is the forecast, and nothing is written until somebody acts.
    /// </summary>
    [HttpGet("{scheduleId:guid}/occurrences")]
    public async Task<IActionResult> Occurrences(
        Guid scheduleId, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct = default)
    {
        if (to <= from) return BadRequest(new { error = "'to' must be after 'from'." });
        return Ok(await schedules.ForecastAsync(scheduleId, from, to, ct));
    }

    /// <summary>Writes the charge for one occurrence. Idempotent — the second call returns the first.</summary>
    [HttpPost("{scheduleId:guid}/occurrences/{occurrenceDate:datetime}")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Materialise(Guid scheduleId, DateTime occurrenceDate, CancellationToken ct = default)
    {
        var charge = await schedules.MaterialiseAsync(scheduleId, occurrenceDate, ct);
        return charge is null
            ? NotFound()
            : Ok(new { chargeId = charge.Id.Value, occurrenceDate = charge.OccurrenceDate, amount = charge.Amount.Amount });
    }
}
