using System.Text.RegularExpressions;

namespace JaneERP.Validation
{
    /// <summary>
    /// Validates and normalizes user input to a consistent format before it is
    /// persisted or used. Every method returns a <see cref="ValidationResult"/>
    /// that carries either the cleaned value <em>or</em> an error message.
    ///
    /// <para>Usage pattern:</para>
    /// <code>
    /// var name  = InputValidator.RequireString(_txtName.Text, "Name");
    /// var email = InputValidator.NormalizeEmail(_txtEmail.Text);
    /// var phone = InputValidator.NormalizePhone(_txtPhone.Text);
    /// if (!InputValidator.ShowErrors(this, name, email, phone)) return;
    ///
    /// vendor.VendorName = name;
    /// vendor.Email      = email;
    /// vendor.Phone      = phone;
    /// </code>
    /// </summary>
    public static class InputValidator
    {
        // ── Phone ─────────────────────────────────────────────────────────────────
        // Target format: ###.###.#### (North American 10-digit)

        /// <summary>
        /// Strips all non-digit characters, removes a leading country code of 1 if
        /// present, then formats as <c>###.###.####</c>.
        /// Returns <see cref="ValidationResult.Ok(string)"/> with an empty string if
        /// the input is blank (phone is optional on most forms).
        /// </summary>
        public static ValidationResult NormalizePhone(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return ValidationResult.Ok("");

            var digits = new string(input.Where(char.IsDigit).ToArray());

            // Strip leading country code "1" for 11-digit numbers
            if (digits.Length == 11 && digits[0] == '1')
                digits = digits[1..];

            if (digits.Length != 10)
                return ValidationResult.Fail(
                    "Phone number must be 10 digits (e.g. 604.123.4567).");

            return ValidationResult.Ok($"{digits[..3]}.{digits[3..6]}.{digits[6..]}");
        }

        // ── Email ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Trims, lowercases, and validates basic email format.
        /// Set <paramref name="required"/> = true to reject blank values.
        /// </summary>
        public static ValidationResult NormalizeEmail(string? input, bool required = false)
        {
            if (string.IsNullOrWhiteSpace(input))
                return required
                    ? ValidationResult.Fail("Email address is required.")
                    : ValidationResult.Ok("");

            var trimmed = input.Trim().ToLowerInvariant();

            if (trimmed.Contains(' '))
                return ValidationResult.Fail("Email address must not contain spaces.");

            var atIdx = trimmed.IndexOf('@');
            if (atIdx <= 0 || atIdx != trimmed.LastIndexOf('@'))
                return ValidationResult.Fail("Email address is not valid.");

            var domain = trimmed[(atIdx + 1)..];
            if (!domain.Contains('.') || domain.StartsWith('.') || domain.EndsWith('.'))
                return ValidationResult.Fail("Email address is not valid.");

            return ValidationResult.Ok(trimmed);
        }

        // ── Required string ───────────────────────────────────────────────────────

        /// <summary>Trims the value and returns an error if the result is empty.</summary>
        public static ValidationResult RequireString(string? input, string fieldName)
        {
            var trimmed = input?.Trim() ?? "";
            return string.IsNullOrEmpty(trimmed)
                ? ValidationResult.Fail($"{fieldName} is required.")
                : ValidationResult.Ok(trimmed);
        }

        // ── Optional string ───────────────────────────────────────────────────────

        /// <summary>Returns the trimmed value; always valid (empty is allowed).</summary>
        public static ValidationResult NormalizeString(string? input)
            => ValidationResult.Ok(input?.Trim() ?? "");

        // ── Name ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Trims, collapses internal runs of whitespace to a single space,
        /// and title-cases each word.
        /// </summary>
        public static ValidationResult NormalizeName(string? input, string fieldName, bool required = true)
        {
            var collapsed = Regex.Replace(input?.Trim() ?? "", @"\s+", " ");

            if (required && string.IsNullOrEmpty(collapsed))
                return ValidationResult.Fail($"{fieldName} is required.");

            // Title-case: first letter of each word uppercase, rest lowercase
            var titled = Regex.Replace(collapsed, @"\b\w", m => m.Value.ToUpperInvariant());
            return ValidationResult.Ok(titled);
        }

        // ── URL / Website ─────────────────────────────────────────────────────────

        /// <summary>
        /// Trims the value; prepends <c>https://</c> if no scheme is present.
        /// Returns an empty string (valid) for blank input.
        /// </summary>
        public static ValidationResult NormalizeUrl(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return ValidationResult.Ok("");

            var trimmed = input.Trim();

            if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                trimmed = "https://" + trimmed;

            return Uri.TryCreate(trimmed, UriKind.Absolute, out _)
                ? ValidationResult.Ok(trimmed)
                : ValidationResult.Fail("Website URL is not valid.");
        }

        // ── SKU / code ────────────────────────────────────────────────────────────

        /// <summary>Trims and uppercases a SKU or short code.</summary>
        public static ValidationResult NormalizeSku(string? input, bool required = true)
        {
            var cleaned = input?.Trim().ToUpperInvariant() ?? "";
            if (required && string.IsNullOrEmpty(cleaned))
                return ValidationResult.Fail("SKU is required.");
            return ValidationResult.Ok(cleaned);
        }

        // ── Positive decimal ─────────────────────────────────────────────────────

        /// <summary>Parses a decimal and requires it to be greater than zero.</summary>
        public static ValidationResult RequirePositiveDecimal(string? input, string fieldName)
        {
            if (!decimal.TryParse(input?.Trim(),
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.CurrentCulture,
                    out var value))
                return ValidationResult.Fail($"{fieldName} must be a valid number.");
            if (value <= 0)
                return ValidationResult.Fail($"{fieldName} must be greater than zero.");
            return ValidationResult.Ok(value.ToString(System.Globalization.CultureInfo.CurrentCulture));
        }

        // ── Batch helpers ─────────────────────────────────────────────────────────

        /// <summary>Returns all error messages from a set of results.</summary>
        public static IReadOnlyList<string> CollectErrors(params ValidationResult[] results)
            => results.Where(r => !r.IsValid).Select(r => r.Error!).ToList();

        /// <summary>
        /// Shows a single MessageBox listing all validation errors and returns
        /// <c>false</c> if any exist, or <c>true</c> if every result is valid.
        /// Intended as the final gate before saving — call after all fields are checked.
        /// </summary>
        public static bool ShowErrors(IWin32Window owner, params ValidationResult[] results)
        {
            var errors = CollectErrors(results);
            if (errors.Count == 0) return true;
            MessageBox.Show(owner,
                string.Join("\n\n", errors),
                "Validation",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }
    }

    /// <summary>
    /// Immutable result of a single validation or normalization step.
    /// <see cref="IsValid"/> is true when the input was acceptable;
    /// <see cref="Value"/> always holds the cleaned/normalized form of the input;
    /// <see cref="Error"/> is non-null only when <see cref="IsValid"/> is false.
    /// </summary>
    public readonly struct ValidationResult
    {
        public bool    IsValid { get; }
        public string  Value   { get; }
        public string? Error   { get; }

        private ValidationResult(bool isValid, string value, string? error)
        {
            IsValid = isValid;
            Value   = value;
            Error   = error;
        }

        public static ValidationResult Ok(string value)
            => new(true, value, null);

        public static ValidationResult Fail(string error, string rawValue = "")
            => new(false, rawValue, error);

        /// <summary>
        /// Implicit conversion so you can write <c>vendor.Phone = phoneResult;</c>
        /// after validating — no need for <c>.Value</c> everywhere.
        /// </summary>
        public static implicit operator string(ValidationResult r) => r.Value;
    }
}
