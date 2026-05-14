using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace JaneERP.Tests.Security
{
    /// <summary>
    /// Unit tests for JWT token generation and validation.
    ///
    /// These tests replicate the exact TokenValidationParameters and claim layout used by
    /// JwtService (JaneERP.Api/Services/JwtService.cs) and the JwtBearer middleware
    /// configured in Program.cs, using the same underlying libraries.
    ///
    /// Because the test project references only the WinForms host project (not JaneERP.Api),
    /// JwtService is not directly instantiated here. The tests instead exercise the JWT
    /// pipeline at the library level, making them independent of the API web host. If
    /// JwtService is ever extracted to a shared library, these tests can be simplified to
    /// call it directly.
    ///
    /// Constants mirror appsettings.json:
    ///   Jwt:Secret   = "ojhweanrgozdjnfa2i3n23newfwaoso23n4oemrfqawv"
    ///   Jwt:Issuer   = "JaneERP.Api"
    ///   Jwt:Audience = "JaneERP.Mobile"
    /// </summary>
    public class JwtTokenTests
    {
        // ── Constants mirroring JaneERP.Api appsettings.json / Program.cs ────────

        private const string ValidSecret   = "ojhweanrgozdjnfa2i3n23newfwaoso23n4oemrfqawv";
        private const string ValidIssuer   = "JaneERP.Api";
        private const string ValidAudience = "JaneERP.Mobile";
        private const string WrongSecret   = "this-is-a-completely-different-secret-key-xyz";

        // ── Token factory (mirrors JwtService.GenerateToken) ─────────────────────

        private static string GenerateToken(
            string username,
            string role,
            string company,
            string  secret       = ValidSecret,
            string  issuer       = ValidIssuer,
            string  audience     = ValidAudience,
            double  expiryHours  = 24)
        {
            var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("company",            company),
                new Claim(ClaimTypes.Role,      role),
                new Claim("username",           username),
            };

            var token = new JwtSecurityToken(
                issuer:             issuer,
                audience:           audience,
                claims:             claims,
                expires:            DateTime.UtcNow.AddHours(expiryHours),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Validates a token using the same TokenValidationParameters as Program.cs.
        /// Returns the ClaimsPrincipal on success, throws on failure.
        /// </summary>
        private static ClaimsPrincipal ValidateToken(
            string tokenString,
            string secret    = ValidSecret,
            string issuer    = ValidIssuer,
            string audience  = ValidAudience)
        {
            var handler = new JwtSecurityTokenHandler();
            var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

            var parameters = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidateAudience         = true,
                ValidateLifetime         = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer              = issuer,
                ValidAudience            = audience,
                IssuerSigningKey         = key,
                ClockSkew                = TimeSpan.Zero,   // exact match — same as Program.cs
            };

            return handler.ValidateToken(tokenString, parameters, out _);
        }

        // ── Valid token ───────────────────────────────────────────────────────────

        [Fact]
        public void ValidToken_WithCorrectSecret_IsAccepted()
        {
            var token = GenerateToken("alice", "Admin", "JaneERP");

            var principal = ValidateToken(token);

            principal.Should().NotBeNull();
            principal.Identity!.IsAuthenticated.Should().BeTrue();
        }

        [Fact]
        public void ValidToken_SubjectClaim_MatchesUsername()
        {
            var token     = GenerateToken("bob", "Warehouse", "JaneERP");
            var principal = ValidateToken(token);

            // JwtRegisteredClaimNames.Sub maps to ClaimTypes.NameIdentifier after validation
            var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)
                   ?? principal.FindFirst(ClaimTypes.NameIdentifier);

            sub.Should().NotBeNull();
            sub!.Value.Should().Be("bob");
        }

        [Fact]
        public void ValidToken_CompanyClaim_IsPresent()
        {
            var token     = GenerateToken("carol", "Finance", "VanGoProduction");
            var principal = ValidateToken(token);

            var company = principal.FindFirst("company");

            company.Should().NotBeNull();
            company!.Value.Should().Be("VanGoProduction");
        }

        [Fact]
        public void ValidToken_UsernameClaim_MatchesInput()
        {
            var token     = GenerateToken("dave", "Sales", "JaneERP");
            var principal = ValidateToken(token);

            var username = principal.FindFirst("username");

            username.Should().NotBeNull();
            username!.Value.Should().Be("dave");
        }

        // ── Role claims ───────────────────────────────────────────────────────────

        [Theory]
        [InlineData("Admin")]
        [InlineData("Finance")]
        [InlineData("Manager")]
        [InlineData("Sales")]
        [InlineData("Warehouse")]
        public void ValidToken_RoleClaim_IsPresentInPrincipal(string role)
        {
            var token     = GenerateToken("user", role, "JaneERP");
            var principal = ValidateToken(token);

            principal.IsInRole(role).Should().BeTrue($"token with role '{role}' should satisfy IsInRole check");
        }

        [Fact]
        public void ValidToken_WrongRoleCheck_ReturnsFalse()
        {
            // A token issued as "Warehouse" must NOT satisfy an "Admin" role check
            var token     = GenerateToken("eve", "Warehouse", "JaneERP");
            var principal = ValidateToken(token);

            principal.IsInRole("Admin").Should().BeFalse();
        }

        // ── Wrong secret ──────────────────────────────────────────────────────────

        [Fact]
        public void Token_SignedWithWrongSecret_FailsValidation()
        {
            var token = GenerateToken("frank", "Admin", "JaneERP", secret: WrongSecret);

            // Validating with the correct secret should reject the tampered signature
            var act = () => ValidateToken(token, secret: ValidSecret);

            act.Should().Throw<SecurityTokenSignatureKeyNotFoundException>(
                "a token signed with a different key must be rejected");
        }

        [Fact]
        public void Token_TamperedPayload_FailsValidation()
        {
            var token  = GenerateToken("grace", "Admin", "JaneERP");
            var parts  = token.Split('.');
            // Replace the payload segment with a base64-encoded tampered value
            parts[1]   = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"sub\":\"attacker\"}"))
                             .TrimEnd('=');
            var tampered = string.Join('.', parts);

            var act = () => ValidateToken(tampered);

            act.Should().Throw<Exception>("tampered JWT must be rejected");
        }

        // ── Expired token ─────────────────────────────────────────────────────────

        [Fact]
        public void ExpiredToken_IsRejected()
        {
            // Generate a token that expired 1 hour ago
            var token = GenerateToken("henry", "Admin", "JaneERP", expiryHours: -1);

            var act = () => ValidateToken(token);

            act.Should().Throw<SecurityTokenExpiredException>(
                "tokens past their expiry time must be rejected (ClockSkew = 0)");
        }

        [Fact]
        public void ExpiredToken_WithClockSkewAllowance_WouldBeAccepted()
        {
            // A token that expired 4 minutes ago would be accepted if ClockSkew were 5 minutes.
            // This test verifies our ClockSkew=0 policy rejects it strictly.
            var token = GenerateToken("irene", "Manager", "JaneERP", expiryHours: -0.07); // ~4 min ago

            var act = () => ValidateToken(token);

            act.Should().Throw<SecurityTokenExpiredException>(
                "ClockSkew = TimeSpan.Zero means even a just-expired token must be rejected");
        }

        // ── Wrong issuer / audience ───────────────────────────────────────────────

        [Fact]
        public void Token_WithWrongIssuer_IsRejected()
        {
            var token = GenerateToken("jack", "Admin", "JaneERP", issuer: "SomeOtherIssuer");

            var act = () => ValidateToken(token, issuer: ValidIssuer);

            act.Should().Throw<SecurityTokenInvalidIssuerException>();
        }

        [Fact]
        public void Token_WithWrongAudience_IsRejected()
        {
            var token = GenerateToken("kate", "Admin", "JaneERP", audience: "SomeOtherAudience");

            var act = () => ValidateToken(token, audience: ValidAudience);

            act.Should().Throw<SecurityTokenInvalidAudienceException>();
        }

        // ── Token structure ───────────────────────────────────────────────────────

        [Fact]
        public void GeneratedToken_HasThreeBase64Segments()
        {
            var token = GenerateToken("lucy", "Sales", "JaneERP");

            // A well-formed JWT has exactly three dot-separated segments: header.payload.signature
            var parts = token.Split('.');
            parts.Should().HaveCount(3, "a JWT must have exactly three dot-separated segments");
        }

        [Fact]
        public void GeneratedToken_ContainsJtiClaim_MakingEachTokenUnique()
        {
            var token1 = GenerateToken("mike", "Admin", "JaneERP");
            var token2 = GenerateToken("mike", "Admin", "JaneERP");

            // Two tokens for the same user must differ because JTI is a new GUID each time
            token1.Should().NotBe(token2, "JTI (jti claim) randomises each token");
        }
    }
}
