# API Integration - Testing Guide

## Prerequisites

Before testing, ensure the following:
1. ✅ PassManAPI is running (Docker or locally on port 5246)
2. ✅ MySQL database is running and accessible
3. ✅ PassManGUI build succeeds (`dotnet build`)

## Test Environment Setup

### Option 1: Docker (Recommended)
```bash
# From project root
docker-compose up --build
```
- PassManAPI will be available at: http://localhost:5246
- PassManGUI will be available at: http://localhost:5247
- MySQL will be available at: localhost:3306

### Option 2: Local Development
```bash
# Terminal 1 - Start API
cd PassManAPI
dotnet run

# Terminal 2 - Start GUI
cd PassManGUI
dotnet run
```
- PassManAPI will run on: http://localhost:5154 (update appsettings if different)
- PassManGUI will run on: http://localhost:5xxx (check terminal output)

## Test Cases

### 1. User Registration Flow ✅
**Steps:**
1. Navigate to http://localhost:5247 (or your GUI URL)
2. Click "Sign Up" link from home page or navigate to `/signup`
3. Fill in the registration form:
   - Username: `testuser`
   - Email: `testuser@example.com`
   - Password: `Password123!`
   - Confirm Password: `Password123!`
   - Check "I agree to Terms of Service"
4. Click "Create account"

**Expected Results:**
- ✅ Form validation works (password match, required fields)
- ✅ Loading state appears ("Creating account...")
- ✅ On success: Redirects to `/vaults` page
- ✅ On error: Error message displays with red background
- ✅ User is automatically logged in after registration

**Backend Verification:**
```bash
# Check user was created in database
mysql -u root -phihi -e "USE passManDB; SELECT * FROM Users WHERE Email='testuser@example.com';"
```

---

### 2. User Login Flow ✅
**Steps:**
1. Navigate to `/login` page
2. Fill in credentials:
   - Email: `testuser@example.com`
   - Password: `Password123!`
3. Optionally check "Remember me" (currently no-op, future PIN feature)
4. Click "Sign in"

**Expected Results:**
- ✅ Form validation works (required fields)
- ✅ Loading state appears ("Signing in...")
- ✅ On success: Redirects to `/vaults` page
- ✅ On error: Error message displays
- ✅ Token is stored in sessionStorage
- ✅ Authorization headers are set (Bearer token + X-UserId)

**Developer Console Checks:**
```javascript
// Open browser DevTools (F12), Console tab:
sessionStorage.getItem('passman_auth_token')  // Should return: "dev-token-{userId}"
sessionStorage.getItem('passman_user_id')     // Should return: "{userId}"
```

---

### 3. Session Persistence ✅
**Steps:**
1. Login successfully to reach `/vaults` page
2. Refresh the page (F5)
3. Observe behavior

**Expected Results:**
- ✅ User remains logged in (does not redirect to login)
- ✅ Vaults page loads successfully
- ✅ Session is restored from sessionStorage
- ✅ Authorization headers are re-applied

**Test Session Expiration:**
1. Open new tab → Navigate to your app
2. Should redirect to `/login` (new session, no token)
3. Close tab with active session
4. Open new tab → Should redirect to `/login` (sessionStorage cleared)

---

### 4. Vault List Display ✅
**Steps:**
1. Login successfully
2. On `/vaults` page, observe the display

**Expected Results:**
- ✅ Loading spinner appears briefly
- ✅ If no vaults: Empty state displays with "Create Your First Vault" button
- ✅ If vaults exist: Grid of vault cards displays
- ✅ Each vault card shows:
  - Vault name
  - Description (if exists)
  - Created date
  - Updated date (if different from created)
- ✅ Hover effect on vault cards (border color changes)
- ✅ Clicking vault card navigates to `/vault/{id}`

**Create Test Vault:**
```bash
# Manually create a vault for testing
mysql -u root -phihi passManDB -e "
INSERT INTO Vaults (Name, Description, UserId, CreatedAt) 
VALUES ('Personal', 'My personal passwords', 1, NOW());
"
```

---

### 5. Vault Detail Page ✅
**Steps:**
1. From vaults page, click on a vault card
2. Observe the vault detail page at `/vault/{id}`

**Expected Results:**
- ✅ Loading spinner appears briefly
- ✅ Vault name displays in header
- ✅ Vault description displays below name
- ✅ "Back to Vaults" link works
- ✅ If no items: Empty state displays with "Add Your First Item" button
- ✅ If items exist: List of item cards displays
- ✅ Each item card shows:
  - Item name
  - Username (if exists)
  - URL (if exists)
  - Label badge (if exists)
  - "Created X ago" timestamp
- ✅ Hover effect on item cards
- ✅ Clicking item card (currently logs to console, TODO: implement detail view)

**Create Test Item:**
```bash
# Manually create a credential item for testing
mysql -u root -phihi passManDB -e "
INSERT INTO Credentials (Name, Username, Password, Url, Label, Notes, VaultId, CreatedAt) 
VALUES ('Gmail', 'test@gmail.com', 'encryptedpassword', 'https://gmail.com', 'Personal', 'Test email account', 1, NOW());
"
```

---

### 6. Authentication Guard ✅
**Steps:**
1. Open browser in incognito mode
2. Navigate directly to `/vaults` (without logging in)

**Expected Results:**
- ✅ Immediately redirects to `/login` page
- ✅ Same behavior for `/vault/{id}` routes

---

### 7. Error Handling ✅

#### Invalid Credentials
**Steps:**
1. Navigate to `/login`
2. Enter incorrect email or password
3. Click "Sign in"

**Expected Results:**
- ✅ Error message displays
- ✅ User stays on login page
- ✅ Can try again

#### Network Error (API Down)
**Steps:**
1. Stop PassManAPI
2. Try to login or load vaults

**Expected Results:**
- ✅ Error message displays
- ✅ "Try Again" button allows retry
- ✅ No app crash or white screen

#### Server Error (500)
**Steps:**
1. Simulate server error (modify API to throw exception)
2. Try any API call

**Expected Results:**
- ✅ Friendly error message displays
- ✅ Error logged to console for debugging

---

## Developer Debugging

### Check Network Requests
Open Browser DevTools (F12) → Network tab:
- ✅ POST `/api/auth/login` returns 200 with token
- ✅ POST `/api/auth/register` returns 201 with token
- ✅ GET `/api/vaults?userId={id}` returns 200 with array
- ✅ GET `/api/vaults/{id}` returns 200 with vault object
- ✅ GET `/api/credentials?vaultId={id}` returns 200 with items array
- ✅ All requests include `Authorization: Bearer dev-token-{userId}` header
- ✅ All requests include `X-UserId: {userId}` header

### Check Console Logs
Browser Console (F12) → Console tab:
- ✅ No red errors (except expected ones you're testing)
- ✅ API calls log their status
- ✅ "Click" actions log to console (Add Item, Open Item, etc.)

### Check sessionStorage
```javascript
// In browser console:
sessionStorage.getItem('passman_auth_token')
sessionStorage.getItem('passman_user_id')

// Clear session (logout simulation):
sessionStorage.clear()
location.reload()
```

---

## Known Limitations (TODOs)

The following features are **not yet implemented** and marked as TODO:
- ❌ Create vault functionality (button logs to console)
- ❌ Add item functionality (button logs to console)
- ❌ Edit item functionality
- ❌ Delete item functionality
- ❌ Item detail view/modal
- ❌ Password copy to clipboard
- ❌ Password visibility toggle
- ❌ OAuth authentication (Google, GitHub, X)
- ❌ Remember me with PIN protection
- ❌ Forgot password flow
- ❌ Search and filter
- ❌ Vault settings (edit, delete, share)
- ❌ Real JWT parsing (currently uses dev token format)

---

## Success Criteria

All of these should work:
- ✅ User can register a new account
- ✅ User can login with credentials
- ✅ User stays logged in on page refresh
- ✅ User is redirected to login if not authenticated
- ✅ Vaults page loads user's vaults from API
- ✅ Vault detail page loads vault and items from API
- ✅ Loading states display during API calls
- ✅ Error messages display on failures
- ✅ Empty states display when no data exists
- ✅ Session expires when tab closes (sessionStorage)
- ✅ UI is responsive and styled correctly

---

## Troubleshooting

### "Failed to load vaults" error
- Check PassManAPI is running on correct port
- Check appsettings.Development.json has correct ApiBaseUrl
- Check browser Network tab for 404 or 500 errors
- Check backend logs for errors

### "Network request failed" error
- API is not running or not accessible
- Check Docker containers: `docker-compose ps`
- Check firewall/port forwarding

### "User not found" after registration
- Check MySQL connection
- Check Migrations are applied: `dotnet ef database update`
- Check Users table: `mysql -u root -phihi -e "USE passManDB; SELECT * FROM Users;"`

### Vaults page shows empty state but vaults exist
- Check userId is correct in sessionStorage
- Check API returns vaults for that user: `curl http://localhost:5246/api/vaults?userId=1`
- Check browser Network tab for API response

### Build errors
- Clean and rebuild: `dotnet clean; dotnet build`
- Check all service registrations in Program.cs
- Check all using directives in Razor files

---

## Next Steps After Testing

Once all tests pass:
1. Commit changes to branch `Alex/feature/api-service-layer`
2. Push to remote
3. Create Pull Request to merge into `develop`
4. In PR description, reference this testing guide
5. Ask team member to review and test
6. After approval, merge and delete feature branch

---

## Questions or Issues?

If you encounter any issues during testing:
1. Check browser DevTools Console for errors
2. Check browser DevTools Network tab for failed requests
3. Check backend API logs
4. Reference the API_INTEGRATION_SUMMARY.md for architecture details
5. Ask the team for help in Slack/Discord/etc.
