using System.Security.Cryptography;
using System.Text;

namespace PassManAPI.Services;

/// <summary>
/// AES-256-GCM encryption service for credential passwords.
/// Implements per-credential key encryption as defined in the UML model.
/// </summary>
public class PasswordEncryptionService : IPasswordEncryptionService
{
    private const int NonceSize = 12; // 96 bits for GCM
    private const int TagSize = 16;   // 128 bits authentication tag
    private const int KeySize = 32;   // 256 bits
    private const int SaltSize = 16;  // 128 bits
    private const int Iterations = 100_000; // PBKDF2 iterations

    /// <inheritdoc />
    public byte[] GeneratePerCredentialKey()
    {
        var key = new byte[KeySize];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(key);
        return key;
    }

    /// <inheritdoc />
    public byte[] EncryptPerCredentialKey(byte[] perKey, string masterPassword)
    {
        if (perKey == null || perKey.Length != KeySize)
            throw new ArgumentException($"Per-credential key must be {KeySize} bytes.", nameof(perKey));

        if (string.IsNullOrEmpty(masterPassword))
            throw new ArgumentException("Master password cannot be null or empty.", nameof(masterPassword));

        // Derive a key from the master password
        var salt = new byte[SaltSize];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);

        var derivedKey = Rfc2898DeriveBytes.Pbkdf2(
            masterPassword,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        // Encrypt the per-credential key
        var encrypted = AesEncrypt(perKey, derivedKey);

        // Prepend salt to the encrypted data
        var result = new byte[SaltSize + encrypted.Length];
        Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
        Buffer.BlockCopy(encrypted, 0, result, SaltSize, encrypted.Length);

        return result;
    }

    /// <inheritdoc />
    public byte[] DecryptPerCredentialKey(byte[] encryptedKey, string masterPassword)
    {
        if (encryptedKey == null || encryptedKey.Length < SaltSize + NonceSize + TagSize)
            throw new ArgumentException("Invalid encrypted key format.", nameof(encryptedKey));

        if (string.IsNullOrEmpty(masterPassword))
            throw new ArgumentException("Master password cannot be null or empty.", nameof(masterPassword));

        // Extract salt from the beginning
        var salt = new byte[SaltSize];
        Buffer.BlockCopy(encryptedKey, 0, salt, 0, SaltSize);

        // Extract encrypted data
        var encryptedData = new byte[encryptedKey.Length - SaltSize];
        Buffer.BlockCopy(encryptedKey, SaltSize, encryptedData, 0, encryptedData.Length);

        // Derive the key from master password and salt
        var derivedKey = Rfc2898DeriveBytes.Pbkdf2(
            masterPassword,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        // Decrypt and return the per-credential key
        return AesDecrypt(encryptedData, derivedKey);
    }

    /// <inheritdoc />
    public byte[] EncryptPassword(string plaintext, byte[] encryptedKey)
    {
        if (string.IsNullOrEmpty(plaintext))
            throw new ArgumentException("Plaintext cannot be null or empty.", nameof(plaintext));

        if (encryptedKey == null || encryptedKey.Length != KeySize)
            throw new ArgumentException($"Key must be {KeySize} bytes.", nameof(encryptedKey));

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        return AesEncrypt(plaintextBytes, encryptedKey);
    }

    /// <inheritdoc />
    public string DecryptPassword(byte[] encrypted, byte[] encryptedKey)
    {
        if (encrypted == null || encrypted.Length < NonceSize + TagSize)
            throw new ArgumentException("Invalid encrypted data format.", nameof(encrypted));

        if (encryptedKey == null || encryptedKey.Length != KeySize)
            throw new ArgumentException($"Key must be {KeySize} bytes.", nameof(encryptedKey));

        var decrypted = AesDecrypt(encrypted, encryptedKey);
        return Encoding.UTF8.GetString(decrypted);
    }

    /// <inheritdoc />
    public byte[] AesEncrypt(byte[] data, byte[] key)
    {
        if (data == null || data.Length == 0)
            throw new ArgumentException("Data cannot be null or empty.", nameof(data));

        if (key == null || key.Length != KeySize)
            throw new ArgumentException($"Key must be {KeySize} bytes.", nameof(key));

        var nonce = new byte[NonceSize];
        var ciphertext = new byte[data.Length];
        var tag = new byte[TagSize];

        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(nonce);

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Encrypt(nonce, data, ciphertext, tag);

        // Combine nonce + ciphertext + tag
        var result = new byte[NonceSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSize + ciphertext.Length, TagSize);

        return result;
    }

    /// <inheritdoc />
    public byte[] AesDecrypt(byte[] data, byte[] key)
    {
        if (data == null || data.Length < NonceSize + TagSize)
            throw new ArgumentException("Invalid encrypted data format.", nameof(data));

        if (key == null || key.Length != KeySize)
            throw new ArgumentException($"Key must be {KeySize} bytes.", nameof(key));

        var nonce = new byte[NonceSize];
        var ciphertextLength = data.Length - NonceSize - TagSize;
        var ciphertext = new byte[ciphertextLength];
        var tag = new byte[TagSize];

        Buffer.BlockCopy(data, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(data, NonceSize, ciphertext, 0, ciphertextLength);
        Buffer.BlockCopy(data, NonceSize + ciphertextLength, tag, 0, TagSize);

        var plaintext = new byte[ciphertextLength];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }
}
