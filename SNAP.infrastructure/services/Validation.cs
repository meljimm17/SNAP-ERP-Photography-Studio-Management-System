using System.Text.RegularExpressions;

namespace SNAP.infrastructure.services
{
    /// <summary>
    /// Validation utility methods for common input patterns.
    /// </summary>
    public static class Validation
    {
        public static bool IsValidPhMobileNumber(string? phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return false;

            phoneNumber = phoneNumber.Trim();

            // Accept formats like:
            // 09171234567, +639171234567, 9171234567, +63917-1234-567
            var validPatterns = new[]
            {
                @"^09\d{9}$",                      // 09171234567
                @"^\+639\d{9}$",                   // +639171234567
                @"^9\d{9}$",                       // 9171234567
                @"^\+63\d{9,11}$"                  // +63... (various formats)
            };

            return validPatterns.Any(pattern => Regex.IsMatch(phoneNumber, pattern));
        }

        public static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}
