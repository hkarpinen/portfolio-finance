using Finance.Application.Contracts;
using Finance.Application.Managers;
using Finance.Application.Queries;
using Client.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Client.Controllers;

[ApiController]
[Authorize]
[Route("api/finance/personal-bills")]
public sealed class PersonalBillsController : ControllerBase
{
    private readonly IPersonalBillManager _manager;
    private readonly IPersonalBillQuery _query;

    public PersonalBillsController(IPersonalBillManager manager, IPersonalBillQuery query)
    {
        _manager = manager;
        _query = query;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _query.ListByUserAsync(new ListPersonalBillsRequest(userId.Value, page, pageSize, ActiveOnly: true), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct = default)
    {
        var result = await _query.GetDetailAsync(new PersonalBillDetailRequest(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePersonalBillRequest request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _manager.CreateAsync(request with { UserId = userId.Value }, ct);
        return CreatedAtAction(nameof(Get), new { id = result.PersonalBillId }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePersonalBillRequest request, CancellationToken ct = default)
    {
        var result = await _manager.UpdateAsync(request with { PersonalBillId = id }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var result = await _manager.DeleteAsync(new DeletePersonalBillRequest(id), ct);
        return result is null ? NotFound() : NoContent();
    }
}
