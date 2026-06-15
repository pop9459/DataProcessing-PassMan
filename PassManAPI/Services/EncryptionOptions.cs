namespace PassManAPI.Services;

/// <summary>
/// Options for at-rest credential encryption. Bound from configuration under "Encryption".
/// </summary>
public class EncryptionOptions
{
    public const string SectionName = "Encryption";

    /// <summary>
    /// Server-side master key used to wrap (encrypt) each per-credential key before it is
    /// persisted, so key material is never stored in the clear alongside the ciphertext.
    /// Supply a strong value via secret/env in real deployments; must be non-empty.
    /// </summary>
    public string MasterKey { get; set; } = string.Empty;
}
