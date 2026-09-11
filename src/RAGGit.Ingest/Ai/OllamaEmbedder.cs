using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OllamaSharp;
using OllamaSharp.Models;
using RAGGit.Core.Abstractions;

namespace RAGGit.Ingest.Ai;

/// <summary>
/// Local embedding client using Ollama <c>POST /api/embed</c>.
/// </summary>
public sealed class OllamaEmbedder : IEmbedder
{
    private readonly OllamaApiClient _client;
    private readonly string _modelName;
    private readonly int _timeoutMs;

    public OllamaEmbedder(string baseUrl, string modelName, int timeoutMs = 5000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        if (timeoutMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutMs));

        _modelName = modelName;
        _timeoutMs = timeoutMs;
        var httpClient = new System.Net.Http.HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromMilliseconds(timeoutMs),
        };
        // Never trigger ollama pull — fail fast if model not present (Constitution IV, FR-007)
        _client = new OllamaApiClient(httpClient, modelName);
    }

    /// <summary>
    /// Back-compat ctor without timeout — defaults to 5000ms (R7).
    /// </summary>
    public OllamaEmbedder(string baseUrl, string modelName, TimeSpan timeout)
        : this(baseUrl, modelName, (int)timeout.TotalMilliseconds) { }

    /// <inheritdoc />
    public async Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(
        IEnumerable<string> inputs,
        CancellationToken cancellationToken = default
    )
    {
        var inputList = inputs?.ToList() ?? throw new ArgumentNullException(nameof(inputs));
        if (inputList.Count == 0)
        {
            return Array.Empty<float[]>();
        }

        var response = await _client.EmbedAsync(
            new EmbedRequest { Model = _modelName, Input = inputList },
            cancellationToken
        );

        return response.Embeddings.Select(e => e.ToArray()).ToList();
    }
}
