using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using PassManAPI.DTOs;
using Xunit;

namespace PassManAPI.Tests;

/// <summary>
/// Integration tests for XML serialization support.
/// Tests both XML request bodies (Content-Type: application/xml) and XML responses (Accept: application/xml).
/// </summary>
public class XmlSerializationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public XmlSerializationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_Should_Accept_XML_Request_And_Return_XML_Response()
    {
        // Arrange - First register a user with JSON
        var registerRequest = new RegisterRequest
        {
            Email = "xmltest@test.local",
            Password = "Password1!",
            ConfirmPassword = "Password1!",
            UserName = "xmltest",
            PhoneNumber = "1234567890"
        };

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        // Act - Login with XML request
        var loginXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<LoginRequest xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
    <Email>xmltest@test.local</Email>
    <Password>Password1!</Password>
</LoginRequest>";

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = new StringContent(loginXml, Encoding.UTF8, "application/xml")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/xml");

        var xmlContent = await response.Content.ReadAsStringAsync();
        xmlContent.Should().NotBeNullOrEmpty();

        // Verify XML structure
        var xmlDoc = XDocument.Parse(xmlContent);
        xmlDoc.Root.Should().NotBeNull();
        xmlDoc.Root!.Name.LocalName.Should().Be("AuthResponse");
        
        var accessTokenElement = xmlDoc.Root.Element("AccessToken");
        accessTokenElement.Should().NotBeNull();
        accessTokenElement!.Value.Should().NotBeNullOrEmpty();

        var userElement = xmlDoc.Root.Element("User");
        userElement.Should().NotBeNull();
        
        var emailElement = userElement!.Element("Email");
        emailElement.Should().NotBeNull();
        emailElement!.Value.Should().Be("xmltest@test.local");
    }

    [Fact]
    public async Task GetVaults_Should_Return_XML_When_Accept_Header_Is_XML()
    {
        // Arrange - Register and create a vault
        var user = await RegisterAsync("vaultxml@test.local");

        var vault = new
        {
            name = "XML Test Vault",
            description = "Testing XML serialization",
            userId = user.User.Id
        };

        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(vault)
        };
        createRequest.Headers.Add("X-UserId", user.User.Id.ToString());
        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();

        // Act - Get vaults with XML accept header
        var getRequest = new HttpRequestMessage(HttpMethod.Get, "/api/vaults");
        getRequest.Headers.Add("X-UserId", user.User.Id.ToString());
        getRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

        var response = await _client.SendAsync(getRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/xml");

        var xmlContent = await response.Content.ReadAsStringAsync();
        xmlContent.Should().NotBeNullOrEmpty();

        // Verify XML contains vault data
        var xmlDoc = XDocument.Parse(xmlContent);
        xmlDoc.Root.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateCredential_Should_Accept_XML_Request()
    {
        // Arrange - Create user and vault
        var user = await RegisterAsync("credxml@test.local");

        var vault = new
        {
            name = "Cred XML Vault",
            description = "vault for xml cred test",
            userId = user.User.Id
        };

        var vaultRequest = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(vault)
        };
        vaultRequest.Headers.Add("X-UserId", user.User.Id.ToString());
        var vaultResponse = await _client.SendAsync(vaultRequest);
        vaultResponse.EnsureSuccessStatusCode();
        var vaultPayload = await vaultResponse.Content.ReadFromJsonAsync<CreatedVaultResponse>();

        // Act - Create credential with XML request
        var credentialXml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<CreateCredentialRequest xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
    <Title>XML Credential</Title>
    <Username>xmluser</Username>
    <EncryptedPassword>enc-xml-pass</EncryptedPassword>
    <Url>https://xml.test</Url>
    <Notes>Created via XML</Notes>
</CreateCredentialRequest>";

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/vaults/{vaultPayload!.Id}/credentials")
        {
            Content = new StringContent(credentialXml, Encoding.UTF8, "application/xml")
        };
        request.Headers.Add("X-UserId", user.User.Id.ToString());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        // Verify the credential was created by listing credentials
        var listRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/vaults/{vaultPayload.Id}/credentials");
        listRequest.Headers.Add("X-UserId", user.User.Id.ToString());
        var listResponse = await _client.SendAsync(listRequest);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var items = await listResponse.Content.ReadFromJsonAsync<List<CredentialListItem>>();
        items.Should().NotBeNull();
        items!.Should().ContainSingle(i => i.Title == "XML Credential" && i.Username == "xmluser");
    }

    [Fact]
    public async Task ErrorResponse_Should_Be_Serialized_As_XML()
    {
        // Act - Make a request that will fail (validation error - empty email)
        var loginXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<LoginRequest xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
    <Email></Email>
    <Password>SomePassword1!</Password>
</LoginRequest>";

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = new StringContent(loginXml, Encoding.UTF8, "application/xml")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

        var response = await _client.SendAsync(request);

        // Assert - Should get BadRequest for validation error
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var xmlContent = await response.Content.ReadAsStringAsync();
        xmlContent.Should().NotBeNullOrEmpty();

        // Verify it's XML (even if it's a ProblemDetails response)
        // ASP.NET Core returns ProblemDetails for validation errors, which should serialize to XML
        var xmlDoc = XDocument.Parse(xmlContent);
        xmlDoc.Root.Should().NotBeNull();
    }

    [Fact]
    public async Task JSON_Should_Still_Work_When_XML_Is_Enabled()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            Email = $"jsontest-{Guid.NewGuid()}@test.local",
            Password = "Password1!",
            ConfirmPassword = "Password1!",
            UserName = "jsontest",
            PhoneNumber = "1234567890"
        };

        // Act - Use JSON (default)
        var response = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        payload.Should().NotBeNull();
        payload!.User.Email.Should().Be(registerRequest.Email);
    }

    [Fact]
    public async Task GetTags_Should_Return_XML_When_Accept_Is_XML()
    {
        // Covers the record->class fix for TagDto (positional records aren't XmlSerializable).
        var user = await RegisterAsync($"tags-xml-{Guid.NewGuid()}@test.local");

        var create = new HttpRequestMessage(HttpMethod.Post, "/api/tags")
        {
            Content = JsonContent.Create(new { name = "xml-tag" })
        };
        create.Headers.Add("X-UserId", user.User.Id.ToString());
        (await _client.SendAsync(create)).StatusCode.Should().Be(HttpStatusCode.Created);

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/tags");
        req.Headers.Add("X-UserId", user.User.Id.ToString());
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        var resp = await _client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("application/xml");
        var body = await resp.Content.ReadAsStringAsync();
        XDocument.Parse(body).Root!.Name.LocalName.Should().Be("ArrayOfTagDto");
        body.Should().Contain("<Name>xml-tag</Name>");
    }

    [Fact]
    public async Task Credentials_List_And_Password_Should_Return_XML_When_Accept_Is_XML()
    {
        // Covers the anonymous-type -> named-DTO fix (CredentialListItemDto, PasswordResponse).
        var user = await RegisterAsync($"creds-xml-{Guid.NewGuid()}@test.local");

        var vReq = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(new { name = "xml-vault", userId = user.User.Id })
        };
        vReq.Headers.Add("X-UserId", user.User.Id.ToString());
        var vaultId = (await (await _client.SendAsync(vReq)).Content.ReadFromJsonAsync<CreatedVaultResponse>())!.Id;

        var cReq = new HttpRequestMessage(HttpMethod.Post, $"/api/vaults/{vaultId}/credentials")
        {
            Content = JsonContent.Create(new { title = "xml-cred", encryptedPassword = "secret" })
        };
        cReq.Headers.Add("X-UserId", user.User.Id.ToString());
        var credId = (await (await _client.SendAsync(cReq)).Content.ReadFromJsonAsync<CreatedVaultResponse>())!.Id;

        var listReq = new HttpRequestMessage(HttpMethod.Get, $"/api/vaults/{vaultId}/credentials");
        listReq.Headers.Add("X-UserId", user.User.Id.ToString());
        listReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        var listResp = await _client.SendAsync(listReq);
        listResp.Content.Headers.ContentType?.MediaType.Should().Be("application/xml");
        XDocument.Parse(await listResp.Content.ReadAsStringAsync())
            .Root!.Name.LocalName.Should().Be("ArrayOfCredentialListItemDto");

        var pwReq = new HttpRequestMessage(HttpMethod.Get, $"/api/credentials/{credId}/password");
        pwReq.Headers.Add("X-UserId", user.User.Id.ToString());
        pwReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        var pwResp = await _client.SendAsync(pwReq);
        pwResp.Content.Headers.ContentType?.MediaType.Should().Be("application/xml");
        var pwBody = await pwResp.Content.ReadAsStringAsync();
        XDocument.Parse(pwBody).Root!.Name.LocalName.Should().Be("PasswordResponse");
        pwBody.Should().Contain("<Password>secret</Password>");
    }

    [Fact]
    public async Task AuditLogs_Should_Return_XML_When_Accept_Is_XML()
    {
        // Covers the IEnumerable -> List fix on PaginatedAuditResult.Items.
        var user = await RegisterAsync($"audit-xml-{Guid.NewGuid()}@test.local");

        // Creating a vault writes an audit log entry for this user.
        var vReq = new HttpRequestMessage(HttpMethod.Post, "/api/vaults")
        {
            Content = JsonContent.Create(new { name = "audit-vault", userId = user.User.Id })
        };
        vReq.Headers.Add("X-UserId", user.User.Id.ToString());
        (await _client.SendAsync(vReq)).EnsureSuccessStatusCode();

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/audit/logs");
        req.Headers.Add("X-UserId", user.User.Id.ToString());
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        var resp = await _client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("application/xml");
        XDocument.Parse(await resp.Content.ReadAsStringAsync())
            .Root!.Name.LocalName.Should().Be("PaginatedAuditResult");
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
