using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Client.Core;
using RAGGit.Client.Core.Models;
using RAGGit.Client.Core.Services;
using RAGGit.Client.Core.ViewModels;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 032 T015/T027/T033: the Ask surface's reasoning lifecycle — rows appear while
/// waiting, never enter the conversation or history, and are cleared the moment
/// the answer or a failure is presented.
/// </summary>
public sealed class QueryViewModelReasoningTests
{
    private const string UnderstandingEvent =
        "{\"type\":\"progress\",\"stage\":\"understanding\",\"text\":\"Reading your question…\"}";
    private const string SourcesEvent =
        "{\"type\":\"progress\",\"stage\":\"sources\",\"text\":\"Found 1 relevant source: policy.pdf\",\"sourceCount\":1,\"documentNames\":[\"policy.pdf\"]}";
    private const string ComposingEvent =
        "{\"type\":\"progress\",\"stage\":\"composing\",\"text\":\"Writing your answer…\"}";
    private const string ErrorEvent =
        "{\"type\":\"error\",\"error\":\"model unavailable offline\",\"status\":503}";

    [Fact]
    public async Task Ask_StreamsReasoning_ThenClearsItWhenTheAnswerArrives()
    {
        var viewModel = CreateViewModel(
            Body(UnderstandingEvent, SourcesEvent, ComposingEvent, AnswerEvent())
        );
        var observed = new List<string>();
        viewModel.ReasoningSteps.CollectionChanged += (_, _) =>
            observed.AddRange(viewModel.ReasoningSteps.Select(step => step.Text));

        viewModel.QueryText = "What is the renewal term?";
        await viewModel.AskCommand.ExecuteAsync(null);

        // The wait was explained, in the order the work happened.
        observed
            .Distinct()
            .Should()
            .ContainInOrder(
                "Reading your question…",
                "Found 1 relevant source: policy.pdf",
                "Writing your answer…"
            );

        // The finished exchange keeps none of it (FR-009, FR-014).
        viewModel.ReasoningSteps.Should().BeEmpty();
        viewModel.HasReasoning.Should().BeFalse();

        viewModel.Answer.Should().Be("The renewal term is 30 days.");
        viewModel.IsResultVisible.Should().BeTrue();
        viewModel.StatusMessage.Should().Contain("Answered in 42ms");
    }

    [Fact]
    public async Task Ask_MarksOnlyTheNewestRowAsCurrent()
    {
        var viewModel = CreateViewModel(
            Body(UnderstandingEvent, SourcesEvent, ComposingEvent, AnswerEvent())
        );
        var snapshots = new List<(int Total, int Current)>();
        viewModel.ReasoningSteps.CollectionChanged += (_, _) =>
            snapshots.Add(
                (
                    viewModel.ReasoningSteps.Count,
                    viewModel.ReasoningSteps.Count(step => step.IsCurrent)
                )
            );

        viewModel.QueryText = "q";
        await viewModel.AskCommand.ExecuteAsync(null);

        snapshots.Should().Contain(snapshot => snapshot.Total >= 2);
        snapshots.Should().OnlyContain(snapshot => snapshot.Total == 0 || snapshot.Current == 1);
    }

    [Fact]
    public async Task Ask_ReasoningNeverEntersTheConversation()
    {
        var conversation = new ConversationStore();
        var viewModel = CreateViewModel(Body(UnderstandingEvent, AnswerEvent()), conversation);

        viewModel.QueryText = "q";
        await viewModel.AskCommand.ExecuteAsync(null);

        viewModel.Messages.Should().HaveCount(2);
        viewModel.Messages[0].IsUser.Should().BeTrue();
        viewModel.Messages[1].IsUser.Should().BeFalse();
        viewModel
            .Messages.Should()
            .NotContain(message => message.Text.Contains("Reading your question"));
        conversation.Messages.Should().HaveCount(2);
        viewModel.ReasoningSteps.Should().BeEmpty();
    }

    [Fact]
    public async Task Ask_ClearsReasoningBeforeAppendingTheAnswerMessage()
    {
        var viewModel = CreateViewModel(Body(UnderstandingEvent, SourcesEvent, AnswerEvent()));
        var reasoningRowsWhenAnswerAppended = -1;
        viewModel.Messages.CollectionChanged += (_, args) =>
        {
            if (args.Action != NotifyCollectionChangedAction.Add)
            {
                return;
            }

            var added = (ChatMessage)args.NewItems![0]!;
            if (!added.IsUser)
            {
                reasoningRowsWhenAnswerAppended = viewModel.ReasoningSteps.Count;
            }
        };

        viewModel.QueryText = "q";
        await viewModel.AskCommand.ExecuteAsync(null);

        reasoningRowsWhenAnswerAppended.Should().Be(0);
        viewModel.HasReasoning.Should().BeFalse();
    }

    [Fact]
    public async Task Ask_RevisitingTheExchange_ShowsNoReasoning()
    {
        var conversation = new ConversationStore();
        var viewModel = CreateViewModel(Body(UnderstandingEvent, AnswerEvent()), conversation);

        viewModel.QueryText = "q";
        await viewModel.AskCommand.ExecuteAsync(null);

        var reopened = CreateViewModel(Body(AnswerEvent()), conversation);

        reopened.Messages.Should().HaveCount(2);
        reopened.ReasoningSteps.Should().BeEmpty();
        reopened.HasReasoning.Should().BeFalse();
    }

    [Fact]
    public async Task Ask_TerminalErrorEvent_SurfacesTheClassicMessageAndClearsReasoning()
    {
        var viewModel = CreateViewModel(Body(UnderstandingEvent, ErrorEvent));

        viewModel.QueryText = "q";
        await viewModel.AskCommand.ExecuteAsync(null);

        viewModel.StatusMessage.Should().Be("model unavailable offline");
        viewModel.StatusSeverity.Should().Be("Error");
        viewModel.ReasoningSteps.Should().BeEmpty();
        viewModel.HasReasoning.Should().BeFalse();
        viewModel.Answer.Should().BeEmpty();
    }

    [Fact]
    public async Task Ask_UnreachableWorkstation_KeepsTheClassicMessageAndClearsReasoning()
    {
        var viewModel = CreateViewModel(new ThrowingHandler());

        viewModel.QueryText = "q";
        await viewModel.AskCommand.ExecuteAsync(null);

        viewModel.StatusMessage.Should().Contain("cannot reach AI workstation");
        viewModel.StatusSeverity.Should().Be("Error");
        viewModel.ReasoningSteps.Should().BeEmpty();
        viewModel.HasReasoning.Should().BeFalse();
    }

    [Fact]
    public async Task Ask_TruncatedStream_FailsInsteadOfShowingAnEmptyAnswer()
    {
        var viewModel = CreateViewModel(Body(UnderstandingEvent));

        viewModel.QueryText = "q";
        await viewModel.AskCommand.ExecuteAsync(null);

        viewModel.StatusSeverity.Should().Be("Error");
        viewModel.Answer.Should().BeEmpty();
        viewModel.ReasoningSteps.Should().BeEmpty();
        viewModel.HasReasoning.Should().BeFalse();
        // Only the user's own question was appended — no invented answer bubble.
        viewModel.Messages.Should().ContainSingle().Which.IsUser.Should().BeTrue();
    }

    [Fact]
    public async Task Ask_FastAnswer_NeverShowsReasoningAtAll()
    {
        var viewModel = CreateViewModel(Body(AnswerEvent()));
        var reasoningWasEverVisible = false;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(QueryViewModel.HasReasoning) && viewModel.HasReasoning)
            {
                reasoningWasEverVisible = true;
            }
        };

        viewModel.QueryText = "q";
        await viewModel.AskCommand.ExecuteAsync(null);

        reasoningWasEverVisible.Should().BeFalse();
        viewModel.Answer.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task StopAnswer_ClearsReasoning_AndLeavesThePromptUnanswered()
    {
        var viewModel = CreateViewModel(new StallingBodyHandler(UnderstandingEvent));
        viewModel.QueryText = "a broad question";

        var askTask = viewModel.AskCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => viewModel.HasReasoning);

        viewModel.StopAnswerCommand.Execute(null);
        await askTask.WaitAsync(TimeSpan.FromSeconds(5));

        viewModel.ReasoningSteps.Should().BeEmpty();
        viewModel.HasReasoning.Should().BeFalse();
        viewModel.Answer.Should().BeEmpty();
        viewModel.IsBusy.Should().BeFalse();
        viewModel.StatusMessage.Should().Be("Question stopped.");
        viewModel.StatusSeverity.Should().Be("Informational");
        viewModel.Messages.Should().ContainSingle().Which.IsUser.Should().BeTrue();
    }

    [Fact]
    public async Task Ask_AppliesTheAnswerWithoutWaitingForTheStreamToClose()
    {
        // The terminal answer is the last event the workstation sends; a stop
        // requested while the connection winds down must not discard it.
        var viewModel = CreateViewModel(new StallingBodyHandler(AnswerEvent()));
        viewModel.QueryText = "q";

        await viewModel.AskCommand.ExecuteAsync(null).WaitAsync(TimeSpan.FromSeconds(5));

        viewModel.Answer.Should().Be("The renewal term is 30 days.");
        viewModel.IsBusy.Should().BeFalse();
        viewModel.Messages.Should().HaveCount(2);
        viewModel.StatusMessage.Should().Contain("Answered in 42ms");
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("condition was not met in time");
            }

            await Task.Delay(10);
        }
    }

    private static string AnswerEvent() =>
        "{\"type\":\"answer\",\"answer\":\"The renewal term is 30 days.\",\"citations\":[],"
        + "\"retrievedChunkIds\":[],\"latencyMs\":42,\"suggestedPersons\":[]}";

    private static string Body(params string[] lines) => string.Join("\n", lines) + "\n";

    private static QueryViewModel CreateViewModel(
        string body,
        ConversationStore? conversation = null
    ) => CreateViewModel(new StubHandler(_ => Ok(body)), conversation);

    private static QueryViewModel CreateViewModel(
        HttpMessageHandler handler,
        ConversationStore? conversation = null
    ) =>
        new(
            new QueryApiClient(
                new HttpClient(handler) { BaseAddress = new Uri("https://w.local/") }
            ),
            EmptyHistoryClient(),
            new ClientSession(),
            conversation ?? new ConversationStore(),
            new DocumentApiClient(new HttpClient { BaseAddress = new Uri("https://w.local/") }),
            new InMemoryFileSaveService()
        );

    private static HttpResponseMessage Ok(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/x-ndjson"),
        };

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

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
            _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(_respond(request));
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => throw new HttpRequestException("cannot reach AI workstation");
    }

    /// <summary>
    /// Sends the given NDJSON prefix, then holds the body open until the reader
    /// cancels — the shape of a long answer that is still being composed.
    /// </summary>
    private sealed class StallingBodyHandler : HttpMessageHandler
    {
        private readonly string _prefix;

        public StallingBodyHandler(string prefix) => _prefix = prefix;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(new StallingStream(_prefix + "\n")),
                }
            );
    }

    private sealed class StallingStream : Stream
    {
        private readonly byte[] _prefix;
        private int _position;

        public StallingStream(string prefix)
        {
            _prefix = Encoding.UTF8.GetBytes(prefix);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            if (_position < _prefix.Length)
            {
                var count = Math.Min(buffer.Length, _prefix.Length - _position);
                _prefix.AsMemory(_position, count).CopyTo(buffer);
                _position += count;
                return count;
            }

            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
