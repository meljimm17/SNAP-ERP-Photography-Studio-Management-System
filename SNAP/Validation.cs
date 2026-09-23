using System.Text.RegularExpressions;

namespace SNAP
{
    /// <summary>
    /// Shared input-format validation for fields that appear in more than one form (customer
    /// contact number and email, currently entered both in CustomerDialogForm and the inline
    /// "+ New Customer" section of BookingDialogForm).
    /// </summary>
    public static class Validation
    {
        // Matches PH mobile numbers in either local (09171234567) or international
        // (+639171234567) format: 09 or +639, followed by 9 more digits.
        private static readonly Regex PhMobileRegex = new(@"^(09\d{9}|\+639\d{9})$", RegexOptions.Compiled);

        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        public static bool IsValidPhMobileNumber(string value) => PhMobileRegex.IsMatch(value.Trim());

        public static bool IsValidEmail(string value) => EmailRegex.IsMatch(value.Trim());
    }
}
