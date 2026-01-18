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

    private record VaultResponse(int Id, string Name, string? Description, string? Icon, DateTime CreatedAt, DateTime? UpdatedAt, int UserId, bool IsOwner);

    [Fact]
    public async Task Create_Vault_With_Icon_Returns_Icon_In_Response()
    {
        var owner = await RegisterAsync("vault-icon-create@test.local");

        var create = new { name = "Vault with Icon", description = "has icon", icon = "🔐", userId = owner.User.Id };
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(create)
        };
        createReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var createResp = await _client.SendAsync(createReq);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<VaultResponse>();
        created.Should().NotBeNull();
        created!.Icon.Should().Be("🔐");
        created.Name.Should().Be("Vault with Icon");
    }

    [Fact]
    public async Task Update_Vault_Icon_Returns_Updated_Icon()
    {
        var owner = await RegisterAsync("vault-icon-update@test.local");

        // Create without icon
        var create = new { name = "Vault to update icon", description = "no icon yet", userId = owner.User.Id };
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(create)
        };
        createReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var createResp = await _client.SendAsync(createReq);
        var vault = await createResp.Content.ReadFromJsonAsync<VaultResponse>();
        vault!.Icon.Should().BeNull();

        // Update with icon
        var update = new { name = "Vault with new icon", description = "now has icon", icon = "📁" };
        var updateReq = new HttpRequestMessage(HttpMethod.Put, $"/api/vaults/{vault.Id}")
        {
            Content = JsonContent.Create(update)
        };
        updateReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var updateResp = await _client.SendAsync(updateReq);
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResp.Content.ReadFromJsonAsync<VaultResponse>();
        updated!.Icon.Should().Be("📁");
    }

    [Fact]
    public async Task Owner_IsOwner_True_In_Vault_Response()
    {
        var owner = await RegisterAsync("vault-isowner-owner@test.local");

        var create = new { name = "Owner IsOwner Test", description = "test", userId = owner.User.Id };
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(create)
        };
        createReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var createResp = await _client.SendAsync(createReq);
        var vault = await createResp.Content.ReadFromJsonAsync<VaultResponse>();
        vault!.IsOwner.Should().BeTrue();

        // Also verify in list
        var listReq = new HttpRequestMessage(HttpMethod.Get, "/api/vaults");
        listReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var listResp = await _client.SendAsync(listReq);
        var list = await listResp.Content.ReadFromJsonAsync<List<VaultResponse>>();
        list!.Single(v => v.Id == vault.Id).IsOwner.Should().BeTrue();
    }

    [Fact]
    public async Task Shared_User_IsOwner_False_In_Vault_Response()
    {
        var owner = await RegisterAsync("vault-isowner-owner2@test.local");
        var reader = await RegisterAsync("vault-isowner-reader@test.local");

        // Create vault as owner
        var create = new { name = "Shared IsOwner Test", description = "test", userId = owner.User.Id };
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(create)
        };
        createReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var createResp = await _client.SendAsync(createReq);
        var vault = await createResp.Content.ReadFromJsonAsync<VaultResponse>();

        // Share to reader
        var shareReq = new HttpRequestMessage(HttpMethod.Post, $"/api/vaults/{vault!.Id}/share")
        {
            Content = JsonContent.Create(new { userEmail = reader.User.Email })
        };
        shareReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        await _client.SendAsync(shareReq);

        // Reader gets vault - IsOwner should be false
        var getReq = new HttpRequestMessage(HttpMethod.Get, $"/api/vaults/{vault.Id}");
        getReq.Headers.Add("X-UserId", reader.User.Id.ToString());
        var getResp = await _client.SendAsync(getReq);
        var readerVault = await getResp.Content.ReadFromJsonAsync<VaultResponse>();
        readerVault!.IsOwner.Should().BeFalse();

        // Also verify in list
        var listReq = new HttpRequestMessage(HttpMethod.Get, "/api/vaults");
        listReq.Headers.Add("X-UserId", reader.User.Id.ToString());
        var listResp = await _client.SendAsync(listReq);
        var list = await listResp.Content.ReadFromJsonAsync<List<VaultResponse>>();
        list!.Single(v => v.Id == vault.Id).IsOwner.Should().BeFalse();
    }

    [Fact]
    public async Task Soft_Deleted_Vault_Does_Not_Appear_In_List()
    {
        var owner = await RegisterAsync("vault-softdelete-list@test.local");

        // Create two vaults
        var create1 = new { name = "Vault To Keep", description = "keep", userId = owner.User.Id };
        var createReq1 = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(create1)
        };
        createReq1.Headers.Add("X-UserId", owner.User.Id.ToString());
        var createResp1 = await _client.SendAsync(createReq1);
        var vault1 = await createResp1.Content.ReadFromJsonAsync<VaultResponse>();

        var create2 = new { name = "Vault To Delete", description = "delete", userId = owner.User.Id };
        var createReq2 = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(create2)
        };
        createReq2.Headers.Add("X-UserId", owner.User.Id.ToString());
        var createResp2 = await _client.SendAsync(createReq2);
        var vault2 = await createResp2.Content.ReadFromJsonAsync<VaultResponse>();

        // Delete vault2
        var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/vaults/{vault2!.Id}");
        delReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        await _client.SendAsync(delReq);

        // List should only contain vault1
        var listReq = new HttpRequestMessage(HttpMethod.Get, "/api/vaults");
        listReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var listResp = await _client.SendAsync(listReq);
        var list = await listResp.Content.ReadFromJsonAsync<List<VaultResponse>>();
        list!.Should().Contain(v => v.Id == vault1!.Id);
        list.Should().NotContain(v => v.Id == vault2.Id);
    }

    [Fact]
    public async Task Soft_Deleted_Vault_Not_Accessible_By_Direct_Get()
    {
        var owner = await RegisterAsync("vault-softdelete-get@test.local");

        // Create vault
        var create = new { name = "Vault To Soft Delete", description = "will be deleted", userId = owner.User.Id };
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(create)
        };
        createReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var createResp = await _client.SendAsync(createReq);
        var vault = await createResp.Content.ReadFromJsonAsync<VaultResponse>();

        // Delete vault
        var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/vaults/{vault!.Id}");
        delReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var delResp = await _client.SendAsync(delReq);
        delResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Try to get the deleted vault
        var getReq = new HttpRequestMessage(HttpMethod.Get, $"/api/vaults/{vault.Id}");
        getReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var getResp = await _client.SendAsync(getReq);
        getResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Soft_Deleted_Vault_Cannot_Be_Updated()
    {
        var owner = await RegisterAsync("vault-softdelete-update@test.local");

        // Create vault
        var create = new { name = "Vault To Delete Then Update", description = "test", userId = owner.User.Id };
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(create)
        };
        createReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var createResp = await _client.SendAsync(createReq);
        var vault = await createResp.Content.ReadFromJsonAsync<VaultResponse>();

        // Delete vault
        var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/vaults/{vault!.Id}");
        delReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        await _client.SendAsync(delReq);

        // Try to update the deleted vault
        var update = new { name = "Should Fail", description = "should not work" };
        var updateReq = new HttpRequestMessage(HttpMethod.Put, $"/api/vaults/{vault.Id}")
        {
            Content = JsonContent.Create(update)
        };
        updateReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var updateResp = await _client.SendAsync(updateReq);
        updateResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Shared_Vault_Not_Visible_To_Shared_User_After_Soft_Delete()
    {
        var owner = await RegisterAsync("vault-softdelete-share-owner@test.local");
        var reader = await RegisterAsync("vault-softdelete-share-reader@test.local");

        // Create vault as owner
        var create = new { name = "Shared Then Deleted", description = "test", userId = owner.User.Id };
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(create)
        };
        createReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var createResp = await _client.SendAsync(createReq);
        var vault = await createResp.Content.ReadFromJsonAsync<VaultResponse>();

        // Share to reader
        var shareReq = new HttpRequestMessage(HttpMethod.Post, $"/api/vaults/{vault!.Id}/share")
        {
            Content = JsonContent.Create(new { userEmail = reader.User.Email })
        };
        shareReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        await _client.SendAsync(shareReq);

        // Verify reader can see it
        var listBefore = new HttpRequestMessage(HttpMethod.Get, "/api/vaults");
        listBefore.Headers.Add("X-UserId", reader.User.Id.ToString());
        var listBeforeResp = await _client.SendAsync(listBefore);
        var vaultsBefore = await listBeforeResp.Content.ReadFromJsonAsync<List<VaultResponse>>();
        vaultsBefore!.Should().Contain(v => v.Id == vault.Id);

        // Owner deletes vault
        var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/vaults/{vault.Id}");
        delReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        await _client.SendAsync(delReq);

        // Reader should no longer see it
        var listAfter = new HttpRequestMessage(HttpMethod.Get, "/api/vaults");
        listAfter.Headers.Add("X-UserId", reader.User.Id.ToString());
        var listAfterResp = await _client.SendAsync(listAfter);
        var vaultsAfter = await listAfterResp.Content.ReadFromJsonAsync<List<VaultResponse>>();
        vaultsAfter!.Should().NotContain(v => v.Id == vault.Id);
    }
}

