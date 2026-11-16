namespace RAGGit.Core;

/// <summary>
/// Script detection shared by answer-language enforcement and document label
/// selection. Greek Unicode blocks only; offline and model-free.
/// </summary>
public static class TextScript
{
    /// <summary>True when <paramref name="value"/> contains a Greek-script character.</summary>
    public static bool IsGreek(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (c is >= '\u0370' and <= '\u03FF' or >= '\u1F00' and <= '\u1FFF')
            {
                return true;
            }
        }

        return false;
    }
}
