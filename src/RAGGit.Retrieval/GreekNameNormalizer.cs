using System.Globalization;
using System.Text;

namespace RAGGit.Retrieval;

/// <summary>
/// Greek-aware normalization for person-name matching: lowercase, diacritics
/// stripping, final-sigma folding (ς→σ). Used for did-you-mean suggestions
/// when the asked first name differs but the surname matches (e.g. Αρετής
/// vs Πηνελόπης with the same Δημοπούλου).
/// </summary>
public static class GreekNameNormalizer
{
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lower = value.Trim().ToLowerInvariant();
        var decomposed = lower.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (c == 'ς')
            {
                builder.Append('σ');
            }
            else if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (char.IsWhiteSpace(c))
            {
                builder.Append(' ');
            }
            // Drop punctuation otherwise.
        }

        var collapsed = string.Join(
            " ",
            builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)
        );
        return collapsed;
    }
}
