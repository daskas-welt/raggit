using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using RAGGit.Core.Abstractions;

namespace RAGGit.Ingest.Vector;

/// <summary>
/// Embedded-file-backed Qdrant client wrapper implementing <see cref="IVectorStore"/>.
/// Manages the <c>library</c> collection with HNSW <c>m=16, efConstruction=128</c>
/// and a keyword payload index on <c>documentId</c>.
/// </summary>
public sealed class QdrantLocalClient : IVectorStore
{
    private const string CollectionName = "library";
    private const int DefaultVectorSize = 384;

    private readonly string _storagePath;
    private readonly int _vectorSize;
    private readonly QdrantClient _client;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    /// <summary>
    /// Creates a local Qdrant client that persists data under <paramref name="storagePath"/>.
    /// The actual Qdrant server endpoint defaults to <c>localhost:6334</c> and can be overridden.
    /// </summary>
    public QdrantLocalClient(
        string storagePath,
        string host = "localhost",
        int port = 6334,
        int vectorSize = DefaultVectorSize)
    {
        _storagePath = storagePath ?? throw new ArgumentNullException(nameof(storagePath));
        _vectorSize = vectorSize;

        Directory.CreateDirectory(_storagePath);
        _client = new QdrantClient(host, port);
    }

    private async Task EnsureCollectionAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            var exists = await _client.CollectionExistsAsync(CollectionName, cancellationToken);
            if (!exists)
            {
                await _client.CreateCollectionAsync(
                    CollectionName,
                    new VectorParams
                    {
                        Size = (ulong)_vectorSize,
                        Distance = Distance.Cosine
                    },
                    hnswConfig: new HnswConfigDiff
                    {
                        M = 16,
                        EfConstruct = 128
                    },
                    cancellationToken: cancellationToken);
            }

            await _client.CreatePayloadIndexAsync(
                CollectionName,
                "documentId",
                cancellationToken: cancellationToken);

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task UpsertAsync(
        IEnumerable<VectorRecord> vectors,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vectors);

        await EnsureCollectionAsync(cancellationToken);

        var points = vectors.Select(v =>
        {
            var point = new PointStruct
            {
                Id = v.Id,
                Vectors = v.Vector
            };

            foreach (var kvp in ConvertPayload(v.Payload))
            {
                point.Payload[kvp.Key] = kvp.Value;
            }

            return point;
        }).ToList();

        if (points.Count == 0)
        {
            return;
        }

        await _client.UpsertAsync(CollectionName, points, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        float[] queryVector,
        int limit,
        string? documentIdFilter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryVector);

        await EnsureCollectionAsync(cancellationToken);

        Filter? filter = null;
        if (!string.IsNullOrWhiteSpace(documentIdFilter))
        {
            filter = new Filter { Must = { Conditions.MatchKeyword("documentId", documentIdFilter) } };
        }

        var results = await _client.SearchAsync(
            CollectionName,
            queryVector,
            filter: filter,
            limit: (ulong)limit,
            cancellationToken: cancellationToken);

        return results.Select(MapScoredPoint).ToList();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        await EnsureCollectionAsync(cancellationToken);

        var filter = new Filter { Must = { Conditions.MatchKeyword("documentId", documentId) } };
        await _client.DeleteAsync(CollectionName, filter, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Returns the storage path used by this client.
    /// </summary>
    public string StoragePath => _storagePath;

    /// <inheritdoc />
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureCollectionAsync(cancellationToken);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            _ = await _client.CollectionExistsAsync(CollectionName, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<KeyValuePair<string, Value>> ConvertPayload(IReadOnlyDictionary<string, object?> payload)
    {
        foreach (var kvp in payload)
        {
            if (kvp.Value is null)
            {
                continue;
            }

            yield return new KeyValuePair<string, Value>(kvp.Key, ConvertValue(kvp.Value));
        }
    }

    private static Value ConvertValue(object value)
    {
        return value switch
        {
            string s => s,
            int i => i,
            long l => l,
            uint ui => ui,
            ulong ul => (long)ul,
            float f => f,
            double d => d,
            bool b => b,
            Guid g => g.ToString(),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static SearchResult MapScoredPoint(ScoredPoint point)
    {
        var id = point.Id.PointIdOptionsCase == PointId.PointIdOptionsOneofCase.Uuid
            ? Guid.Parse(point.Id.Uuid)
            : Guid.Empty;

        point.Payload.TryGetValue("documentId", out var documentIdValue);
        point.Payload.TryGetValue("text", out var textValue);
        point.Payload.TryGetValue("ordinal", out var ordinalValue);

        return new SearchResult(
            id,
            documentIdValue?.StringValue ?? string.Empty,
            textValue?.StringValue ?? string.Empty,
            (int)(ordinalValue?.IntegerValue ?? 0),
            point.Score);
    }
}
