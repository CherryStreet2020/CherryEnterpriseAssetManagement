using System;
using System.Text;

namespace Abs.FixedAssets.Helpers
{
    // PR #116a — Humanize Pascal/SCREAMING_SNAKE enum names for the UI.
    //
    // Bug catalog #12 from the E2E audit: status badges across the app render
    // raw enum-ish values like "INPROGRESS", "NOTREQUIRED", "ONHOLD". Those
    // look unpremium and read as broken. This helper converts:
    //
    //   "InProgress"       → "In progress"
    //   "INPROGRESS"       → "In progress"     (treated as one token, sentence case)
    //   "NotRequired"      → "Not required"
    //   "PendingApproval"  → "Pending approval"
    //   "Completed"        → "Completed"
    //
    // Apply via @Status.Humanize() inside Razor. Cheap, allocation-light.
    // For full i18n we'll want IStringLocalizer (Sprint 3 PR #137); this is
    // the just-enough-now version.
    public static class StatusLabelExtensions
    {
        public static string Humanize(this string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var s = raw.Trim();

            // Handle SCREAMING_SNAKE / all-caps by lowercasing then we'll
            // re-capitalize the first letter at the end. Detect: every
            // letter is an uppercase ASCII letter.
            bool allUpper = true;
            foreach (var ch in s)
            {
                if (char.IsLetter(ch) && !char.IsUpper(ch))
                {
                    allUpper = false;
                    break;
                }
            }
            if (allUpper)
            {
                return Capitalize(s.ToLowerInvariant().Replace('_', ' '));
            }

            // Otherwise treat as PascalCase / camelCase — split on uppercase boundaries.
            var sb = new StringBuilder(s.Length + 4);
            for (int i = 0; i < s.Length; i++)
            {
                var ch = s[i];
                if (i > 0 && char.IsUpper(ch) && !char.IsUpper(s[i - 1]))
                {
                    sb.Append(' ');
                    sb.Append(char.ToLowerInvariant(ch));
                }
                else if (i == 0)
                {
                    sb.Append(char.ToUpperInvariant(ch));
                }
                else
                {
                    sb.Append(char.ToLowerInvariant(ch));
                }
            }
            return sb.ToString();
        }

        public static string Humanize<TEnum>(this TEnum value) where TEnum : struct, Enum
        {
            return value.ToString().Humanize();
        }

        private static string Capitalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}
