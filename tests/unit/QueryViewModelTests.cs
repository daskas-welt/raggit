using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Core.Models;
using RAGGit.Client.Maui;
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
            ),
            EmptyHistoryClient(),
            new ClientSession(),
            new ConversationStore()
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
            ),
            EmptyHistoryClient(),
            new ClientSession(),
            new ConversationStore()
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
        return new QueryViewModel(
            apiClient,
            EmptyHistoryClient(),
            new ClientSession(),
            new ConversationStore()
        );
    }

    private static QueryHistoryApiClient EmptyHistoryClient()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        items = Array.Empty<object>(),
                        total = 0,
                        limit = 100,
                        offset = 0,
                    }
                )
            ),
        });
        return new QueryHistoryApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://w.local/") }
        );
    }

    [Fact]
    public async Task LoadHistory_PopulatesPairsOldestFirst()
    {
        var older = new HistorySeed(
            Guid.NewGuid(),
            "older prompt",
            "older answer",
            new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            Array.Empty<CitationSeed>()
        );
        var newer = new HistorySeed(
            Guid.NewGuid(),
            "newer prompt",
            "newer answer",
            new DateTime(2026, 5, 6, 7, 8, 9, DateTimeKind.Utc),
            Array.Empty<CitationSeed>()
        );
        var viewModel = CreateViewModelWithHistory(new HistoryStubHandler(new[] { newer, older }));

        await viewModel.LoadHistoryAsync();

        viewModel.Messages.Should().HaveCount(4);
        viewModel.Messages[0].IsUser.Should().BeTrue();
        viewModel.Messages[0].Text.Should().Be("older prompt");
        viewModel.Messages[0].Timestamp.Should().Be(older.CreatedAt);
        viewModel.Messages[1].IsUser.Should().BeFalse();
        viewModel.Messages[1].Text.Should().Be("older answer");
        viewModel.Messages[2].Text.Should().Be("newer prompt");
        viewModel.Messages[3].Text.Should().Be("newer answer");
        viewModel.StatusMessage.Should().BeNull();
        viewModel.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task LoadHistory_MapsCitationsOrderedByOrdinal()
    {
        var docId = Guid.NewGuid();
        var chunkA = Guid.NewGuid();
        var chunkB = Guid.NewGuid();
        var seed = new HistorySeed(
            Guid.NewGuid(),
            "prompt",
            "answer",
            new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc),
            new[]
            {
                new CitationSeed(chunkB, "b.pdf", chunkB, "second quote", 1),
                new CitationSeed(docId, "a.pdf", chunkA, "first quote", 0),
            }
        );
        var viewModel = CreateViewModelWithHistory(new HistoryStubHandler(new[] { seed }));

        await viewModel.LoadHistoryAsync();

        var assistant = viewModel.Messages.Single(m => m.IsAssistant);
        assistant.Citations.Should().HaveCount(2);
        assistant.Citations.Select(c => c.Ordinal).Should().Equal(0, 1);
        assistant.Citations[0].Text.Should().Be("first quote");
        assistant.Citations[0].DocumentId.Should().Be(docId);
        assistant.Citations[0].DocumentName.Should().Be("a.pdf");
        assistant.Citations[0].ChunkId.Should().Be(chunkA);
        assistant.HasCitations.Should().BeTrue();
    }

    [Fact]
    public async Task LoadHistory_PagesUntilTotal()
    {
        var handler = new HistoryStubHandler(
            new[]
            {
                new HistorySeed(
                    Guid.NewGuid(),
                    "p1",
                    "a1",
                    new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Array.Empty<CitationSeed>()
                ),
                new HistorySeed(
                    Guid.NewGuid(),
                    "p2",
                    "a2",
                    new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                    Array.Empty<CitationSeed>()
                ),
                new HistorySeed(
                    Guid.NewGuid(),
                    "p3",
                    "a3",
                    new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc),
                    Array.Empty<CitationSeed>()
                ),
            },
            firstPageSize: 1
        );
        var viewModel = CreateViewModelWithHistory(handler);

        await viewModel.LoadHistoryAsync();

        viewModel.Messages.Should().HaveCount(6);
        viewModel.Messages[0].Text.Should().Be("p1");
        viewModel.Messages[4].Text.Should().Be("p3");
        handler.HistoryCalls.Should().Be(2);
        handler.DetailCalls.Should().Be(3);
    }

    [Fact]
    public async Task LoadHistory_OnlyKeepsQueriesSinceLastLogin()
    {
        var loginAt = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        var seeds = new[]
        {
            new HistorySeed(
                Guid.NewGuid(),
                "old prompt",
                "old answer",
                loginAt.AddHours(-2),
                Array.Empty<CitationSeed>()
            ),
            new HistorySeed(
                Guid.NewGuid(),
                "boundary prompt",
                "boundary answer",
                loginAt,
                Array.Empty<CitationSeed>()
            ),
            new HistorySeed(
                Guid.NewGuid(),
                "new prompt",
                "new answer",
                loginAt.AddHours(1),
                Array.Empty<CitationSeed>()
            ),
        };
        var session = new ClientSession { LastLoginAtUtc = loginAt };
        var viewModel = CreateViewModelWithHistory(new HistoryStubHandler(seeds), session);

        await viewModel.LoadHistoryAsync();

        viewModel.Messages.Should().HaveCount(4);
        viewModel.Messages[0].Text.Should().Be("boundary prompt");
        viewModel.Messages[1].Text.Should().Be("boundary answer");
        viewModel.Messages[2].Text.Should().Be("new prompt");
        viewModel.Messages[3].Text.Should().Be("new answer");
        viewModel.StatusMessage.Should().BeNull();
    }

    [Fact]
    public async Task LoadHistory_AllOlderThanLogin_ShowsEmptyWithoutError()
    {
        var loginAt = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        var seeds = new[]
        {
            new HistorySeed(
                Guid.NewGuid(),
                "old prompt",
                "old answer",
                loginAt.AddHours(-1),
                Array.Empty<CitationSeed>()
            ),
        };
        var session = new ClientSession { LastLoginAtUtc = loginAt };
        var viewModel = CreateViewModelWithHistory(new HistoryStubHandler(seeds), session);

        await viewModel.LoadHistoryAsync();

        viewModel.Messages.Should().BeEmpty();
        viewModel.StatusMessage.Should().BeNull();
    }

    [Fact]
    public async Task ReturnToAsk_ReseedsMessagesFromStore_WhenHistoryFails()
    {
        var session = new ClientSession
        {
            Username = "maria",
            LastLoginAtUtc = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc),
        };
        var store = new ConversationStore();
        var firstVisit = CreateViewModelWithHistory(
            new HistoryStubHandler(Array.Empty<HistorySeed>()),
            session,
            store
        );
        firstVisit.QueryText = "question asked this login";

        await firstVisit.AskCommand.ExecuteAsync(null);

        firstVisit.Messages.Should().HaveCount(2);
        store.Messages.Should().HaveCount(2);

        // Second visit (new page + ViewModel, same login): history fails,
        // but the asked question survives via the store.
        var failingHistory = new StubHandler(_ => throw new HttpRequestException("down"));
        var secondVisit = CreateViewModelWithHistory(failingHistory, session, store);
        secondVisit.Messages.Should().HaveCount(2);

        await secondVisit.LoadHistoryAsync();

        secondVisit.Messages.Should().HaveCount(2);
        secondVisit.Messages[0].Text.Should().Be("question asked this login");
        secondVisit.StatusMessage.Should().Contain("cannot reach AI workstation");
    }

    [Fact]
    public void NewLogin_ClearsCachedMessages()
    {
        var store = new ConversationStore();
        store.EnsureOwner("maria|previous-login");
        store.Messages.Add(new ChatMessage { Text = "previous login question", IsUser = true });
        var session = new ClientSession
        {
            Username = "maria",
            LastLoginAtUtc = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc),
        };

        var viewModel = CreateViewModelWithHistory(
            new HistoryStubHandler(Array.Empty<HistorySeed>()),
            session,
            store
        );

        viewModel.Messages.Should().BeEmpty();
        store.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadHistory_SyncsStore_WithServerConversation()
    {
        var seed = new HistorySeed(
            Guid.NewGuid(),
            "prompt",
            "answer",
            new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc),
            Array.Empty<CitationSeed>()
        );
        var store = new ConversationStore();
        var viewModel = CreateViewModelWithHistory(
            new HistoryStubHandler(new[] { seed }),
            null,
            store
        );

        await viewModel.LoadHistoryAsync();

        store.Messages.Should().HaveCount(2);
        store.Messages[0].Text.Should().Be("prompt");
        store.Messages[1].Text.Should().Be("answer");
    }

    [Fact]
    public async Task LoadHistory_CapsSessionItems_AtMaxSessionItems()
    {
        var baseTime = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        var seeds = Enumerable
            .Range(0, QueryViewModel.MaxSessionItems + 10)
            .Select(i => new HistorySeed(
                Guid.NewGuid(),
                $"prompt {i}",
                $"answer {i}",
                baseTime.AddMinutes(i),
                Array.Empty<CitationSeed>()
            ))
            .ToArray();
        var handler = new HistoryStubHandler(seeds);
        var viewModel = CreateViewModelWithHistory(handler);

        await viewModel.LoadHistoryAsync();

        viewModel.Messages.Should().HaveCount(QueryViewModel.MaxSessionItems * 2);
        viewModel.Messages[0].Text.Should().Be("prompt 10");
        viewModel.Messages[^1].Text.Should().Be($"answer {QueryViewModel.MaxSessionItems + 9}");
        handler.DetailCalls.Should().Be(QueryViewModel.MaxSessionItems);
    }

    [Fact]
    public async Task LoadHistory_StopsPaging_WhenPagePredatesLogin()
    {
        var loginAt = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        var seeds = new[]
        {
            new HistorySeed(
                Guid.NewGuid(),
                "new prompt",
                "new answer",
                loginAt.AddHours(1),
                Array.Empty<CitationSeed>()
            ),
            new HistorySeed(
                Guid.NewGuid(),
                "old prompt 1",
                "old answer 1",
                loginAt.AddHours(-1),
                Array.Empty<CitationSeed>()
            ),
            new HistorySeed(
                Guid.NewGuid(),
                "old prompt 2",
                "old answer 2",
                loginAt.AddHours(-2),
                Array.Empty<CitationSeed>()
            ),
        };
        var handler = new HistoryStubHandler(seeds, firstPageSize: 2);
        var session = new ClientSession { LastLoginAtUtc = loginAt };
        var viewModel = CreateViewModelWithHistory(handler, session);

        await viewModel.LoadHistoryAsync();

        handler.HistoryCalls.Should().Be(1);
        handler.DetailCalls.Should().Be(1);
        viewModel.Messages.Should().HaveCount(2);
        viewModel.Messages[0].Text.Should().Be("new prompt");
    }

    [Fact]
    public async Task LoadHistory_Cancelled_LeavesCacheUntouched()
    {
        var viewModel = CreateViewModelWithHistory(
            new HistoryStubHandler(Array.Empty<HistorySeed>())
        );

        await viewModel.LoadHistoryAsync(new CancellationToken(canceled: true));

        viewModel.Messages.Should().BeEmpty();
        viewModel.StatusMessage.Should().BeNull();
        viewModel.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task LoadHistory_EmptyHistory_ClearsMessages()
    {
        var viewModel = CreateViewModelWithHistory(
            new HistoryStubHandler(Array.Empty<HistorySeed>())
        );
        viewModel.Messages = new ObservableCollection<ChatMessage>
        {
            new() { Text = "stale", IsUser = true },
        };

        await viewModel.LoadHistoryAsync();

        viewModel.Messages.Should().BeEmpty();
        viewModel.StatusMessage.Should().BeNull();
    }

    [Fact]
    public async Task LoadHistory_Unauthorized_SurfacesSignInPromptAndKeepsMessages()
    {
        var handler = new HistoryStubHandler(
            Array.Empty<HistorySeed>(),
            historyStatus: HttpStatusCode.Unauthorized,
            historyBody: "no session"
        );
        var viewModel = CreateViewModelWithHistory(handler);
        viewModel.Messages = new ObservableCollection<ChatMessage>
        {
            new() { Text = "existing", IsUser = true },
        };

        await viewModel.LoadHistoryAsync();

        viewModel.StatusMessage.Should().Be("Please sign in to see your conversation.");
        viewModel.Messages.Should().HaveCount(1);
        viewModel.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task LoadHistory_Unreachable_KeepsMessagesAndSurfacesStatus()
    {
        var viewModel = CreateViewModelWithHistory(
            new StubHandler(_ => throw new HttpRequestException("connection refused"))
        );
        viewModel.Messages = new ObservableCollection<ChatMessage>
        {
            new() { Text = "existing", IsUser = true },
        };

        await viewModel.LoadHistoryAsync();

        viewModel.StatusMessage.Should().Contain("cannot reach AI workstation");
        viewModel.Messages.Should().HaveCount(1);
        viewModel.IsBusy.Should().BeFalse();
    }

    private static QueryViewModel CreateViewModelWithHistory(
        HttpMessageHandler historyHandler,
        ClientSession? session = null,
        ConversationStore? store = null
    )
    {
        var askHandler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        answer = "unused",
                        citations = Array.Empty<object>(),
                        retrievedChunkIds = Array.Empty<Guid>(),
                        latencyMs = 1,
                        suggestedPersons = Array.Empty<object>(),
                    }
                )
            ),
        });
        return new QueryViewModel(
            new QueryApiClient(
                new HttpClient(askHandler) { BaseAddress = new Uri("https://w.local/") }
            ),
            new QueryHistoryApiClient(
                new HttpClient(historyHandler) { BaseAddress = new Uri("https://w.local/") }
            ),
            session ?? new ClientSession(),
            store ?? new ConversationStore()
        );
    }

    private sealed record HistorySeed(
        Guid Id,
        string Prompt,
        string Answer,
        DateTime CreatedAt,
        CitationSeed[] Citations
    );

    private sealed record CitationSeed(
        Guid DocumentId,
        string? DocumentName,
        Guid ChunkId,
        string Text,
        int Ordinal
    );

    private sealed class HistoryStubHandler : HttpMessageHandler
    {
        private readonly List<HistorySeed> _seeds;
        private readonly HttpStatusCode _historyStatus;
        private readonly string _historyBody;
        private readonly int? _firstPageSize;
        private int _historyCalls;
        private int _detailCalls;

        public int HistoryCalls => _historyCalls;

        public int DetailCalls => _detailCalls;

        public HistoryStubHandler(
            IEnumerable<HistorySeed> seeds,
            HttpStatusCode historyStatus = HttpStatusCode.OK,
            string historyBody = "",
            int? firstPageSize = null
        )
        {
            _seeds = seeds.ToList();
            _historyStatus = historyStatus;
            _historyBody = historyBody;
            _firstPageSize = firstPageSize;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(Respond(request));

        private HttpResponseMessage Respond(HttpRequestMessage request)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("api/queries/history", StringComparison.OrdinalIgnoreCase))
            {
                Interlocked.Increment(ref _historyCalls);
                if (_historyStatus != HttpStatusCode.OK)
                {
                    return new HttpResponseMessage(_historyStatus)
                    {
                        Content = new StringContent(_historyBody),
                    };
                }

                var (limit, offset) = ParsePaging(request.RequestUri!.Query);
                if (HistoryCalls == 1 && _firstPageSize is not null)
                {
                    limit = _firstPageSize.Value;
                }

                var slice = _seeds.Skip(offset).Take(limit).ToArray();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(
                            new
                            {
                                items = slice.Select(s => new
                                {
                                    id = s.Id,
                                    promptPreview = s.Prompt,
                                    answerPreview = s.Answer,
                                    citationCount = s.Citations.Length,
                                    latencyMs = 5,
                                    createdAt = s.CreatedAt,
                                }),
                                total = _seeds.Count,
                                limit,
                                offset,
                            }
                        )
                    ),
                };
            }

            Interlocked.Increment(ref _detailCalls);
            var id = Guid.Parse(path.Split('/').Last());
            var seed = _seeds.Single(s => s.Id == id);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(
                        new
                        {
                            id = seed.Id,
                            prompt = seed.Prompt,
                            answer = seed.Answer,
                            citations = seed.Citations.Select(c => new
                            {
                                documentId = c.DocumentId,
                                documentName = c.DocumentName,
                                chunkId = c.ChunkId,
                                text = c.Text,
                                ordinal = c.Ordinal,
                            }),
                            latencyMs = 5,
                            createdAt = seed.CreatedAt,
                        }
                    )
                ),
            };
        }

        private static (int Limit, int Offset) ParsePaging(string query)
        {
            var limit = 20;
            var offset = 0;
            foreach (
                var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            )
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2)
                {
                    continue;
                }

                if (kv[0] == "limit")
                {
                    int.TryParse(kv[1], out limit);
                }
                else if (kv[0] == "offset")
                {
                    int.TryParse(kv[1], out offset);
                }
            }

            return (limit, offset);
        }
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
