using System.Net.Http.Json;
using System.Text.Json;
using PassManGUI.Models;

namespace PassManGUI.Services;

/// <summary>
/// Implementation of API service using HttpClient
/// </summary>
public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiService(HttpClient httpClient, ILogger<ApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
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

    #endregion

    #region Vault Items

    public async Task<ApiResponse<List<VaultItemModel>>> GetVaultItemsAsync(int vaultId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/credentials?vaultId={vaultId}");
            
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

    public async Task<ApiResponse<VaultItemModel>> GetVaultItemByIdAsync(int itemId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/credentials/{itemId}");
            
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

    public async Task<ApiResponse<VaultItemModel>> CreateVaultItemAsync(CreateVaultItemRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/credentials", request);
            
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

    public async Task<ApiResponse<VaultItemModel>> UpdateVaultItemAsync(int itemId, UpdateVaultItemRequest request)
    {
        try
        {
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

    public async Task<ApiResponse<bool>> DeleteVaultItemAsync(int itemId)
    {
        try
        {
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
