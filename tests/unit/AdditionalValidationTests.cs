using FluentAssertions;
using RAGGit.Workstation.Api.Config;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// Additional edge-case validation per T028 polish.
/// Covers blank paths and unknown embed models.
/// </summary>
public sealed class AdditionalValidationTests
{
    [Fact]
    public void Validate_BlankVectorDbPath_AllowsWithNoError()
    {
        var result = WorkstationConfigValidator.Validate(384, "all-minilm", "", null);
        result.IsValid.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Validate_UnknownEmbedModel_NoWarning()
    {
        var result = WorkstationConfigValidator.Validate(
            384,
            "unknown-model",
            "./data/lancedb",
            null
        );
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ZeroVectorSize_Fails()
    {
        WorkstationConfigValidator.IsSupportedVectorSize(0).Should().BeFalse();
        var result = WorkstationConfigValidator.Validate(0, "all-minilm", "./data/lancedb", null);
        result.IsValid.Should().BeFalse();
    }
}
