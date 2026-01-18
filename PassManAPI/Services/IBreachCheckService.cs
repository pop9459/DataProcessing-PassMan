namespace PassManAPI.Services;

/// <summary>
/// Service interface for checking passwords against known data breaches.
/// Uses the Have I Been Pwned API with k-Anonymity model.
/// </summary>
public interface IBreachCheckService
{
    /// <summary>
    /// Checks if a password has been exposed in known data breaches.
    /// Uses k-Anonymity to protect the password during the check.
    /// </summary>
    /// <param name="password">The password to check.</param>
    /// <returns>True if the password has been breached, false otherwise.</returns>
    Task<bool> IsPasswordBreachedAsync(string password);

    /// <summary>
    /// Gets the number of times a password has appeared in known breaches.
    /// </summary>
    /// <param name="password">The password to check.</param>
    /// <returns>The breach count, or 0 if never breached.</returns>
    Task<int> GetBreachCountAsync(string password);

    /// <summary>
    /// Checks if an email address has been involved in known data breaches.
    /// Note: Requires API key for HIBP API v3.
    /// </summary>
    /// <param name="email">The email address to check.</param>
    /// <returns>A list of breach names the email was found in.</returns>
    Task<IList<string>> GetEmailBreachesAsync(string email);
}
