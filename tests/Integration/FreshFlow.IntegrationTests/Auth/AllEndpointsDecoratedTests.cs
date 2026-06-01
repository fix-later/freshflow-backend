using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace FreshFlow.IntegrationTests.Auth;

[Trait("Category", "Unit")]
public sealed class AllEndpointsDecoratedTests
{
    private static readonly Assembly ApiAssembly = typeof(FreshFlow.API.Controllers.AuthController).Assembly;

    [Fact]
    public void AllControllerActions_HaveExplicitAuthAttribute()
    {
        var controllers = ApiAssembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ControllerBase)) && !t.IsAbstract);

        var violations = new List<string>();

        foreach (var controller in controllers)
        {
            var controllerHasAuthorize = controller.GetCustomAttribute<AuthorizeAttribute>() is not null;
            var actions = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

            foreach (var action in actions)
            {
                var hasExplicit =
                    action.GetCustomAttribute<AuthorizeAttribute>() is not null ||
                    action.GetCustomAttribute<AllowAnonymousAttribute>() is not null ||
                    controllerHasAuthorize;

                if (!hasExplicit)
                    violations.Add($"{controller.Name}.{action.Name}");
            }
        }

        violations.Should().BeEmpty(
            "every controller action must have [Authorize], [Authorize(Roles=...)], or [AllowAnonymous]");
    }
}
