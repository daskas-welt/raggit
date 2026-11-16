using System;
using RAGGit.Core.Models;

namespace RAGGit.Documents;

/// <summary>
/// The document-eligibility rules (033 FR-018/FR-019/FR-025): only a grounded,
/// cited answer can become a document, and only within the size/row caps.
/// </summary>
public static class DocumentEligibility
{
    /// <summary>25 MiB cap; well above a normal report, below the 100 MiB upload cap.</summary>
    public const long MaxDocumentBytes = 25L * 1024 * 1024;

    /// <summary>Cap on spreadsheet rows; keeps the renderer and Excel responsive.</summary>
    public const int MaxTableRows = 5_000;

    public static bool IsContentEligible(QueryDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        if (
            string.Equals(
                detail.Answer.Trim(),
                "no relevant content found",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return false;
        }

        return detail.Citations.Count > 0;
    }

    public static bool IsWithinCaps(long byteCount, int rowCount) =>
        byteCount is >= 0 and <= MaxDocumentBytes && rowCount is >= 0 and <= MaxTableRows;
}
