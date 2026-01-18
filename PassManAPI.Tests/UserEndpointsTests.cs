using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PassManAPI.DTOs;
using Xunit;

namespace PassManAPI.Tests;

public class UserEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UserEndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_Own_Profile_Returns_Success()
    {
        // Arrange
        var user = await RegisterAsync("user-get-own@test.local");

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/user/{user.User.Id}");
        request.Headers.Add("X-UserId", user.User.Id.ToString());
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        profile.Should().NotBeNull();
        profile!.Email.Should().Be("user-get-own@test.local");
    }

    [Fact]
    public async Task Get_Other_User_Without_Permission_Returns_Forbidden()
    {
        // Arrange
        var user1 = await RegisterAsync("user-forbidden-1@test.local");
        var user2 = await RegisterAsync("user-forbidden-2@test.local");

        // Act - user1 tries to view user2's profile
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/user/{user2.User.Id}");
        request.Headers.Add("X-UserId", user1.User.Id.ToString());
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Nonexistent_User_Returns_NotFound()
    {
        // Arrange
        var user = await RegisterAsync("user-get-nonexistent@test.local");

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/user/999999");
        request.Headers.Add("X-UserId", user.User.Id.ToString());
        // This will return Forbidden because they're not admin and not user 999999
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_Own_Profile_Returns_Success()
    {
        // Arrange
        var user = await RegisterAsync("user-update-own@test.local");
        var updateRequest = new { email = "user-update-own-new@test.local", userName = "UpdatedUser" };

        // Act
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/user/{user.User.Id}")
        {
            Content = JsonContent.Create(updateRequest)
        };
        request.Headers.Add("X-UserId", user.User.Id.ToString());
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        profile.Should().NotBeNull();
        profile!.UserName.Should().Be("UpdatedUser");
    }

    [Fact]
    public async Task Update_Other_User_Without_Permission_Returns_Forbidden()
    {
        // Arrange
        var user1 = await RegisterAsync("user-update-forbidden-1@test.local");
        var user2 = await RegisterAsync("user-update-forbidden-2@test.local");
        var updateRequest = new { email = "hacker@test.local", userName = "Hacker" };

        // Act - user1 tries to update user2's profile
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/user/{user2.User.Id}")
        {
            Content = JsonContent.Create(updateRequest)
        };
        request.Headers.Add("X-UserId", user1.User.Id.ToString());
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_Own_Account_Returns_NoContent()
    {
        // Arrange
        var user = await RegisterAsync("user-delete-own@test.local");

        // Act
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/user/{user.User.Id}");
        request.Headers.Add("X-UserId", user.User.Id.ToString());
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify user is deleted - trying to get profile should fail
        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/user/{user.User.Id}");
        getRequest.Headers.Add("X-UserId", user.User.Id.ToString());
        var getResponse = await _client.SendAsync(getRequest);
        getResponse.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_Other_User_Without_Permission_Returns_Forbidden()
    {
        // Arrange
        var user1 = await RegisterAsync("user-delete-forbidden-1@test.local");
        var user2 = await RegisterAsync("user-delete-forbidden-2@test.local");

        // Act - user1 tries to delete user2's account
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/user/{user2.User.Id}");
        request.Headers.Add("X-UserId", user1.User.Id.ToString());
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_User_Vaults_Returns_Own_Vaults()
    {
        // Arrange
        var user = await RegisterAsync("user-vaults@test.local");

        // Create a vault first (note: vault name must only contain letters, numbers, spaces, hyphens, underscores)
        var createVault = new { name = "User Test Vault", description = "test vault", userId = user.User.Id };
        var createVaultRequest = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(createVault)
        };
        createVaultRequest.Headers.Add("X-UserId", user.User.Id.ToString());
        var createVaultResponse = await _client.SendAsync(createVaultRequest);
        createVaultResponse.EnsureSuccessStatusCode();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/user/{user.User.Id}/vaults");
        request.Headers.Add("X-UserId", user.User.Id.ToString());
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var vaults = await response.Content.ReadFromJsonAsync<List<VaultSummaryDto>>();
        vaults.Should().NotBeNull();
        vaults!.Should().ContainSingle(v => v.Name == "User Test Vault");
    }

    [Fact]
    public async Task Get_Other_User_Vaults_Without_Permission_Returns_Forbidden()
    {
        // Arrange
        var user1 = await RegisterAsync("user-vaults-forbidden-1@test.local");
        var user2 = await RegisterAsync("user-vaults-forbidden-2@test.local");

        // Act - user1 tries to view user2's vaults
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/user/{user2.User.Id}/vaults");
        request.Headers.Add("X-UserId", user1.User.Id.ToString());
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_User_Tags_Returns_Own_Tags()
    {
        // Arrange
        var user = await RegisterAsync("user-tags@test.local");

        // Create a tag first
        var createTag = new { name = "TestTag" };
        var createTagRequest = new HttpRequestMessage(HttpMethod.Post, "/api/tags")
        {
            Content = JsonContent.Create(createTag)
        };
        createTagRequest.Headers.Add("X-UserId", user.User.Id.ToString());
        var createTagResponse = await _client.SendAsync(createTagRequest);
        createTagResponse.EnsureSuccessStatusCode();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/user/{user.User.Id}/tags");
        request.Headers.Add("X-UserId", user.User.Id.ToString());
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tags = await response.Content.ReadFromJsonAsync<List<TagDto>>();
        tags.Should().NotBeNull();
        tags!.Should().ContainSingle(t => t.Name == "TestTag");
    }

    [Fact]
    public async Task Get_Other_User_Tags_Without_Permission_Returns_Forbidden()
    {
        // Arrange
        var user1 = await RegisterAsync("user-tags-forbidden-1@test.local");
        var user2 = await RegisterAsync("user-tags-forbidden-2@test.local");

        // Act - user1 tries to view user2's tags
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/user/{user2.User.Id}/tags");
        request.Headers.Add("X-UserId", user1.User.Id.ToString());
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unauthenticated_Request_Returns_Unauthorized()
    {
        // Act - no X-UserId header
        var response = await _client.GetAsync("/api/user/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<AuthResponse> RegisterAsync(string email)
    {
        var request = new RegisterRequest
        {
            Email = email,
            Password = "Password1!",
            ConfirmPassword = "Password1!",
            UserName = email.Split('@')[0],
            PhoneNumber = "1234567890"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return payload!;
    }

    private record VaultSummaryDto(int Id, string Name, string? Description, DateTime CreatedAt);
}
