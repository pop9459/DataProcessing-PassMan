using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PassManAPI.Models;

namespace PassManAPI.Data;

/// <summary>
/// Seeds roles, permissions, demo users, and sample data for development.
/// </summary>
public static class DbSeeder
{
    /// <summary>
    /// Ensures roles and permissions exist. Optionally seeds demo users and sample data when enabled.
    /// </summary>
    public static async Task SeedAsync(
        IServiceProvider serviceProvider,
        bool seedDemoUsers = false
    )
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();

        await EnsureRolesWithPermissionsAsync(roleManager);

        if (seedDemoUsers)
        {
            await EnsureDemoUsersAsync(userManager);
            await EnsureCategoriesAsync(dbContext);
            await EnsureDemoDataAsync(dbContext, userManager);
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

    /// <summary>
    /// Seeds default categories if they don't exist.
    /// </summary>
    private static async Task EnsureCategoriesAsync(ApplicationDbContext dbContext)
    {
        if (await dbContext.Categories.AnyAsync())
        {
            Console.WriteLine("Categories already seeded, skipping...");
            return;
        }

        foreach (var category in Category.DefaultCategories)
        {
            dbContext.Categories.Add(new Category
            {
                Name = category.Name,
                Description = category.Description
            });
        }

        await dbContext.SaveChangesAsync();
        Console.WriteLine($"Seeded {Category.DefaultCategories.Count} default categories");
    }

    /// <summary>
    /// Seeds demo vaults, credentials, tags, and shares for testing the GUI.
    /// </summary>
    private static async Task EnsureDemoDataAsync(ApplicationDbContext dbContext, UserManager<User> userManager)
    {
        // Check if demo data already exists
        var ownerUser = await userManager.FindByEmailAsync("owner@passman.test");
        var readerUser = await userManager.FindByEmailAsync("reader@passman.test");

        if (ownerUser == null)
        {
            Console.WriteLine("Owner user not found, skipping demo data seeding");
            return;
        }

        // Check if vaults already exist for this user
        if (await dbContext.Vaults.IgnoreQueryFilters().AnyAsync(v => v.UserId == ownerUser.Id))
        {
            Console.WriteLine("Demo vaults already exist, skipping...");
            return;
        }

        // Get categories for assigning to credentials
        var categories = await dbContext.Categories.ToListAsync();
        var bankingCategory = categories.FirstOrDefault(c => c.Name == "Banking");
        var socialCategory = categories.FirstOrDefault(c => c.Name == "Social Media");
        var emailCategory = categories.FirstOrDefault(c => c.Name == "Email");
        var workCategory = categories.FirstOrDefault(c => c.Name == "Work");
        var shoppingCategory = categories.FirstOrDefault(c => c.Name == "Shopping");
        var entertainmentCategory = categories.FirstOrDefault(c => c.Name == "Entertainment");

        // Create tags for owner user
        var tags = new List<Tag>
        {
            new Tag { Name = "important", UserId = ownerUser.Id },
            new Tag { Name = "shared", UserId = ownerUser.Id },
            new Tag { Name = "temporary", UserId = ownerUser.Id },
            new Tag { Name = "secure", UserId = ownerUser.Id },
            new Tag { Name = "2fa-enabled", UserId = ownerUser.Id }
        };
        dbContext.Tags.AddRange(tags);
        await dbContext.SaveChangesAsync();
        Console.WriteLine($"Created {tags.Count} demo tags for owner user");

        // Create demo vaults
        var personalVault = new Vault
        {
            Name = "Personal",
            Description = "Personal accounts and credentials",
            Icon = "🏠",
            UserId = ownerUser.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };

        var workVault = new Vault
        {
            Name = "Work",
            Description = "Work-related accounts and services",
            Icon = "💼",
            UserId = ownerUser.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        };

        var bankingVault = new Vault
        {
            Name = "Banking & Finance",
            Description = "Bank accounts, credit cards, and financial services",
            Icon = "🏦",
            UserId = ownerUser.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-15)
        };

        dbContext.Vaults.AddRange(personalVault, workVault, bankingVault);
        await dbContext.SaveChangesAsync();
        Console.WriteLine("Created 3 demo vaults for owner user");

        // Reload tags to get IDs
        var importantTag = tags.First(t => t.Name == "important");
        var sharedTag = tags.First(t => t.Name == "shared");
        var secureTag = tags.First(t => t.Name == "secure");
        var twoFaTag = tags.First(t => t.Name == "2fa-enabled");

        // Create credentials for Personal vault
        var personalCredentials = new List<Credential>
        {
            new Credential
            {
                Title = "Gmail Account",
                Username = "demo.user@gmail.com",
                EncryptedPassword = "[DEMO_ENCRYPTED_PASSWORD]",
                Url = "https://mail.google.com",
                Notes = "Primary email account. Recovery phone: +1-555-0123",
                VaultId = personalVault.Id,
                CategoryId = emailCategory?.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-28)
            },
            new Credential
            {
                Title = "Netflix",
                Username = "demo.user@gmail.com",
                EncryptedPassword = "[DEMO_ENCRYPTED_PASSWORD]",
                Url = "https://www.netflix.com",
                Notes = "Family plan - 4 screens",
                VaultId = personalVault.Id,
                CategoryId = entertainmentCategory?.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-25)
            },
            new Credential
            {
                Title = "Amazon",
                Username = "demo.user@gmail.com",
                EncryptedPassword = "[DEMO_ENCRYPTED_PASSWORD]",
                Url = "https://www.amazon.com",
                Notes = "Prime member since 2020",
                VaultId = personalVault.Id,
                CategoryId = shoppingCategory?.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-20)
            },
            new Credential
            {
                Title = "Twitter/X",
                Username = "@demouser",
                EncryptedPassword = "[DEMO_ENCRYPTED_PASSWORD]",
                Url = "https://x.com",
                Notes = "Personal account",
                VaultId = personalVault.Id,
                CategoryId = socialCategory?.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-15)
            }
        };

        // Create credentials for Work vault
        var workCredentials = new List<Credential>
        {
            new Credential
            {
                Title = "Company VPN",
                Username = "duser",
                EncryptedPassword = "[DEMO_ENCRYPTED_PASSWORD]",
                Url = "https://vpn.company.com",
                Notes = "Use with company authenticator app",
                VaultId = workVault.Id,
                CategoryId = workCategory?.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-18)
            },
            new Credential
            {
                Title = "Slack Workspace",
                Username = "demo.user@company.com",
                EncryptedPassword = "[DEMO_ENCRYPTED_PASSWORD]",
                Url = "https://company.slack.com",
                Notes = "Main communication channel",
                VaultId = workVault.Id,
                CategoryId = workCategory?.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-18)
            },
            new Credential
            {
                Title = "Jira",
                Username = "demo.user@company.com",
                EncryptedPassword = "[DEMO_ENCRYPTED_PASSWORD]",
                Url = "https://company.atlassian.net",
                Notes = "Project management - Team Alpha",
                VaultId = workVault.Id,
                CategoryId = workCategory?.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-17)
            },
            new Credential
            {
                Title = "GitHub Enterprise",
                Username = "demouser",
                EncryptedPassword = "[DEMO_ENCRYPTED_PASSWORD]",
                Url = "https://github.company.com",
                Notes = "Use SSH key for git operations",
                VaultId = workVault.Id,
                CategoryId = workCategory?.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-16)
            }
        };

        // Create credentials for Banking vault
        var bankingCredentials = new List<Credential>
        {
            new Credential
            {
                Title = "Chase Bank",
                Username = "demouser123",
                EncryptedPassword = "[DEMO_ENCRYPTED_PASSWORD]",
                Url = "https://www.chase.com",
                Notes = "Checking & Savings. Security questions set.",
                VaultId = bankingVault.Id,
                CategoryId = bankingCategory?.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-14)
            },
            new Credential
            {
                Title = "Fidelity Investments",
                Username = "demo_investor",
                EncryptedPassword = "[DEMO_ENCRYPTED_PASSWORD]",
                Url = "https://www.fidelity.com",
                Notes = "401k and brokerage accounts",
                VaultId = bankingVault.Id,
                CategoryId = bankingCategory?.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-12)
            },
            new Credential
            {
                Title = "PayPal",
                Username = "demo.user@gmail.com",
                EncryptedPassword = "[DEMO_ENCRYPTED_PASSWORD]",
                Url = "https://www.paypal.com",
                Notes = "Linked to Chase checking",
                VaultId = bankingVault.Id,
                CategoryId = bankingCategory?.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            }
        };

        dbContext.Credentials.AddRange(personalCredentials);
        dbContext.Credentials.AddRange(workCredentials);
        dbContext.Credentials.AddRange(bankingCredentials);
        await dbContext.SaveChangesAsync();
        Console.WriteLine($"Created {personalCredentials.Count + workCredentials.Count + bankingCredentials.Count} demo credentials");

        // Add tags to some credentials (CredentialTags)
        var credentialTags = new List<CredentialTag>
        {
            // Gmail - important, 2fa-enabled
            new CredentialTag { CredentialId = personalCredentials[0].Id, TagId = importantTag.Id },
            new CredentialTag { CredentialId = personalCredentials[0].Id, TagId = twoFaTag.Id },
            // Netflix - shared
            new CredentialTag { CredentialId = personalCredentials[1].Id, TagId = sharedTag.Id },
            // Company VPN - important, secure, 2fa-enabled
            new CredentialTag { CredentialId = workCredentials[0].Id, TagId = importantTag.Id },
            new CredentialTag { CredentialId = workCredentials[0].Id, TagId = secureTag.Id },
            new CredentialTag { CredentialId = workCredentials[0].Id, TagId = twoFaTag.Id },
            // Chase Bank - important, secure, 2fa-enabled
            new CredentialTag { CredentialId = bankingCredentials[0].Id, TagId = importantTag.Id },
            new CredentialTag { CredentialId = bankingCredentials[0].Id, TagId = secureTag.Id },
            new CredentialTag { CredentialId = bankingCredentials[0].Id, TagId = twoFaTag.Id },
            // Fidelity - important, secure
            new CredentialTag { CredentialId = bankingCredentials[1].Id, TagId = importantTag.Id },
            new CredentialTag { CredentialId = bankingCredentials[1].Id, TagId = secureTag.Id },
        };

        dbContext.CredentialTags.AddRange(credentialTags);
        await dbContext.SaveChangesAsync();
        Console.WriteLine($"Created {credentialTags.Count} credential-tag associations");

        // Share the Work vault with the reader user
        if (readerUser != null)
        {
            var vaultShare = new VaultShare
            {
                VaultId = workVault.Id,
                UserId = readerUser.Id,
                Permission = SharePermission.View,
                SharedAt = DateTime.UtcNow.AddDays(-5)
            };

            dbContext.VaultShares.Add(vaultShare);
            await dbContext.SaveChangesAsync();
            Console.WriteLine($"Shared 'Work' vault with reader user (read-only)");
        }

        Console.WriteLine("Demo data seeding completed!");
    }
}
