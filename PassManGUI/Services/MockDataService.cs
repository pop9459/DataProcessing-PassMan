namespace PassManGUI.Services;

/// <summary>
/// Temporary mock data service for development
/// TODO: Replace with actual API service calls via HttpClient
/// </summary>
public static class MockDataService
{
    private static Dictionary<int, List<VaultItemModel>>? _vaultItems;

    private static Dictionary<int, List<VaultItemModel>> VaultItems
    {
        get
        {
            if (_vaultItems == null)
            {
                InitializeMockData();
            }
            return _vaultItems!;
        }
    }

    private static void InitializeMockData()
    {
        _vaultItems = new Dictionary<int, List<VaultItemModel>>
        {
            {
                1, new List<VaultItemModel> // Personal vault
                {
                    new VaultItemModel
                    {
                        Id = 1,
                        Name = "Gmail Account",
                        Username = "john.doe@gmail.com",
                        Url = "gmail.com",
                        Label = "Personal",
                        CreatedAt = "11 months ago"
                    },
                    new VaultItemModel
                    {
                        Id = 2,
                        Name = "GitHub",
                        Username = "johndoe",
                        Url = "github.com",
                        Label = "",
                        CreatedAt = "8 months ago"
                    },
                    new VaultItemModel
                    {
                        Id = 3,
                        Name = "Netflix",
                        Username = "john.doe@gmail.com",
                        Url = "netflix.com",
                        Label = "Streaming",
                        CreatedAt = "3 months ago"
                    }
                }
            },
            {
                2, new List<VaultItemModel> // Work vault
                {
                    new VaultItemModel
                    {
                        Id = 4,
                        Name = "Office 365",
                        Username = "john.doe@company.com",
                        Url = "office.com",
                        Label = "Work",
                        CreatedAt = "1 year ago"
                    },
                    new VaultItemModel
                    {
                        Id = 5,
                        Name = "Slack",
                        Username = "john.doe@company.com",
                        Url = "slack.com",
                        Label = "",
                        CreatedAt = "2 days ago"
                    }
                }
            },
            { 3, new List<VaultItemModel>() }, // Family vault - empty
            { 4, new List<VaultItemModel>() }, // Social Media vault - empty
            { 5, new List<VaultItemModel>() }  // Financial vault - empty
        };
    }

    public static List<VaultModel> GetVaults()
    {
        return new List<VaultModel>
        {
            new VaultModel
            {
                Id = 1,
                Name = "Personal",
                Description = "Personal accounts and passwords",
                ItemCount = GetVaultItemCount(1),
                LastModified = "11 months ago"
            },
            new VaultModel
            {
                Id = 2,
                Name = "Work",
                Description = "Work-related credentials",
                ItemCount = GetVaultItemCount(2),
                LastModified = "2 days ago"
            },
            new VaultModel
            {
                Id = 3,
                Name = "Family",
                Description = "Shared family accounts",
                ItemCount = GetVaultItemCount(3),
                LastModified = "1 week ago"
            },
            new VaultModel
            {
                Id = 4,
                Name = "Social Media",
                Description = "Social network logins and credentials",
                ItemCount = GetVaultItemCount(4),
                LastModified = "3 days ago"
            },
            new VaultModel
            {
                Id = 5,
                Name = "Financial",
                Description = "Banking and payment service accounts",
                ItemCount = GetVaultItemCount(5),
                LastModified = "1 month ago"
            }
        };
    }

    public static List<VaultItemModel> GetVaultItems(int vaultId)
    {
        return VaultItems.ContainsKey(vaultId) 
            ? VaultItems[vaultId] 
            : new List<VaultItemModel>();
    }

    public static int GetVaultItemCount(int vaultId)
    {
        return GetVaultItems(vaultId).Count;
    }

    public static string GetVaultName(int vaultId)
    {
        return vaultId switch
        {
            1 => "Personal",
            2 => "Work",
            3 => "Family",
            4 => "Social Media",
            5 => "Financial",
            _ => "Unknown Vault"
        };
    }

    public static string GetVaultDescription(int vaultId)
    {
        return vaultId switch
        {
            1 => "Personal accounts and passwords",
            2 => "Work-related credentials",
            3 => "Shared family accounts",
            4 => "Social network logins and credentials",
            5 => "Banking and payment service accounts",
            _ => ""
        };
    }

    public class VaultModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int ItemCount { get; set; }
        public string LastModified { get; set; } = "";
    }

    public class VaultItemModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Url { get; set; } = "";
        public string Label { get; set; } = "";
        public string CreatedAt { get; set; } = "";
    }
}
