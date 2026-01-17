using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PassManAPI.DTOs;
using Xunit;

namespace PassManAPI.Tests;

public class VaultEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public VaultEndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Owner_Can_Create_List_Get_And_Delete_Vault()
    {
        var owner = await RegisterAsync("vault-owner@test.local");

        // Create
        var create = new { name = "Owner Vault", description = "owner vault", userId = owner.User.Id };
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(create)
        };
        createReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var createResp = await _client.SendAsync(createReq);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<VaultResponse>();
        created.Should().NotBeNull();

        // List (owner sees own vaults)
        var listReq = new HttpRequestMessage(HttpMethod.Get, "/api/vaults");
        listReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var listResp = await _client.SendAsync(listReq);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResp.Content.ReadFromJsonAsync<List<VaultResponse>>();
        list.Should().NotBeNull();
        list!.Should().Contain(v => v.Id == created!.Id);

        // Get
        var getReq = new HttpRequestMessage(HttpMethod.Get, $"/api/vaults/{created!.Id}");
        getReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var getResp = await _client.SendAsync(getReq);
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Delete
        var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/vaults/{created.Id}");
        delReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var delResp = await _client.SendAsync(delReq);
        delResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Confirm gone
        var getAfterDel = new HttpRequestMessage(HttpMethod.Get, $"/api/vaults/{created.Id}");
        getAfterDel.Headers.Add("X-UserId", owner.User.Id.ToString());
        var getAfterDelResp = await _client.SendAsync(getAfterDel);
        getAfterDelResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Shared_User_Can_List_And_Get_But_Cannot_Update_Delete()
    {
        var owner = await RegisterAsync("vault-share-owner@test.local");
        var reader = await RegisterAsync("vault-share-reader@test.local");

        // Create vault as owner
        var create = new { name = "Shared Vault", description = "shared vault", userId = owner.User.Id };
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

        // Reader can list and see shared vault
        var listReq = new HttpRequestMessage(HttpMethod.Get, "/api/vaults");
        listReq.Headers.Add("X-UserId", reader.User.Id.ToString());
        var listResp = await _client.SendAsync(listReq);
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResp.Content.ReadFromJsonAsync<List<VaultResponse>>();
        list.Should().NotBeNull();
        list!.Should().Contain(v => v.Id == vault.Id);

        // Reader can get
        var getReq = new HttpRequestMessage(HttpMethod.Get, $"/api/vaults/{vault.Id}");
        getReq.Headers.Add("X-UserId", reader.User.Id.ToString());
        var getResp = await _client.SendAsync(getReq);
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Reader cannot update
        var update = new { name = "should not", description = "nope" };
        var updateReq = new HttpRequestMessage(HttpMethod.Put, $"/api/vaults/{vault.Id}")
        {
            Content = JsonContent.Create(update)
        };
        updateReq.Headers.Add("X-UserId", reader.User.Id.ToString());
        var updateResp = await _client.SendAsync(updateReq);
        updateResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Reader cannot delete
        var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/vaults/{vault.Id}");
        delReq.Headers.Add("X-UserId", reader.User.Id.ToString());
        var delResp = await _client.SendAsync(delReq);
        delResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Owner_Can_Update_Vault()
    {
        var owner = await RegisterAsync("vault-owner-update@test.local");

        // Create
        var create = new { name = "Owner Vault", description = "owner vault", userId = owner.User.Id };
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(create)
        };
        createReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var createResp = await _client.SendAsync(createReq);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var vault = await createResp.Content.ReadFromJsonAsync<VaultResponse>();

        // Update
        var update = new { name = "Updated Vault", description = "updated desc" };
        var updateReq = new HttpRequestMessage(HttpMethod.Put, $"/api/vaults/{vault!.Id}")
        {
            Content = JsonContent.Create(update)
        };
        updateReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var updateResp = await _client.SendAsync(updateReq);
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResp.Content.ReadFromJsonAsync<VaultResponse>();
        updated!.Name.Should().Be("Updated Vault");
        updated.Description.Should().Be("updated desc");
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

