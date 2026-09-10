using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using RAGGit.Core.Abstractions;

namespace RAGGit.Retrieval;

/// <summary>
/// Decorator around <see cref="IEmbedder"/> that caches query embeddings in
/// <see cref="IMemoryCache"/>. Normalizes inputs (trim, lowercase, remove
/// punctuation) and uses a per-key <see cref="SemaphoreSlim"/> to prevent
/// thundering herds on cache misses.
/// </summary>
public sealed class CachedEmbedder : IEmbedder, IDisposable
{
    private readonly IEmbedder _inner;
    private readonly IMemoryCache _cache;
    private readonly string _modelName;
    private readonly int _ttlHours;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public CachedEmbedder(
        IEmbedder inner,
        IMemoryCache cache,
        string modelName,
        int capacity,
        int ttlHours)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _modelName = modelName ?? throw new ArgumentNullException(nameof(modelName));
        _ttlHours = ttlHours > 0 ? ttlHours : 24;
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

        var results = new float[]?[inputList.Count];
        var missingIndices = new List<int>();
        var acquiredLocks = new List<(string Key, SemaphoreSlim Semaphore)>();

        try
        {
            // First pass: serve cached values; acquire locks for misses.
            for (var i = 0; i < inputList.Count; i++)
            {
                var key = ComputeCacheKey(inputList[i]);
                if (_cache.TryGetValue(key, out float[]? cached))
                {
                    results[i] = cached;
                    continue;
                }

                var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
                await semaphore.WaitAsync(cancellationToken);
                acquiredLocks.Add((key, semaphore));

                if (_cache.TryGetValue(key, out cached))
                {
                    results[i] = cached;
                    continue;
                }

                missingIndices.Add(i);
            }

            // Compute embeddings for all misses in one batch call.
            if (missingIndices.Count > 0)
            {
                var missingInputs = missingIndices.Select(i => inputList[i]).ToList();
                var embeddings = await _inner.GetEmbeddingsAsync(missingInputs, cancellationToken);

                if (embeddings.Count != missingInputs.Count)
                {
                    throw new InvalidOperationException(
                        $"Embedder returned {embeddings.Count} vectors for {missingInputs.Count} inputs.");
                }

                for (var m = 0; m < missingIndices.Count; m++)
                {
                    var originalIndex = missingIndices[m];
                    var embedding = embeddings[m];
                    results[originalIndex] = embedding;

                    var key = ComputeCacheKey(inputList[originalIndex]);
                    _cache.Set(key, embedding, CreateEntryOptions());
                }
            }
        }
        finally
        {
            foreach (var (key, semaphore) in acquiredLocks)
            {
                semaphore.Release();
                // Best-effort cleanup: remove the semaphore once no one is waiting.
                if (semaphore.CurrentCount == 1)
                {
                    _locks.TryRemove(key, out _);
                }
            }
        }

        return results.Select(r => r!).ToList();
    }

    private string ComputeCacheKey(string input)
    {
        var normalized = Normalize(input);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{_modelName}:{normalized}"));
        return $"embed:{_modelName}:{Convert.ToHexString(hash)}";
    }

    private static string Normalize(string input)
    {
        var trimmed = input.Trim().ToLowerInvariant();
        var builder = new StringBuilder(trimmed.Length);
        foreach (var c in trimmed)
        {
            if (!char.IsPunctuation(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private MemoryCacheEntryOptions CreateEntryOptions()
    {
        return new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromHours(_ttlHours))
            .SetSize(1);
    }

    public void Dispose()
    {
        foreach (var semaphore in _locks.Values)
        {
            semaphore.Dispose();
        }

        _locks.Clear();
    }
}
