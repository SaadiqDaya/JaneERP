using System.Security.Cryptography;
using System.Text;

namespace JaneERP.Api.Services;

/// <summary>
/// Exact replica of the WinForms PasswordHasher.
/// PBKDF2-SHA256, 100k iterations, 16-byte salt, 32-byte hash — all Base64 encoded.
/// Must match the desktop app's algorithm so mobile login works against the same Users table.
/// </summary>
public static class PasswordHasher
{
    private const int SaltSize  = 16;
    private const int HashSize  = 32;
    private const int Iterations = 100_000;

    public static (string hash, string salt) Hash(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            saltBytes,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    public static bool Verify(string password, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            saltBytes,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);
        return CryptographicOperations.FixedTimeEquals(
            hashBytes,
            Convert.FromBase64String(hash));
    }
}
