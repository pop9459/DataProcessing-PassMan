using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PassManAPI.Models;
using Xunit;

namespace PassManAPI.Tests;

public class AuthorizationSeedingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuthorizationSeedingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Roles_Are_Seeded_With_Permissions()
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();

        foreach (var (roleName, expectedPermissions) in PermissionConstants.RolePermissions)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            role.Should().NotBeNull($"role '{roleName}' should be seeded");

            var claims = await roleManager.GetClaimsAsync(role!);
            var permissionValues = claims
                .Where(c => c.Type == PermissionConstants.ClaimType)
                .Select(c => c.Value)
                .ToArray();

            permissionValues.Should().BeEquivalentTo(
                expectedPermissions,
                options => options.WithoutStrictOrdering(),
                $"role '{roleName}' should have the mapped permissions"
            );
        }
    }

    [Fact]
    public async Task ClaimType_Is_Permission_For_All_RoleClaims()
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();

        foreach (var (roleName, _) in PermissionConstants.RolePermissions)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            role.Should().NotBeNull();

            var claims = await roleManager.GetClaimsAsync(role!);
            claims.Should().OnlyContain(c => c.Type == PermissionConstants.ClaimType);
        }
    }
}

