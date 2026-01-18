using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PassManAPI.Data;
using PassManAPI.DTOs;
using PassManAPI.Models;
using PassManAPI.Managers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PassManAPI.Tests
{
    public class InvitationTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;
        private readonly HttpClient _client;
        private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public InvitationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private void Authenticate(int userId)
        {
            _client.DefaultRequestHeaders.Remove("X-UserId");
            _client.DefaultRequestHeaders.Add("X-UserId", userId.ToString());
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
            return (await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions))!;
        }

        [Fact]
        public async Task InvitationLifecycle_Create_Accept_Verify()
        {
            // Arrange users
            var ownerEmail = $"owner_{Guid.NewGuid()}@test.com";
            var inviteeEmail = $"invitee_{Guid.NewGuid()}@test.com";

            var ownerAuth = await RegisterAsync(ownerEmail);
            var inviteeAuth = await RegisterAsync(inviteeEmail);

            // Login as Owner
            // Login as Owner
            Authenticate(ownerAuth.User.Id);

            // Create Vault
            var vaultPayload = new { Name = "Test Vault", Description = "Description", UserId = ownerAuth.User.Id };
            var createVaultResponse = await _client.PostAsJsonAsync("/api/vaults", vaultPayload);
            createVaultResponse.EnsureSuccessStatusCode();
            var vault = await createVaultResponse.Content.ReadFromJsonAsync<VaultDto>(_jsonOptions);

            // Act: Send Invitation
            var inviteRequest = new CreateInvitationRequest 
            { 
                VaultId = vault!.Id, 
                Email = inviteeEmail, 
                Role = "Edit" 
            };
            var inviteResponseMsg = await _client.PostAsJsonAsync("/api/invitations", inviteRequest);
            inviteResponseMsg.EnsureSuccessStatusCode();
            var inviteResponse = await inviteResponseMsg.Content.ReadFromJsonAsync<InvitationResponse>(_jsonOptions);

            // Assert: Invitation Created
            Assert.NotNull(inviteResponse);
            Assert.Equal(inviteeEmail, inviteResponse.InvitedEmail);
            Assert.NotEmpty(inviteResponse.InviteToken);

            // Login as Invitee
            // Login as Invitee
            Authenticate(inviteeAuth.User.Id);

            // Act: List Invitations
            var listResponse = await _client.GetFromJsonAsync<List<InvitationResponse>>("/api/invitations", _jsonOptions);
            Assert.Contains(listResponse!, i => i.Id == inviteResponse.Id);

            // Act: Accept Invitation
            var acceptResponse = await _client.PostAsync($"/api/invitations/{inviteResponse.InviteToken}/accept", null);
            acceptResponse.EnsureSuccessStatusCode();

            // Assert: Verify Vault Access
            var sharedVaults = await _client.GetFromJsonAsync<List<VaultDto>>("/api/vaults", _jsonOptions); 
            Assert.Contains(sharedVaults!, v => v.Id == vault.Id);
        }

        [Fact]
        public async Task RevokeInvitation_ShouldDeleteInvite()
        {
            // Arrange
            var ownerEmail = $"owner_revoke_{Guid.NewGuid()}@test.com";
            var inviteeEmail = $"invitee_revoke_{Guid.NewGuid()}@test.com";
            
            var ownerAuth = await RegisterAsync(ownerEmail);
            // Invitee needs to be registered so email is valid? API doesn't enforce user existence for inviting by email? 
            // Controller: "var user = await _userManager.FindByEmailAsync(request.Email);" NO, it doesn't check user existence for invite creation.
            // But validation logic might check.
            // The controller code I wrote: I only check if already shared. I didn't check if user exists.
            // So invitee doesn't need to be registered to receive invitation, BUT to revoke, I need to be owner.
            // Wait, Revoke checks `if (invitation.Vault.UserId != userId)`.
            // So owner needs log in.
            
            Authenticate(ownerAuth.User.Id);

            var vaultPayload = new { Name = "Revoke Vault", Description = "Desc", UserId = ownerAuth.User.Id };
            var createVaultResponse = await _client.PostAsJsonAsync("/api/vaults", vaultPayload);
            createVaultResponse.EnsureSuccessStatusCode();
            var vault = await createVaultResponse.Content.ReadFromJsonAsync<VaultDto>(_jsonOptions);

            // Act: Send Invitation
            var inviteRequest = new CreateInvitationRequest 
            { 
                VaultId = vault!.Id, 
                Email = inviteeEmail, 
                Role = "Edit" 
            };
            var inviteResponseMsg = await _client.PostAsJsonAsync("/api/invitations", inviteRequest);
            inviteResponseMsg.EnsureSuccessStatusCode();
            var inviteResponse = await inviteResponseMsg.Content.ReadFromJsonAsync<InvitationResponse>(_jsonOptions);

            // Act: Revoke
            var deleteResponse = await _client.DeleteAsync($"/api/invitations/{inviteResponse!.Id}");
            deleteResponse.EnsureSuccessStatusCode();

            // Assert: Check DB (direct check) or List Invitations (as invitee - if registered)
            // Use DB check
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var invite = await db.Invitations.FindAsync(inviteResponse.Id);
                Assert.Null(invite);
            }
        }
    }
}
