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

    public OllamaEmbedder(string baseUrl, string modelName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);

        _client = new OllamaApiClient(baseUrl, modelName);
        _modelName = modelName;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<float[]>> GetEmbeddingsAsync(
        IEnumerable<string> inputs,
        CancellationToken cancellationToken = default)
    {
        var inputList = inputs?.ToList() ?? throw new ArgumentNullException(nameof(inputs));
        if (inputList.Count == 0)
        {
            return Array.Empty<float[]>();
        }

        var response = await _client.EmbedAsync(new EmbedRequest
        {
            Model = _modelName,
            Input = inputList
        }, cancellationToken);

        return response.Embeddings.Select(e => e.ToArray()).ToList();
    }
}
