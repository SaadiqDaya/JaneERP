using JaneERP.Security;
using Xunit;

namespace JaneERP.Tests.Security
{
    public class PasswordHasherTests
    {
        [Fact]
        public void Hash_ProducesNonEmptyHashAndSalt()
        {
            var (hash, salt) = PasswordHasher.Hash("password123");

            Assert.False(string.IsNullOrWhiteSpace(hash));
            Assert.False(string.IsNullOrWhiteSpace(salt));
        }

        [Fact]
        public void Hash_TwoCallsProduceDifferentSalts()
        {
            var (_, salt1) = PasswordHasher.Hash("password123");
            var (_, salt2) = PasswordHasher.Hash("password123");

            // Random salt means two hashes of the same password must differ
            Assert.NotEqual(salt1, salt2);
        }

        [Fact]
        public void Verify_CorrectPassword_ReturnsTrue()
        {
            var (hash, salt) = PasswordHasher.Hash("correct-horse-battery-staple");

            Assert.True(PasswordHasher.Verify("correct-horse-battery-staple", hash, salt));
        }

        [Fact]
        public void Verify_WrongPassword_ReturnsFalse()
        {
            var (hash, salt) = PasswordHasher.Hash("correct-horse-battery-staple");

            Assert.False(PasswordHasher.Verify("wrong-password", hash, salt));
        }

        [Fact]
        public void Verify_EmptyPassword_ReturnsFalse()
        {
            var (hash, salt) = PasswordHasher.Hash("correct-horse-battery-staple");

            Assert.False(PasswordHasher.Verify("", hash, salt));
        }

        [Fact]
        public void Verify_CaseSensitive()
        {
            var (hash, salt) = PasswordHasher.Hash("Password123");

            Assert.False(PasswordHasher.Verify("password123", hash, salt));
        }

        [Fact]
        public void Hash_ShortAndLongPasswordsBothWork()
        {
            var (h1, s1) = PasswordHasher.Hash("a");
            var (h2, s2) = PasswordHasher.Hash(new string('x', 1000));

            Assert.True(PasswordHasher.Verify("a", h1, s1));
            Assert.True(PasswordHasher.Verify(new string('x', 1000), h2, s2));
        }
    }
}
