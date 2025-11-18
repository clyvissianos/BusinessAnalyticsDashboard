using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BusinessAnalytics.Infrastructure.Parsing.Inference;

public static class ColumnInference
{
    // Greek diacritics removal then lower-case alnum
    public static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim();
        var formD = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in formD)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        }
        var clean = sb.ToString().Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();
        clean = Regex.Replace(clean, "[^a-z0-9α-ω ]", ""); // keep greek letters
        return Regex.Replace(clean, "\\s+", " ");
    }

    // Synonyms for each canonical field (el + en)
    public static readonly Dictionary<string, string[]> Synonyms = new()
    {
        ["Date"] = new[] { "ημερομηνια", "date", "transaction date", "doc date", "ημ", "ημερα" },
        ["Product"] = new[] { "προιον", "product", "item", "sku", "κωδικος προιοντος", "περιγραφη" },
        ["Customer"] = new[] { "πελατης", "customer", "client", "account", "αγοραστης" },
        ["Quantity"] = new[] { "ποσοτητα", "qty", "quantity", "τεμ", "τμχ", "pieces", "units" },
        ["Amount"] = new[] { "ποσο", "amount", "value", "total", "συνολο", "ποσον", "τιμη", "net", "καθαρο" },
    };

    // Score header against a canonical field
    public static double Score(string header, string canonical)
    {
        var h = Normalize(header);
        // exact synonym match
        if (Synonyms.TryGetValue(canonical, out var words) && words.Contains(h)) return 1.0;

        // contains check
        if (words != null && words.Any(w => h.Contains(w))) return 0.8;

        // prefix match
        if (words != null && words.Any(w => h.StartsWith(w))) return 0.7;

        // fallback: simple levenshtein-like proxy via length diff
        var wmin = words?.Min(w => Math.Abs(w.Length - h.Length)) ?? 99;
        return 0.5 - Math.Min(wmin, 10) / 20.0;
    }

    public static Dictionary<string, string?> SuggestMap(IEnumerable<string> headers)
    {
        // returns CanonicalField -> SourceHeader (or null if not found)
        var result = new Dictionary<string, string?>
        {
            ["Date"] = null,
            ["Product"] = null,
            ["Customer"] = null,
            ["Quantity"] = null,
            ["Amount"] = null
        };
        var headersList = headers.ToList();

        foreach (var target in result.Keys.ToList())
        {
            var best = headersList
                .Select(h => new { h, s = Score(h, target) })
                .OrderByDescending(x => x.s)
                .FirstOrDefault();

            if (best != null && best.s >= 0.65) // threshold
            {
                result[target] = best.h;
                headersList.Remove(best.h); // avoid reusing same column
            }
        }
        return result;
    }
}

