using Client.Extensions;
using Finance.Application.Queries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Client.Filters;

/// <summary>
/// Restricts a controller's group-scoped routes to CURRENT members of that group.
///
/// Membership alone is not enough, so there are two checks. When the route also
/// carries an <c>{expenseId}</c>, that charge must really belong to the
/// <c>{groupId}</c> — most such actions look the charge up by id alone, so without
/// it a member of their own household could reach another household's bill through
/// their own group's URL.
///
/// Answers 404, never 403: a 403 confirms the resource exists, which turns the
/// endpoint into an oracle for probing ids.
///
/// A no-op on actions with no <c>{groupId}</c>, so it is safe on a controller that
/// mixes personal and group routes.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireGroupMembershipAttribute : Attribute, IFilterFactory
{
    public bool IsReusable => false;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        new GroupMembershipFilter(serviceProvider.GetRequiredService<IGroupQuery>());
}

internal sealed class GroupMembershipFilter : IAsyncAuthorizationFilter
{
    private const string GroupKey = "groupId";
    private const string ChargeKey = "expenseId";

    private readonly IGroupQuery _reader;

    public GroupMembershipFilter(IGroupQuery reader) => _reader = reader;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!context.RouteData.Values.TryGetValue(GroupKey, out var rawGroup) ||
            !Guid.TryParse(rawGroup?.ToString(), out var groupId))
            return;

        // Left to the auth layer, so a missing cookie stays a 401 rather than a 404.
        if (context.HttpContext.User?.Identity?.IsAuthenticated != true)
            return;

        var ct = context.HttpContext.RequestAborted;
        var userId = context.HttpContext.User.GetUserId();

        if (!await _reader.IsCurrentMemberAsync(groupId, userId.Value, ct))
        {
            context.Result = new NotFoundResult();
            return;
        }

        if (context.RouteData.Values.TryGetValue(ChargeKey, out var rawCharge) &&
            Guid.TryParse(rawCharge?.ToString(), out var chargeId) &&
            !await _reader.ChargeBelongsToGroupAsync(groupId, chargeId, ct))
        {
            // Same answer as "no such charge" — neither confirms an id.
            context.Result = new NotFoundResult();
        }
    }
}
