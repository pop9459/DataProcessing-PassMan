-- Roles, permissions, and database-object audit queries for PassMan
-- Run against passManDB.

USE passManDB;

-- 1) List all roles
SELECT r.Id, r.Name, r.NormalizedName
FROM AspNetRoles r
ORDER BY r.Name;

-- 2) List permissions per role (ClaimType = 'permission')
SELECT r.Name AS RoleName, c.ClaimType, c.ClaimValue AS Permission
FROM AspNetRoles r
LEFT JOIN AspNetRoleClaims c ON c.RoleId = r.Id AND c.ClaimType = 'permission'
ORDER BY r.Name, c.ClaimValue;

-- 3) List user-role assignments
SELECT u.Id AS UserId, u.Email, r.Name AS RoleName
FROM AspNetUsers u
LEFT JOIN AspNetUserRoles ur ON ur.UserId = u.Id
LEFT JOIN AspNetRoles r ON r.Id = ur.RoleId
ORDER BY u.Id, r.Name;

-- 4) Verify key domain foreign keys
SELECT TABLE_NAME, COLUMN_NAME, CONSTRAINT_NAME, REFERENCED_TABLE_NAME, REFERENCED_COLUMN_NAME
FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
WHERE TABLE_SCHEMA = DATABASE()
  AND REFERENCED_TABLE_NAME IS NOT NULL
ORDER BY TABLE_NAME, COLUMN_NAME;

-- 5) Verify required DB artifacts expected by project
SELECT TABLE_NAME
FROM INFORMATION_SCHEMA.VIEWS
WHERE TABLE_SCHEMA = DATABASE()
ORDER BY TABLE_NAME;

SELECT ROUTINE_NAME, ROUTINE_TYPE
FROM INFORMATION_SCHEMA.ROUTINES
WHERE ROUTINE_SCHEMA = DATABASE()
ORDER BY ROUTINE_NAME;

SELECT TRIGGER_NAME, EVENT_MANIPULATION, EVENT_OBJECT_TABLE
FROM INFORMATION_SCHEMA.TRIGGERS
WHERE TRIGGER_SCHEMA = DATABASE()
ORDER BY TRIGGER_NAME;
