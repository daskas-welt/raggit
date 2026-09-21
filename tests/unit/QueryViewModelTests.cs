using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Maui.Services;
using RAGGit.Client.Maui.ViewModels;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 006-client-architecture US2/FR-004/FR-005 + SC-006: the Ask screen is a
/// conversation of ordered person/assistant messages, assistant replies carry
/// the answer (citations tracked alongside), and history is retained in-session.
/// </summary>
public sealed class QueryViewModelTests
{
    [Fact]
    public async Task Ask_AppendsUserThenAssistantMessage()
    {
        var viewModel = CreateViewModel(answer: "grounded answer", citationCount: 2);
        viewModel.QueryText = "what is offline rag?";

        await viewModel.AskCommand.ExecuteAsync(null);

        viewModel.Messages.Should().HaveCount(2);
        viewModel.Messages[0].IsUser.Should().BeTrue();
        viewModel.Messages[0].Text.Should().Be("what is offline rag?");
        viewModel.Messages[1].IsUser.Should().BeFalse();
        viewModel.Messages[1].Text.Should().Be("grounded answer");
        viewModel.Messages[1].Citations.Should().HaveCount(2);
        viewModel.Messages[1].HasCitations.Should().BeTrue();
        viewModel.Citations.Should().HaveCount(2);
        viewModel.HasCitations.Should().BeTrue();
    }

    [Fact]
    public async Task Ask_NoRelevantContent_RepliesWithZeroCitations()
    {
        var viewModel = CreateViewModel(answer: "no relevant content found", citationCount: 0);
        viewModel.QueryText = "obscure question";

        await viewModel.AskCommand.ExecuteAsync(null);

        viewModel.Messages.Should().HaveCount(2);
        viewModel.Messages[1].Text.Should().Be("no relevant content found");
        viewModel.Citations.Should().BeEmpty();
        viewModel.HasCitations.Should().BeFalse();
    }

    [Fact]
    public async Task Ask_Repeated_PreservesAtLeastTwentyMessagesInOrder()
    {
        var viewModel = CreateViewModel(answer: "a", citationCount: 1);

        for (var i = 0; i < 11; i++)
        {
            viewModel.QueryText = $"question {i}";
            await viewModel.AskCommand.ExecuteAsync(null);
        }

        viewModel.Messages.Should().HaveCount(22);
        viewModel
            .Messages.Select(m => m.IsUser)
            .Should()
            .Equal(Enumerable.Range(0, 22).Select(i => i % 2 == 0));
        viewModel.Messages[0].Text.Should().Be("question 0");
        viewModel.Messages[20].Text.Should().Be("question 10");
    }

    [Fact]
    public async Task Ask_WithSuggestedPersons_PopulatesChips()
    {
        var viewModel = CreateViewModel(
            answer: "no relevant content found",
            citationCount: 0,
            suggestedPersons: new[] { "Δημοπούλου Πηνελόπη του Σωτηρίου" }
        );
        viewModel.QueryText = "συνολικό χρόνο προϋπηρεσίας της δημοπούλου αρετής";

        await viewModel.AskCommand.ExecuteAsync(null);

        viewModel.SuggestedPersons.Should().HaveCount(1);
        viewModel.SuggestedPersons[0].Name.Should().Contain("Πηνελόπη");
        viewModel.HasSuggestedPersons.Should().BeTrue();
    }

    [Fact]
    public async Task UseSuggestedPerson_SwapsTrailingName_AndRequeries()
    {
#pragma warning disable xUnit1031 // Test stub reads the request body synchronously.
        var requests = new List<string>();
        var handler = new StubHandler(request =>
        {
            var body = request.Content!.ReadAsStringAsync().Result;
            requests.Add(body);
            var payload =
                requests.Count == 1
                    ? JsonSerializer.Serialize(
                        new
                        {
                            answer = "no relevant content found Μήπως εννοούσατε: Δημοπούλου Πηνελόπη του Σωτηρίου; Παρακαλώ επιβεβαιώστε.",
                            citations = Array.Empty<object>(),
                            retrievedChunkIds = Array.Empty<Guid>(),
                            latencyMs = 7,
                            suggestedPersons = new[]
                            {
                                new
                                {
                                    name = "Δημοπούλου Πηνελόπη του Σωτηρίου",
                                    documentId = (Guid?)null,
                                },
                            },
                        }
                    )
                    : JsonSerializer.Serialize(
                        new
                        {
                            answer = "3 έτη 2 μήνες 15 ημέρες",
                            citations = new[]
                            {
                                new
                                {
                                    documentId = Guid.NewGuid(),
                                    chunkId = Guid.NewGuid(),
                                    text = "quote",
                                    ordinal = 0,
                                },
                            },
                            retrievedChunkIds = Array.Empty<Guid>(),
                            latencyMs = 7,
                            suggestedPersons = Array.Empty<object>(),
                        }
                    );
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload),
            };
        });
        var viewModel = new QueryViewModel(
            new QueryApiClient(
                new HttpClient(handler) { BaseAddress = new Uri("https://w.local/") }
            )
        );
        viewModel.QueryText =
            "συνολικό χρόνο προϋπηρεσίας εντός δημοσίου τομέα της δημοπούλου αρετής";

        await viewModel.AskCommand.ExecuteAsync(null);
        viewModel.HasSuggestedPersons.Should().BeTrue();

        await viewModel.UseSuggestedPersonCommand.ExecuteAsync(viewModel.SuggestedPersons[0]);
#pragma warning restore xUnit1031

        requests.Should().HaveCount(2);
        var followUp = JsonDocument.Parse(requests[1]).RootElement.GetProperty("query").GetString();
        followUp.Should().Contain("Δημοπούλου Πηνελόπη");
        followUp.Should().NotContain("αρετής");
        viewModel.Messages.Should().HaveCount(4);
        viewModel.HasSuggestedPersons.Should().BeFalse("follow-up answer carries citations");
    }

    [Fact]
    public void BuildFollowUpQuestion_NoHistory_FallsBackToTemplate()
    {
        var viewModel = CreateViewModel(answer: "a", citationCount: 0);

        var greek = viewModel.BuildFollowUpQuestion("Δημοπούλου Πηνελόπη");
        greek.Should().Contain("Δημοπούλου Πηνελόπη");

        var english = viewModel.BuildFollowUpQuestion("Maria Schmidt");
        english.Should().Contain("Maria Schmidt");
    }

    [Fact]
    public async Task Reask_WhileBusy_DoesNothing()
    {
        var viewModel = CreateViewModel(answer: "a", citationCount: 0);
        viewModel.IsBusy = true;

        await viewModel.ReaskAsync("another question");

        viewModel.Messages.Should().BeEmpty();
        viewModel.UseSuggestedPersonCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Reask_RunsGivenPrompt_AsFreshQuestion()
    {
        var viewModel = CreateViewModel(answer: "grounded answer", citationCount: 1);

        await viewModel.ReaskAsync("  original history prompt  ");

        viewModel.Messages.Should().HaveCount(2);
        viewModel.Messages[0].IsUser.Should().BeTrue();
        viewModel.Messages[0].Text.Should().Be("original history prompt");
        viewModel.Messages[1].Text.Should().Be("grounded answer");
        viewModel.QueryText.Should().BeEmpty();
    }

    [Fact]
    public async Task Reask_EmptyPrompt_SurfacesStatusWithoutAsking()
    {
        var viewModel = CreateViewModel(answer: "a", citationCount: 0);

        await viewModel.ReaskAsync("   ");

        viewModel.Messages.Should().BeEmpty();
        viewModel.StatusMessage.Should().Be("Please enter a question.");
    }

    [Fact]
    public async Task Ask_OfflineModelUnavailable_SurfacesStatus()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(
            HttpStatusCode.ServiceUnavailable
        )
        {
            Content = new StringContent("model unavailable offline"),
        });
        var viewModel = new QueryViewModel(
            new QueryApiClient(
                new HttpClient(handler) { BaseAddress = new Uri("https://w.local/") }
            )
        );
        viewModel.QueryText = "q";

        await viewModel.AskCommand.ExecuteAsync(null);

        viewModel.StatusMessage.Should().Be("model unavailable offline");
    }

    private static QueryViewModel CreateViewModel(
        string answer,
        int citationCount,
        string[]? suggestedPersons = null
    )
    {
        var citations = Enumerable
            .Range(0, citationCount)
            .Select(i => new
            {
                documentId = Guid.NewGuid(),
                chunkId = Guid.NewGuid(),
                text = $"quote {i}",
                ordinal = i,
                documentName = $"document-{i}.pdf",
            })
            .ToArray();
        var suggestions = (suggestedPersons ?? Array.Empty<string>())
            .Select(name => new { name, documentId = (Guid?)null })
            .ToArray();

        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        answer,
                        citations,
                        retrievedChunkIds = Array.Empty<Guid>(),
                        latencyMs = 7,
                        suggestedPersons = suggestions,
                    }
                )
            ),
        });
        var apiClient = new QueryApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://w.local/") }
        );
        return new QueryViewModel(apiClient);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
            _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(_responder(request));
    }
}
