using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PassManAPI.Services;

/// <summary>
/// Service for checking passwords against the Have I Been Pwned database.
/// Implements k-Anonymity model to protect password privacy during checks.
/// </summary>
public class BreachCheckService : IBreachCheckService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BreachCheckService> _logger;
    private readonly BreachCheckSettings _settings;

    private const string PwnedPasswordsApiUrl = "https://api.pwnedpasswords.com/range/";
    private const string PwnedBreachesApiUrl = "https://haveibeenpwned.com/api/v3/breachedaccount/";

    public BreachCheckService(
        HttpClient httpClient,
        ILogger<BreachCheckService> logger,
        IOptions<BreachCheckSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value;

        // Set user agent as required by HIBP API
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PassMan-PasswordManager/1.0");

        // Add API key for breach lookups if configured
        if (!string.IsNullOrEmpty(_settings.HibpApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("hibp-api-key", _settings.HibpApiKey);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsPasswordBreachedAsync(string password)
    {
        var count = await GetBreachCountAsync(password);
        return count > 0;
    }

    /// <inheritdoc />
    public async Task<int> GetBreachCountAsync(string password)
    {
        if (string.IsNullOrEmpty(password))
            return 0;

        try
        {
            // Hash the password using SHA-1
            var sha1Hash = ComputeSha1Hash(password);
            var prefix = sha1Hash[..5];
            var suffix = sha1Hash[5..];

            // Send only the first 5 characters (k-Anonymity)
            var response = await _httpClient.GetStringAsync($"{PwnedPasswordsApiUrl}{prefix}");

            // Parse the response to find matching suffix
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var parts = line.Split(':');
                if (parts.Length == 2)
                {
                    var hashSuffix = parts[0].Trim();
                    if (string.Equals(hashSuffix, suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(parts[1].Trim(), out var count))
                        {
                            _logger.LogInformation("Password breach check completed. Found in {Count} breaches.", count);
                            return count;
                        }
                    }
                }
            }

            _logger.LogInformation("Password breach check completed. Password not found in any breaches.");
            return 0;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to check password against HIBP API.");
            throw new BreachCheckException("Failed to check password against breach database.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<IList<string>> GetEmailBreachesAsync(string email)
    {
        if (string.IsNullOrEmpty(email))
            return Array.Empty<string>();

        if (string.IsNullOrEmpty(_settings.HibpApiKey))
        {
            _logger.LogWarning("HIBP API key not configured. Email breach check requires an API key.");
            throw new BreachCheckException("Email breach check requires an HIBP API key.");
        }

        try
        {
            var encodedEmail = Uri.EscapeDataString(email);
            var response = await _httpClient.GetAsync($"{PwnedBreachesApiUrl}{encodedEmail}?truncateResponse=true");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Email {Email} not found in any breaches.", email);
                return Array.Empty<string>();
            }

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var breaches = JsonSerializer.Deserialize<List<BreachInfo>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var breachNames = breaches?.Select(b => b.Name ?? "Unknown").ToList() ?? new List<string>();

            _logger.LogInformation("Email {Email} found in {Count} breaches.", email, breachNames.Count);
            return breachNames;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning("HIBP API rate limit exceeded.");
            throw new BreachCheckException("Rate limit exceeded. Please try again later.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to check email {Email} against HIBP API.", email);
            throw new BreachCheckException("Failed to check email against breach database.", ex);
        }
    }

    private static string ComputeSha1Hash(string input)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA1.HashData(inputBytes);
        return Convert.ToHexString(hashBytes);
    }

    private class BreachInfo
    {
        public string? Name { get; set; }
    }
}

/// <summary>
/// Configuration settings for breach checking service.
/// </summary>
public class BreachCheckSettings
{
    public const string SectionName = "BreachCheck";

    /// <summary>
    /// Optional API key for Have I Been Pwned API.
    /// Required for email breach lookups.
    /// </summary>
    public string? HibpApiKey { get; set; }
}

/// <summary>
/// Exception thrown when breach check operations fail.
/// </summary>
public class BreachCheckException : Exception
{
    public BreachCheckException(string message) : base(message) { }
    public BreachCheckException(string message, Exception innerException) : base(message, innerException) { }
}
