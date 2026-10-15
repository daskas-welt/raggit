using System;
using System.Collections.Generic;
using FluentAssertions;
using RAGGit.Core.Models;
using RAGGit.Retrieval;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 032 T016/T025: the narration shown while waiting is a plain-language sentence
/// per stage, follows the question's language, and never claims a source it
/// cannot back.
/// </summary>
public sealed class ReasoningTextTests
{
    private const string GreekQuestion = "Ποιος είναι ο χρόνος ανανέωσης;";
    private const string EnglishQuestion = "What is the renewal term?";

    private static readonly QueryStage[] Stages =
    [
        QueryStage.Understanding,
        QueryStage.Searching,
        QueryStage.Sources,
        QueryStage.Composing,
    ];

    [Fact]
    public void EveryStage_HasANonEmptySentence()
    {
        foreach (var stage in Stages)
        {
            var text = ReasoningText.For(
                stage,
                EnglishQuestion,
                2,
                new List<string> { "policy.pdf" }
            );

            text.Should().NotBeNullOrWhiteSpace($"stage {stage} must be explained to the user");
        }
    }

    [Fact]
    public void GreekQuestion_EveryStage_IsRenderedInGreek()
    {
        foreach (var stage in Stages)
        {
            var text = ReasoningText.For(
                stage,
                GreekQuestion,
                2,
                new List<string> { "policy.pdf" }
            );

            AnswerLanguage
                .IsGreek(text)
                .Should()
                .BeTrue($"stage {stage} must follow the question's language");
        }
    }

    [Fact]
    public void EnglishQuestion_EveryStage_IsRenderedInEnglish()
    {
        foreach (var stage in Stages)
        {
            var text = ReasoningText.For(
                stage,
                EnglishQuestion,
                2,
                new List<string> { "policy.pdf" }
            );

            AnswerLanguage.IsGreek(text).Should().BeFalse();
        }
    }

    [Fact]
    public void Sources_NamesTheSourcesAndTheCount()
    {
        var text = ReasoningText.For(
            QueryStage.Sources,
            EnglishQuestion,
            2,
            new List<string> { "policy.pdf", "renewal.pdf" }
        );

        text.Should().Contain("2").And.Contain("policy.pdf").And.Contain("renewal.pdf");
    }

    [Fact]
    public void Sources_SingleSource_UsesSingularWording()
    {
        var text = ReasoningText.For(
            QueryStage.Sources,
            EnglishQuestion,
            1,
            new List<string> { "policy.pdf" }
        );

        text.Should().Contain("1").And.Contain("policy.pdf");
        text.Should().NotContain("sources");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Sources_WithoutSources_NeverClaimsAFind(int? sourceCount)
    {
        var text = ReasoningText.For(QueryStage.Sources, EnglishQuestion, sourceCount, null);

        text.Should().StartWith("No relevant sources");
        text.Should().NotContain("1").And.NotContain("2").And.NotContain(":");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Sources_WithoutSources_InGreek_NeverClaimsAFind(int? sourceCount)
    {
        var text = ReasoningText.For(QueryStage.Sources, GreekQuestion, sourceCount, null);

        text.Should().StartWith("Δεν βρέθηκαν");
    }

    [Fact]
    public void Sources_WithoutKnownFilenames_ReportsTheCountWithoutAnIdentifier()
    {
        var documentId = Guid.NewGuid().ToString();

        var text = ReasoningText.For(QueryStage.Sources, EnglishQuestion, 1, new List<string>());

        text.Should().Contain("1");
        text.Should().NotContain(documentId);
    }

    [Fact]
    public void Sentences_NeverContainInternalIdentifiersOrPromptText()
    {
        var documentId = Guid.NewGuid().ToString();
        var forbidden = new[]
        {
            "You are a helpful assistant",
            "Context:",
            "chunk",
            "Ollama",
            "lancedb",
            ".onnx",
            "localhost",
            "MinScore",
        };

        foreach (var stage in Stages)
        {
            var text = ReasoningText.For(
                stage,
                EnglishQuestion,
                3,
                new List<string> { "policy.pdf" }
            );

            text.Should().NotContain(documentId, "an internal id must never be shown");
            foreach (var token in forbidden)
            {
                text.Should().NotContain(token, "stage text must stay plain language");
                text.ToLowerInvariant()
                    .Should()
                    .NotContain(token.ToLowerInvariant(), "case-insensitive: plain language only");
            }
        }
    }
}
