using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PassManAPI.DTOs;
using PassManAPI.Models;
using Xunit;

namespace PassManAPI.Tests;

public class VaultSharesEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public VaultSharesEndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Owner_Can_Share_And_Revoke()
    {
        var owner = await RegisterAsync("share-owner2@test.local");
        var reader = await RegisterAsync("share-reader2@test.local");

        // Create vault as owner
        var create = new { name = "Shareable Vault", description = "shareable", userId = owner.User.Id };
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(create)
        };
        createReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var createResp = await _client.SendAsync(createReq);
        createResp.EnsureSuccessStatusCode();
        var vault = await createResp.Content.ReadFromJsonAsync<VaultResponse>();

        // Share to reader
        var shareReq = new HttpRequestMessage(HttpMethod.Post, $"/api/vaults/{vault!.Id}/share")
        {
            Content = JsonContent.Create(new { userEmail = reader.User.Email })
        };
        shareReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var shareResp = await _client.SendAsync(shareReq);
        shareResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Reader can see the vault via list
        var listReq = new HttpRequestMessage(HttpMethod.Get, "/api/vaults");
        listReq.Headers.Add("X-UserId", reader.User.Id.ToString());
        var listResp = await _client.SendAsync(listReq);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResp.Content.ReadFromJsonAsync<List<VaultResponse>>();
        list.Should().NotBeNull();
        list!.Should().Contain(v => v.Id == vault.Id);

        // Owner revokes share
        var revokeReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/vaults/{vault.Id}/share/{reader.User.Id}");
        revokeReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var revokeResp = await _client.SendAsync(revokeReq);
        revokeResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Reader should no longer see the vault
        var listReqAfter = new HttpRequestMessage(HttpMethod.Get, "/api/vaults");
        listReqAfter.Headers.Add("X-UserId", reader.User.Id.ToString());
        var listRespAfter = await _client.SendAsync(listReqAfter);
        listRespAfter.StatusCode.Should().Be(HttpStatusCode.OK);
        var listAfter = await listRespAfter.Content.ReadFromJsonAsync<List<VaultResponse>>();
        listAfter.Should().NotBeNull();
        listAfter!.Should().NotContain(v => v.Id == vault.Id);
    }

    [Fact]
    public async Task NonOwner_Cannot_Share()
    {
        var owner = await RegisterAsync("share-owner3@test.local");
        var other = await RegisterAsync("share-other3@test.local");
        var target = await RegisterAsync("share-target3@test.local");

        // Create vault as owner
        var create = new { name = "Owner Only", description = "no share", userId = owner.User.Id };
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(create)
        };
        createReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var createResp = await _client.SendAsync(createReq);
        createResp.EnsureSuccessStatusCode();
        var vault = await createResp.Content.ReadFromJsonAsync<VaultResponse>();

        // Other tries to share -> should be forbidden
        var shareReq = new HttpRequestMessage(HttpMethod.Post, $"/api/vaults/{vault!.Id}/share")
        {
            Content = JsonContent.Create(new { userEmail = target.User.Email })
        };
        shareReq.Headers.Add("X-UserId", other.User.Id.ToString());
        var shareResp = await _client.SendAsync(shareReq);
        shareResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Shared_User_Cannot_Revoke()
    {
        var owner = await RegisterAsync("share-owner4@test.local");
        var shared = await RegisterAsync("share-shared4@test.local");
        var target = await RegisterAsync("share-target4@test.local");

        // Create vault
        var create = new { name = "Revokable Vault", description = "revokable", userId = owner.User.Id };
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(create)
        };
        createReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var createResp = await _client.SendAsync(createReq);
        createResp.EnsureSuccessStatusCode();
        var vault = await createResp.Content.ReadFromJsonAsync<VaultResponse>();

        // Owner shares to both users
        foreach (var email in new[] { shared.User.Email, target.User.Email })
        {
            var shareReq = new HttpRequestMessage(HttpMethod.Post, $"/api/vaults/{vault!.Id}/share")
            {
                Content = JsonContent.Create(new { userEmail = email })
            };
            shareReq.Headers.Add("X-UserId", owner.User.Id.ToString());
            var shareResp = await _client.SendAsync(shareReq);
            shareResp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Shared user tries to revoke target -> forbidden
        var revokeReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/vaults/{vault!.Id}/share/{target.User.Id}");
        revokeReq.Headers.Add("X-UserId", shared.User.Id.ToString());
        var revokeResp = await _client.SendAsync(revokeReq);
        revokeResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Share_To_Nonexistent_User_Returns_NotFound()
    {
        var owner = await RegisterAsync("share-owner5@test.local");

        // Create vault
        var create = new { name = "Missing User Vault", description = "missing user", userId = owner.User.Id };
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(create)
        };
        createReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var createResp = await _client.SendAsync(createReq);
        createResp.EnsureSuccessStatusCode();
        var vault = await createResp.Content.ReadFromJsonAsync<VaultResponse>();

        // Share to nonexistent email
        var shareReq = new HttpRequestMessage(HttpMethod.Post, $"/api/vaults/{vault!.Id}/share")
        {
            Content = JsonContent.Create(new { userEmail = "doesnotexist@test.local" })
        };
        shareReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var shareResp = await _client.SendAsync(shareReq);
        shareResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MyAccess_Returns_Owned_And_Shared_Vaults()
    {
        var owner = await RegisterAsync("access-owner@test.local");
        var reader = await RegisterAsync("access-reader@test.local");

        // Owner creates a vault
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(new { name = "Access Vault", description = "for access test", userId = owner.User.Id })
        };
        createReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var createResp = await _client.SendAsync(createReq);
        createResp.EnsureSuccessStatusCode();
        var vault = await createResp.Content.ReadFromJsonAsync<VaultResponse>();

        // Owner shares it with reader
        var shareReq = new HttpRequestMessage(HttpMethod.Post, $"/api/vaults/{vault!.Id}/share")
        {
            Content = JsonContent.Create(new { userEmail = reader.User.Email })
        };
        shareReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var shareResp2 = await _client.SendAsync(shareReq);
        shareResp2.StatusCode.Should().Be(HttpStatusCode.OK,
            $"share failed (vaultId={vault.Id}): {await shareResp2.Content.ReadAsStringAsync()}");

        // Owner sees their vault as "Owner"
        var ownerReq = new HttpRequestMessage(HttpMethod.Get, "/api/vaults/my-access");
        ownerReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var ownerResp = await _client.SendAsync(ownerReq);
        ownerResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var ownerRows = await ownerResp.Content.ReadFromJsonAsync<List<VaultAccessRow>>();
        ownerRows.Should().Contain(r => r.VaultId == vault.Id && r.AccessType == "Owner");

        // Reader sees the vault as "Shared"
        var readerReq = new HttpRequestMessage(HttpMethod.Get, "/api/vaults/my-access");
        readerReq.Headers.Add("X-UserId", reader.User.Id.ToString());
        var readerResp = await _client.SendAsync(readerReq);
        readerResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var readerRows = await readerResp.Content.ReadFromJsonAsync<List<VaultAccessRow>>();
        readerRows.Should().Contain(r => r.VaultId == vault.Id && r.AccessType == "Shared");
    }

    [Fact]
    public async Task MyAccess_Unauthenticated_Returns_Unauthorized()
    {
        var resp = await _client.GetAsync("/api/vaults/my-access");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

    private record VaultResponse(int Id, string Name, string? Description, DateTime CreatedAt, DateTime? UpdatedAt, int UserId);
}

