using Bills.Application.Contracts;
using Bills.Application.Managers;
using Client.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Client.Controllers;

/// <summary>
/// Top-level membership endpoint for operations that don't require knowing the household upfront.
/// </summary>
[ApiController]
[Authorize]
[Route("api/bills/members")]
public sealed class MembersController : ControllerBase
{
    private readonly IHouseholdMembershipManager _membershipManager;

    public MembersController(IHouseholdMembershipManager membershipManager)
        => _membershipManager = membershipManager;

    /// <summary>
    /// Join a household using only the invitation code. The backend resolves which household
    /// the code belongs to, eliminating the need for the client to know the householdId.
    /// </summary>
    [HttpPost("join")]
    public async Task<IActionResult> JoinByCode([FromBody] JoinByCodeRequest request, CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;
        var result = await _membershipManager.JoinByCodeAsync(request, userId, ct);
        return result is null
            ? NotFound(new { error = "Invalid invitation code." })
            : Ok(result);
    }
}
