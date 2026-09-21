namespace RAGGit.Core;

/// <summary>
/// Shared single-line truncation for diagnostic log previews.
/// </summary>
public static class TextPreview
{
    /// <summary>
    /// Truncates <paramref name="text"/> to <paramref name="maxLength"/>
    /// characters, appending an ellipsis when truncated. Newlines are
    /// flattened so previews stay on one log line.
    /// </summary>
    public static string Truncate(string? text, int maxLength = 80)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        if (maxLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength));
        }

        var flattened = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return flattened.Length <= maxLength ? flattened : flattened.Substring(0, maxLength) + "…";
    }
}
