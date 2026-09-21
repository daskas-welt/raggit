using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Core.Abstractions;
using RAGGit.Retrieval;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// Same-language answers: a Greek query must get a Greek answer, enforced
/// with a single retry when the local model answers in English.
/// </summary>
public sealed class AnswerLanguageTests
{
    [Fact]
    public void IsGreek_DetectsScript()
    {
        AnswerLanguage.IsGreek("Ποιος είναι ο συνολικός χρόνος προϋπηρεσίας;").Should().BeTrue();
        AnswerLanguage.IsGreek("What is the total service time?").Should().BeFalse();
        AnswerLanguage.IsGreek("1 year and 5 months").Should().BeFalse();
        AnswerLanguage.IsGreek("1 έτος και 5 μήνες").Should().BeTrue();
    }

    [Fact]
    public void MatchesQueryLanguage_EnforcesGreekQueriesOnly()
    {
        AnswerLanguage
            .MatchesQueryLanguage("Ποιος είναι ο χρόνος;", "1 year and 5 months")
            .Should()
            .BeFalse();
        AnswerLanguage
            .MatchesQueryLanguage("Ποιος είναι ο χρόνος;", "1 έτος και 5 μήνες")
            .Should()
            .BeTrue();
        // English queries always pass: Greek proper names may appear.
        AnswerLanguage
            .MatchesQueryLanguage("What is the time?", "Δημοπούλου Πηνελόπη: 2 months")
            .Should()
            .BeTrue();
    }

    [Fact]
    public void IsRefusalLike_DetectsRefusals()
    {
        AnswerLanguage.IsRefusalLike("no relevant content found").Should().BeTrue();
        AnswerLanguage
            .IsRefusalLike("There is no information about this person.")
            .Should()
            .BeTrue();
        AnswerLanguage.IsRefusalLike("Δεν υπάρχει σχετικό περιεχόμενο.").Should().BeTrue();
        AnswerLanguage.IsRefusalLike(null).Should().BeTrue();
        AnswerLanguage.IsRefusalLike("Ο χρόνος είναι 1 έτος και 5 μήνες.").Should().BeFalse();
        AnswerLanguage.IsRefusalLike("Maria works in accounting.").Should().BeFalse();
    }

    [Fact]
    public async Task Generate_GreekQueryEnglishAnswer_RetriesOnceInGreek()
    {
        var chunkId = Guid.NewGuid();
        var chunks = new List<SearchResult>
        {
            new(
                chunkId,
                Guid.NewGuid().ToString(),
                "Αναγνωρίστηκε προϋπηρεσία εντός Δημοσίου τομέα: ενός (1) έτους και πέντε (5) μηνών.",
                0,
                0.95f
            ),
        };
        var llm = new SequenceLlm(
            "The total period is 1 year and 5 months.",
            $"Ο συνολικός χρόνος είναι 1 έτος και 5 μήνες. [{chunkId}]"
        );
        var generation = new GenerationService(llm);

        var (answer, _) = await generation.GenerateAsync(
            "Ποιος είναι ο συνολικός χρόνος προϋπηρεσίας της Πηνελόπης Δημοπούλου;",
            chunks
        );

        answer.Should().Contain("έτος");
        llm.CallCount.Should().Be(2);
        llm.LastSystemPrompt.Should().Contain("MUST answer in Greek");
    }

    [Fact]
    public async Task Generate_GreekQueryGreekAnswer_NoRetry()
    {
        var chunks = new List<SearchResult>
        {
            new(Guid.NewGuid(), Guid.NewGuid().ToString(), "Αναγνωρίστηκε προϋπηρεσία.", 0, 0.95f),
        };
        var llm = new SequenceLlm("Ο χρόνος είναι 1 έτος και 5 μήνες.");
        var generation = new GenerationService(llm);

        var (answer, _) = await generation.GenerateAsync("Ποιος είναι ο χρόνος;", chunks);

        answer.Should().Contain("έτος");
        llm.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Generate_Prompt_DeclaresQuestionLanguage()
    {
        var chunkId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var chunks = new List<SearchResult>
        {
            new(chunkId, Guid.NewGuid().ToString(), "Αναγνωρίστηκε προϋπηρεσία.", 0, 0.95f),
        };
        var llm = new CapturingLlm($"Απάντηση. [{chunkId}]");
        var generation = new GenerationService(llm);

        await generation.GenerateAsync("Ποιος είναι ο χρόνος;", chunks, null);

        llm.LastSystemPrompt.Should().Contain("MUST answer in Greek");
        llm.LastUserPrompt.Should().Contain("answer in Greek");
    }

    private sealed class SequenceLlm : ILlmClient
    {
        private readonly Queue<string> _responses;

        public SequenceLlm(params string[] responses) => _responses = new Queue<string>(responses);

        public int CallCount { get; private set; }

        public string LastSystemPrompt { get; private set; } = string.Empty;

        public Task<string> ChatAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken ct = default
        )
        {
            CallCount++;
            LastSystemPrompt = systemPrompt;
            return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : string.Empty);
        }

        public Task<bool> IsHealthyAsync(CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class CapturingLlm : ILlmClient
    {
        private readonly string _response;

        public CapturingLlm(string response) => _response = response;

        public string LastSystemPrompt { get; private set; } = string.Empty;

        public string LastUserPrompt { get; private set; } = string.Empty;

        public Task<string> ChatAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken ct = default
        )
        {
            LastSystemPrompt = systemPrompt;
            LastUserPrompt = userPrompt;
            return Task.FromResult(_response);
        }

        public Task<bool> IsHealthyAsync(CancellationToken ct = default) => Task.FromResult(true);
    }
}
