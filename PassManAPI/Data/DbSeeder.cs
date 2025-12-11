using Microsoft.AspNetCore.Identity;
using PassManAPI.Models;

namespace PassManAPI.Data;

/// <summary>
/// Seeds the database with test data for development
/// </summary>
public static class DbSeeder
{
    /// <summary>
    /// Seeds test users and roles into the database
    /// </summary>
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();

        // Seed roles
        string[] roles = { "Admin", "User" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<int> { Name = role });
                Console.WriteLine($"Created role: {role}");
            }
        }

        // Seed test users
        var testUsers = new List<(string UserName, string Email, string Password, string Role)>
        {
            ("admin", "admin@passman.test", "Admin123!", "Admin"),
            ("testuser", "user@passman.test", "Test123!", "User"),
            ("alice", "alice@passman.test", "Alice123!", "User"),
            ("bob", "bob@passman.test", "Bob123!!", "User")
        };

        foreach (var (userName, email, password, role) in testUsers)
        {
            if (await userManager.FindByEmailAsync(email) == null)
            {
                var user = new User
                {
                    UserName = userName,
                    Email = email,
                    EmailConfirmed = true, // Skip email confirmation for test users
                    CreatedAt = DateTime.UtcNow
                };

                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, role);
                    Console.WriteLine($"Created test user: {userName} ({role})");
                }
                else
                {
                    Console.WriteLine($"Failed to create user {userName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
        }

        Console.WriteLine("Database seeding completed!");
    }
}
