using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RAGGit.Core.Models;

/// <summary>
/// One newline-delimited event on <c>POST /api/queries/stream</c> (032,
/// contracts/api.yaml 1.6.0). A single envelope with a <c>type</c> discriminator
/// keeps the client parser trivial and forward compatible: unknown fields are
/// ignored, and an unknown <c>type</c> is skipped by the reader.
/// </summary>
public sealed class QueryStreamEvent
{
    /// <summary>Work-in-progress event (carries the <see cref="QueryProgress"/> fields).</summary>
    public const string ProgressType = "progress";

    /// <summary>Terminal event carrying the same field set as <see cref="QueryResponse"/>.</summary>
    public const string AnswerType = "answer";

    /// <summary>Terminal event for a failure after the stream has started.</summary>
    public const string ErrorType = "error";

    /// <summary><c>progress</c>, <c>answer</c>, or <c>error</c>.</summary>
    public string Type { get; set; } = string.Empty;

    // --- progress variant ---

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public QueryStage? Stage { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SourceCount { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? DocumentNames { get; set; }

    // --- answer variant (same field set as QueryResponse) ---

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Answer { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Citation>? Citations { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Guid>? RetrievedChunkIds { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LatencyMs { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public QueryIntent? Mode { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PersonSuggestion>? SuggestedPersons { get; set; }

    // --- error variant ---

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Status { get; set; }

    [JsonIgnore]
    public bool IsProgress => string.Equals(Type, ProgressType, StringComparison.Ordinal);

    [JsonIgnore]
    public bool IsAnswer => string.Equals(Type, AnswerType, StringComparison.Ordinal);

    [JsonIgnore]
    public bool IsError => string.Equals(Type, ErrorType, StringComparison.Ordinal);

    /// <summary>Builds a <c>progress</c> event from the workstation's narration.</summary>
    public static QueryStreamEvent FromProgress(QueryProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return new QueryStreamEvent
        {
            Type = ProgressType,
            Stage = progress.Stage,
            Text = progress.Text,
            SourceCount = progress.SourceCount,
            DocumentNames = progress.DocumentNames,
        };
    }

    /// <summary>Builds the terminal <c>answer</c> event from the query outcome.</summary>
    public static QueryStreamEvent FromAnswer(QueryResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new QueryStreamEvent
        {
            Type = AnswerType,
            Answer = response.Answer,
            Citations = response.Citations,
            RetrievedChunkIds = response.RetrievedChunkIds,
            LatencyMs = response.LatencyMs,
            Mode = response.Mode,
            SuggestedPersons = response.SuggestedPersons,
        };
    }

    /// <summary>Builds a terminal <c>error</c> event matching the non-streaming response.</summary>
    public static QueryStreamEvent FromError(string error, int status) =>
        new()
        {
            Type = ErrorType,
            Error = error,
            Status = status,
        };

    /// <summary>Reads this event as a progress update.</summary>
    public QueryProgress ToProgress() =>
        new()
        {
            Stage = Stage ?? QueryStage.Understanding,
            Text = Text ?? string.Empty,
            SourceCount = SourceCount,
            DocumentNames = DocumentNames,
        };

    /// <summary>
    /// Projects an answer event back onto the non-streaming response shape, so
    /// both transports share one post-answer code path.
    /// </summary>
    public QueryResponse ToResponse() =>
        new()
        {
            Answer = Answer ?? string.Empty,
            Citations = Citations ?? new List<Citation>(),
            RetrievedChunkIds = RetrievedChunkIds ?? new List<Guid>(),
            LatencyMs = LatencyMs ?? 0,
            Mode = Mode,
            SuggestedPersons = SuggestedPersons ?? new List<PersonSuggestion>(),
        };
}
