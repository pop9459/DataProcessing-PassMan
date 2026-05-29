using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using PassManAPI.DTOs;
using Xunit;

namespace PassManAPI.Tests;

/// <summary>
/// Exercises the real JWT bearer-token path (as opposed to the X-UserId dev header used by
/// the other endpoint tests). Regression coverage for #159: the JWT issued on register/login
/// must embed the user's role + permission claims, otherwise every permission-protected
/// endpoint returns 403 even though the user has the permissions in the database.
/// </summary>
public class JwtAuthFlowTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public JwtAuthFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task BearerToken_From_Register_Can_Access_Permission_Protected_Endpoint()
    {
        var auth = await RegisterAsync("jwt-flow@test.local");
        auth.AccessToken.Should().NotBeNullOrWhiteSpace();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/vaults");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await _client.SendAsync(request);

        // Before the fix this returned 403 (token carried no "permission" claims).
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BearerToken_From_Login_Can_Access_Permission_Protected_Endpoint()
    {
        const string email = "jwt-login-flow@test.local";
        await RegisterAsync(email);

        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = email, Password = "Password1!" });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/tags");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Permission_Protected_Endpoint_Without_Token_Returns_401()
    {
        var response = await _client.GetAsync("/api/vaults");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
