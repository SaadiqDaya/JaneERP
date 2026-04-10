using System.Security.Cryptography;
using System.Text;

namespace JaneERP.Security
{
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
}
