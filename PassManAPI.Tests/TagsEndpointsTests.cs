using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PassManAPI.DTOs;
using Xunit;

namespace PassManAPI.Tests;

public class TagsEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public TagsEndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Owner_Can_Create_List_Update_And_Delete_Tags()
    {
        var user = await RegisterAsync("tags-owner@test.local");
        var userIdString = user.User.Id.ToString();

        // 1. Create a Tag
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/tags")
        {
            Content = JsonContent.Create(new CreateTagRequest { Name = "Work" })
        };
        createRequest.Headers.Add("X-UserId", userIdString);
        
        var createResponse = await _client.SendAsync(createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var createdTag = await createResponse.Content.ReadFromJsonAsync<TagDto>();
        createdTag.Should().NotBeNull();
        createdTag!.Name.Should().Be("Work");

        // 2. List Tags
        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/tags");
        listRequest.Headers.Add("X-UserId", userIdString);
        
        var listResponse = await _client.SendAsync(listRequest);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var tags = await listResponse.Content.ReadFromJsonAsync<List<TagDto>>();
        tags.Should().ContainSingle(t => t.Id == createdTag.Id && t.Name == "Work");

        // 3. Update Tag (Rename)
        var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/tags/{createdTag.Id}")
        {
            Content = JsonContent.Create(new UpdateTagRequest { Name = "Work-Updated" })
        };
        updateRequest.Headers.Add("X-UserId", userIdString);

        var updateResponse = await _client.SendAsync(updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedTag = await updateResponse.Content.ReadFromJsonAsync<TagDto>();
        updatedTag!.Name.Should().Be("Work-Updated");

        // Verify update in list
        listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/tags");
        listRequest.Headers.Add("X-UserId", userIdString);
        tags = await (await _client.SendAsync(listRequest)).Content.ReadFromJsonAsync<List<TagDto>>();
        tags.Should().ContainSingle(t => t.Name == "Work-Updated");

        // 4. Delete Tag
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/tags/{createdTag.Id}");
        deleteRequest.Headers.Add("X-UserId", userIdString);

        var deleteResponse = await _client.SendAsync(deleteRequest);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deletion
        listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/tags");
        listRequest.Headers.Add("X-UserId", userIdString);
        tags = await (await _client.SendAsync(listRequest)).Content.ReadFromJsonAsync<List<TagDto>>();
        tags.Should().BeEmpty();
    }

    [Fact]
    public async Task Cannot_Create_Duplicate_Tag()
    {
        var user = await RegisterAsync("tags-dup@test.local");
        var userIdString = user.User.Id.ToString();

        // Create initial tag
        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/tags")
        {
            Content = JsonContent.Create(new CreateTagRequest { Name = "Unique" })
        };
        req1.Headers.Add("X-UserId", userIdString);
        (await _client.SendAsync(req1)).EnsureSuccessStatusCode();

        // Try duplicate
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/tags")
        {
            Content = JsonContent.Create(new CreateTagRequest { Name = "Unique" })
        };
        req2.Headers.Add("X-UserId", userIdString);
        var resp2 = await _client.SendAsync(req2);

        resp2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await resp2.Content.ReadAsStringAsync();
        error.Should().Contain("already exists");
    }

    [Fact]
    public async Task Users_Cannot_See_Or_Modify_Others_Tags()
    {
        var owner = await RegisterAsync("tag-owner-2@test.local");
        var other = await RegisterAsync("tag-other@test.local");

        // Owner creates a tag
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/tags")
        {
            Content = JsonContent.Create(new CreateTagRequest { Name = "SecretTag" })
        };
        createReq.Headers.Add("X-UserId", owner.User.Id.ToString());
        var createResp = await _client.SendAsync(createReq);
        var tag = await createResp.Content.ReadFromJsonAsync<TagDto>();

        // Other tries to get owner's tag
        var getReq = new HttpRequestMessage(HttpMethod.Get, $"/api/tags/{tag!.Id}");
        getReq.Headers.Add("X-UserId", other.User.Id.ToString());
        var getResp = await _client.SendAsync(getReq);
        getResp.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);

        // Other tries to delete owner's tag
        var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/tags/{tag.Id}");
        delReq.Headers.Add("X-UserId", other.User.Id.ToString());
        var delResp = await _client.SendAsync(delReq);
        delResp.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
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
