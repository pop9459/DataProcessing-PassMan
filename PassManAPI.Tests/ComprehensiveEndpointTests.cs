using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PassManAPI.DTOs;
using Xunit;

namespace PassManAPI.Tests;

public class ComprehensiveEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ComprehensiveEndpointTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ProtectedEndpoints_WithoutAuth_ReturnUnauthorized()
    {
        var endpoints = new[]
        {
            (HttpMethod.Get, "/api/vaults"),
            (HttpMethod.Get, "/api/credentials"),
            (HttpMethod.Get, "/api/tags"),
            (HttpMethod.Get, "/api/audit/logs")
        };

        foreach (var (method, path) in endpoints)
        {
            var req = new HttpRequestMessage(method, path);
            var resp = await _client.SendAsync(req);
            resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized, because: path);
        }
    }

    [Fact]
    public async Task Register_InvalidPayload_ReturnsBadRequest_WithErrors()
    {
        // Missing required fields
        var bad = new { Email = "not-an-email" };
        var resp = await _client.PostAsJsonAsync("/api/auth/register", bad);

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var text = await resp.Content.ReadAsStringAsync();
        text.Should().Contain("Password").OrContain("ConfirmPassword").OrContain("UserName");
    }

    [Fact]
    public async Task CreateVault_MissingName_ReturnsBadRequest()
    {
        var user = await RegisterAsync("smoke-vault-invalid@test.local");

        var create = new { description = "no name provided" };
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/vaults") { Content = JsonContent.Create(create) };
        req.Headers.Add("X-UserId", user.User.Id.ToString());

        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var txt = await resp.Content.ReadAsStringAsync();
        txt.Should().Contain("Name").OrContain("required");
    }

    [Fact]
    public async Task CreateVault_AsAuthenticatedUser_Succeeds()
    {
        var user = await RegisterAsync("smoke-vault-ok@test.local");

        var create = new { name = "Smoke Vault", description = "created by smoke test" };
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/vaults") { Content = JsonContent.Create(create) };
        req.Headers.Add("X-UserId", user.User.Id.ToString());

        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await resp.Content.ReadFromJsonAsync<object>();
        payload.Should().NotBeNull();
    }

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

    #endregion
}
