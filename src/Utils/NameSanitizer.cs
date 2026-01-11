using System.Text.RegularExpressions;

namespace WearWare.Utils
{
    public static class FilenameValidator
    {
        public const string AllowedPattern = "A-Za-z0-9_-";
        private static readonly Regex _sanitizer = new($"[^{AllowedPattern}]", RegexOptions.Compiled);
        private static readonly Regex _validator = new($"^[{AllowedPattern}]+$", RegexOptions.Compiled);

        /// <summary>
        /// Returns true if the input matches the allowed filename pattern (letters, numbers, dashes, underscores)
        /// </summary>
        public static bool Validate(string input)
        {
            return _validator.IsMatch(input);
        }

        /// <summary>
        /// Replaces any invalid characters with '-'
        /// </summary>
        public static string Sanitize(string input)
        {
            return _sanitizer.Replace(input, "-");
        }
    }
}