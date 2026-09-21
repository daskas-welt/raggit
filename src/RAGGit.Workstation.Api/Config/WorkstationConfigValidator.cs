using System;
using System.Collections.Generic;

namespace RAGGit.Workstation.Api.Config;

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Warnings, string? Error);

public static class WorkstationConfigValidator
{
    public static ValidationResult Validate(int vectorSize, string embedModel, string? vectorDbPath)
    {
        var warnings = new List<string>();
        string? error = null;

        if (!IsSupportedVectorSize(vectorSize))
        {
            error =
                $"VectorDb:VectorSize {vectorSize} is not supported — must be 384, 768, or 1024.";
            return new ValidationResult(false, warnings, error);
        }

        if (!IsEmbedModelAligned(embedModel, vectorSize))
        {
            warnings.Add(
                $"EmbedModel '{embedModel}' mismatched with VectorSize {vectorSize} — expected {ExpectedModelFor(vectorSize)}."
            );
        }

        return new ValidationResult(true, warnings, null);
    }

    public static bool IsSupportedVectorSize(int vectorSize) =>
        vectorSize == 384 || vectorSize == 768 || vectorSize == 1024;

    public static bool IsEmbedModelAligned(string embedModel, int vectorSize)
    {
        if (string.IsNullOrWhiteSpace(embedModel))
            return true;
        var m = embedModel.Trim().ToLowerInvariant();
        // 384-dim English + multilingual MiniLM family.
        if (
            m.Contains("all-minilm")
            || m.Contains("all_minilm")
            || m.Contains("bge-micro")
            || m.Contains("paraphrase-multilingual")
        )
            return vectorSize == 384;
        // 768-dim models (English nomic v1, multilingual arctic v1 medium).
        if (m.Contains("nomic-embed"))
            return vectorSize == 768;
        // 1024-dim models: multilingual bge-m3 / arctic-embed2 (non-English
        // libraries, e.g. Greek) and English mxbai-embed-large.
        if (m.Contains("bge-m3") || m.Contains("arctic-embed2") || m.Contains("mxbai"))
            return vectorSize == 1024;
        // Unknown model — no enforcement
        return true;
    }

    private static string ExpectedModelFor(int vectorSize) =>
        vectorSize switch
        {
            384 => "all-minilm ↔ 384",
            768 => "nomic-embed-text ↔ 768",
            _ => "bge-m3 / snowflake-arctic-embed2 ↔ 1024",
        };
}
