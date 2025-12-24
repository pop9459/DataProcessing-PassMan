using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using PassManAPI.DTOs;
using Xunit;

namespace PassManAPI.Tests;

public class AuthEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AuthEndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_Returns_Token_And_User()
    {
        var request = NewRegister("reg1@test.local");

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);
        payload.Should().NotBeNull();
        payload!.AccessToken.Should().NotBeNullOrWhiteSpace();
        payload.User.Id.Should().BeGreaterThan(0);
        payload.User.Email.Should().Be(request.Email);
    }

    [Fact]
    public async Task Login_Returns_Token_When_Credentials_Correct()
    {
        var email = "login1@test.local";
        await _client.PostAsJsonAsync("/api/auth/register", NewRegister(email));

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password1!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);
        payload.Should().NotBeNull();
        payload!.AccessToken.Should().NotBeNullOrWhiteSpace();
        payload.User.Email.Should().Be(email);
    }

    [Fact]
    public async Task GetMe_Returns_Profile_When_Header_Present()
    {
        var reg = await RegisterAndGet();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("X-UserId", reg.User.Id.ToString());
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>(_jsonOptions);
        profile!.Email.Should().Be(reg.User.Email);
    }

    [Fact]
    public async Task UpdateProfile_Changes_UserName()
    {
        var reg = await RegisterAndGet();

        var update = new UpdateProfileRequest { UserName = "updated-user" };
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/auth/me")
        {
            Content = JsonContent.Create(update)
        };
        request.Headers.Add("X-UserId", reg.User.Id.ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>(_jsonOptions);
        profile!.UserName.Should().Be("updated-user");
    }

    [Fact]
    public async Task DeleteAccount_Removes_User()
    {
        var reg = await RegisterAndGet();

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/auth/me");
        deleteRequest.Headers.Add("X-UserId", reg.User.Id.ToString());
        var deleteResponse = await _client.SendAsync(deleteRequest);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Add("X-UserId", reg.User.Id.ToString());
        var meResponse = await _client.SendAsync(meRequest);
        meResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static RegisterRequest NewRegister(string email) =>
        new()
        {
            Email = email,
            Password = "Password1!",
            ConfirmPassword = "Password1!",
            UserName = email.Split('@')[0],
            PhoneNumber = "1234567890"
        };

    private async Task<AuthResponse> RegisterAndGet()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", NewRegister(Guid.NewGuid() + "@test.local"));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);
        return payload!;
    }
}

