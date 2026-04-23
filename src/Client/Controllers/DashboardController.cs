using Bills.Application.Contracts;
using Bills.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Client.Controllers;

[ApiController]
[Authorize]
[Route("api/bills/households/{householdId:guid}/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardQuery _query;

    public DashboardController(IDashboardQuery query) => _query = query;

    [HttpGet]
    public async Task<IActionResult> Query(Guid householdId, [FromQuery] DateTime? periodStart, [FromQuery] DateTime? periodEnd, CancellationToken ct = default)
    {
        var start = periodStart ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var end   = periodEnd   ?? start.AddMonths(1).AddDays(-1);
        var result = await _query.QueryAsync(new DashboardQueryRequest(householdId, start, end), ct);
        return Ok(result);
    }

    [HttpGet("coverage")]
    public async Task<IActionResult> Coverage(Guid householdId, [FromQuery] DateTime? periodStart, [FromQuery] DateTime? periodEnd, CancellationToken ct = default)
    {
        var start = periodStart ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var end   = periodEnd   ?? start.AddMonths(1).AddDays(-1);
        var result = await _query.GetCoverageStatusAsync(new CoverageStatusQueryRequest(householdId, start, end), ct);
        return Ok(result);
    }
}

