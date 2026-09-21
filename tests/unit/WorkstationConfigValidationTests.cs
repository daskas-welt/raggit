using FluentAssertions;
using RAGGit.Workstation.Api.Config;
using Xunit;

namespace RAGGit.Tests.Unit;

public sealed class WorkstationConfigValidationTests
{
    [Theory]
    [InlineData(384)]
    [InlineData(768)]
    [InlineData(1024)]
    public void SupportedVectorSizes_AreValid(int size)
    {
        WorkstationConfigValidator.IsSupportedVectorSize(size).Should().BeTrue();
        var result = WorkstationConfigValidator.Validate(
            size,
            size switch
            {
                384 => "all-minilm",
                768 => "nomic-embed-text",
                _ => "bge-m3",
            },
            "./data/lancedb"
        );
        result.IsValid.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(128)]
    [InlineData(512)]
    public void UnsupportedVectorSize_FailsValidation(int size)
    {
        WorkstationConfigValidator.IsSupportedVectorSize(size).Should().BeFalse();
        var result = WorkstationConfigValidator.Validate(size, "all-minilm", "./data/lancedb");
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("VectorSize");
    }

    [Theory]
    [InlineData("all-minilm", 768)]
    [InlineData("nomic-embed-text", 384)]
    public void MismatchedEmbedModel_EmitsWarning(string embedModel, int vectorSize)
    {
        WorkstationConfigValidator.IsEmbedModelAligned(embedModel, vectorSize).Should().BeFalse();
        var result = WorkstationConfigValidator.Validate(vectorSize, embedModel, "./data/lancedb");
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
    [InlineData("bge-m3", 1024)]
    [InlineData("snowflake-arctic-embed2", 1024)]
    public void AlignedEmbedModel_NoWarning(string embedModel, int vectorSize)
    {
        WorkstationConfigValidator.IsEmbedModelAligned(embedModel, vectorSize).Should().BeTrue();
        var result = WorkstationConfigValidator.Validate(vectorSize, embedModel, "./data/lancedb");
        result.Warnings.Should().BeEmpty();
    }
}
