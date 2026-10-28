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
    /// Negative-answer phrases. Small models rarely echo the exact prompt
    /// refusal, so matching only "no relevant content found" lets a paraphrased
    /// refusal reach the citation fallback. Strictness matters more than
    /// precision here: a false positive only loses a fallback citation, while a
    /// false negative attaches a citation to an answer that says the content is
    /// absent.
    /// </summary>
    private static readonly string[] RefusalPhrases =
    [
        // English
        "no relevant content",
        "no relevant information",
        "no information",
        "no mention",
        "no specific information",
        "no details",
        "no answer",
        "no such",
        "not contain",
        "doesn't contain",
        "not include",
        "doesn't include",
        "not mention",
        "doesn't mention",
        "not state",
        "doesn't state",
        "not mentioned",
        "not specified",
        "not provided",
        "not available",
        "not present",
        "not found",
        "cannot find",
        "can't find",
        "could not find",
        "couldn't find",
        "unable to find",
        "not enough",
        "insufficient information",
        "i don't know",
        "i do not know",
        "does not have",
        "doesn't have",
        "context does not",
        "text does not",
        "document does not",
        // Greek
        "δεν υπάρχει",
        "δεν υπάρχουν",
        "δεν βρέθηκε",
        "δεν βρέθηκαν",
        "καμία πληροφορία",
        "καμια πληροφορια",
        "δεν περιέχει",
        "δεν περιέχουν",
        "δεν αναφέρεται",
        "δεν αναφέρονται",
        "δεν αναγράφεται",
        "δεν αναγράφονται",
        "δεν προκύπτει",
        "δεν εντοπίστηκε",
        "δεν εντοπίστηκαν",
        "δεν είναι διαθέσιμο",
        "δεν είναι διαθέσιμη",
        "δεν μπορώ να βρω",
        "δεν μπορούμε να βρούμε",
        "χωρίς πληροφορίες",
        "δεν έχουμε πληροφορίες",
        "δεν δίνεται",
        "δεν παρέχεται",
        "δεν τεκμηριώνεται",
    ];

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

        foreach (var phrase in RefusalPhrases)
        {
            if (answer.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
