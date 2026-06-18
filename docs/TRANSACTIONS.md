# Transactions in PassMan

## Overview

PassMan uses **explicit EF Core transactions** for any operation that involves more than one database write that must succeed or fail together. The isolation level for all MySQL sessions is set to **READ COMMITTED** (configured in `DatabaseArtifacts.EnsureAsync`).

---

## Full Transaction Example — Accepting a Vault Invitation

The cleanest example of an explicit transaction in the codebase is `SharingManager.AcceptInvitationAsync` — it wraps vault-share creation in an explicit transaction so that the in-memory invitation is only consumed **after** the DB commit succeeds.

> **Note:** `SharingManager` is registered in the DI container but is not currently wired to a controller endpoint. The HTTP invitation-acceptance endpoint (`POST /api/invitations/{token}/accept`) is in `InvitationsController`, which uses EF's implicit single-`SaveChangesAsync` transaction. The `SharingManager` pattern is documented here as the intended transactional design.

The operation:

1. Reads the invitation from in-memory store
2. **Writes** a new `VaultShare` row (or updates an existing one) — this is the critical step
3. Commits — only then removes the invitation from memory

If step 2 fails without a transaction, the `VaultShare` row might be partially written and the invitation might already be consumed, leaving the database in an inconsistent state.

### Code (`PassManAPI/Managers/SharingManager.cs`)

```csharp
// Begin an explicit transaction before any database write.
await using var transaction = await _db.Database.BeginTransactionAsync();
try
{
    // Step 1 — create or update the VaultShare record
    var existingShare = await _db.VaultShares
        .FirstOrDefaultAsync(vs => vs.VaultId == invitation.VaultId && vs.UserId == userId);

    if (existingShare != null)
    {
        existingShare.Permission   = invitation.Permission;
        existingShare.SharedByUserId = invitation.CreatedByUserId;
    }
    else
    {
        _db.VaultShares.Add(new VaultShare
        {
            VaultId          = invitation.VaultId,
            UserId           = userId,
            Permission       = invitation.Permission,
            SharedAt         = DateTime.UtcNow,
            SharedByUserId   = invitation.CreatedByUserId
        });
    }

    // Step 2 — flush all EF changes to the database (still inside the transaction)
    await _db.SaveChangesAsync();

    // Step 3 — if everything is OK, commit
    await transaction.CommitAsync();
}
catch (Exception ex)
{
    // Something failed — roll back so the database stays clean
    await transaction.RollbackAsync();
    _logger.LogError(ex, "Transaction rolled back for VaultId={VaultId}", invitation.VaultId);
    return SharingResult<VaultShareInfo>.Fail("Failed to accept invitation due to a database error.");
}

// Step 4 — only consume the in-memory invitation AFTER the commit succeeds
_invitations.TryRemove(token, out _);
```

### Why the order matters

| Step | Without transaction risk | With transaction |
|------|--------------------------|------------------|
| `SaveChangesAsync` fails | Partial write leaks into DB; invitation is still in memory (invitation reusable but share corrupted) | Full rollback — DB untouched, invitation still valid |
| `CommitAsync` fails | n/a | Rollback — same safety |
| `_invitations.Remove` before commit | If commit fails after removal, invitation is gone but share was never written | Removal happens only after successful commit ✅ |

---

## Session-Level Isolation

Every MySQL connection used by the API runs at **READ COMMITTED**. This is enforced by `ReadCommittedInterceptor` (`PassManAPI/Helpers/ReadCommittedInterceptor.cs`), a `DbConnectionInterceptor` registered on the MySQL `DbContext` in `Program.cs`. It executes:

```sql
SET SESSION TRANSACTION ISOLATION LEVEL READ COMMITTED;
```

on every connection as it opens — including pooled connections — so the setting is never lost.

**Why READ COMMITTED?**
- Prevents dirty reads (seeing another transaction's uncommitted writes), which would be a serious correctness issue for a password manager.
- Avoids the extra locking overhead of REPEATABLE READ, which would be overkill here: the app does not run long-running reads that need a stable snapshot across multiple queries in the same request.

---

## Other Transactional Writes

- **`DatabaseArtifacts.EnsureAsync`** — wraps the creation of all DB artifacts (view, stored procedures, trigger) in a single transaction so startup is atomic. See `Data/DatabaseArtifacts.cs`.
- Individual CRUD endpoints use EF's implicit transaction (one `SaveChangesAsync` = one implicit transaction). Explicit transactions are only needed when two or more separate `SaveChangesAsync` calls must be atomic together.
