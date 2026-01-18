namespace PassManAPI.Services;

/// <summary>
/// Service interface for encrypting and decrypting credential passwords.
/// Uses AES-256-GCM for authenticated encryption with per-credential keys.
/// </summary>
public interface IPasswordEncryptionService
{
    /// <summary>
    /// Generates a unique encryption key for a single credential.
    /// </summary>
    /// <returns>A 32-byte (256-bit) random key for the credential.</returns>
    byte[] GeneratePerCredentialKey();

    /// <summary>
    /// Encrypts a per-credential key using the user's master password.
    /// This allows the credential key to be stored securely.
    /// </summary>
    /// <param name="perKey">The per-credential key to encrypt.</param>
    /// <param name="masterPassword">The user's master password.</param>
    /// <returns>The encrypted per-credential key.</returns>
    byte[] EncryptPerCredentialKey(byte[] perKey, string masterPassword);

    /// <summary>
    /// Decrypts a per-credential key using the user's master password.
    /// </summary>
    /// <param name="encryptedKey">The encrypted per-credential key.</param>
    /// <param name="masterPassword">The user's master password.</param>
    /// <returns>The decrypted per-credential key.</returns>
    byte[] DecryptPerCredentialKey(byte[] encryptedKey, string masterPassword);

    /// <summary>
    /// Encrypts a plaintext password using the per-credential key.
    /// </summary>
    /// <param name="plaintext">The password to encrypt.</param>
    /// <param name="encryptedKey">The per-credential key.</param>
    /// <returns>The encrypted password bytes.</returns>
    byte[] EncryptPassword(string plaintext, byte[] encryptedKey);

    /// <summary>
    /// Decrypts an encrypted password using the per-credential key.
    /// </summary>
    /// <param name="encrypted">The encrypted password bytes.</param>
    /// <param name="encryptedKey">The per-credential key.</param>
    /// <returns>The decrypted plaintext password.</returns>
    string DecryptPassword(byte[] encrypted, byte[] encryptedKey);

    /// <summary>
    /// Low-level AES encryption of raw data.
    /// </summary>
    /// <param name="data">The data to encrypt.</param>
    /// <param name="key">The encryption key.</param>
    /// <returns>The encrypted data with nonce and tag.</returns>
    byte[] AesEncrypt(byte[] data, byte[] key);

    /// <summary>
    /// Low-level AES decryption of raw data.
    /// </summary>
    /// <param name="data">The encrypted data with nonce and tag.</param>
    /// <param name="key">The decryption key.</param>
    /// <returns>The decrypted data.</returns>
    byte[] AesDecrypt(byte[] data, byte[] key);
}
