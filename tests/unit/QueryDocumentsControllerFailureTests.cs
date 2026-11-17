using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Models;
using RAGGit.Documents;
using RAGGit.Workstation.Api.Controllers;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// 033 US4: a renderer failure is reported as a user-safe 500 and never leaks
/// bytes or internals.
/// </summary>
public sealed class QueryDocumentsControllerFailureTests
{
    [Fact]
    public async Task RendererFailure_Returns500()
    {
        var queryId = Guid.NewGuid();
        var detail = new QueryDetail
        {
            Id = queryId,
            Prompt = "Create a text document.",
            Answer = "An answer [1].",
            Citations = new List<HistoryCitation>
            {
                new()
                {
                    DocumentId = Guid.NewGuid(),
                    ChunkId = Guid.NewGuid(),
                    Text = "quote",
                    Ordinal = 0,
                },
            },
            LatencyMs = 1,
            CreatedAt = DateTime.UtcNow,
        };
        var controller = new QueryDocumentsController(
            new StubQueryRepository(detail),
            new DocumentGenerator(new IDocumentRenderer[] { new ThrowingRenderer() }),
            NullLogger<QueryDocumentsController>.Instance
        )
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[] { new Claim(ClaimTypes.NameIdentifier, "sub") },
                            "test"
                        )
                    ),
                },
            },
        };

        var result = await controller.Render(
            queryId,
            new DocumentRequest { Format = "txt" },
            CancellationToken.None
        );

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(500);
    }

    private sealed class ThrowingRenderer : IDocumentRenderer
    {
        public DocumentOutputFormat Format => DocumentOutputFormat.Txt;

        public GeneratedDocument Render(QueryDetail detail) =>
            throw new InvalidOperationException("renderer exploded");

        public GeneratedDocument Render(DocumentDraft draft) =>
            throw new InvalidOperationException("renderer exploded");
    }

    private sealed class StubQueryRepository(QueryDetail detail) : IQueryRepository
    {
        public Task<HistoryPage> ListHistoryAsync(
            string sub,
            int? limit,
            int? offset,
            CancellationToken cancellationToken = default
        ) => Task.FromResult(new HistoryPage());

        public Task<QueryDetail?> GetDetailAsync(
            string sub,
            Guid id,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<QueryDetail?>(id == detail.Id ? detail : null);

        public Task AddAsync(Query query, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<int>> GetRecentLatenciesAsync(
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());

        public Task<IReadOnlyList<string>> GetExplainPlanAsync(
            string sub,
            int limit,
            int offset,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}
