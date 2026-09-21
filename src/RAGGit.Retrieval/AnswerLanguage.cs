namespace RAGGit.Retrieval;

/// <summary>
/// Same-language enforcement for answers: a Greek query must get a Greek
/// answer. Detection is script-based (Greek Unicode blocks) so it works
/// offline with no extra model calls.
/// </summary>
public static class AnswerLanguage
{
    /// <summary>
    /// True when <paramref name="value"/> contains any Greek-script character.
    /// </summary>
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

    /// <summary>
    /// True when <paramref name="answer"/> satisfies the query language.
    /// Only Greek queries are enforced (an English answer to a Greek query
    /// is a mismatch); English queries always pass since Greek proper names
    /// may legitimately appear in an English answer.
    /// </summary>
    public static bool MatchesQueryLanguage(string query, string? answer) =>
        !IsGreek(query) || IsGreek(answer);

    /// <summary>
    /// True when <paramref name="answer"/> is a refusal / no-content answer
    /// rather than a grounded claim. The citation fallback must never attach
    /// chunk IDs to these: a refusal with citations both violates the prompt
    /// contract and suppresses the did-you-mean suggestion path.
    /// </summary>
    public static bool IsRefusalLike(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return true;
        }

        return answer.Contains("no relevant content", StringComparison.OrdinalIgnoreCase)
            || answer.Contains("no information", StringComparison.OrdinalIgnoreCase)
            || answer.Contains("δεν υπάρχει", StringComparison.OrdinalIgnoreCase)
            || answer.Contains("δεν βρέθηκε", StringComparison.OrdinalIgnoreCase)
            || answer.Contains("καμία πληροφορία", StringComparison.OrdinalIgnoreCase)
            || answer.Contains("καμια πληροφορια", StringComparison.OrdinalIgnoreCase);
    }
}
