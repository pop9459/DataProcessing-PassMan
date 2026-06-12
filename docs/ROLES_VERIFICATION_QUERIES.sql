-- ============================================================================
-- ROLES VERIFICATION - EXECUTABLE SQL QUERIES
-- Issue #166: Roles Investigation
-- ============================================================================
-- This file contains ready-to-run SQL queries to verify all roles exist
-- Copy and paste any query directly into your database client
-- ============================================================================

-- QUERY 1: Verify all 4 roles exist
-- Expected Result: 4 rows (Admin, SecurityAuditor, VaultOwner, VaultReader)
-- ============================================================================
SELECT 
    'VERIFICATION 1: Expected Roles' as Query,
    Id as RoleId,
    Name as RoleName,
    'FOUND ✓' as Status
FROM AspNetRoles
WHERE Name IN ('Admin', 'SecurityAuditor', 'VaultOwner', 'VaultReader')
ORDER BY Name;


-- QUERY 2: Count total permissions per role
-- Expected: Admin=17, SecurityAuditor=4, VaultOwner=13, VaultReader=3
-- ============================================================================
SELECT 
    'VERIFICATION 2: Permission Count Per Role' as Query,
    ar.Name as RoleName,
    COUNT(rc.Id) as PermissionCount,
    CASE 
        WHEN ar.Name = 'Admin' AND COUNT(rc.Id) = 17 THEN 'CORRECT ✓'
        WHEN ar.Name = 'SecurityAuditor' AND COUNT(rc.Id) = 4 THEN 'CORRECT ✓'
        WHEN ar.Name = 'VaultOwner' AND COUNT(rc.Id) = 13 THEN 'CORRECT ✓'
        WHEN ar.Name = 'VaultReader' AND COUNT(rc.Id) = 3 THEN 'CORRECT ✓'
        ELSE 'MISMATCH ✗'
    END as Status
FROM AspNetRoles ar
LEFT JOIN AspNetRoleClaims rc ON ar.Id = rc.RoleId AND rc.ClaimType = 'permission'
WHERE ar.Name IN ('Admin', 'SecurityAuditor', 'VaultOwner', 'VaultReader')
GROUP BY ar.Id, ar.Name
ORDER BY ar.Name;


-- QUERY 3: Show all Admin permissions (should be 17)
-- ============================================================================
SELECT 
    'VERIFICATION 3: Admin Permissions' as Query,
    ROW_NUMBER() OVER (ORDER BY rc.ClaimValue) as '#',
    'Admin' as RoleName,
    rc.ClaimValue as Permission
FROM AspNetRoleClaims rc
INNER JOIN AspNetRoles ar ON rc.RoleId = ar.Id
WHERE ar.Name = 'Admin' AND rc.ClaimType = 'permission'
ORDER BY rc.ClaimValue;


-- QUERY 4: Show all SecurityAuditor permissions (should be 4)
-- ============================================================================
SELECT 
    'VERIFICATION 4: SecurityAuditor Permissions' as Query,
    ROW_NUMBER() OVER (ORDER BY rc.ClaimValue) as '#',
    'SecurityAuditor' as RoleName,
    rc.ClaimValue as Permission
FROM AspNetRoleClaims rc
INNER JOIN AspNetRoles ar ON rc.RoleId = ar.Id
WHERE ar.Name = 'SecurityAuditor' AND rc.ClaimType = 'permission'
ORDER BY rc.ClaimValue;


-- QUERY 5: Show all VaultOwner permissions (should be 13)
-- ============================================================================
SELECT 
    'VERIFICATION 5: VaultOwner Permissions' as Query,
    ROW_NUMBER() OVER (ORDER BY rc.ClaimValue) as '#',
    'VaultOwner' as RoleName,
    rc.ClaimValue as Permission
FROM AspNetRoleClaims rc
INNER JOIN AspNetRoles ar ON rc.RoleId = ar.Id
WHERE ar.Name = 'VaultOwner' AND rc.ClaimType = 'permission'
ORDER BY rc.ClaimValue;


-- QUERY 6: Show all VaultReader permissions (should be 3)
-- ============================================================================
SELECT 
    'VERIFICATION 6: VaultReader Permissions' as Query,
    ROW_NUMBER() OVER (ORDER BY rc.ClaimValue) as '#',
    'VaultReader' as RoleName,
    rc.ClaimValue as Permission
FROM AspNetRoleClaims rc
INNER JOIN AspNetRoles ar ON rc.RoleId = ar.Id
WHERE ar.Name = 'VaultReader' AND rc.ClaimType = 'permission'
ORDER BY rc.ClaimValue;


-- QUERY 7: Complete permission overview (all roles with all permissions)
-- ============================================================================
SELECT 
    'VERIFICATION 7: Complete Permission Matrix' as Query,
    ar.Name as RoleName,
    STRING_AGG(rc.ClaimValue, ', ' ORDER BY rc.ClaimValue) as Permissions
FROM AspNetRoles ar
LEFT JOIN AspNetRoleClaims rc ON ar.Id = rc.RoleId AND rc.ClaimType = 'permission'
WHERE ar.Name IN ('Admin', 'SecurityAuditor', 'VaultOwner', 'VaultReader')
GROUP BY ar.Id, ar.Name
ORDER BY ar.Name;


-- QUERY 8: Show demo users and their roles
-- Expected: admin, auditor, owner, reader with their respective roles
-- ============================================================================
SELECT 
    'VERIFICATION 8: Demo Users Assignments' as Query,
    u.Id as UserId,
    u.UserName,
    u.Email,
    ar.Name as AssignedRole,
    CASE 
        WHEN u.Email = 'admin@passman.test' AND ar.Name = 'Admin' THEN 'CORRECT ✓'
        WHEN u.Email = 'auditor@passman.test' AND ar.Name = 'SecurityAuditor' THEN 'CORRECT ✓'
        WHEN u.Email = 'owner@passman.test' AND ar.Name = 'VaultOwner' THEN 'CORRECT ✓'
        WHEN u.Email = 'reader@passman.test' AND ar.Name = 'VaultReader' THEN 'CORRECT ✓'
        ELSE 'CHECK'
    END as Status
FROM AspNetUsers u
LEFT JOIN AspNetUserRoles ur ON u.Id = ur.UserId
LEFT JOIN AspNetRoles ar ON ur.RoleId = ar.Id
WHERE u.Email IN ('admin@passman.test', 'auditor@passman.test', 'owner@passman.test', 'reader@passman.test')
ORDER BY u.UserName, ar.Name;


-- QUERY 9: Summary statistics
-- ============================================================================
SELECT 
    'VERIFICATION 9: Database Statistics' as Query,
    'Total Roles' as Metric,
    COUNT(*) as Count
FROM AspNetRoles

UNION ALL

SELECT 'VERIFICATION 9: Database Statistics', 'Total Role Claims', COUNT(*)
FROM AspNetRoleClaims
WHERE ClaimType = 'permission'

UNION ALL

SELECT 'VERIFICATION 9: Database Statistics', 'Total Demo Users', COUNT(*)
FROM AspNetUsers
WHERE Email IN ('admin@passman.test', 'auditor@passman.test', 'owner@passman.test', 'reader@passman.test')

UNION ALL

SELECT 'VERIFICATION 9: Database Statistics', 'Total User-Role Assignments', COUNT(*)
FROM AspNetUserRoles;


-- QUERY 10: Check for any missing required permissions
-- ============================================================================
SELECT 
    'VERIFICATION 10: Permission Completeness Check' as Query,
    RequiredPermission,
    CASE 
        WHEN EXISTS (
            SELECT 1 FROM AspNetRoleClaims 
            WHERE ClaimType = 'permission' AND ClaimValue = RequiredPermission
        ) THEN 'EXISTS ✓'
        ELSE 'MISSING ✗'
    END as Status
FROM (
    SELECT 'vault.read' as RequiredPermission UNION ALL
    SELECT 'vault.create' UNION ALL
    SELECT 'vault.update' UNION ALL
    SELECT 'vault.delete' UNION ALL
    SELECT 'vault.share' UNION ALL
    SELECT 'credential.read' UNION ALL
    SELECT 'credential.create' UNION ALL
    SELECT 'credential.update' UNION ALL
    SELECT 'credential.delete' UNION ALL
    SELECT 'tag.read' UNION ALL
    SELECT 'tag.create' UNION ALL
    SELECT 'tag.update' UNION ALL
    SELECT 'tag.delete' UNION ALL
    SELECT 'audit.read' UNION ALL
    SELECT 'user.manage' UNION ALL
    SELECT 'role.manage' UNION ALL
    SELECT 'system.health'
) as RequiredPermissions
ORDER BY RequiredPermission;


-- QUERY 11: Detailed role-permission mapping (formatted for readability)
-- ============================================================================
SELECT 
    'VERIFICATION 11: Detailed Role-Permission Mapping' as Query,
    ar.Name as Role,
    rc.ClaimValue as Permission,
    rc.Id as ClaimId
FROM AspNetRoles ar
INNER JOIN AspNetRoleClaims rc ON ar.Id = rc.RoleId
WHERE rc.ClaimType = 'permission'
ORDER BY ar.Name, rc.ClaimValue;


-- QUERY 12: Export all roles and permissions as formatted list
-- ============================================================================
SELECT 
    'EXPORT: All Roles and Permissions' as 'Report',
    ar.Name as Role,
    STRING_AGG(rc.ClaimValue, ', ' ORDER BY rc.ClaimValue) as PermissionList,
    COUNT(DISTINCT rc.Id) as TotalPermissions
FROM AspNetRoles ar
LEFT JOIN AspNetRoleClaims rc ON ar.Id = rc.RoleId AND rc.ClaimType = 'permission'
WHERE ar.Name IN ('Admin', 'SecurityAuditor', 'VaultOwner', 'VaultReader')
GROUP BY ar.Id, ar.Name
ORDER BY ar.Name;


-- ============================================================================
-- TROUBLESHOOTING QUERIES
-- ============================================================================

-- If queries return no results, check these:

-- Q1: Are the Identity tables empty?
-- ============================================================================
SELECT COUNT(*) as TotalRoles FROM AspNetRoles;
-- Expected: >= 4

SELECT COUNT(*) as TotalRoleClaims FROM AspNetRoleClaims;
-- Expected: >= 37


-- Q2: Are there any roles without permissions?
-- ============================================================================
SELECT ar.Name as Role, COUNT(rc.Id) as PermissionCount
FROM AspNetRoles ar
LEFT JOIN AspNetRoleClaims rc ON ar.Id = rc.RoleId AND rc.ClaimType = 'permission'
GROUP BY ar.Id, ar.Name
HAVING COUNT(rc.Id) = 0;
-- Expected: 0 rows


-- Q3: Are there any orphaned role claims?
-- ============================================================================
SELECT rc.* 
FROM AspNetRoleClaims rc
WHERE rc.RoleId NOT IN (SELECT Id FROM AspNetRoles);
-- Expected: 0 rows


-- Q4: List all users and their roles
-- ============================================================================
SELECT 
    u.Id,
    u.UserName,
    u.Email,
    STRING_AGG(ar.Name, ', ') as Roles
FROM AspNetUsers u
LEFT JOIN AspNetUserRoles ur ON u.Id = ur.UserId
LEFT JOIN AspNetRoles ar ON ur.RoleId = ar.Id
GROUP BY u.Id, u.UserName, u.Email
ORDER BY u.UserName;


-- ============================================================================
-- RESET/FIX QUERIES (Use with caution - uncomment only if needed)
-- ============================================================================

-- RESET: Delete all role seeding and start fresh
-- WARNING: This will remove all roles and permissions!
-- Only run this if you need to reseed everything
-- SOLUTION: Just restart the application - DbSeeder will fix it automatically

/*
-- Delete all role claims
DELETE FROM AspNetRoleClaims;

-- Delete all user role assignments
DELETE FROM AspNetUserRoles;

-- Delete all roles
DELETE FROM AspNetRoles;

-- Then restart the application to re-seed
*/

-- ============================================================================
-- COPY-PASTE SUMMARY FOR QUICK TESTING
-- ============================================================================

-- Test 1: Show all roles exist
SELECT * FROM AspNetRoles WHERE Name IN ('Admin', 'SecurityAuditor', 'VaultOwner', 'VaultReader');

-- Test 2: Show Admin has 17 permissions
SELECT COUNT(*) as AdminPermissions FROM AspNetRoleClaims rc
INNER JOIN AspNetRoles ar ON rc.RoleId = ar.Id
WHERE ar.Name = 'Admin' AND rc.ClaimType = 'permission';

-- Test 3: Show SecurityAuditor has 4 permissions
SELECT COUNT(*) as AuditorPermissions FROM AspNetRoleClaims rc
INNER JOIN AspNetRoles ar ON rc.RoleId = ar.Id
WHERE ar.Name = 'SecurityAuditor' AND rc.ClaimType = 'permission';

-- Test 4: Show all permissions for Admin
SELECT rc.ClaimValue FROM AspNetRoleClaims rc
INNER JOIN AspNetRoles ar ON rc.RoleId = ar.Id
WHERE ar.Name = 'Admin' AND rc.ClaimType = 'permission'
ORDER BY rc.ClaimValue;

-- Test 5: Show demo users with roles
SELECT u.UserName, ar.Name FROM AspNetUsers u
LEFT JOIN AspNetUserRoles ur ON u.Id = ur.UserId
LEFT JOIN AspNetRoles ar ON ur.RoleId = ar.Id
WHERE u.Email LIKE '%@passman.test%'
ORDER BY u.UserName;

-- ============================================================================
-- END OF VERIFICATION QUERIES
-- ============================================================================
-- Generated: 2026-03-22
-- Issue: #166
-- All queries ready to execute!
-- ============================================================================
