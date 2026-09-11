using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Data;
using RAGGit.Workstation.Api.Auth;

namespace RAGGit.Tests.Contract;

/// <summary>
/// Shared factory for contract tests. Replaces AI/vector dependencies with fast
/// in-memory fakes and points SQLite/LanceDB at temp paths.
/// </summary>
public sealed class TestApiFactory : WebApplicationFactory<Program>
{
    public string AdminKey { get; } = "admin-contract-test";
    public string EmployeeKey { get; } = "employee-contract-test";

    private readonly string _dbPath;

    public TestApiFactory()
    {
        var baseDir = Path.Combine(
            Path.GetTempPath(),
            "raggit-contract-tests",
            Guid.NewGuid().ToString()
        );
        Directory.CreateDirectory(baseDir);
        _dbPath = Path.Combine(baseDir, "rag.db");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.Configure<ApiKeyAuthOptions>(
                ApiKeyAuthOptions.Scheme,
                options =>
                {
                    options.AdminApiKey = AdminKey;
                    options.EmployeeApiKey = EmployeeKey;
                }
            );

            services.AddSingleton(new RagDbContext($"Data Source={_dbPath}"));
            services.AddSingleton<IVectorStore>(new FakeVectorStore());
            services.AddSingleton<IEmbedder>(new FakeEmbedder());
            services.AddSingleton<ILlmClient>(new FakeLlmClient());
        });
    }
}

internal sealed class FakeVectorStore : IVectorStore
{
    private readonly List<VectorRecord> _records = new();

    public void Seed(IEnumerable<VectorRecord> records)
    {
        lock (_records)
        {
            _records.AddRange(records);
        }
    }

    public void Clear()
    {
        lock (_records)
        {
            _records.Clear();
        }
    }

    public Task UpsertAsync(
        IEnumerable<VectorRecord> vectors,
        CancellationToken cancellationToken = default
    )
    {
        lock (_records)
        {
            _records.AddRange(vectors);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SearchResult>> SearchAsync(
        float[] queryVector,
        int limit,
        string? documentIdFilter = null,
        CancellationToken cancellationToken = default
    )
    {
        lock (_records)
        {
            var query = _records.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(documentIdFilter))
            {
                query = query.Where(r =>
                    GetPayloadString(r.Payload, "documentId") == documentIdFilter
                );
            }

            var results = query
                .Take(limit)
                .Select(r => new SearchResult(
                    r.Id,
                    GetPayloadString(r.Payload, "documentId"),
                    GetPayloadString(r.Payload, "text"),
                    GetPayloadInt32(r.Payload, "ordinal"),
                    1.0f
                ))
                .ToList();

            return Task.FromResult<IReadOnlyList<SearchResult>>(results);
        }
    }

    public Task DeleteAsync(string documentId, CancellationToken cancellationToken = default)
    {
        lock (_records)
        {
            _records.RemoveAll(r => GetPayloadString(r.Payload, "documentId") == documentId);
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    private static string GetPayloadString(IReadOnlyDictionary<string, object?> payload, string key)
    {
        return payload.TryGetValue(key, out var value) && value is not null
            ? value.ToString() ?? string.Empty
            : string.Empty;
    }

    private static int GetPayloadInt32(IReadOnlyDictionary<string, object?> payload, string key)
    {
        if (payload.TryGetValue(key, out var value) && value is not null)
        {
            return value switch
            {
                int i => i,
                long l => (int)l,
                _ => int.TryParse(value.ToString(), out var parsed) ? parsed : 0,
            };
        }

        return 0;
    }
}

internal sealed class FakeEmbedder : IEmbedder
{
    public Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(
        IEnumerable<string> inputs,
        CancellationToken cancellationToken = default
    )
    {
        var embeddings = inputs
            .Select(_ =>
            {
                var vector = new float[384];
                vector[0] = 1.0f;
                return vector;
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<float[]>>(embeddings);
    }
}

internal sealed class FakeLlmClient : ILlmClient
{
    public string ResponseText { get; set; } = "fake answer";
    public bool Healthy { get; set; } = true;

    public Task<string> ChatAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(ResponseText);

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Healthy);
}
