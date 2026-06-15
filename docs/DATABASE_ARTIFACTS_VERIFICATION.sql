-- ============================================================================
-- DATABASE ARTIFACTS VERIFICATION QUERIES
-- Investigation of Stored Procedures, Views, Triggers, and Transactions
-- ============================================================================
-- This file contains ready-to-run SQL queries to verify all database artifacts
-- Copy and paste any query directly into your MySQL database client
-- ============================================================================

-- ============================================================================
-- SECTION 1: VERIFY DATABASE ARTIFACTS EXIST
-- ============================================================================

-- QUERY 1: Check if VIEW exists (vwUserVaultAccess)
-- Expected: 1 row with VIEW type
-- ============================================================================
SELECT 
    'VERIFICATION 1: View Existence' as Query,
    TABLE_SCHEMA,
    TABLE_NAME,
    TABLE_TYPE,
    'FOUND ✓' as Status
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'passManDB' 
  AND TABLE_NAME = 'vwUserVaultAccess';

-- If no results, view doesn't exist. Check if application started with MySQL.


-- QUERY 2: Check if STORED PROCEDURES exist
-- Expected: 2 rows (sp_AddVaultShare, sp_LogAudit)
-- ============================================================================
SELECT 
    'VERIFICATION 2: Stored Procedures' as Query,
    ROUTINE_NAME as ProcedureName,
    ROUTINE_TYPE,
    'FOUND ✓' as Status
FROM INFORMATION_SCHEMA.ROUTINES
WHERE ROUTINE_SCHEMA = 'passManDB'
  AND ROUTINE_NAME LIKE 'sp_%'
ORDER BY ROUTINE_NAME;

-- Expected procedures:
-- - sp_AddVaultShare
-- - sp_LogAudit


-- QUERY 3: Check if TRIGGERS exist
-- Expected: 1 row (trg_Credentials_SetUpdatedAt)
-- ============================================================================
SELECT 
    'VERIFICATION 3: Triggers' as Query,
    TRIGGER_NAME,
    EVENT_OBJECT_TABLE as TableName,
    EVENT_MANIPULATION as TriggerEvent,
    ACTION_TIMING as Timing,
    'FOUND ✓' as Status
FROM INFORMATION_SCHEMA.TRIGGERS
WHERE TRIGGER_SCHEMA = 'passManDB'
ORDER BY TRIGGER_NAME;

-- Expected trigger:
-- - trg_Credentials_SetUpdatedAt (BEFORE UPDATE on Credentials)


-- ============================================================================
-- SECTION 2: VIEW FUNCTIONALITY TESTS
-- ============================================================================

-- QUERY 4: Test VIEW - Show user vault access
-- Expected: List of all vaults user has access to (owned or shared)
-- ============================================================================
SELECT 
    'TEST 4: View Query Results' as Query,
    VaultId,
    VaultName,
    OwnerId,
    AccessUserId,
    AccessType
FROM vwUserVaultAccess
LIMIT 10;


-- QUERY 5: Count vaults by access type using VIEW
-- Expected: Summary of owned vs shared vaults per user
-- ============================================================================
SELECT 
    'TEST 5: Access Type Summary' as Query,
    AccessUserId as UserId,
    AccessType,
    COUNT(*) as VaultCount
FROM vwUserVaultAccess
GROUP BY AccessUserId, AccessType
ORDER BY AccessUserId, AccessType;


-- QUERY 6: Find all vaults accessible by a specific user
-- Expected: All vaults (owned + shared) for that user
-- ============================================================================
SELECT 
    'TEST 6: User Vault Access (UserId=1)' as Query,
    VaultId,
    VaultName,
    OwnerId,
    AccessType,
    CASE 
        WHEN AccessType = 'Owner' THEN 'Full Control'
        WHEN AccessType = 'Shared' THEN 'Shared Access'
    END as Permission
FROM vwUserVaultAccess
WHERE AccessUserId = 1;


-- ============================================================================
-- SECTION 3: STORED PROCEDURE VERIFICATION
-- ============================================================================

-- QUERY 7: Show all stored procedures with their parameters
-- ============================================================================
SELECT 
    'VERIFICATION 7: Procedure Details' as Query,
    ROUTINE_NAME as ProcedureName,
    COUNT(PARAMETER_NAME) as ParameterCount,
    GROUP_CONCAT(
        CONCAT(PARAMETER_NAME, ' ', PARAMETER_TYPE) 
        ORDER BY ORDINAL_POSITION 
        SEPARATOR ', '
    ) as Parameters
FROM INFORMATION_SCHEMA.PARAMETERS
WHERE SPECIFIC_SCHEMA = 'passManDB'
  AND ROUTINE_TYPE = 'PROCEDURE'
GROUP BY ROUTINE_NAME
ORDER BY ROUTINE_NAME;


-- QUERY 8: Get full procedure code for sp_AddVaultShare
-- ============================================================================
SELECT 
    'VERIFICATION 8: sp_AddVaultShare Code' as Query,
    ROUTINE_NAME,
    ROUTINE_DEFINITION
FROM INFORMATION_SCHEMA.ROUTINES
WHERE ROUTINE_SCHEMA = 'passManDB'
  AND ROUTINE_NAME = 'sp_AddVaultShare';


-- QUERY 9: Get full procedure code for sp_LogAudit
-- ============================================================================
SELECT 
    'VERIFICATION 9: sp_LogAudit Code' as Query,
    ROUTINE_NAME,
    ROUTINE_DEFINITION
FROM INFORMATION_SCHEMA.ROUTINES
WHERE ROUTINE_SCHEMA = 'passManDB'
  AND ROUTINE_NAME = 'sp_LogAudit';


-- ============================================================================
-- SECTION 4: TRIGGER VERIFICATION
-- ============================================================================

-- QUERY 10: Show trigger details and code
-- ============================================================================
SELECT 
    'VERIFICATION 10: Trigger Details' as Query,
    TRIGGER_NAME,
    EVENT_OBJECT_TABLE as TableName,
    EVENT_MANIPULATION as Event,
    ACTION_TIMING as Timing,
    ACTION_STATEMENT as TriggerCode
FROM INFORMATION_SCHEMA.TRIGGERS
WHERE TRIGGER_SCHEMA = 'passManDB';


-- QUERY 11: Verify trigger is attached to Credentials table
-- ============================================================================
SELECT 
    'VERIFICATION 11: Credentials Triggers' as Query,
    TRIGGER_NAME,
    EVENT_MANIPULATION as TriggerEvent,
    ACTION_TIMING as Timing,
    'FOUND ✓' as Status
FROM INFORMATION_SCHEMA.TRIGGERS
WHERE TRIGGER_SCHEMA = 'passManDB'
  AND EVENT_OBJECT_TABLE = 'Credentials';


-- ============================================================================
-- SECTION 5: FUNCTIONALITY TESTS
-- ============================================================================

-- QUERY 12: Test VIEW by checking vault ownership chain
-- Shows: VaultId → VaultName → Owner → AccessUser → Type
-- ============================================================================
SELECT 
    'TEST 12: Vault Ownership Chain' as Query,
    va.VaultId,
    va.VaultName,
    COALESCE(o.UserName, 'UNKNOWN') as OwnerName,
    COALESCE(u.UserName, 'UNKNOWN') as AccessUserName,
    va.AccessType,
    CASE 
        WHEN va.AccessType = 'Owner' THEN va.OwnerId = va.AccessUserId
        WHEN va.AccessType = 'Shared' THEN va.OwnerId <> va.AccessUserId
    END as IsValid
FROM vwUserVaultAccess va
LEFT JOIN AspNetUsers o ON va.OwnerId = o.Id
LEFT JOIN AspNetUsers u ON va.AccessUserId = u.Id
LIMIT 20;


-- QUERY 13: Count audit logs (result of sp_LogAudit calls)
-- ============================================================================
SELECT 
    'TEST 13: Audit Logs Summary' as Query,
    'Total Audit Entries' as Metric,
    COUNT(*) as Count
FROM AuditLogs

UNION ALL

SELECT 'TEST 13: Audit Logs Summary', 'Total Users Audited', COUNT(DISTINCT UserId)
FROM AuditLogs

UNION ALL

SELECT 'TEST 13: Audit Logs Summary', 'Audit Entries Last 24 Hours', 
       COUNT(*) 
FROM AuditLogs 
WHERE Timestamp >= DATE_SUB(NOW(), INTERVAL 1 DAY)

UNION ALL

SELECT 'TEST 13: Audit Logs Summary', 'Different Action Types', 
       COUNT(DISTINCT Action)
FROM AuditLogs;


-- QUERY 14: Show recent audit log entries
-- ============================================================================
SELECT 
    'TEST 14: Recent Audit Entries' as Query,
    al.Id,
    COALESCE(u.UserName, 'System') as User,
    al.Action,
    al.EntityType,
    al.EntityId,
    al.Details,
    al.Timestamp
FROM AuditLogs al
LEFT JOIN AspNetUsers u ON al.UserId = u.Id
ORDER BY al.Timestamp DESC
LIMIT 20;


-- ============================================================================
-- SECTION 6: TRANSACTION & ISOLATION LEVEL TESTS
-- ============================================================================

-- QUERY 15: Check current session isolation level
-- Expected: READ-COMMITTED or REPEATABLE-READ (depending on MySQL config)
-- ============================================================================
SELECT 
    'VERIFICATION 15: Isolation Level' as Query,
    @@SESSION.transaction_isolation as CurrentIsolation,
    @@GLOBAL.transaction_isolation as GlobalIsolation;


-- QUERY 16: Check transaction support
-- Expected: MyISAM, InnoDB (with InnoDB supporting transactions)
-- ============================================================================
SELECT 
    'VERIFICATION 16: Storage Engines' as Query,
    TABLE_NAME,
    ENGINE
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'passManDB'
GROUP BY ENGINE
ORDER BY TABLE_NAME;


-- QUERY 17: Verify all PassManDB tables support transactions (InnoDB)
-- ============================================================================
SELECT 
    'VERIFICATION 17: InnoDB Tables' as Query,
    TABLE_NAME,
    ENGINE,
    'SUPPORTS TRANSACTIONS ✓' as Status
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'passManDB'
  AND ENGINE = 'InnoDB';


-- ============================================================================
-- SECTION 7: COMPREHENSIVE SUMMARY
-- ============================================================================

-- QUERY 18: Database Artifacts Summary
-- ============================================================================
SELECT 
    'SUMMARY 18: All Artifacts' as Report,
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
     WHERE TABLE_SCHEMA = 'passManDB' AND TABLE_TYPE = 'VIEW') as ViewCount,
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.ROUTINES 
     WHERE ROUTINE_SCHEMA = 'passManDB' AND ROUTINE_TYPE = 'PROCEDURE') as ProcedureCount,
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TRIGGERS 
     WHERE TRIGGER_SCHEMA = 'passManDB') as TriggerCount;


-- QUERY 19: List ALL database objects
-- ============================================================================
SELECT 
    'SUMMARY 19: All Objects' as ObjectType,
    'VIEWS' as Type,
    TABLE_NAME as Name,
    'Database View' as Description
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'passManDB' AND TABLE_TYPE = 'VIEW'

UNION ALL

SELECT 'SUMMARY 19: All Objects', 'PROCEDURES', ROUTINE_NAME, 'Stored Procedure'
FROM INFORMATION_SCHEMA.ROUTINES
WHERE ROUTINE_SCHEMA = 'passManDB' AND ROUTINE_TYPE = 'PROCEDURE'

UNION ALL

SELECT 'SUMMARY 19: All Objects', 'TRIGGERS', TRIGGER_NAME, 'Database Trigger'
FROM INFORMATION_SCHEMA.TRIGGERS
WHERE TRIGGER_SCHEMA = 'passManDB'

ORDER BY Type, Name;


-- QUERY 20: Test Procedure Execution (sp_AddVaultShare)
-- WARNING: This will attempt to add a vault share
-- Modify vaultId and email as needed for your test data
-- ============================================================================
-- CALL sp_AddVaultShare(1, 'test@example.com');
-- Check result in VaultShares table:
SELECT 
    'TEST 20: VaultShares After Procedure' as Query,
    vs.VaultId,
    vs.UserId,
    COALESCE(u.UserName, 'UNKNOWN') as UserName,
    COALESCE(v.Name, 'UNKNOWN') as VaultName,
    vs.Permission
FROM VaultShares vs
LEFT JOIN AspNetUsers u ON vs.UserId = u.Id
LEFT JOIN Vaults v ON vs.VaultId = v.Id
ORDER BY vs.VaultId, vs.UserId;


-- ============================================================================
-- SECTION 8: TRIGGER TESTING
-- ============================================================================

-- QUERY 21: Check if trigger updates UpdatedAt field
-- This query shows credentials sorted by UpdatedAt to verify trigger works
-- ============================================================================
SELECT 
    'TEST 21: Trigger - UpdatedAt Field' as Query,
    c.Id as CredentialId,
    c.Title,
    c.CreatedAt,
    c.UpdatedAt,
    CASE 
        WHEN c.UpdatedAt > c.CreatedAt THEN 'Has Been Updated ✓'
        WHEN c.UpdatedAt = c.CreatedAt THEN 'Never Updated'
    END as Status
FROM Credentials c
ORDER BY c.UpdatedAt DESC
LIMIT 20;


-- QUERY 22: Count credentials with UpdatedAt different from CreatedAt
-- Higher count = more trigger activity
-- ============================================================================
SELECT 
    'TEST 22: Trigger Activity Count' as Query,
    'Credentials Updated (UpdatedAt > CreatedAt)' as Metric,
    COUNT(*) as Count
FROM Credentials
WHERE UpdatedAt > CreatedAt

UNION ALL

SELECT 'TEST 22: Trigger Activity Count', 'Credentials Never Updated', COUNT(*)
FROM Credentials
WHERE UpdatedAt = CreatedAt

UNION ALL

SELECT 'TEST 22: Trigger Activity Count', 'Total Credentials', COUNT(*)
FROM Credentials;


-- ============================================================================
-- SECTION 9: TROUBLESHOOTING QUERIES
-- ============================================================================

-- QUERY 23: Check if artifacts were created (diagnostic)
-- ============================================================================
SELECT 
    'TROUBLESHOOTING 23: Artifact Status' as Check,
    CASE 
        WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES 
                     WHERE TABLE_SCHEMA = 'passManDB' AND TABLE_NAME = 'vwUserVaultAccess')
        THEN 'View EXISTS ✓'
        ELSE 'View MISSING ✗'
    END as View_Status,
    CASE 
        WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.ROUTINES 
                     WHERE ROUTINE_SCHEMA = 'passManDB' AND ROUTINE_NAME = 'sp_AddVaultShare')
        THEN 'Procedure sp_AddVaultShare EXISTS ✓'
        ELSE 'Procedure sp_AddVaultShare MISSING ✗'
    END as Proc1_Status,
    CASE 
        WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.ROUTINES 
                     WHERE ROUTINE_SCHEMA = 'passManDB' AND ROUTINE_NAME = 'sp_LogAudit')
        THEN 'Procedure sp_LogAudit EXISTS ✓'
        ELSE 'Procedure sp_LogAudit MISSING ✗'
    END as Proc2_Status,
    CASE 
        WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TRIGGERS 
                     WHERE TRIGGER_SCHEMA = 'passManDB' AND TRIGGER_NAME = 'trg_Credentials_SetUpdatedAt')
        THEN 'Trigger EXISTS ✓'
        ELSE 'Trigger MISSING ✗'
    END as Trigger_Status;


-- QUERY 24: Check database permissions for current user
-- Helps diagnose if artifacts can be created/dropped
-- ============================================================================
SELECT 
    'TROUBLESHOOTING 24: User Permissions' as Check,
    GRANTEE,
    PRIVILEGE_TYPE,
    IS_GRANTABLE
FROM INFORMATION_SCHEMA.USER_PRIVILEGES
WHERE GRANTEE LIKE '%passman%'
   OR GRANTEE LIKE '%root%'
LIMIT 20;


-- QUERY 25: Check MySQL version and features
-- ============================================================================
SELECT 
    'TROUBLESHOOTING 25: MySQL Version Info' as Check,
    @@VERSION as MySQLVersion,
    @@GLOBAL.version_compile_machine as Architecture,
    @@global.max_connections as MaxConnections,
    @@global.default_storage_engine as DefaultEngine;


-- ============================================================================
-- COPY-PASTE QUICK TESTS
-- ============================================================================

-- Quick Test 1: Do the artifacts exist?
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'passManDB' AND TABLE_NAME = 'vwUserVaultAccess'
) THEN 'VIEW EXISTS' ELSE 'VIEW MISSING' END;

-- Quick Test 2: Query the view
SELECT * FROM vwUserVaultAccess LIMIT 5;

-- Quick Test 3: Check procedures
SELECT ROUTINE_NAME FROM INFORMATION_SCHEMA.ROUTINES 
WHERE ROUTINE_SCHEMA = 'passManDB' AND ROUTINE_TYPE = 'PROCEDURE';

-- Quick Test 4: Check triggers
SELECT TRIGGER_NAME FROM INFORMATION_SCHEMA.TRIGGERS 
WHERE TRIGGER_SCHEMA = 'passManDB';

-- Quick Test 5: Count everything
SELECT 
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
     WHERE TABLE_SCHEMA = 'passManDB' AND TABLE_TYPE = 'VIEW') as Views,
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.ROUTINES 
     WHERE ROUTINE_SCHEMA = 'passManDB' AND ROUTINE_TYPE = 'PROCEDURE') as Procedures,
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TRIGGERS 
     WHERE TRIGGER_SCHEMA = 'passManDB') as Triggers;

-- ============================================================================
-- END OF VERIFICATION QUERIES
-- ============================================================================
-- Generated: 2026-03-22
-- All queries ready to execute!
-- ============================================================================
