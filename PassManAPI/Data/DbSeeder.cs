using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using PassManAPI.Models;

namespace PassManAPI.Data;

/// <summary>
/// Seeds roles, permissions and (optionally) demo users.
/// </summary>
public static class DbSeeder
{
    /// <summary>
    /// Ensures roles and permissions exist. Optionally seeds demo users when enabled.
    /// </summary>
    public static async Task SeedAsync(
        IServiceProvider serviceProvider,
        bool seedDemoUsers = false
    )
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();

        await EnsureRolesWithPermissionsAsync(roleManager);

        if (seedDemoUsers)
        {
            await EnsureDemoUsersAsync(userManager);
        }

        Console.WriteLine("Authorization seeding completed!");
    }

    private static async Task EnsureRolesWithPermissionsAsync(RoleManager<IdentityRole<int>> roleManager)
    {
        foreach (var roleDefinition in PermissionConstants.RolePermissions)
        {
            var roleName = roleDefinition.Key;
            var permissions = roleDefinition.Value;

            var role = await roleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                role = new IdentityRole<int> { Name = roleName };
                await roleManager.CreateAsync(role);
                Console.WriteLine($"Created role: {roleName}");
            }

            var existingClaims = await roleManager.GetClaimsAsync(role);
            foreach (var permission in permissions.Distinct())
            {
                var hasPermission = existingClaims.Any(c =>
                    c.Type == PermissionConstants.ClaimType && c.Value == permission);

                if (!hasPermission)
                {
                    await roleManager.AddClaimAsync(
                        role,
                        new Claim(PermissionConstants.ClaimType, permission)
                    );
                    Console.WriteLine($"Attached permission '{permission}' to role '{roleName}'");
                }
            }
        }
    }

    private static async Task EnsureDemoUsersAsync(UserManager<User> userManager)
    {
        var demoUsers = new List<(string UserName, string Email, string Password, string[] Roles)>
        {
            ("admin", "admin@passman.test", "Admin123!", new[] { "Admin" }),
            ("auditor", "auditor@passman.test", "Audit123!", new[] { "SecurityAuditor" }),
            ("owner", "owner@passman.test", "Owner123!", new[] { "VaultOwner" }),
            ("reader", "reader@passman.test", "Reader123!", new[] { "VaultReader" })
        };

        foreach (var (userName, email, password, roles) in demoUsers)
        {
            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser is not null)
            {
                foreach (var role in roles)
                {
                    if (!await userManager.IsInRoleAsync(existingUser, role))
                    {
                        await userManager.AddToRoleAsync(existingUser, role);
                        Console.WriteLine($"Linked existing user {userName} to role {role}");
                    }
                }
                continue;
            }

            var user = new User
            {
                UserName = userName,
                Email = email,
                EmailConfirmed = true, // demo users skip email confirmation
                CreatedAt = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString()
            };

            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userManager.AddToRolesAsync(user, roles);
                Console.WriteLine($"Created demo user: {userName} ({string.Join(", ", roles)})");
            }
            else
            {
                Console.WriteLine($"Failed to create user {userName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
    }
}
