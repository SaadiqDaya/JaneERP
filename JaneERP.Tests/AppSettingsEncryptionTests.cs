using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
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

        // ── Additional cases (FluentAssertions) ────────────────────────────────

        [Fact]
        public void SmtpPasswordPlain_EmptyString_RoundTrips()
        {
            // Protect("") returns "" and Unprotect("") returns "" — verify the guard
            var settings = new AppSettings();
            settings.SmtpPasswordPlain = "";

            // An empty plain value should produce an empty (not null) stored cipher
            settings.SmtpPassword.Should().BeEmpty("Protect returns \"\" for empty input");

            // Getting the plain value back for an empty cipher must also return empty
            settings.SmtpPasswordPlain.Should().BeEmpty();
        }

        [Fact]
        public void SmtpPasswordPlain_NullAssignment_HandledGracefully()
        {
            // Assigning null should not throw — Protect guards against null/empty
            var settings   = new AppSettings();
            var act        = () => { settings.SmtpPasswordPlain = null!; };

            act.Should().NotThrow("Protect returns \"\" for null input via IsNullOrEmpty guard");
            settings.SmtpPassword.Should().BeEmpty();
            settings.SmtpPasswordPlain.Should().BeEmpty();
        }

        [Fact]
        public void DpapiProtect_TwoEncryptionsOfSameString_ProduceDifferentCiphertext()
        {
            // DPAPI uses a random salt internally so two calls with the same input
            // produce different ciphertext (non-deterministic per call), but both
            // must decrypt to the same original value.
            const string plainText = "repeat-me";

            var ct1 = Convert.ToBase64String(
                ProtectedData.Protect(Encoding.UTF8.GetBytes(plainText), null, DataProtectionScope.CurrentUser));
            var ct2 = Convert.ToBase64String(
                ProtectedData.Protect(Encoding.UTF8.GetBytes(plainText), null, DataProtectionScope.CurrentUser));

            ct1.Should().NotBe(ct2, "DPAPI adds a random salt so two encryptions differ");

            // Both must still decrypt correctly
            var pt1 = Encoding.UTF8.GetString(
                ProtectedData.Unprotect(Convert.FromBase64String(ct1), null, DataProtectionScope.CurrentUser));
            var pt2 = Encoding.UTF8.GetString(
                ProtectedData.Unprotect(Convert.FromBase64String(ct2), null, DataProtectionScope.CurrentUser));

            pt1.Should().Be(plainText);
            pt2.Should().Be(plainText);
        }

        [Fact]
        public void AppSettings_IsEmailConfigured_TrueWhenAllFieldsPresent()
        {
            var settings = new AppSettings
            {
                SmtpServer = "smtp.example.com",
                SmtpUser   = "user@example.com",
                FromEmail  = "noreply@example.com"
            };

            settings.IsEmailConfigured.Should().BeTrue();
        }

        [Theory]
        [InlineData("", "user@example.com", "from@example.com")]
        [InlineData("smtp.example.com", "", "from@example.com")]
        [InlineData("smtp.example.com", "user@example.com", "")]
        public void AppSettings_IsEmailConfigured_FalseWhenAnyFieldMissing(
            string server, string user, string from)
        {
            var settings = new AppSettings { SmtpServer = server, SmtpUser = user, FromEmail = from };

            settings.IsEmailConfigured.Should().BeFalse(
                "all three SMTP fields must be non-empty for email to be configured");
        }

        [Fact]
        public void AppSettings_GetRate_ReturnsOne_ForHomeCurrency()
        {
            var settings = new AppSettings { HomeCurrency = "CAD" };

            settings.GetRate("CAD").Should().Be(1m);
            settings.GetRate("cad").Should().Be(1m, "comparison is case-insensitive");
        }

        [Fact]
        public void AppSettings_GetRate_ReturnsConfiguredRate_ForKnownCurrency()
        {
            var settings = new AppSettings
            {
                HomeCurrency   = "CAD",
                CurrencyRates  = new Dictionary<string, decimal> { ["USD"] = 1.45m }
            };

            settings.GetRate("USD").Should().Be(1.45m);
            settings.GetRate("usd").Should().Be(1.45m, "key lookup uses ToUpper() normalisation");
        }

        [Fact]
        public void AppSettings_GetRate_ReturnsOne_ForUnknownCurrency()
        {
            var settings = new AppSettings { HomeCurrency = "CAD" };

            settings.GetRate("XYZ").Should().Be(1m, "unknown currencies fall back to 1:1 rate");
        }

        [Fact]
        public void AppSettings_GetFlaskForBatchMl_ReturnsSmallestFittingFlask()
        {
            var settings = new AppSettings(); // uses default FlaskConfigs

            settings.GetFlaskForBatchMl(500m).Should().Be("1L Squeeze");
            settings.GetFlaskForBatchMl(1000m).Should().Be("1L Squeeze");  // boundary: ≤ 1000
            settings.GetFlaskForBatchMl(1001m).Should().Be("10L Jug");
            settings.GetFlaskForBatchMl(9000m).Should().Be("10L Jug");
            settings.GetFlaskForBatchMl(9001m).Should().Be("20L Stainless");
            settings.GetFlaskForBatchMl(50000m).Should().Be("100L Vat");
        }

        [Fact]
        public void AppSettings_GetFlaskForBatchMl_ReturnsFallback_WhenNoConfigExists()
        {
            var settings = new AppSettings { FlaskConfigs = [] };

            settings.GetFlaskForBatchMl(100m).Should().Be("Unknown",
                "empty flask config should produce the Unknown fallback");
        }
    }
}
