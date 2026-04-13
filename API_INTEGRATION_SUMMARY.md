# API Integration Summary

## ✅ Completed Implementation

This document summarizes the API integration work completed for connecting the PassManGUI frontend to the PassManAPI backend.

### Phase 1: API Service Layer ✅

**Created Models** (`PassManGUI/Models/`):
- `AuthModels.cs` - Login/Register requests, AuthResponse, UserProfile
- `VaultModels.cs` - VaultResponse, VaultItemModel with time formatting helper
- `ApiResponse.cs` - Generic API response wrapper with success/error factory methods

**Created Services** (`PassManGUI/Services/`):
- `IApiService.cs` - Interface defining all API operations
- `ApiService.cs` - HttpClient implementation for API calls
  - Authentication: Login, Register, GetCurrentUser
  - Vaults: GetVaults, GetVaultById
  - Credentials: CRUD operations for vault items
  - Error handling and logging throughout
- `AuthService.cs` - Authentication and token management
  - JWT token storage using sessionStorage (secure, expires on tab close)
  - Real JWT parsing with System.IdentityModel.Tokens.Jwt
  - Authorization header setup (Bearer token)
  - Session restoration on app startup
  - TODO: PIN feature for localStorage persistence

**Registered Services** (`PassManGUI/Program.cs`):
- Added `IApiService` and `ApiService` as scoped services
- Added `AuthService` as scoped service
- HttpClient already configured with base URL from appsettings

### Phase 2: Authentication Pages ✅

**Updated SignIn.razor**:
- Injected AuthService
- Added form validation
- Loading states and error messages
- Calls AuthService.LoginAsync on form submit
- Redirects to /vaults on success
- Error message display with styled component

**Updated SignUp.razor**:
- Injected AuthService
- Added form validation (email, password, password match, terms agreement)
- Loading states and error messages
- Calls AuthService.RegisterAsync on form submit
- Auto-login after successful registration
- Redirects to /vaults on success

**Added CSS Styles** (`wwwroot/app.css`):
- `.error-message` - Error display component with icon
- `.loading-state` - Loading spinner and message
- `.error-state` - Error state for pages
- `.empty-state` - Empty state for pages
- `.spinner` - Animated loading spinner

### Phase 3: Vault Pages ✅

**Updated Vaults.razor**:
- Injected AuthService and IApiService
- Authentication check on page load (redirects to /login if not authenticated)
- Calls ApiService.GetVaultsAsync to load user's vaults
- Loading, error, and empty states
- Displays vault cards with name, description, and timestamps
- Navigation to vault detail page on card click

**Updated VaultDetail.razor**:
- Injected AuthService and IApiService
- Authentication check on page load
- Loads vault details with ApiService.GetVaultByIdAsync
- Loads vault items with ApiService.GetVaultItemsAsync
- Loading, error, and empty states
- Displays item cards with name, username, URL, label
- FormatTimeAgo helper for friendly timestamps

**Added CSS Styles** (`wwwroot/app.css`):
- `.vault-card` - Vault card styles with hover effects
- `.item-card` - Item card styles for credentials
- `.item-label` - Badge-style label for items
- `.icon-btn` - Icon button styles

### Phase 4: Configuration ✅

**Updated appsettings.Development.json**:
- Set ApiBaseUrl to `http://localhost:5246` (for local development)
- Docker environment uses `http://passman-api:8080` (set in docker-compose.yml)

**Updated Routes.razor**:
- Added AuthService initialization
- Calls InitializeAsync on app startup to restore authentication session

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                         PassManGUI                          │
│                    (Blazor Server - .NET 10)                │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Pages:                                                     │
│  ├─ SignIn.razor ──────────┐                                │
│  ├─ SignUp.razor ──────────┼──> AuthService                 │
│  ├─ Vaults.razor ──────────┤                                │
│  └─ VaultDetail.razor ─────┴──> IApiService / ApiService    │
│                                                             │
│  Services:                                                  │
│  ├─ AuthService (token management, sessionStorage)          │
│  └─ ApiService (HTTP calls to backend)                      │
│                                                             │
│  Models:                                                    │
│  ├─ AuthModels (Login/Register/UserProfile)                 │
│  └─ VaultModels (Vault/Item responses)                      │
│                                                             │
└───────────────────┬─────────────────────────────────────────┘
                    │ HTTP (port 5246 local, :8080 docker)
                    ▼
┌─────────────────────────────────────────────────────────────┐
│                         PassManAPI                          │
│                   (ASP.NET Core - .NET 10)                  │
├─────────────────────────────────────────────────────────────┤
│  Controllers:                                               │
│  ├─ /api/auth/login                                         │
│  ├─ /api/auth/register                                      │
│  ├─ /api/auth/me                                            │
│  ├─ /api/vaults?userId={id}                                 │
│  ├─ /api/vaults/{id}                                        │
│  ├─ /api/credentials?vaultId={id}                           │
│  └─ /api/credentials/{id} (GET/POST/PUT/DELETE)             │
└───────────────────┬─────────────────────────────────────────┘
                    │
                    ▼
              MySQL Database (port 3306)
```

## Authentication Flow

1. **Login**:
   - User enters credentials on SignIn.razor
   - AuthService.LoginAsync calls ApiService.LoginAsync
   - ApiService calls POST /api/auth/login
   - Backend returns JWT access token
   - AuthService stores token in sessionStorage
   - JWT contains user claims (sub=userId, email, name)
   - Sets Authorization header: `Bearer {jwt-token}`
   - Redirects to /vaults

2. **Session Restoration**:
   - On app startup, Routes.razor calls AuthService.InitializeAsync
   - AuthService checks sessionStorage for token
   - If found, restores Authorization header
   - User stays logged in across page refreshes (within same tab)

3. **API Calls**:
   - Pages inject IApiService
   - Call ApiService methods (GetVaultsAsync, etc.)
   - ApiService uses HttpClient with auth headers already set
   - Backend validates JWT and extracts userId from claims
   - Returns ApiResponse<T> with Success/Data/ErrorMessage

## Security Implementation

- **Token Storage**: sessionStorage (session-scoped, cleared on tab close)
- **JWT Format**: Standard JWT with HS256 signing
- **Token Claims**: UserId (sub, ClaimTypes.NameIdentifier), Email, Username
- **Authorization Header**: Standard Bearer token format
- **Token Generation**: `JwtTokenService` with configurable expiration
- **Future Enhancement**: PIN-protected localStorage (see TODO in AuthService)

## Testing Checklist

- [ ] Run PassManAPI on port 5246
- [ ] Run PassManGUI on port 5247
- [ ] Test registration flow
- [ ] Test login flow
- [ ] Test session persistence (refresh page)
- [ ] Test vault list loading
- [ ] Test vault detail loading
- [ ] Test item list loading
- [ ] Test authentication redirect (access /vaults without login)
- [ ] Test error handling (invalid credentials, network errors)

## Next Steps (Future Work)

1. **Enhanced Token Management**:
   - Implement token refresh mechanism
   - Add token expiration warnings
   - Automatic re-authentication on token expiry

2. **PIN Feature**:
   - Add PIN setup dialog after login (if "Remember me" checked)
   - Encrypt token before storing in localStorage
   - Prompt for PIN on app startup to decrypt token
   - See AuthService TODO for implementation notes

3. **Item Management**:
   - Add item creation modal/page
   - Add item edit functionality
   - Add item delete confirmation
   - Add password copy to clipboard
   - Add password visibility toggle

4. **Vault Management**:
   - Add vault creation modal
   - Add vault edit functionality
   - Add vault delete confirmation
   - Add vault sharing

5. **Enhanced Features**:
   - Search and filter for vaults/items
   - Tags and categories
   - Password strength indicator
   - Password generator
   - Export/import functionality

## Files Modified

### Created Files:
- PassManGUI/Models/AuthModels.cs
- PassManGUI/Models/VaultModels.cs
- PassManGUI/Models/ApiResponse.cs
- PassManGUI/Services/IApiService.cs
- PassManGUI/Services/ApiService.cs
- PassManGUI/Services/AuthService.cs

### Modified Files:
- PassManGUI/Program.cs (service registration)
- PassManGUI/Components/Routes.razor (auth initialization)
- PassManGUI/Components/Pages/SignIn.razor (API integration)
- PassManGUI/Components/Pages/SignUp.razor (API integration)
- PassManGUI/Components/Pages/Vaults.razor (API integration)
- PassManGUI/Components/Pages/VaultDetail.razor (API integration)
- PassManGUI/wwwroot/app.css (new styles)
- PassManGUI/appsettings.Development.json (API URL)

## Notes

- **MockDataService** can now be removed (no longer used)
- All pages now use real API calls
- Error handling is comprehensive with user-friendly messages
- Loading states provide feedback during API calls
- Empty states guide users when no data exists
- Code is structured for easy JWT migration
- TODO comments mark future enhancement points
