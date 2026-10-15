using System.Text.Json.Serialization;

namespace RAGGit.Core.Models;

/// <summary>
/// Identity of one reasoning update streamed while an answer is produced
/// (032, contracts/api.yaml 1.6.0). Wire values are lowercase per the
/// existing <see cref="QueryMode"/>/<see cref="QueryIntent"/> convention;
/// the stage is language-independent so tests and automation never depend on
/// the rendered sentence.
/// </summary>
public enum QueryStage
{
    [JsonStringEnumMemberName("understanding")]
    Understanding = 0,

    [JsonStringEnumMemberName("searching")]
    Searching = 1,

    [JsonStringEnumMemberName("sources")]
    Sources = 2,

    [JsonStringEnumMemberName("composing")]
    Composing = 3,
}
