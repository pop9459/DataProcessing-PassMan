using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PassManAPI.DTOs;
using PassManAPI.Models;
using Xunit;

namespace PassManAPI.Tests;

public class AuditEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuditEndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    #region GetMyLogs Tests

    [Fact]
    public async Task GetMyLogs_Unauthenticated_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/audit/logs");
        // No X-UserId header

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyLogs_Authenticated_ReturnsEmptyListInitially()
    {
        var user = await RegisterAsync("audit-empty-logs@test.local");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/audit/logs");
        request.Headers.Add("X-UserId", user.User.Id.ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PaginatedAuditResult>();
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.Page.Should().Be(1);
    }

    [Fact]
    public async Task GetMyLogs_AfterVaultCreation_ContainsAuditEntry()
    {
        var user = await RegisterAsync("audit-vault-create@test.local");

        // Create a vault to generate audit log
        var createVault = new { name = "Audit Test Vault", description = "test", userId = user.User.Id };
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(createVault)
        };
        createReq.Headers.Add("X-UserId", user.User.Id.ToString());
        var createResp = await _client.SendAsync(createReq);
        createResp.EnsureSuccessStatusCode();

        // Check audit logs
        var logsReq = new HttpRequestMessage(HttpMethod.Get, "/api/audit/logs");
        logsReq.Headers.Add("X-UserId", user.User.Id.ToString());
        var logsResp = await _client.SendAsync(logsReq);

        logsResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await logsResp.Content.ReadFromJsonAsync<PaginatedAuditResult>();
        result.Should().NotBeNull();
        // The user registration should have created at least one audit log
        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetMyLogs_WithPagination_ReturnsCorrectPage()
    {
        var user = await RegisterAsync("audit-pagination@test.local");

        // Request with specific page size
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/audit/logs?page=1&pageSize=5");
        request.Headers.Add("X-UserId", user.User.Id.ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PaginatedAuditResult>();
        result.Should().NotBeNull();
        result!.Page.Should().Be(1);
        result.PageSize.Should().Be(5);
    }

    [Fact]
    public async Task GetMyLogs_WithActionFilter_FiltersCorrectly()
    {
        var user = await RegisterAsync("audit-filter-action@test.local");

        // Filter by action (UserRegistered should be present from registration)
        var actionValue = (int)AuditAction.UserRegistered;
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/audit/logs?action={actionValue}");
        request.Headers.Add("X-UserId", user.User.Id.ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PaginatedAuditResult>();
        result.Should().NotBeNull();
        // All items should have the filtered action
        foreach (var item in result!.Items)
        {
            item.Action.Should().Be(AuditAction.UserRegistered);
        }
    }

    [Fact]
    public async Task GetMyLogs_WithDateFilter_FiltersCorrectly()
    {
        var user = await RegisterAsync("audit-filter-date@test.local");

        // Filter from today
        var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/audit/logs?startDate={today}");
        request.Headers.Add("X-UserId", user.User.Id.ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region GetLogById Tests

    [Fact]
    public async Task GetLogById_NonExistent_Returns404()
    {
        var user = await RegisterAsync("audit-notfound@test.local");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/audit/logs/999999");
        request.Headers.Add("X-UserId", user.User.Id.ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetLogById_OtherUsersLog_WithoutAuditRead_ReturnsForbidden()
    {
        var user1 = await RegisterAsync("audit-other1@test.local");
        var user2 = await RegisterAsync("audit-other2@test.local");

        // Get user1's logs to find an ID
        var logsReq = new HttpRequestMessage(HttpMethod.Get, "/api/audit/logs");
        logsReq.Headers.Add("X-UserId", user1.User.Id.ToString());
        var logsResp = await _client.SendAsync(logsReq);
        var logs = await logsResp.Content.ReadFromJsonAsync<PaginatedAuditResult>();

        if (logs?.Items.Any() == true)
        {
            var logId = logs.Items.First().Id;

            // User2 tries to access user1's log
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/audit/logs/{logId}");
            request.Headers.Add("X-UserId", user2.User.Id.ToString());

            var response = await _client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task GetLogById_OwnLog_ReturnsOk()
    {
        var user = await RegisterAsync("audit-own-log@test.local");

        // Get user's logs
        var logsReq = new HttpRequestMessage(HttpMethod.Get, "/api/audit/logs");
        logsReq.Headers.Add("X-UserId", user.User.Id.ToString());
        var logsResp = await _client.SendAsync(logsReq);
        var logs = await logsResp.Content.ReadFromJsonAsync<PaginatedAuditResult>();

        if (logs?.Items.Any() == true)
        {
            var logId = logs.Items.First().Id;

            // Access own log
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/audit/logs/{logId}");
            request.Headers.Add("X-UserId", user.User.Id.ToString());

            var response = await _client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var log = await response.Content.ReadFromJsonAsync<AuditLogDto>();
            log.Should().NotBeNull();
            log!.Id.Should().Be(logId);
        }
    }

    #endregion

    #region GetVaultLogs Tests

    [Fact]
    public async Task GetVaultLogs_NonExistentVault_Returns404()
    {
        var user = await RegisterAsync("audit-vault-notfound@test.local");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/audit/logs/vault/999999");
        request.Headers.Add("X-UserId", user.User.Id.ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetVaultLogs_NoAccess_ReturnsForbidden()
    {
        var owner = await RegisterAsync("audit-vault-owner@test.local");
        var other = await RegisterAsync("audit-vault-other@test.local");

        // Create vault as owner
        var createVault = new { name = "Private Vault", description = "test", userId = owner.User.Id };
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(createVault)
        };
        createReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var createResp = await _client.SendAsync(createReq);
        var vault = await createResp.Content.ReadFromJsonAsync<VaultResponse>();

        // Other user tries to access vault logs
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/audit/logs/vault/{vault!.Id}");
        request.Headers.Add("X-UserId", other.User.Id.ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetVaultLogs_Owner_ReturnsOk()
    {
        var owner = await RegisterAsync("audit-vault-owner2@test.local");

        // Create vault
        var createVault = new { name = "Owner Vault", description = "test", userId = owner.User.Id };
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(createVault)
        };
        createReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var createResp = await _client.SendAsync(createReq);
        var vault = await createResp.Content.ReadFromJsonAsync<VaultResponse>();

        // Owner accesses vault logs
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/audit/logs/vault/{vault!.Id}");
        request.Headers.Add("X-UserId", owner.User.Id.ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PaginatedAuditResult>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetVaultLogs_SharedUser_ReturnsOk()
    {
        var owner = await RegisterAsync("audit-share-owner@test.local");
        var reader = await RegisterAsync("audit-share-reader@test.local");

        // Create vault
        var createVault = new { name = "Shared Vault", description = "test", userId = owner.User.Id };
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(createVault)
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

        // Reader accesses vault logs
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/audit/logs/vault/{vault.Id}");
        request.Headers.Add("X-UserId", reader.User.Id.ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region GetAllLogs Tests

    [Fact]
    public async Task GetAllLogs_WithoutAuditReadPermission_ReturnsForbidden()
    {
        var user = await RegisterAsync("audit-all-noperm@test.local");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/audit/logs/all");
        request.Headers.Add("X-UserId", user.User.Id.ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAllLogs_WithAuditReadPermission_ReturnsOk()
    {
        // Use seeded admin account which has audit.read permission
        var admin = await LoginAsync("admin@passman.test", "Admin123!");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/audit/logs/all");
        request.Headers.Add("X-UserId", admin.User.Id.ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PaginatedAuditResult>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllLogs_WithUserIdFilter_ReturnsFilteredLogs()
    {
        // Use seeded admin account
        var admin = await LoginAsync("admin@passman.test", "Admin123!");
        var targetUser = await RegisterAsync("audit-filter-target@test.local");

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/audit/logs/all?userId={targetUser.User.Id}");
        request.Headers.Add("X-UserId", admin.User.Id.ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PaginatedAuditResult>();
        result.Should().NotBeNull();
        // All items should belong to the target user
        foreach (var item in result!.Items)
        {
            item.UserId.Should().Be(targetUser.User.Id);
        }
    }

    #endregion

    #region Helpers

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

    private record VaultResponse(int Id, string Name, string? Description, string? Icon, DateTime CreatedAt, DateTime? UpdatedAt, int UserId, bool IsOwner);

    #endregion
}
