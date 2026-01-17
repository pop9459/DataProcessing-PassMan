# 👥 PassMan Team Work Distribution Plan

> **Created:** January 16, 2026  
> **Team Size:** 3 members  
> **Estimated Duration:** 3 weeks

---

## 📋 Overview

This document outlines the work distribution for implementing the remaining features and rubric requirements for the PassMan project. Work has been divided to minimize merge conflicts and allow parallel development.

---

## 🏗️ Team Member 1: Domain Models & Managers

**Focus Area:** Core data layer and business logic

### Week 1 - Foundation (Start Here)

| Issue | Title | Priority | Files Affected |
|-------|-------|----------|----------------|
| #62 | Create Tag.cs model | 🔴 High | `Models/Tag.cs` |
| #63 | Create CredentialTag.cs join model | 🔴 High | `Models/CredentialTag.cs` |
| #64 | Create Attachment.cs model | 🔴 High | `Models/Attachment.cs` |
| #65 | Create Invitation.cs model | 🔴 High | `Models/Invitation.cs` |
| #107 | Create SubscriptionTier.cs model | ⚪ Nice to have | `Models/SubscriptionTier.cs` |

### Week 2 - Model Updates

| Issue | Title | Depends On |
|-------|-------|------------|
| #78 | Add TotpSecret/SubscriptionTierId to User | #107 |
| #79 | Add Icon/IsDeleted/Invitations to Vault | #65 |
| #80 | Add navigation properties to Credential | #62, #63, #64 |
| #81 | Add VaultId/CredentialId to AuditLog | - |

### Week 3 - Managers

| Issue | Title | Depends On |
|-------|-------|------------|
| #73 | Create VaultManager | Models complete |
| #74 | Create CredentialManager | Models complete |
| #75 | Create AuditManager | #81 |

### Branch Strategy
```bash
git checkout develop && git pull
git checkout -b feature/domain-models      # Week 1
git checkout -b feature/model-updates      # Week 2
git checkout -b feature/managers           # Week 3
```

---

## 🔐 Team Member 2: Authentication & Services

**Focus Area:** Security layer and API enhancements

### Week 1 - Services (Start Here)

| Issue | Title | Priority | Files Affected |
|-------|-------|----------|----------------|
| #69 | Create TokenService | 🔴 High | `Services/TokenService.cs` |
| #70 | Create PasswordEncryptionService | 🔴 High | `Services/PasswordEncryptionService.cs` |
| #71 | Create TwoFactorService | 🟡 Medium | `Services/TwoFactorService.cs` |
| #72 | Create BreachCheckService | 🟡 Medium | `Services/BreachCheckService.cs` |

### Week 2 - JWT Authentication

| Issue | Title | Depends On |
|-------|-------|------------|
| #92 | Implement JWT token generation | #69 (TokenService) |
| #93 | Implement token expiration/refresh | #92 |
| #96 | Add [Authorize] attributes | #92 |

### Week 3 - Validation & Remaining Managers

| Issue | Title | Depends On |
|-------|-------|------------|
| #103 | Create standardized error response | - |
| #105 | Add FluentValidation | - |
| #76 | Create SharingManager | TM1's models |
| #77 | Create AuthManager | #69, #71 |

### Branch Strategy
```bash
git checkout develop && git pull
git checkout -b feature/services           # Week 1
git checkout -b feature/jwt-auth           # Week 2
git checkout -b feature/validation         # Week 3
```

---

## 🗄️ Team Member 3: Database & DevOps

**Focus Area:** Database objects, testing, and infrastructure

### Week 1 - Quick Wins (Start Here)

| Issue | Title | Priority | Files Affected |
|-------|-------|----------|----------------|
| #91 | Add XML serializer formatters | 🟢 Easy | `Program.cs` |
| #101 | Create MySQL users with permissions | 🔴 High | `scripts/create-db-users.sql` |
| #104 | Create ER Diagram | 🔴 High | `diagrams/ERDiagram.*` |

### Week 2 - Database Integrity

| Issue | Title | Depends On |
|-------|-------|------------|
| #100 | Create SQL Views | - |
| #106 | Create Stored Procedures | - |
| #99 | Create Database Triggers | - |
| #98 | Document transaction isolation levels | - |

### Week 3 - Backup & Testing

| Issue | Title | Depends On |
|-------|-------|------------|
| #97 | Create backup/recovery documentation | - |
| #95 | Create automated backup scripts | - |
| #94 | Create Postman collection | APIs working |
| #102 | Expand unit test coverage | Features done |

### Branch Strategy
```bash
git checkout develop && git pull
git checkout -b feature/xml-support        # Week 1 (quick)
git checkout -b feature/db-integrity       # Week 2
git checkout -b feature/backup-recovery    # Week 3
git checkout -b feature/testing            # Week 3
```

---

## 📊 Dependency Chain (Critical Path)

```
Week 1 (Parallel Start - No Dependencies):
├── TM1: Domain Models (#62-65, #107)
├── TM2: Services (#69-72)
└── TM3: XML + DB Users + ER Diagram (#91, #101, #104)

Week 2 (After Week 1):
├── TM1: Model Updates (#78-81) ← needs Week 1 models
├── TM2: JWT Auth (#92-93, #96) ← needs #69 TokenService
└── TM3: DB Objects (#98-100, #106) ← independent

Week 3 (After Week 2):
├── TM1: Managers (#73-75) ← needs models complete
├── TM2: Validation + Managers (#76-77, #103, #105)
└── TM3: Backup + Testing (#94-95, #97, #102) ← needs features
```

---

## ⚠️ Key Dependencies to Watch

| Must Complete First | Before Starting | Reason |
|---------------------|-----------------|--------|
| #69 TokenService | #92 JWT Implementation | JWT needs token generator |
| #62-64 Models | #80 Credential nav props | Can't add navs without models |
| #65 Invitation | #79 Vault update | Vault needs Invitation navigation |
| #71 TwoFactorService | #77 AuthManager | Auth manager uses 2FA service |
| All features | #94, #102 Testing | Need working code to test |

---

## 🔀 Merge Strategy

### Recommended Order
1. **TM1** merges domain models first (other work depends on these)
2. **TM3** merges XML support (quick, no dependencies)
3. **TM2** merges services (needed for auth)
4. **TM1** merges model updates
5. **TM2** merges JWT auth
6. Continue in dependency order...

### Before Each Merge
```bash
git checkout develop
git pull
git checkout your-feature-branch
git rebase develop
# Resolve any conflicts
git push --force-with-lease
# Create PR for review
```

---

## ✅ Definition of Done

For each issue, ensure:
- [ ] Code compiles without errors
- [ ] Unit tests pass (where applicable)
- [ ] Code follows existing project patterns
- [ ] EF Core migration created (for model changes)
- [ ] PR reviewed by at least one team member
- [ ] Issue linked in PR description

---

## 📞 Communication Points

| Event | Action Required |
|-------|-----------------|
| Starting a new issue | Comment on GitHub issue |
| Blocked by dependency | Notify blocking team member |
| Ready for review | Create PR, request reviewer |
| Merge conflict | Coordinate with affected member |
| Completing a week's work | Team sync meeting |

---

## 🔗 Quick Links

- **GitHub Issues:** https://github.com/pop9459/DataProcessing-PassMan/issues
- **Parent Issues (Tracking):** #61, #66, #67, #68, #84-90

---

*Last updated: January 16, 2026*
