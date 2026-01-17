# Google Authentication Implementation & Architecture

This document explains the technical implementation of Google OAuth2 in the PassMan project.

## Architecture Guidelines

The key design constraint for this implementation is the separation between the **Frontend (`PassManGUI`)** and the **Backend (`PassManAPI`)**.
*   **Monolithic Blazor** (Standard): Usually, Blazor Server talks directly to the database and uses ASP.NET Core Identity Cookies directly.
*   **PassMan Architecture**: `PassManGUI` is a UI client that treats `PassManAPI` as an external service. It cannot "log in" the user directly; it must obtain an **API Token** from the backend.

Therefore, we implemented a **Token Exchange Flow**.

## Authentication Flow

Here is the step-by-step definition of how a user logs in with Google:

1.  **Initiation**:
    *   User clicks "Sign in with Google" on `SignIn.razor`.
    *   Browser navigates to `/login/google` (handled by `LoginController.cs`).

2.  **OAuth Challenge**:
    *   `LoginController` issues a Challenge using the `Google` authentication scheme.
    *   User is redirected to Google's permission screen.

3.  **Callback & Token Retrieval**:
    *   Google redirects back to `/login/google-callback`.
    *   ASP.NET Core Middleware accepts the callback and temporarily signs the user in via a Cookie.
    *   **Crucial Step**: We use `OnCreatingTicket` in `Program.cs` to forcibly capture the **Google ID Token** (`id_token`) and store it in the cookie properties.

4.  **Token Exchange (The Bridge)**:
    *   `LoginController` retrieves the `id_token` from the cookie.
    *   It calls `AuthService.LoginWithGoogleAsync(idToken)`, which sends a `POST` request to the Backend API (`/api/auth/google`).

5.  **Backend Verification**:
    *   `PassManAPI` receives the `ID Token`.
    *   It verifies the token's signature using `Google.Apis.Auth`.
    *   It checks against the `Users` database table:
        *   **Existing User**: Logs them in.
        *   **New User**: Automatically registers them.
    *   The API generates and returns an **Internal App Token** (`dev-token-XYZ`).

6.  **Client Session Storage**:
    *   `LoginController` receives the App Token.
    *   It cannot save the token to the browser's Storage directly (Storage is client-side, Controller is server-side).
    *   It redirects the user to a temporary "Bridge Page": `/login-check?token=...`.
    *   `LoginCheck.razor` (Interactive Mode) activates, grabs the token from the URL, calls JavaScript to save it to `sessionStorage`, and redirects to `/vaults`.

## Key Files

### Backend (`PassManAPI`)
*   **`Controllers/AuthController.cs`**: Contains the `[HttpPost("google")]` endpoint that verifies the external token and issues the internal one.
*   **`DTOs/GoogleLoginRequest.cs`**: Simple Data Transfer Object for carrying the token.

### Frontend (`PassManGUI`)
*   **`Program.cs`**:
    *   Configures `AddAuthentication().AddCookie().AddGoogle()`.
    *   **Important**: Contains the `OnCreatingTicket` event handler to ensure `id_token` is persisted.
    *   **Important**: Explicitly requests `openid`, `email`, `profile` scopes.
*   **`Controllers/LoginController.cs`**:
    *   Handles the ASP.NET Core MVC redirect dance.
    *   Acts as the orchestrator between Google and our API.
*   **`Services/AuthService.cs`**:
    *   `LoginWithGoogleAsync`: Performs the HTTP POST to the backend.
*   **`Components/Pages/LoginCheck.razor`**:
    *   The "Bridge" page. Must be `@rendermode InteractiveServer` to handle JavaScript storage.

## Troubleshooting

If `NoToken` errors occur in the future, check:
1.  **Scopes**: Are `openid` and `email` requested?
2.  **SaveTokens**: Is `OnCreatingTicket` correctly capturing the token?
3.  **Redirect URI**: Does `appsettings.json` match the Google Cloud Console Authorized Redirect URIs?
