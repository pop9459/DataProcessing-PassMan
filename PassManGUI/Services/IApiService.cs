using PassManGUI.Models;

namespace PassManGUI.Services;

/// <summary>
/// Interface for API service to communicate with PassManAPI backend
/// </summary>
public interface IApiService
{
    // Authentication
    Task<ApiResponse<AuthResponse>> LoginAsync(LoginRequest request);
    Task<ApiResponse<AuthResponse>> RegisterAsync(RegisterRequest request);
    Task<ApiResponse<UserProfile>> GetCurrentUserAsync();

    // Vaults
    Task<ApiResponse<List<VaultResponse>>> GetVaultsAsync(int userId);
    Task<ApiResponse<VaultResponse>> GetVaultByIdAsync(int vaultId);
    
    // Vault Items (Credentials)
    Task<ApiResponse<List<VaultItemModel>>> GetVaultItemsAsync(int vaultId);
    Task<ApiResponse<VaultItemModel>> GetVaultItemByIdAsync(int itemId);
    Task<ApiResponse<VaultItemModel>> CreateVaultItemAsync(CreateVaultItemRequest request);
    Task<ApiResponse<VaultItemModel>> UpdateVaultItemAsync(int itemId, UpdateVaultItemRequest request);
    Task<ApiResponse<bool>> DeleteVaultItemAsync(int itemId);
}
