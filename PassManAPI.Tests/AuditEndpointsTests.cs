using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PassManAPI.DTOs;
using Xunit;

namespace PassManAPI.Tests;

public class AuditEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuditEndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Actions_Create_Audit_Logs()
    {
        var user = await RegisterAsync("audit-user@test.local");
        var userIdString = user.User.Id.ToString();

        // 1. Perform an action that should be audited (Create Tag)
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/tags")
        {
            Content = JsonContent.Create(new CreateTagRequest { Name = "AuditedTag" })
        };
        createRequest.Headers.Add("X-UserId", userIdString);
        (await _client.SendAsync(createRequest)).EnsureSuccessStatusCode();

        // 2. Retrieve Audit Logs
        var logRequest = new HttpRequestMessage(HttpMethod.Get, "/api/audit/logs");
        logRequest.Headers.Add("X-UserId", userIdString);
        
        var logResponse = await _client.SendAsync(logRequest);
        logResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var logs = await logResponse.Content.ReadFromJsonAsync<List<AuditLogDto>>();
        logs.Should().NotBeNull();
        logs.Should().Contain(l => l.Action == "TagCreated" && l.Details.Contains("AuditedTag"));
    }

    [Fact]
    public async Task Users_Cannot_See_Others_Logs()
    {
        var userA = await RegisterAsync("audit-a@test.local");
        var userB = await RegisterAsync("audit-b@test.local");

        // User A creates a tag (generates log)
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/tags")
        {
            Content = JsonContent.Create(new CreateTagRequest { Name = "UserATag" })
        };
        createRequest.Headers.Add("X-UserId", userA.User.Id.ToString());
        (await _client.SendAsync(createRequest)).EnsureSuccessStatusCode();

        // User B tries to read logs
        var logRequest = new HttpRequestMessage(HttpMethod.Get, "/api/audit/logs");
        logRequest.Headers.Add("X-UserId", userB.User.Id.ToString());
        
        var logResponse = await _client.SendAsync(logRequest);
        logResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var logs = await logResponse.Content.ReadFromJsonAsync<List<AuditLogDto>>();
        
        // B should NOT see A's logs
        logs.Should().NotContain(l => l.Details.Contains("UserATag"));
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
}
