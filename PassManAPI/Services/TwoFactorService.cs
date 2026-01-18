using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace PassManAPI.Services;

/// <summary>
/// TOTP (Time-based One-Time Password) two-factor authentication service.
/// Implements RFC 6238 for time-based OTP generation and validation.
/// </summary>
public class TwoFactorService : ITwoFactorService
{
    private const int SecretKeyLength = 20; // 160 bits as per RFC 4226
    private const int CodeDigits = 6;
    private const int TimeStepSeconds = 30;
    private const int BackupCodeLength = 8;

    /// <inheritdoc />
    public string GenerateSecretKey()
    {
        var secretBytes = new byte[SecretKeyLength];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(secretBytes);
        return Base32Encode(secretBytes);
    }

    /// <inheritdoc />
    public string GenerateCode(string secretKey)
    {
        if (string.IsNullOrEmpty(secretKey))
            throw new ArgumentException("Secret key cannot be null or empty.", nameof(secretKey));

        var secretBytes = Base32Decode(secretKey);
        var counter = GetCurrentCounter();

        return GenerateTotpCode(secretBytes, counter);
    }

    /// <inheritdoc />
    public bool ValidateCode(string secretKey, string code, int toleranceSteps = 1)
    {
        if (string.IsNullOrEmpty(secretKey))
            throw new ArgumentException("Secret key cannot be null or empty.", nameof(secretKey));

        if (string.IsNullOrEmpty(code) || code.Length != CodeDigits)
            return false;

        var secretBytes = Base32Decode(secretKey);
        var currentCounter = GetCurrentCounter();

        // Check current time step and surrounding steps for clock drift tolerance
        for (var i = -toleranceSteps; i <= toleranceSteps; i++)
        {
            var counter = currentCounter + i;
            var expectedCode = GenerateTotpCode(secretBytes, counter);

            if (ConstantTimeEquals(code, expectedCode))
                return true;
        }

        return false;
    }

    /// <inheritdoc />
    public string GenerateQrCodeUri(string secretKey, string email, string issuer = "PassMan")
    {
        if (string.IsNullOrEmpty(secretKey))
            throw new ArgumentException("Secret key cannot be null or empty.", nameof(secretKey));

        if (string.IsNullOrEmpty(email))
            throw new ArgumentException("Email cannot be null or empty.", nameof(email));

        var encodedIssuer = HttpUtility.UrlEncode(issuer);
        var encodedEmail = HttpUtility.UrlEncode(email);
        var encodedSecret = HttpUtility.UrlEncode(secretKey);

        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={encodedSecret}&issuer={encodedIssuer}&algorithm=SHA1&digits={CodeDigits}&period={TimeStepSeconds}";
    }

    /// <inheritdoc />
    public IList<string> GenerateBackupCodes(int count = 10)
    {
        if (count <= 0 || count > 20)
            throw new ArgumentException("Count must be between 1 and 20.", nameof(count));

        var codes = new List<string>(count);
        using var rng = RandomNumberGenerator.Create();

        for (var i = 0; i < count; i++)
        {
            var codeBytes = new byte[BackupCodeLength / 2];
            rng.GetBytes(codeBytes);
            var code = BitConverter.ToString(codeBytes).Replace("-", "").ToUpperInvariant();
            // Format as XXXX-XXXX for readability
            codes.Add($"{code[..4]}-{code[4..]}");
        }

        return codes;
    }

    private static long GetCurrentCounter()
    {
        var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return unixTimestamp / TimeStepSeconds;
    }

    private static string GenerateTotpCode(byte[] secretBytes, long counter)
    {
        var counterBytes = BitConverter.GetBytes(counter);

        // Ensure big-endian byte order
        if (BitConverter.IsLittleEndian)
            Array.Reverse(counterBytes);

        using var hmac = new HMACSHA1(secretBytes);
        var hash = hmac.ComputeHash(counterBytes);

        // Dynamic truncation per RFC 4226
        var offset = hash[^1] & 0x0F;
        var binaryCode = ((hash[offset] & 0x7F) << 24)
                       | ((hash[offset + 1] & 0xFF) << 16)
                       | ((hash[offset + 2] & 0xFF) << 8)
                       | (hash[offset + 3] & 0xFF);

        var otp = binaryCode % (int)Math.Pow(10, CodeDigits);
        return otp.ToString().PadLeft(CodeDigits, '0');
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new StringBuilder((data.Length * 8 + 4) / 5);

        int buffer = 0, bitsInBuffer = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsInBuffer += 8;

            while (bitsInBuffer >= 5)
            {
                bitsInBuffer -= 5;
                result.Append(alphabet[(buffer >> bitsInBuffer) & 0x1F]);
            }
        }

        if (bitsInBuffer > 0)
        {
            result.Append(alphabet[(buffer << (5 - bitsInBuffer)) & 0x1F]);
        }

        return result.ToString();
    }

    private static byte[] Base32Decode(string encoded)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        encoded = encoded.ToUpperInvariant().TrimEnd('=');

        var result = new List<byte>();
        int buffer = 0, bitsInBuffer = 0;

        foreach (var c in encoded)
        {
            var value = alphabet.IndexOf(c);
            if (value < 0)
                throw new ArgumentException($"Invalid Base32 character: {c}");

            buffer = (buffer << 5) | value;
            bitsInBuffer += 5;

            if (bitsInBuffer >= 8)
            {
                bitsInBuffer -= 8;
                result.Add((byte)(buffer >> bitsInBuffer));
            }
        }

        return result.ToArray();
    }

    /// <summary>
    /// Compares two strings in constant time to prevent timing attacks.
    /// </summary>
    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length)
            return false;

        var result = 0;
        for (var i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }
}
