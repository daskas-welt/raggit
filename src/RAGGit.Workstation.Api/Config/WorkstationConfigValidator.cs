using System;
using System.Collections.Generic;

namespace RAGGit.Workstation.Api.Config;

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Warnings, string? Error);

public static class WorkstationConfigValidator
{
    // Stub for T005 red — real validation lands in T009.
    public static ValidationResult Validate(int vectorSize, string embedModel, string? vectorDbPath, string? qdrantPath)
    {
        return new ValidationResult(true, Array.Empty<string>(), null);
    }

    public static bool IsSupportedVectorSize(int vectorSize) => vectorSize == 384 || vectorSize == 768;

    public static bool IsEmbedModelAligned(string embedModel, int vectorSize)
    {
        return true;
    }
}
