using System.Text.Json.Serialization;

namespace RAGGit.Core.Models;

/// <summary>
/// Requested retrieval granularity for <c>POST /api/queries</c> (030).
/// The wire values are lowercase (<c>auto|broad|specific</c>) per
/// contracts/api.yaml; matching is case-insensitive.
/// </summary>
public enum QueryMode
{
    [JsonStringEnumMemberName("auto")]
    Auto = 0,

    [JsonStringEnumMemberName("broad")]
    Broad = 1,

    [JsonStringEnumMemberName("specific")]
    Specific = 2,
}

/// <summary>
/// Effective retrieval granularity actually used for a query (030).
/// <see cref="QueryMode.Specific"/> resolves to <see cref="Granular"/>;
/// <see cref="QueryMode.Broad"/> to <see cref="Broad"/>; <see cref="QueryMode.Auto"/>
/// runs the rule-based classifier (safe default <see cref="Granular"/>).
/// The wire values are lowercase (<c>broad|granular</c>).
/// </summary>
public enum QueryIntent
{
    [JsonStringEnumMemberName("granular")]
    Granular = 0,

    [JsonStringEnumMemberName("broad")]
    Broad = 1,
}
