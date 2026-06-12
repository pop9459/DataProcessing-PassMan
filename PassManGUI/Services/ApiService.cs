using System.Net.Http.Json;
using System.Text.Json;
using PassManGUI.Models;
using Microsoft.JSInterop;

namespace PassManGUI.Services;

/// <summary>
/// Implementation of API service using HttpClient
/// </summary>
public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiService> _logger;
    private readonly IJSRuntime _jsRuntime;
    private readonly JsonSerializerOptions _jsonOptions;
    private const string UserIdKey = "passman_userId";

    public ApiService(HttpClient httpClient, ILogger<ApiService> logger, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsRuntime = jsRuntime;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }
    
    /// <summary>
    /// Ensures the X-UserId header is set for authenticated API calls
    /// </summary>
    private async Task EnsureAuthHeadersAsync()
    {
        try
        {
            var userIdStr = await _jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", UserIdKey);
            if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out var userId))
            {
                _httpClient.DefaultRequestHeaders.Remove("X-UserId");
                _httpClient.DefaultRequestHeaders.Add("X-UserId", userId.ToString());
            }
        }
        catch (InvalidOperationException)
        {
            // JS interop not available during prerendering - skip header setup
        }
    }

    #region Authentication

    public async Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request);
            
            if (response.IsSuccessStatusCode)
            {
                var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);
                return ApiResponse<AuthResponse>.SuccessResponse(authResponse!);
            }

            var errorMessage = await response.Content.ReadAsStringAsync();
            return ApiResponse<AuthResponse>.ErrorResponse(errorMessage, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return ApiResponse<AuthResponse>.ErrorResponse("Network error. Please try again.");
        }
    }

    public async Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request);
            
            if (response.IsSuccessStatusCode)
            {
                var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions);
                return ApiResponse<AuthResponse>.SuccessResponse(authResponse!);
            }

            var errorMessage = await response.Content.ReadAsStringAsync();
            return ApiResponse<AuthResponse>.ErrorResponse(errorMessage, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            return ApiResponse<AuthResponse>.ErrorResponse("Network error. Please try again.");
        }
    }

    public async Task<ApiResponse<UserProfile>> GetCurrentUserAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/auth/me");
            
            if (response.IsSuccessStatusCode)
            {
                var profile = await response.Content.ReadFromJsonAsync<UserProfile>(_jsonOptions);
                return ApiResponse<UserProfile>.SuccessResponse(profile!);
            }

            var errorMessage = await response.Content.ReadAsStringAsync();
            return ApiResponse<UserProfile>.ErrorResponse(errorMessage, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching current user");
            return ApiResponse<UserProfile>.ErrorResponse("Network error. Please try again.");
        }
    }

    #endregion

    #region Vaults

    public async Task<ApiResponse<List<VaultResponse>>> GetVaultsAsync(int userId)
    {
        try
        {
            await EnsureAuthHeadersAsync();
            var response = await _httpClient.GetAsync($"/api/vaults?userId={userId}");
            
            if (response.IsSuccessStatusCode)
            {
                var vaults = await response.Content.ReadFromJsonAsync<List<VaultResponse>>(_jsonOptions);
                return ApiResponse<List<VaultResponse>>.SuccessResponse(vaults!);
            }

            var errorMessage = await response.Content.ReadAsStringAsync();
            return ApiResponse<List<VaultResponse>>.ErrorResponse(errorMessage, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vaults for user {UserId}", userId);
            return ApiResponse<List<VaultResponse>>.ErrorResponse("Network error. Please try again.");
        }
    }

    public async Task<ApiResponse<VaultResponse>> GetVaultByIdAsync(int vaultId)
    {
        try
        {
            await EnsureAuthHeadersAsync();
            var response = await _httpClient.GetAsync($"/api/vaults/{vaultId}");
            
            if (response.IsSuccessStatusCode)
            {
                var vault = await response.Content.ReadFromJsonAsync<VaultResponse>(_jsonOptions);
                return ApiResponse<VaultResponse>.SuccessResponse(vault!);
            }

            var errorMessage = await response.Content.ReadAsStringAsync();
            return ApiResponse<VaultResponse>.ErrorResponse(errorMessage, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vault {VaultId}", vaultId);
            return ApiResponse<VaultResponse>.ErrorResponse("Network error. Please try again.");
        }
    }

    public async Task<ApiResponse<VaultResponse>> CreateVaultAsync(CreateVaultRequest request)
    {
        try
        {
            await EnsureAuthHeadersAsync();
            var response = await _httpClient.PostAsJsonAsync("/api/vaults", request);
            
            if (response.IsSuccessStatusCode)
            {
                var vault = await response.Content.ReadFromJsonAsync<VaultResponse>(_jsonOptions);
                return ApiResponse<VaultResponse>.SuccessResponse(vault!);
            }

            var errorMessage = await response.Content.ReadAsStringAsync();
            return ApiResponse<VaultResponse>.ErrorResponse(errorMessage, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating vault");
            return ApiResponse<VaultResponse>.ErrorResponse("Network error. Please try again.");
        }
    }

    public async Task<ApiResponse<VaultResponse>> UpdateVaultAsync(int vaultId, UpdateVaultRequest request)
    {
        try
        {
            await EnsureAuthHeadersAsync();
            var response = await _httpClient.PutAsJsonAsync($"/api/vaults/{vaultId}", request);
            
            if (response.IsSuccessStatusCode)
            {
                var vault = await response.Content.ReadFromJsonAsync<VaultResponse>(_jsonOptions);
                return ApiResponse<VaultResponse>.SuccessResponse(vault!);
            }

            var errorMessage = await response.Content.ReadAsStringAsync();
            return ApiResponse<VaultResponse>.ErrorResponse(errorMessage, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating vault {VaultId}", vaultId);
            return ApiResponse<VaultResponse>.ErrorResponse("Network error. Please try again.");
        }
    }

    public async Task<ApiResponse<bool>> DeleteVaultAsync(int vaultId)
    {
        try
        {
            await EnsureAuthHeadersAsync();
            var response = await _httpClient.DeleteAsync($"/api/vaults/{vaultId}");
            
            if (response.IsSuccessStatusCode)
            {
                return ApiResponse<bool>.SuccessResponse(true);
            }

            var errorMessage = await response.Content.ReadAsStringAsync();
            return ApiResponse<bool>.ErrorResponse(errorMessage, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting vault {VaultId}", vaultId);
            return ApiResponse<bool>.ErrorResponse("Network error. Please try again.");
        }
    }

    #endregion

    #region Vault Items

    public async Task<ApiResponse<List<VaultItemModel>>> GetVaultItemsAsync(int vaultId)
    {
        try
        {
            await EnsureAuthHeadersAsync();
            var response = await _httpClient.GetAsync($"/api/vaults/{vaultId}/credentials");
            
            if (response.IsSuccessStatusCode)
            {
                var items = await response.Content.ReadFromJsonAsync<List<VaultItemModel>>(_jsonOptions);
                return ApiResponse<List<VaultItemModel>>.SuccessResponse(items!);
            }

            var errorMessage = await response.Content.ReadAsStringAsync();
            return ApiResponse<List<VaultItemModel>>.ErrorResponse(errorMessage, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vault items for vault {VaultId}", vaultId);
            return ApiResponse<List<VaultItemModel>>.ErrorResponse("Network error. Please try again.");
        }
    }

    public async Task<ApiResponse<VaultItemModel>> GetVaultItemByIdAsync(int vaultId, int itemId)
    {
        try
        {
            await EnsureAuthHeadersAsync();
            var response = await _httpClient.GetAsync($"/api/vaults/{vaultId}/credentials/{itemId}");
            
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<VaultItemModel>(_jsonOptions);
                return ApiResponse<VaultItemModel>.SuccessResponse(item!);
            }

            var errorMessage = await response.Content.ReadAsStringAsync();
            return ApiResponse<VaultItemModel>.ErrorResponse(errorMessage, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vault item {ItemId}", itemId);
            return ApiResponse<VaultItemModel>.ErrorResponse("Network error. Please try again.");
        }
    }

    public async Task<ApiResponse<VaultItemModel>> CreateVaultItemAsync(int vaultId, CreateVaultItemRequest request)
    {
        try
        {
            await EnsureAuthHeadersAsync();
            var response = await _httpClient.PostAsJsonAsync($"/api/vaults/{vaultId}/credentials", request);
            
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<VaultItemModel>(_jsonOptions);
                return ApiResponse<VaultItemModel>.SuccessResponse(item!);
            }

            var errorMessage = await response.Content.ReadAsStringAsync();
            return ApiResponse<VaultItemModel>.ErrorResponse(errorMessage, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating vault item");
            return ApiResponse<VaultItemModel>.ErrorResponse("Network error. Please try again.");
        }
    }

    public async Task<ApiResponse<VaultItemModel>> UpdateVaultItemAsync(int vaultId, int itemId, UpdateVaultItemRequest request)
    {
        try
        {
            await EnsureAuthHeadersAsync();
            var response = await _httpClient.PutAsJsonAsync($"/api/credentials/{itemId}", request);
            
            if (response.IsSuccessStatusCode)
            {
                var item = await response.Content.ReadFromJsonAsync<VaultItemModel>(_jsonOptions);
                return ApiResponse<VaultItemModel>.SuccessResponse(item!);
            }

            var errorMessage = await response.Content.ReadAsStringAsync();
            return ApiResponse<VaultItemModel>.ErrorResponse(errorMessage, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating vault item {ItemId}", itemId);
            return ApiResponse<VaultItemModel>.ErrorResponse("Network error. Please try again.");
        }
    }

    public async Task<ApiResponse<bool>> DeleteVaultItemAsync(int vaultId, int itemId)
    {
        try
        {
            await EnsureAuthHeadersAsync();
            var response = await _httpClient.DeleteAsync($"/api/credentials/{itemId}");
            
            if (response.IsSuccessStatusCode)
            {
                return ApiResponse<bool>.SuccessResponse(true);
            }

            var errorMessage = await response.Content.ReadAsStringAsync();
            return ApiResponse<bool>.ErrorResponse(errorMessage, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting vault item {ItemId}", itemId);
            return ApiResponse<bool>.ErrorResponse("Network error. Please try again.");
        }
    }

    #endregion
}
