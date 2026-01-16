using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace PassManAPI.Data;

/// <summary>
/// Ensures database-level artifacts (views, procedures, triggers) exist and sets session isolation.
/// Only runs for MySQL provider; skipped for SQLite/Test.
/// </summary>
public static class DatabaseArtifacts
{
    public static async Task EnsureAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        if (!db.Database.IsMySql())
        {
            return;
        }

        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            // Set isolation for this session (applies to commands on this connection).
            await ExecuteAsync(conn, tx, "SET SESSION TRANSACTION ISOLATION LEVEL READ COMMITTED;");

            // View: user access to vaults (owner or shared)
            const string createView = @"
CREATE OR REPLACE VIEW vwUserVaultAccess AS
SELECT v.Id AS VaultId,
       v.Name AS VaultName,
       v.UserId AS OwnerId,
       v.UserId AS AccessUserId,
       'Owner' AS AccessType
  FROM Vaults v
UNION
SELECT vs.VaultId,
       v.Name AS VaultName,
       v.UserId AS OwnerId,
       vs.UserId AS AccessUserId,
       'Shared' AS AccessType
  FROM VaultShares vs
  JOIN Vaults v ON v.Id = vs.VaultId;";
            await ExecuteAsync(conn, tx, createView);

            // Stored procedure: add vault share by email with validation and idempotency
            const string dropSpShare = "DROP PROCEDURE IF EXISTS sp_AddVaultShare;";
            const string createSpShare = @"
CREATE PROCEDURE sp_AddVaultShare(IN pVaultId INT, IN pUserEmail VARCHAR(256))
BEGIN
    DECLARE vUserId INT;
    DECLARE vOwnerId INT;
    SELECT UserId INTO vOwnerId FROM Vaults WHERE Id = pVaultId;
    IF vOwnerId IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Vault not found';
    END IF;
    SELECT Id INTO vUserId FROM Users WHERE Email = pUserEmail;
    IF vUserId IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'User not found';
    END IF;
    IF vOwnerId = vUserId THEN
        LEAVE proc_end;
    END IF;
    INSERT IGNORE INTO VaultShares (VaultId, UserId) VALUES (pVaultId, vUserId);
    proc_end: BEGIN END;
END;";
            await ExecuteAsync(conn, tx, dropSpShare);
            await ExecuteAsync(conn, tx, createSpShare);

            // Stored procedure: log audit entries
            const string dropSpAudit = "DROP PROCEDURE IF EXISTS sp_LogAudit;";
            const string createSpAudit = @"
CREATE PROCEDURE sp_LogAudit(
    IN pUserId INT,
    IN pAction INT,
    IN pEntityType VARCHAR(50),
    IN pEntityId INT,
    IN pDetails TEXT,
    IN pIp VARCHAR(45),
    IN pUserAgent VARCHAR(500))
BEGIN
    INSERT INTO AuditLogs (Action, EntityType, EntityId, Details, UserId, IpAddress, UserAgent, Timestamp)
    VALUES (pAction, pEntityType, pEntityId, pDetails, pUserId, pIp, pUserAgent, CURRENT_TIMESTAMP);
END;";
            await ExecuteAsync(conn, tx, dropSpAudit);
            await ExecuteAsync(conn, tx, createSpAudit);

            // Trigger: maintain UpdatedAt for credentials
            const string dropTrigger = "DROP TRIGGER IF EXISTS trg_Credentials_SetUpdatedAt;";
            const string createTrigger = @"
CREATE TRIGGER trg_Credentials_SetUpdatedAt
BEFORE UPDATE ON Credentials
FOR EACH ROW
BEGIN
    SET NEW.UpdatedAt = CURRENT_TIMESTAMP;
END;";
            await ExecuteAsync(conn, tx, dropTrigger);
            await ExecuteAsync(conn, tx, createTrigger);

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
        finally
        {
            await conn.CloseAsync();
        }
    }

    private static async Task ExecuteAsync(DbConnection conn, DbTransaction tx, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }
}

