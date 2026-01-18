using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PassManAPI.DTOs;
using Xunit;

namespace PassManAPI.Tests;

public class AuthAssignRoleTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthAssignRoleTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Admin_Can_Assign_Role_To_User()
    {
        // Seeded admin
        var admin = await LoginAsync("admin@passman.test", "Admin123!");

        // Register a new user (defaults to VaultOwner)
        var user = await RegisterAsync("role-target@test.local");

        // Assign role to VaultReader
        var assignReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/assign-role")
        {
            Content = JsonContent.Create(new { userId = user.User.Id, role = "VaultReader" })
        };
        assignReq.Headers.Add("X-UserId", admin.User.Id.ToString());

        var assignResp = await _client.SendAsync(assignReq);
        assignResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify permissions reflect the new role
        var permsReq = new HttpRequestMessage(HttpMethod.Get, "/api/auth/permissions");
        permsReq.Headers.Add("X-UserId", user.User.Id.ToString());
        var permsResp = await _client.SendAsync(permsReq);
        permsResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var perms = await permsResp.Content.ReadFromJsonAsync<string[]>();
        perms.Should().NotBeNull();
        perms!.Should().Contain("vault.read");
        perms.Should().NotContain("vault.create"); // VaultReader should not create
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

    private async Task<AuthResponse> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return payload!;
    }
}

