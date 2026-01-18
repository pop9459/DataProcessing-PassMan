using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PassManAPI.Data;
using PassManAPI.Models;
using Xunit;

namespace PassManAPI.Tests;

public class AttachmentModelTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AttachmentModelTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Attachment_Constructor_Sets_Properties_Correctly()
    {
        // Arrange
        var credentialId = 1;
        var filePath = "/path/to/file.pdf";
        var encryptedKey = new byte[] { 1, 2, 3, 4, 5 };
        var originalFileName = "document.pdf";

        // Act
        var attachment = new Attachment(credentialId, filePath, encryptedKey, originalFileName);

        // Assert
        attachment.CredentialId.Should().Be(credentialId);
        attachment.FilePath.Should().Be(filePath);
        attachment.EncryptedSymmetricKey.Should().BeEquivalentTo(encryptedKey);
        attachment.OriginalFileName.Should().Be(originalFileName);
        attachment.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Attachment_Default_Constructor_Creates_Empty_Instance()
    {
        // Act
        var attachment = new Attachment();

        // Assert
        attachment.Id.Should().Be(0);
        attachment.CredentialId.Should().Be(0);
        attachment.FilePath.Should().BeEmpty();
        attachment.EncryptedSymmetricKey.Should().BeEmpty();
        attachment.OriginalFileName.Should().BeEmpty();
    }

    [Fact]
    public void Rename_Updates_OriginalFileName_When_Valid()
    {
        // Arrange
        var attachment = new Attachment(1, "/path/file.pdf", new byte[] { 1, 2, 3 }, "old.pdf");

        // Act
        attachment.Rename("new.pdf");

        // Assert
        attachment.OriginalFileName.Should().Be("new.pdf");
    }

    [Fact]
    public void Rename_Throws_When_FileName_Is_Null()
    {
        // Arrange
        var attachment = new Attachment(1, "/path/file.pdf", new byte[] { 1, 2, 3 }, "old.pdf");

        // Act & Assert
        var act = () => attachment.Rename(null!);
        act.Should().Throw<ArgumentException>()
            .WithMessage("File name cannot be null or whitespace.*");
    }

    [Fact]
    public void Rename_Throws_When_FileName_Is_Whitespace()
    {
        // Arrange
        var attachment = new Attachment(1, "/path/file.pdf", new byte[] { 1, 2, 3 }, "old.pdf");

        // Act & Assert
        var act = () => attachment.Rename("   ");
        act.Should().Throw<ArgumentException>()
            .WithMessage("File name cannot be null or whitespace.*");
    }

    [Fact]
    public void Rename_Throws_When_FileName_Exceeds_MaxLength()
    {
        // Arrange
        var attachment = new Attachment(1, "/path/file.pdf", new byte[] { 1, 2, 3 }, "old.pdf");
        var longName = new string('a', 256); // 256 characters

        // Act & Assert
        var act = () => attachment.Rename(longName);
        act.Should().Throw<ArgumentException>()
            .WithMessage("File name cannot exceed 255 characters.*");
    }

    [Fact]
    public void UpdateFilePath_Updates_FilePath_When_Valid()
    {
        // Arrange
        var attachment = new Attachment(1, "/old/path.pdf", new byte[] { 1, 2, 3 }, "file.pdf");

        // Act
        attachment.UpdateFilePath("/new/path.pdf");

        // Assert
        attachment.FilePath.Should().Be("/new/path.pdf");
    }

    [Fact]
    public void UpdateFilePath_Throws_When_Path_Is_Null()
    {
        // Arrange
        var attachment = new Attachment(1, "/path/file.pdf", new byte[] { 1, 2, 3 }, "file.pdf");

        // Act & Assert
        var act = () => attachment.UpdateFilePath(null!);
        act.Should().Throw<ArgumentException>()
            .WithMessage("File path cannot be null or whitespace.*");
    }

    [Fact]
    public void UpdateFilePath_Throws_When_Path_Exceeds_MaxLength()
    {
        // Arrange
        var attachment = new Attachment(1, "/path/file.pdf", new byte[] { 1, 2, 3 }, "file.pdf");
        var longPath = "/" + new string('a', 500); // 501 characters

        // Act & Assert
        var act = () => attachment.UpdateFilePath(longPath);
        act.Should().Throw<ArgumentException>()
            .WithMessage("File path cannot exceed 500 characters.*");
    }

    [Fact]
    public void UpdateEncryptionKey_Updates_Key_When_Valid()
    {
        // Arrange
        var attachment = new Attachment(1, "/path/file.pdf", new byte[] { 1, 2, 3 }, "file.pdf");
        var newKey = new byte[] { 5, 6, 7, 8 };

        // Act
        attachment.UpdateEncryptionKey(newKey);

        // Assert
        attachment.EncryptedSymmetricKey.Should().BeEquivalentTo(newKey);
    }

    [Fact]
    public void UpdateEncryptionKey_Throws_When_Key_Is_Null()
    {
        // Arrange
        var attachment = new Attachment(1, "/path/file.pdf", new byte[] { 1, 2, 3 }, "file.pdf");

        // Act & Assert
        var act = () => attachment.UpdateEncryptionKey(null!);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Encryption key cannot be null or empty.*");
    }

    [Fact]
    public void UpdateEncryptionKey_Throws_When_Key_Is_Empty()
    {
        // Arrange
        var attachment = new Attachment(1, "/path/file.pdf", new byte[] { 1, 2, 3 }, "file.pdf");

        // Act & Assert
        var act = () => attachment.UpdateEncryptionKey(Array.Empty<byte>());
        act.Should().Throw<ArgumentException>()
            .WithMessage("Encryption key cannot be null or empty.*");
    }

    [Fact]
    public async Task Attachment_Can_Be_Saved_To_Database()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Create a user and vault first
        var user = new User
        {
            UserName = "testuser",
            Email = "test@test.com",
            EmailConfirmed = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var vault = new Vault
        {
            Name = "Test Vault",
            UserId = user.Id
        };
        db.Vaults.Add(vault);
        await db.SaveChangesAsync();

        var credential = new Credential
        {
            Title = "Test Credential",
            EncryptedPassword = "encrypted",
            VaultId = vault.Id
        };
        db.Credentials.Add(credential);
        await db.SaveChangesAsync();

        // Act
        var attachment = new Attachment(
            credential.Id,
            "/storage/attachments/file.pdf",
            new byte[] { 1, 2, 3, 4, 5 },
            "document.pdf"
        );
        db.Attachments.Add(attachment);
        await db.SaveChangesAsync();

        // Assert
        attachment.Id.Should().BeGreaterThan(0);
        var savedAttachment = await db.Attachments
            .Include(a => a.Credential)
            .FirstOrDefaultAsync(a => a.Id == attachment.Id);
        savedAttachment.Should().NotBeNull();
        savedAttachment!.CredentialId.Should().Be(credential.Id);
        savedAttachment.Credential.Should().NotBeNull();
        savedAttachment.Credential.Title.Should().Be("Test Credential");
    }

    [Fact]
    public async Task Attachment_Navigation_Property_Works()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new User
        {
            UserName = "testuser2",
            Email = "test2@test.com",
            EmailConfirmed = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var vault = new Vault
        {
            Name = "Test Vault 2",
            UserId = user.Id
        };
        db.Vaults.Add(vault);
        await db.SaveChangesAsync();

        var credential = new Credential
        {
            Title = "Test Credential 2",
            EncryptedPassword = "encrypted",
            VaultId = vault.Id
        };
        db.Credentials.Add(credential);
        await db.SaveChangesAsync();

        var attachment1 = new Attachment(credential.Id, "/file1.pdf", new byte[] { 1 }, "file1.pdf");
        var attachment2 = new Attachment(credential.Id, "/file2.pdf", new byte[] { 2 }, "file2.pdf");
        db.Attachments.AddRange(attachment1, attachment2);
        await db.SaveChangesAsync();

        // Act
        var credentialWithAttachments = await db.Credentials
            .Include(c => c.Attachments)
            .FirstOrDefaultAsync(c => c.Id == credential.Id);

        // Assert
        credentialWithAttachments.Should().NotBeNull();
        credentialWithAttachments!.Attachments.Should().HaveCount(2);
        credentialWithAttachments.Attachments.Should().Contain(a => a.OriginalFileName == "file1.pdf");
        credentialWithAttachments.Attachments.Should().Contain(a => a.OriginalFileName == "file2.pdf");
    }

    [Fact]
    public async Task Attachment_Cascade_Delete_Works()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new User
        {
            UserName = "testuser3",
            Email = "test3@test.com",
            EmailConfirmed = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var vault = new Vault
        {
            Name = "Test Vault 3",
            UserId = user.Id
        };
        db.Vaults.Add(vault);
        await db.SaveChangesAsync();

        var credential = new Credential
        {
            Title = "Test Credential 3",
            EncryptedPassword = "encrypted",
            VaultId = vault.Id
        };
        db.Credentials.Add(credential);
        await db.SaveChangesAsync();

        var attachment = new Attachment(credential.Id, "/file.pdf", new byte[] { 1 }, "file.pdf");
        db.Attachments.Add(attachment);
        await db.SaveChangesAsync();

        var attachmentId = attachment.Id;

        // Act - Delete the credential
        db.Credentials.Remove(credential);
        await db.SaveChangesAsync();

        // Assert - Attachment should be deleted too
        var deletedAttachment = await db.Attachments.FindAsync(attachmentId);
        deletedAttachment.Should().BeNull();
    }
}
