using System;
using System.Collections.Generic;

namespace RAGGit.Workstation.Api.Config;

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Warnings, string? Error);

public static class WorkstationConfigValidator
{
    public static ValidationResult Validate(
        int vectorSize,
        string embedModel,
        string? vectorDbPath,
        string? qdrantPath
    )
    {
        var warnings = new List<string>();
        string? error = null;

        if (!IsSupportedVectorSize(vectorSize))
        {
            error = $"VectorDb:VectorSize {vectorSize} is not supported — must be 384 or 768.";
            return new ValidationResult(false, warnings, error);
        }

        if (!IsEmbedModelAligned(embedModel, vectorSize))
        {
            warnings.Add(
                $"EmbedModel '{embedModel}' mismatched with VectorSize {vectorSize} — expected {(vectorSize == 384 ? "all-minilm ↔ 384" : "nomic-embed-text ↔ 768")}."
            );
        }

        if (!string.IsNullOrWhiteSpace(qdrantPath) && !string.IsNullOrWhiteSpace(vectorDbPath))
        {
            var normVector = vectorDbPath.Trim().TrimEnd('/', '\\');
            var normQdrant = qdrantPath.Trim().TrimEnd('/', '\\');
            if (!string.Equals(normVector, normQdrant, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(
                    $"Legacy Qdrant:Path '{qdrantPath}' disagrees with VectorDb:Path '{vectorDbPath}' — VectorDb:Path is the single source of truth."
                );
            }
        }

        return new ValidationResult(true, warnings, null);
    }

    public static bool IsSupportedVectorSize(int vectorSize) =>
        vectorSize == 384 || vectorSize == 768;

    public static bool IsEmbedModelAligned(string embedModel, int vectorSize)
    {
        if (string.IsNullOrWhiteSpace(embedModel))
            return true;
        var m = embedModel.Trim().ToLowerInvariant();
        if (m.Contains("all-minilm") || m.Contains("all_minilm") || m.Contains("bge-micro"))
            return vectorSize == 384;
        if (m.Contains("nomic-embed"))
            return vectorSize == 768;
        // Unknown model — no enforcement
        return true;
    }
}
