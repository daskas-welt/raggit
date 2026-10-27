using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 032 T007: the NDJSON stream contract (contracts/api.yaml 1.6.0) — lowercase
/// stage values, one envelope discriminated by <c>type</c>, unused variant
/// fields omitted, and an answer event that mirrors the classic response shape.
/// </summary>
public sealed class QueryStreamEventTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Theory]
    [InlineData(QueryStage.Understanding, "understanding")]
    [InlineData(QueryStage.Searching, "searching")]
    [InlineData(QueryStage.Sources, "sources")]
    [InlineData(QueryStage.Composing, "composing")]
    public void Progress_UsesLowercaseStageWireValues(QueryStage stage, string expected)
    {
        var json = JsonSerializer.Serialize(
            QueryStreamEvent.FromProgress(
                new QueryProgress { Stage = stage, Text = "some sentence" }
            ),
            Options
        );

        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("type").GetString().Should().Be("progress");
        document.RootElement.GetProperty("stage").GetString().Should().Be(expected);
        document.RootElement.GetProperty("text").GetString().Should().Be("some sentence");
    }

    [Fact]
    public void Progress_OmitsFieldsBelongingToOtherVariants()
    {
        var json = JsonSerializer.Serialize(
            QueryStreamEvent.FromProgress(
                new QueryProgress { Stage = QueryStage.Searching, Text = "Searching…" }
            ),
            Options
        );

        using var document = JsonDocument.Parse(json);
        document.RootElement.TryGetProperty("answer", out _).Should().BeFalse();
        document.RootElement.TryGetProperty("citations", out _).Should().BeFalse();
        document.RootElement.TryGetProperty("error", out _).Should().BeFalse();
        document.RootElement.TryGetProperty("status", out _).Should().BeFalse();
        // SourceCount/DocumentNames belong to the sources stage only (data-model §2).
        document.RootElement.TryGetProperty("sourceCount", out _).Should().BeFalse();
        document.RootElement.TryGetProperty("documentNames", out _).Should().BeFalse();
    }

    [Fact]
    public void SourcesProgress_CarriesCountAndNames()
    {
        var streamEvent = QueryStreamEvent.FromProgress(
            new QueryProgress
            {
                Stage = QueryStage.Sources,
                Text = "Found 2 relevant sources: a.pdf, b.pdf",
                SourceCount = 2,
                DocumentNames = new List<string> { "a.pdf", "b.pdf" },
            }
        );

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(streamEvent, Options));

        document.RootElement.GetProperty("sourceCount").GetInt32().Should().Be(2);
        document
            .RootElement.GetProperty("documentNames")
            .EnumerateArray()
            .Select(item => item.GetString())
            .Should()
            .Equal("a.pdf", "b.pdf");
    }

    [Fact]
    public void Progress_DeserializesWithAndWithoutSourceFields()
    {
        var withSources = JsonSerializer.Deserialize<QueryStreamEvent>(
            "{\"type\":\"progress\",\"stage\":\"sources\",\"text\":\"t\",\"sourceCount\":1,\"documentNames\":[\"a.pdf\"]}",
            Options
        )!;
        withSources.IsProgress.Should().BeTrue();
        withSources.SourceCount.Should().Be(1);
        withSources.ToProgress().DocumentNames.Should().Equal("a.pdf");

        var withoutSources = JsonSerializer.Deserialize<QueryStreamEvent>(
            "{\"type\":\"progress\",\"stage\":\"composing\",\"text\":\"t\"}",
            Options
        )!;
        withoutSources.SourceCount.Should().BeNull();
        withoutSources.ToProgress().DocumentNames.Should().BeNull();
    }

    [Fact]
    public void Answer_RoundTripsOntoTheClassicResponseShape()
    {
        var documentId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        var response = new QueryResponse
        {
            Answer = "The renewal term is 30 days.",
            Citations = new List<Citation>
            {
                new()
                {
                    DocumentId = documentId,
                    DocumentName = "policy.pdf",
                    ChunkId = chunkId,
                    Text = "Renewal term: 30 days.",
                    Ordinal = 1,
                },
            },
            RetrievedChunkIds = new List<Guid> { chunkId },
            LatencyMs = 4210,
            SuggestedPersons = new List<PersonSuggestion> { new() { Name = "Someone" } },
        };

        var roundTripped = QueryStreamEvent.FromAnswer(response).ToResponse();

        roundTripped.Answer.Should().Be(response.Answer);
        roundTripped.LatencyMs.Should().Be(4210);
        roundTripped.RetrievedChunkIds.Should().Equal(chunkId);
        roundTripped.SuggestedPersons.Should().ContainSingle().Which.Name.Should().Be("Someone");
        roundTripped
            .Citations.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    DocumentId = documentId,
                    DocumentName = "policy.pdf",
                    ChunkId = chunkId,
                    Text = "Renewal term: 30 days.",
                    Ordinal = 1,
                }
            );
    }

    [Fact]
    public void Answer_CarriesOnlyFieldsTheClassicResponseAlsoHas()
    {
        var response = new QueryResponse { Answer = "a" };

        using var classic = JsonDocument.Parse(JsonSerializer.Serialize(response, Options));
        using var streamed = JsonDocument.Parse(
            JsonSerializer.Serialize(QueryStreamEvent.FromAnswer(response), Options)
        );

        var classicNames = classic
            .RootElement.EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        var streamedNames = streamed
            .RootElement.EnumerateObject()
            .Select(property => property.Name)
            .Where(name => name != "type")
            .ToHashSet(StringComparer.Ordinal);

        streamedNames.Should().BeSubsetOf(classicNames);
        streamedNames.Should().Contain("answer").And.Contain("citations");
    }

    [Fact]
    public void Error_CarriesTheClassicMessageAndStatus()
    {
        var streamEvent = QueryStreamEvent.FromError("model unavailable offline", 503);

        streamEvent.IsError.Should().BeTrue();
        streamEvent.Error.Should().Be("model unavailable offline");
        streamEvent.Status.Should().Be(503);
    }

    [Fact]
    public void UnknownType_IsNotAnyKnownVariant()
    {
        var streamEvent = JsonSerializer.Deserialize<QueryStreamEvent>(
            "{\"type\":\"telemetry\",\"text\":\"ignore me\"}",
            Options
        )!;

        streamEvent.IsProgress.Should().BeFalse();
        streamEvent.IsAnswer.Should().BeFalse();
        streamEvent.IsError.Should().BeFalse();
    }

    [Fact]
    public void UnknownFields_AreIgnored()
    {
        var streamEvent = JsonSerializer.Deserialize<QueryStreamEvent>(
            "{\"type\":\"progress\",\"stage\":\"composing\",\"text\":\"t\",\"futureField\":123}",
            Options
        )!;

        streamEvent.IsProgress.Should().BeTrue();
        streamEvent.Stage.Should().Be(QueryStage.Composing);
    }
}
