using FluentAssertions;
using RAGGit.Workstation.Api.Config;
using Xunit;

namespace RAGGit.Tests.Unit;

public sealed class WorkstationConfigValidationTests
{
    [Theory]
    [InlineData(384)]
    [InlineData(768)]
    public void SupportedVectorSizes_AreValid(int size)
    {
        WorkstationConfigValidator.IsSupportedVectorSize(size).Should().BeTrue();
        var result = WorkstationConfigValidator.Validate(
            size,
            size == 384 ? "all-minilm" : "nomic-embed-text",
            "./data/lancedb",
            null
        );
        result.IsValid.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(128)]
    [InlineData(512)]
    [InlineData(1024)]
    public void UnsupportedVectorSize_FailsValidation(int size)
    {
        WorkstationConfigValidator.IsSupportedVectorSize(size).Should().BeFalse();
        var result = WorkstationConfigValidator.Validate(
            size,
            "all-minilm",
            "./data/lancedb",
            null
        );
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("VectorSize");
    }

    [Theory]
    [InlineData("all-minilm", 768)]
    [InlineData("nomic-embed-text", 384)]
    public void MismatchedEmbedModel_EmitsWarning(string embedModel, int vectorSize)
    {
        WorkstationConfigValidator.IsEmbedModelAligned(embedModel, vectorSize).Should().BeFalse();
        var result = WorkstationConfigValidator.Validate(
            vectorSize,
            embedModel,
            "./data/lancedb",
            null
        );
        result.IsValid.Should().BeTrue(); // mismatch is warning, not failure
        result
            .Warnings.Should()
            .Contain(w =>
                w.Contains("EmbedModel") || w.Contains("VectorSize") || w.Contains("mismatch")
            );
    }

    [Theory]
    [InlineData("all-minilm", 384)]
    [InlineData("nomic-embed-text", 768)]
    public void AlignedEmbedModel_NoWarning(string embedModel, int vectorSize)
    {
        WorkstationConfigValidator.IsEmbedModelAligned(embedModel, vectorSize).Should().BeTrue();
        var result = WorkstationConfigValidator.Validate(
            vectorSize,
            embedModel,
            "./data/lancedb",
            null
        );
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void LegacyQdrantPath_Disagreeing_EmitsWarningNotFailure()
    {
        var result = WorkstationConfigValidator.Validate(
            384,
            "all-minilm",
            "./data/lancedb",
            "./data/qdrant"
        );
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("Qdrant") || w.Contains("VectorDb"));
    }

    [Fact]
    public void LegacyQdrantPath_SameAsVectorDb_NoWarning()
    {
        var result = WorkstationConfigValidator.Validate(
            384,
            "all-minilm",
            "./data/lancedb",
            "./data/lancedb"
        );
        result.Warnings.Should().NotContain(w => w.Contains("Qdrant"));
    }

    [Fact]
    public void LegacyQdrantPath_Null_NoWarning()
    {
        var result = WorkstationConfigValidator.Validate(384, "all-minilm", "./data/lancedb", null);
        result.Warnings.Should().NotContain(w => w.Contains("Qdrant"));
    }
}
