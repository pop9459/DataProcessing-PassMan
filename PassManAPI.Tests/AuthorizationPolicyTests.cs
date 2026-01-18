using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PassManAPI.DTOs;
using PassManAPI.Models;
using Xunit;

namespace PassManAPI.Tests;

public class AuthorizationPolicyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthorizationPolicyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task VaultOwner_Can_Create_Vault()
    {
        var user = await RegisterAsync("owner-policy@test.local");

        var create = new
        {
            name = "Policy Vault",
            description = "created by vault owner",
            userId = user.User.Id
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(create)
        };
        request.Headers.Add("X-UserId", user.User.Id.ToString());

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task VaultReader_Cannot_Create_Vault()
    {
        var adminId = await GetAdminUserIdAsync();
        var reader = await RegisterAsync("reader-policy@test.local");

        // Assign the reader role via admin-protected endpoint
        var assignRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/assign-role")
        {
            Content = JsonContent.Create(new { userId = reader.User.Id, role = "VaultReader" })
        };
        assignRequest.Headers.Add("X-UserId", adminId.ToString());

        var assignResponse = await _client.SendAsync(assignRequest);
        assignResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var create = new
        {
            name = "Reader Vault",
            description = "should fail",
            userId = reader.User.Id
        };

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(create)
        };
        createRequest.Headers.Add("X-UserId", reader.User.Id.ToString());

        var createResponse = await _client.SendAsync(createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Permissions_Endpoint_Returns_Role_Claims()
    {
        var user = await RegisterAsync("perms-policy@test.local");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/permissions");
        request.Headers.Add("X-UserId", user.User.Id.ToString());

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var permissions = await response.Content.ReadFromJsonAsync<string[]>();
        permissions.Should().NotBeNull();
        permissions!.Should().Contain(PermissionConstants.VaultCreate);
        permissions.Should().Contain(PermissionConstants.CredentialRead);
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

    private async Task<int> GetAdminUserIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var admin = await userManager.FindByEmailAsync("admin@passman.test");
        admin.Should().NotBeNull("seeded admin user should exist");
        return admin!.Id;
    }
}

