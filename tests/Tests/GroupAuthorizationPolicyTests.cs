using System.Reflection;
using Client.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Tests;

// The group-membership policy is an attribute, which is explicit at the endpoint but — unlike a
// globally registered filter — something a new controller can forget. These tests are what stops
// that being silent: adding a {groupId} route without the annotation fails here rather than
// shipping an unauthorised door.
public class GroupAuthorizationPolicyTests
{
    private const string GroupToken = "{groupId:guid}";

    private static IEnumerable<Type> Controllers =>
        typeof(RequireGroupMembershipAttribute).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

    private static IEnumerable<string> RouteTemplates(Type controller)
    {
        foreach (var r in controller.GetCustomAttributes<RouteAttribute>())
            if (r.Template is not null)
                yield return r.Template;

        foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            foreach (var attr in method.GetCustomAttributes<HttpMethodAttribute>())
                if (attr.Template is not null)
                    yield return attr.Template;
    }

    private static bool IsGroupScoped(Type controller) =>
        RouteTemplates(controller).Any(t => t.Contains(GroupToken, StringComparison.Ordinal));

    private static bool Declares(Type controller) =>
        controller.GetCustomAttribute<RequireGroupMembershipAttribute>() is not null ||
        controller.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Any(m => m.GetCustomAttribute<RequireGroupMembershipAttribute>() is not null);

    [Fact]
    public void EveryGroupScopedController_DeclaresTheMembershipPolicy()
    {
        var unguarded = Controllers
            .Where(IsGroupScoped)
            .Where(c => !Declares(c))
            .Select(c => c.Name)
            .OrderBy(n => n)
            .ToList();

        Assert.True(
            unguarded.Count == 0,
            "These controllers expose a {groupId} route without [RequireGroupMembership], so any "
                + "signed-in user could reach another group's data: "
                + string.Join(", ", unguarded));
    }

    [Fact]
    public void TheTestItself_CanSeeTheGroupScopedControllers()
    {
        // A guard on the guard: if the reflection above ever stops matching — a route token renamed, say
        // — the test above would pass vacuously while checking nothing at all.
        var scoped = Controllers.Where(IsGroupScoped).Select(c => c.Name).ToList();

        Assert.Contains("LedgerController", scoped);
        Assert.Contains("ExpensesController", scoped);
    }
}
