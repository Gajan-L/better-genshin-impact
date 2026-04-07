using System.Text;
using System.Text.RegularExpressions;

namespace BetterGenshinImpact.Modules.MultiAccount;

public static class RememberedAccountMatcher
{
    public static bool Matches(string targetAccountText, string candidateText)
    {
        var normalizedTarget = Normalize(targetAccountText);
        var normalizedCandidate = Normalize(candidateText);

        if (string.IsNullOrWhiteSpace(normalizedTarget) || string.IsNullOrWhiteSpace(normalizedCandidate))
        {
            return false;
        }

        if (string.Equals(normalizedTarget, normalizedCandidate, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalizedCandidate.Contains(normalizedTarget, StringComparison.OrdinalIgnoreCase)
            || normalizedTarget.Contains(normalizedCandidate, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return MatchesWildcardPattern(normalizedTarget, normalizedCandidate)
               || MatchesWildcardPattern(normalizedCandidate, normalizedTarget);
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            if (char.IsWhiteSpace(ch) || ch == '\u00A0')
            {
                continue;
            }

            builder.Append(ch switch
            {
                '\uFF0A' => '*',
                '\u2022' => '*',
                '\u25CF' => '*',
                '\u25E6' => '*',
                '\u00B7' => '*',
                '\u2219' => '*',
                '\u2027' => '*',
                '\u30FB' => '*',
                '路' => '*',
                _ => ch,
            });
        }

        return builder.ToString();
    }

    private static bool MatchesWildcardPattern(string wildcardPattern, string textToTest)
    {
        if (string.IsNullOrWhiteSpace(wildcardPattern) || string.IsNullOrWhiteSpace(textToTest))
        {
            return false;
        }

        if (!wildcardPattern.Contains('*', StringComparison.Ordinal))
        {
            return false;
        }

        var pattern = "^" + Regex.Escape(wildcardPattern) + "$";
        pattern = Regex.Replace(pattern, @"(\\\*)+", ".*");
        return Regex.IsMatch(textToTest, pattern, RegexOptions.IgnoreCase);
    }
}
