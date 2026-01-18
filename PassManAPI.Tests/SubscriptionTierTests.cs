using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PassManAPI.Data;
using PassManAPI.DTOs;
using PassManAPI.Models;
using Xunit;

namespace PassManAPI.Tests;

public class SubscriptionTierTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public SubscriptionTierTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DefaultTiers_AreSeeded_Correctly()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tiers = await db.SubscriptionTiers.ToListAsync();
        tiers.Should().HaveCount(2);

        var freeTier = tiers.Single(t => t.Name == "Free");
        freeTier.MaxVaults.Should().Be(3);
        freeTier.Price.Should().Be(0);

        var premiumTier = tiers.Single(t => t.Name == "Premium");
        premiumTier.MaxVaults.Should().Be(50);
        premiumTier.Price.Should().Be(9.99m);
    }

    [Fact]
    public async Task Register_NewUser_AssignsFreeTier()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "tier-test@user.com",
            Password = "TierTest123!",
            ConfirmPassword = "TierTest123!",
            UserName = "TierTester"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        authResponse.Should().NotBeNull();
        
        // Check profile in response
        authResponse!.User.SubscriptionTierId.Should().NotBeNull();
        
        // Verify in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var user = await db.Users
            .Include(u => u.SubscriptionTier)
            .FirstOrDefaultAsync(u => u.Email == request.Email);
            
        user.Should().NotBeNull();
        user!.SubscriptionTier.Should().NotBeNull();
        user.SubscriptionTier!.Name.Should().Be("Free");
    }

    [Fact]
    public async Task GetProfile_ReturnsSubscriptionTierId()
    {
        // Arrange: Create a user manually with a specific tier
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new User
        {
            UserName = "ProfileTierUser",
            Email = "profile-tier@test.com",
            SubscriptionTierId = SubscriptionTier.DefaultTiers.First(t => t.Name == "Premium").Id
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act
        Authenticate(user.Id);
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        
        profile.Should().NotBeNull();
        profile!.SubscriptionTierId.Should().Be(user.SubscriptionTierId);
    }

    private void Authenticate(int userId)
    {
        _client.DefaultRequestHeaders.Remove("X-UserId");
        _client.DefaultRequestHeaders.Add("X-UserId", userId.ToString());
    }
}
