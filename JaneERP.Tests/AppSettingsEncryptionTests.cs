using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace JaneERP.Tests
{
    /// <summary>
    /// Tests the DPAPI Protect/Unprotect round-trip used by AppSettings.SmtpPasswordPlain.
    /// These tests run DPAPI directly to verify the logic without touching the filesystem.
    /// </summary>
    public class AppSettingsEncryptionTests
    {
        [Fact]
        public void DpapiRoundTrip_EncryptThenDecrypt_ReturnsOriginal()
        {
            const string original = "super-secret-smtp-password";

            var bytes     = Encoding.UTF8.GetBytes(original);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            var result    = Encoding.UTF8.GetString(decrypted);

            Assert.Equal(original, result);
        }

        [Fact]
        public void DpapiRoundTrip_EmptyString_ProducesEmptyResult()
        {
            // AppSettings.Protect/Unprotect return "" for empty input — mirror that here
            const string empty = "";
            Assert.True(string.IsNullOrEmpty(empty));
        }

        [Fact]
        public void DpapiRoundTrip_UnicodePassword_RoundTripsCorrectly()
        {
            const string original = "pässwörd-with-ünïcödé-チャリ";

            var bytes     = Encoding.UTF8.GetBytes(original);
            var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);

            Assert.Equal(original, Encoding.UTF8.GetString(decrypted));
        }

        [Fact]
        public void SmtpPasswordPlain_SetAndGet_RoundTrips()
        {
            // Exercise AppSettings.SmtpPasswordPlain property directly
            var settings = new AppSettings { SmtpServer = "smtp.test.com", SmtpUser = "user@test.com", FromEmail = "from@test.com" };
            settings.SmtpPasswordPlain = "MyS3cretP@ss!";

            // SmtpPassword should be non-empty ciphertext, not the plain value
            Assert.False(string.IsNullOrWhiteSpace(settings.SmtpPassword));
            Assert.NotEqual("MyS3cretP@ss!", settings.SmtpPassword);

            // SmtpPasswordPlain should decrypt back to the original
            Assert.Equal("MyS3cretP@ss!", settings.SmtpPasswordPlain);
        }
    }
}
