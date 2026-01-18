using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PassManAPI.Data;
using PassManAPI.Models;
using Xunit;

namespace PassManAPI.Tests;

public class InvitationModelTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public InvitationModelTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Invitation_Constructor_Sets_Properties_Correctly()
    {
        // Arrange
        int vaultId = 1;
        string invitedEmail = "test@example.com";
        AccessRole role = AccessRole.Edit;
        DateTime expiresAt = DateTime.UtcNow.AddDays(7);
        string inviteToken = Guid.NewGuid().ToString();

        // Act
        var invitation = new Invitation(vaultId, invitedEmail, role, expiresAt, inviteToken);

        // Assert
        invitation.VaultId.Should().Be(vaultId);
        invitation.InvitedEmail.Should().Be(invitedEmail);
        invitation.Role.Should().Be(role);
        invitation.ExpiresAt.Should().Be(expiresAt);
        invitation.InviteToken.Should().Be(inviteToken);
        invitation.AcceptedAt.Should().BeNull();
    }

    [Fact]
    public void Invitation_DefaultConstructor_Works()
    {
        // Act
        var invitation = new Invitation();

        // Assert
        invitation.Should().NotBeNull();
        invitation.Id.Should().Be(0);
        invitation.InvitedEmail.Should().Be(string.Empty);
        invitation.InviteToken.Should().Be(string.Empty);
        invitation.AcceptedAt.Should().BeNull();
    }

    [Fact]
    public void Invitation_Constructor_Throws_When_Email_Is_Null()
    {
        // Act & Assert
        Action act = () => new Invitation(1, null!, AccessRole.View, DateTime.UtcNow.AddDays(7), "token");
        act.Should().Throw<ArgumentException>().WithMessage("Invited email cannot be null or whitespace. (Parameter 'invitedEmail')");
    }

    [Fact]
    public void Invitation_Constructor_Throws_When_Email_Is_Invalid()
    {
        // Act & Assert
        Action act = () => new Invitation(1, "invalid-email", AccessRole.View, DateTime.UtcNow.AddDays(7), "token");
        act.Should().Throw<ArgumentException>().WithMessage("Invalid email address format. (Parameter 'invitedEmail')");
    }

    [Fact]
    public void Invitation_Constructor_Throws_When_Token_Is_Null()
    {
        // Act & Assert
        Action act = () => new Invitation(1, "test@example.com", AccessRole.View, DateTime.UtcNow.AddDays(7), null!);
        act.Should().Throw<ArgumentException>().WithMessage("Invite token cannot be null or whitespace. (Parameter 'inviteToken')");
    }

    [Fact]
    public void Invitation_Constructor_Throws_When_Token_Is_Too_Long()
    {
        // Arrange
        string longToken = new string('a', 101); // Exceeds MaxLength(100)

        // Act & Assert
        Action act = () => new Invitation(1, "test@example.com", AccessRole.View, DateTime.UtcNow.AddDays(7), longToken);
        act.Should().Throw<ArgumentException>().WithMessage("Invite token cannot exceed 100 characters. (Parameter 'inviteToken')");
    }

    [Fact]
    public void Invitation_Constructor_Throws_When_ExpiresAt_Is_In_Past()
    {
        // Act & Assert
        Action act = () => new Invitation(1, "test@example.com", AccessRole.View, DateTime.UtcNow.AddDays(-1), "token");
        act.Should().Throw<ArgumentException>().WithMessage("Expiration date must be in the future. (Parameter 'expiresAt')");
    }

    [Fact]
    public void IsExpired_Returns_False_When_Not_Expired()
    {
        // Arrange
        var invitation = new Invitation(1, "test@example.com", AccessRole.View, DateTime.UtcNow.AddDays(7), "token");

        // Act
        bool result = invitation.IsExpired();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_Returns_True_When_Expired()
    {
        // Arrange - Create with valid date, then manually set to expired
        var invitation = new Invitation(1, "test@example.com", AccessRole.View, DateTime.UtcNow.AddDays(7), "token");
        // Use reflection or direct property access to set expired date (since constructor validates)
        var expiresAtProperty = typeof(Invitation).GetProperty(nameof(Invitation.ExpiresAt));
        expiresAtProperty!.SetValue(invitation, DateTime.UtcNow.AddDays(-1));

        // Act
        bool result = invitation.IsExpired();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MarkAccepted_Sets_AcceptedAt()
    {
        // Arrange
        var invitation = new Invitation(1, "test@example.com", AccessRole.View, DateTime.UtcNow.AddDays(7), "token");

        // Act
        invitation.MarkAccepted();

        // Assert
        invitation.AcceptedAt.Should().NotBeNull();
        invitation.AcceptedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MarkAccepted_Throws_When_Already_Accepted()
    {
        // Arrange
        var invitation = new Invitation(1, "test@example.com", AccessRole.View, DateTime.UtcNow.AddDays(7), "token");
        invitation.MarkAccepted();

        // Act & Assert
        Action act = () => invitation.MarkAccepted();
        act.Should().Throw<InvalidOperationException>().WithMessage("Invitation has already been accepted.");
    }

    [Fact]
    public void MarkAccepted_Throws_When_Expired()
    {
        // Arrange - Create with valid date, then manually set to expired
        var invitation = new Invitation(1, "test@example.com", AccessRole.View, DateTime.UtcNow.AddDays(7), "token");
        var expiresAtProperty = typeof(Invitation).GetProperty(nameof(Invitation.ExpiresAt));
        expiresAtProperty!.SetValue(invitation, DateTime.UtcNow.AddDays(-1));

        // Act & Assert
        Action act = () => invitation.MarkAccepted();
        act.Should().Throw<InvalidOperationException>().WithMessage("Cannot accept an expired invitation.");
    }

    [Fact]
    public void ChangeRole_Updates_Role()
    {
        // Arrange
        var invitation = new Invitation(1, "test@example.com", AccessRole.View, DateTime.UtcNow.AddDays(7), "token");

        // Act
        invitation.ChangeRole(AccessRole.Admin);

        // Assert
        invitation.Role.Should().Be(AccessRole.Admin);
    }

    [Fact]
    public void ChangeRole_Throws_When_Already_Accepted()
    {
        // Arrange
        var invitation = new Invitation(1, "test@example.com", AccessRole.View, DateTime.UtcNow.AddDays(7), "token");
        invitation.MarkAccepted();

        // Act & Assert
        Action act = () => invitation.ChangeRole(AccessRole.Edit);
        act.Should().Throw<InvalidOperationException>().WithMessage("Cannot change role of an accepted invitation.");
    }

    [Fact]
    public void ChangeRole_Throws_When_Expired()
    {
        // Arrange - Create with valid date, then manually set to expired
        var invitation = new Invitation(1, "test@example.com", AccessRole.View, DateTime.UtcNow.AddDays(7), "token");
        var expiresAtProperty = typeof(Invitation).GetProperty(nameof(Invitation.ExpiresAt));
        expiresAtProperty!.SetValue(invitation, DateTime.UtcNow.AddDays(-1));

        // Act & Assert
        Action act = () => invitation.ChangeRole(AccessRole.Edit);
        act.Should().Throw<InvalidOperationException>().WithMessage("Cannot change role of an expired invitation.");
    }

    [Fact]
    public void Revoke_Sets_ExpiresAt_To_Past()
    {
        // Arrange
        var invitation = new Invitation(1, "test@example.com", AccessRole.View, DateTime.UtcNow.AddDays(7), "token");
        var originalExpiresAt = invitation.ExpiresAt;

        // Act
        invitation.Revoke();

        // Assert
        invitation.ExpiresAt.Should().BeBefore(DateTime.UtcNow);
        invitation.IsExpired().Should().BeTrue();
    }

    [Fact]
    public void Revoke_Throws_When_Already_Accepted()
    {
        // Arrange
        var invitation = new Invitation(1, "test@example.com", AccessRole.View, DateTime.UtcNow.AddDays(7), "token");
        invitation.MarkAccepted();

        // Act & Assert
        Action act = () => invitation.Revoke();
        act.Should().Throw<InvalidOperationException>().WithMessage("Cannot revoke an accepted invitation.");
    }

    [Fact]
    public async Task Invitation_Can_Be_Saved_To_Database()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Create a dummy user and vault
        var user = new User { UserName = "testuser", Email = "test@test.com", EmailConfirmed = true };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var vault = new Vault { Name = "Test Vault", UserId = user.Id };
        dbContext.Vaults.Add(vault);
        await dbContext.SaveChangesAsync();

        // Create an invitation
        var invitation = new Invitation(
            vault.Id,
            "invited@example.com",
            AccessRole.Edit,
            DateTime.UtcNow.AddDays(7),
            Guid.NewGuid().ToString()
        );
        dbContext.Invitations.Add(invitation);
        await dbContext.SaveChangesAsync();

        // Retrieve and verify
        var retrievedInvitation = await dbContext.Invitations
            .Include(i => i.Vault)
            .FirstOrDefaultAsync(i => i.Id == invitation.Id);

        retrievedInvitation.Should().NotBeNull();
        retrievedInvitation!.InvitedEmail.Should().Be("invited@example.com");
        retrievedInvitation.Role.Should().Be(AccessRole.Edit);
        retrievedInvitation.Vault.Should().NotBeNull();
        retrievedInvitation.Vault.Id.Should().Be(vault.Id);
    }

    [Fact]
    public async Task Invitation_Cascade_Delete_Works()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Create a dummy user and vault
        var user = new User { UserName = "testuser2", Email = "test2@test.com", EmailConfirmed = true };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var vault = new Vault { Name = "Test Vault 2", UserId = user.Id };
        dbContext.Vaults.Add(vault);
        await dbContext.SaveChangesAsync();

        // Create an invitation
        var invitation = new Invitation(
            vault.Id,
            "invited@example.com",
            AccessRole.View,
            DateTime.UtcNow.AddDays(7),
            Guid.NewGuid().ToString()
        );
        dbContext.Invitations.Add(invitation);
        await dbContext.SaveChangesAsync();

        // Delete the vault
        dbContext.Vaults.Remove(vault);
        await dbContext.SaveChangesAsync();

        // Verify invitation is also deleted
        var deletedInvitation = await dbContext.Invitations.FirstOrDefaultAsync(i => i.Id == invitation.Id);
        deletedInvitation.Should().BeNull();
    }

    [Fact]
    public async Task Invitation_Navigation_Property_Works()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Create a dummy user and vault
        var user = new User { UserName = "testuser3", Email = "test3@test.com", EmailConfirmed = true };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var vault = new Vault { Name = "Test Vault 3", UserId = user.Id };
        dbContext.Vaults.Add(vault);
        await dbContext.SaveChangesAsync();

        // Add multiple invitations to the vault
        var invitation1 = new Invitation(vault.Id, "invited1@example.com", AccessRole.View, DateTime.UtcNow.AddDays(7), Guid.NewGuid().ToString());
        var invitation2 = new Invitation(vault.Id, "invited2@example.com", AccessRole.Edit, DateTime.UtcNow.AddDays(7), Guid.NewGuid().ToString());
        dbContext.Invitations.AddRange(invitation1, invitation2);
        await dbContext.SaveChangesAsync();

        // Retrieve the vault and check its invitations
        var retrievedVault = await dbContext.Vaults
            .Include(v => v.Invitations)
            .FirstOrDefaultAsync(v => v.Id == vault.Id);

        retrievedVault.Should().NotBeNull();
        retrievedVault!.Invitations.Should().HaveCount(2);
        retrievedVault.Invitations.Should().Contain(i => i.InvitedEmail == "invited1@example.com");
        retrievedVault.Invitations.Should().Contain(i => i.InvitedEmail == "invited2@example.com");
    }

    [Fact]
    public async Task Invitation_Token_Is_Unique()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Create a dummy user and vault
        var user = new User { UserName = "testuser4", Email = "test4@test.com", EmailConfirmed = true };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var vault = new Vault { Name = "Test Vault 4", UserId = user.Id };
        dbContext.Vaults.Add(vault);
        await dbContext.SaveChangesAsync();

        // Create first invitation
        string token = Guid.NewGuid().ToString();
        var invitation1 = new Invitation(vault.Id, "invited1@example.com", AccessRole.View, DateTime.UtcNow.AddDays(7), token);
        dbContext.Invitations.Add(invitation1);
        await dbContext.SaveChangesAsync();

        // Try to create second invitation with same token
        var invitation2 = new Invitation(vault.Id, "invited2@example.com", AccessRole.View, DateTime.UtcNow.AddDays(7), token);
        dbContext.Invitations.Add(invitation2);

        // Act & Assert
        Func<Task> act = async () => await dbContext.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
