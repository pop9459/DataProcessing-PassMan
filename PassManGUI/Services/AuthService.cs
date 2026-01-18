using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.JSInterop;
using PassManGUI.Models;

namespace PassManGUI.Services;

/// <summary>
/// Authentication service that handles login, registration, and token management
/// Stores tokens in sessionStorage for security (token expires when browser closes)
/// 
/// TODO: Future Enhancement - PIN Protection Feature
/// - After successful login, prompt user to set an optional 4-6 digit PIN
/// - If PIN is set, store encrypted token in localStorage (persists across sessions)
/// - On app startup, if token exists in localStorage, require PIN entry to decrypt
/// - This provides convenience (stay logged in) + security (PIN required to access)
/// - If user declines PIN, fall back to sessionStorage (current behavior)
/// </summary>
public class AuthService
{
    private readonly IApiService _apiService;
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<AuthService> _logger;
    
    private const string TokenKey = "auth_token";
    private const string UserIdKey = "user_id";

    private UserProfile? _currentUser;

    public AuthService(
        IApiService apiService,
        HttpClient httpClient,
        IJSRuntime jsRuntime,
        ILogger<AuthService> logger)
    {
        _apiService = apiService;
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    /// <summary>
    /// Gets the currently logged-in user profile
    /// </summary>
    public UserProfile? CurrentUser => _currentUser;

    /// <summary>
    /// Checks if user is authenticated
    /// </summary>
    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetTokenAsync();
        return !string.IsNullOrEmpty(token);
    }

    /// <summary>
    /// Authenticates user with email and password
    /// </summary>
    public async Task<ApiResponse<UserProfile>> LoginAsync(string email, string password)
    {
        try
        {
            var request = new LoginRequest
            {
                Email = email,
                Password = password
            };

            var response = await _apiService.LoginAsync(request);

            if (response.Success && response.Data != null)
            {
                // Store token and user info
                await SetTokenAsync(response.Data.AccessToken);
                await SetUserIdAsync(response.Data.User.Id);
                _currentUser = response.Data.User;

                // Set authorization header for future requests
                SetAuthorizationHeader(response.Data.AccessToken);

                return ApiResponse<UserProfile>.SuccessResponse(response.Data.User);
            }

            return ApiResponse<UserProfile>.ErrorResponse(
                response.ErrorMessage ?? "Login failed",
                response.StatusCode ?? 401
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return ApiResponse<UserProfile>.ErrorResponse("An error occurred during login");
        }
    }

    /// <summary>
    /// Authenticates user with Google ID Token
    /// </summary>
    public async Task<ApiResponse<AuthResponse>> LoginWithGoogleAsync(string idToken)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/google", new { IdToken = idToken });

            if (response.IsSuccessStatusCode)
            {
                var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
                return ApiResponse<AuthResponse>.SuccessResponse(authResponse!);
            }

            var errorMessage = await response.Content.ReadAsStringAsync();
            return ApiResponse<AuthResponse>.ErrorResponse(errorMessage, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Google login");
            return ApiResponse<AuthResponse>.ErrorResponse("An error occurred during Google login");
        }
    }

    /// <summary>
    /// Registers a new user account
    /// </summary>
    public async Task<ApiResponse<UserProfile>> RegisterAsync(
        string email,
        string password,
        string confirmPassword,
        string? userName = null,
        string? phoneNumber = null)
    {
        try
        {
            var request = new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = confirmPassword,
                UserName = userName,
                PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber
            };

            var response = await _apiService.RegisterAsync(request);

            if (response.Success && response.Data != null)
            {
                // Auto-login after successful registration
                await SetTokenAsync(response.Data.AccessToken);
                await SetUserIdAsync(response.Data.User.Id);
                _currentUser = response.Data.User;

                SetAuthorizationHeader(response.Data.AccessToken);

                return ApiResponse<UserProfile>.SuccessResponse(response.Data.User);
            }

            return ApiResponse<UserProfile>.ErrorResponse(
                response.ErrorMessage ?? "Registration failed",
                response.StatusCode ?? 400
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            return ApiResponse<UserProfile>.ErrorResponse("An error occurred during registration");
        }
    }

    /// <summary>
    /// Logs out the current user and clears stored data
    /// </summary>
    public async Task LogoutAsync()
    {
        await RemoveTokenAsync();
        await RemoveUserIdAsync();
        _currentUser = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>
    /// Gets the current user ID from storage
    /// </summary>
    public async Task<int?> GetUserIdAsync()
    {
        try
        {
            var userIdStr = await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", UserIdKey);
            if (int.TryParse(userIdStr, out var userId))
            {
                return userId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user ID from session storage");
        }
        return null;
    }

    /// <summary>
    /// Initializes authentication state (call on app startup)
    /// </summary>
    public async Task InitializeAsync()
    {
        var token = await GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            SetAuthorizationHeader(token);
            
            // Try to fetch current user profile
            var response = await _apiService.GetCurrentUserAsync();
            if (response.Success && response.Data != null)
            {
                _currentUser = response.Data;
            }
            else
            {
                // Token is invalid, clear it
                await LogoutAsync();
            }
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Extracts user ID from dev token (format: "dev-token-123")
    /// TODO: Replace with real JWT parsing when backend is upgraded
    /// </summary>
    private int? ExtractUserIdFromToken(string token)
    {
        try
        {
            // Dev token format: "dev-token-{userId}"
            if (token.StartsWith("dev-token-"))
            {
                var userIdStr = token.Replace("dev-token-", "");
                if (int.TryParse(userIdStr, out var userId))
                {
                    return userId;
                }
            }
            
            // TODO: For real JWT, use JWT library to decode and extract userId from claims
            // Example:
            // var handler = new JwtSecurityTokenHandler();
            // var jwtToken = handler.ReadJwtToken(token);
            // var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == "userId");
            // return int.Parse(userIdClaim.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting user ID from token");
        }
        return null;
    }

    private async Task<string?> GetTokenAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", TokenKey);
        }
        catch
        {
            return null;
        }
    }

    private async Task SetTokenAsync(string token)
    {
        await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", TokenKey, token);
    }

    private async Task RemoveTokenAsync()
    {
        await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", TokenKey);
    }

    private async Task SetUserIdAsync(int userId)
    {
        await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", UserIdKey, userId.ToString());
    }

    private async Task RemoveUserIdAsync()
    {
        await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", UserIdKey);
    }

    private void SetAuthorizationHeader(string token)
    {
        // Set Authorization header for all future API requests
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);
        
        // Also set X-UserId header for dev token compatibility
        var userId = ExtractUserIdFromToken(token);
        if (userId.HasValue)
        {
            _httpClient.DefaultRequestHeaders.Remove("X-UserId");
            _httpClient.DefaultRequestHeaders.Add("X-UserId", userId.Value.ToString());
        }
    }

    #endregion
}
