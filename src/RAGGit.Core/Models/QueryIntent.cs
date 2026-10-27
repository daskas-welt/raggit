using System.Text.Json.Serialization;

namespace RAGGit.Core.Models;

/// <summary>
/// Effective retrieval granularity actually used for a query (030).
/// <see cref="Granular"/> is the safe default; <see cref="Broad"/> uses
/// parent-context retrieval. The rule-based classifier picks it per query.
/// The wire values are lowercase (<c>broad|granular</c>).
/// </summary>
public enum QueryIntent
{
    [JsonStringEnumMemberName("granular")]
    Granular = 0,

    [JsonStringEnumMemberName("broad")]
    Broad = 1,
}
