using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PassManAPI.Models;
using PassManAPI.DTOs;
using Xunit;

namespace PassManAPI.Tests;

public class CredentialsEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CredentialsEndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Owner_Can_Create_And_List_Credentials()
    {
        var user = await RegisterAsync("cred-owner@test.local");

        // Create vault first
        var vault = new
        {
            name = "Owner Vault",
            description = "owner vault",
            userId = user.User.Id
        };
        var vaultRequest = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(vault)
        };
        vaultRequest.Headers.Add("X-UserId", user.User.Id.ToString());
        var vaultResponse = await _client.SendAsync(vaultRequest);
        vaultResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var vaultPayload = await vaultResponse.Content.ReadFromJsonAsync<CreatedVaultResponse>();
        vaultPayload.Should().NotBeNull();

        // Create credential in that vault
        var cred = new
        {
            Title = "Email",
            Username = "alice",
            EncryptedPassword = "enc-pass",
            Url = "https://mail.test",
            Notes = "test note",
            CategoryId = (int?)null
        };
        var credRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/vaults/{vaultPayload!.Id}/credentials")
        {
            Content = JsonContent.Create(cred)
        };
        credRequest.Headers.Add("X-UserId", user.User.Id.ToString());
        var credResponse = await _client.SendAsync(credRequest);
        credResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // List credentials
        var listRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/vaults/{vaultPayload.Id}/credentials");
        listRequest.Headers.Add("X-UserId", user.User.Id.ToString());
        var listResponse = await _client.SendAsync(listRequest);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await listResponse.Content.ReadFromJsonAsync<List<CredentialListItem>>();
        items.Should().NotBeNull();
        items!.Should().ContainSingle(i => i.Title == "Email" && i.Username == "alice");
    }

    [Fact]
    public async Task Reader_Cannot_Create_Credentials_In_Shared_Vault()
    {
        var owner = await RegisterAsync("share-owner@test.local");
        var reader = await RegisterAsync("share-reader@test.local");

        // Make reader a VaultReader (default is VaultOwner)
        var admin = await LoginAsync("admin@passman.test", "Admin123!");
        var assignRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/assign-role")
        {
            Content = JsonContent.Create(new { userId = reader.User.Id, role = "VaultReader" })
        };
        assignRequest.Headers.Add("X-UserId", admin.User.Id.ToString());
        var assignResponse = await _client.SendAsync(assignRequest);
        assignResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Create vault as owner
        var vault = new
        {
            name = "Shared Vault",
            description = "shared vault",
            userId = owner.User.Id
        };
        var vaultRequest = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(vault)
        };
        vaultRequest.Headers.Add("X-UserId", owner.User.Id.ToString());
        var vaultResponse = await _client.SendAsync(vaultRequest);
        vaultResponse.EnsureSuccessStatusCode();
        var vaultPayload = await vaultResponse.Content.ReadFromJsonAsync<CreatedVaultResponse>();

        // Share vault to reader via admin-like role manage? Instead use share endpoint as owner
        var shareRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/vaults/{vaultPayload!.Id}/share")
        {
            Content = JsonContent.Create(new { userEmail = reader.User.Email })
        };
        shareRequest.Headers.Add("X-UserId", owner.User.Id.ToString());
        var shareResponse = await _client.SendAsync(shareRequest);
        shareResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Reader attempts to create credential -> should be forbidden by policy (no vault.create) and by owner check
        var cred = new
        {
            Title = "ShouldFail",
            Username = "reader",
            EncryptedPassword = "enc",
            Url = (string?)null,
            Notes = (string?)null,
            CategoryId = (int?)null
        };
        var credRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/vaults/{vaultPayload.Id}/credentials")
        {
            Content = JsonContent.Create(cred)
        };
        credRequest.Headers.Add("X-UserId", reader.User.Id.ToString());

        var credResponse = await _client.SendAsync(credRequest);
        credResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

    private record CreatedVaultResponse(int Id);

    private record CredentialListItem(
        int Id,
        string Title,
        string? Username,
        string? Url,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        DateTime? LastAccessed
    );
}

