using System.Collections.Generic;
using System.Text.RegularExpressions;

// One-time migration aid used to seed stat-badge extraction from the old
// prose descriptions before they were rewritten as short, number-free flavor
// text. Not wired into runtime code - new upgrades author their `stats` list
// by hand alongside a short description.
public readonly struct StatBadgeInfo
{
    public readonly string ValueText;
    public readonly string LabelText;
    public readonly bool IsPositive;

    public StatBadgeInfo(string valueText, string labelText, bool isPositive)
    {
        ValueText = valueText;
        LabelText = labelText;
        IsPositive = isPositive;
    }
}

public static class UpgradeStatParser
{
    private static readonly Regex StatPattern = new Regex(
        @"(?<![\w.,])(?<sign>[+-])?(?<value>\d+(?:\.\d+)?)(?<pct>%)?(?![\d.%])",
        RegexOptions.Compiled);

    private static readonly HashSet<string> Stopwords = new HashSet<string>(new[]
    {
        "of", "to", "a", "an", "the", "your", "and", "that", "for", "on", "in",
        "with", "it", "its", "each", "per", "between"
    });

    public static List<StatBadgeInfo> Parse(string description, int maxBadges = 2)
    {
        var results = new List<StatBadgeInfo>();
        if (string.IsNullOrEmpty(description)) return results;

        foreach (Match match in StatPattern.Matches(description))
        {
            if (results.Count >= maxBadges) break;

            bool hasSign = match.Groups["sign"].Success;
            bool hasPct = match.Groups["pct"].Success;
            if (!hasSign && !hasPct) continue; // filters "Boost 1." / "for 2 seconds" noise

            string sign = hasSign ? match.Groups["sign"].Value : "";
            string valueText = $"{sign}{match.Groups["value"].Value}{(hasPct ? "%" : "")}";
            bool isPositive = sign != "-";

            string label = ExtractLabel(description, match.Index + match.Length);
            results.Add(new StatBadgeInfo(valueText, label, isPositive));
        }

        return results;
    }

    private static string ExtractLabel(string description, int startIndex)
    {
        var words = new List<string>();
        var remainder = description.Substring(startIndex).TrimStart();

        foreach (var rawToken in remainder.Split(' '))
        {
            if (words.Count >= 2) break;
            if (string.IsNullOrEmpty(rawToken)) continue;

            int punctIndex = rawToken.IndexOfAny(new[] { '.', ',', '!', '?', '-', ':' });
            string token = punctIndex >= 0 ? rawToken.Substring(0, punctIndex) : rawToken;

            if (token.Length == 0) break;
            if (Stopwords.Contains(token.ToLowerInvariant())) break;

            words.Add(token.ToUpperInvariant());
            if (punctIndex >= 0) break;
        }

        return string.Join(" ", words);
    }
}
