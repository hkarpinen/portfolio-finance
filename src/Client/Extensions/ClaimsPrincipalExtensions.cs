using System.Security.Claims;
using Finance.Domain.ValueObjects;

namespace Client.Extensions;

internal static class ClaimsPrincipalExtensions
{
    public static UserId GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue("sub")
                  ?? throw new InvalidOperationException("User ID claim not found.");
        return new UserId(Guid.Parse(sub));
    }
}
