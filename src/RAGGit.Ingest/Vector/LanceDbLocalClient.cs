using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Apache.Arrow;
using Apache.Arrow.Types;
using lancedb;
using RAGGit.Core.Abstractions;

namespace RAGGit.Ingest.Vector;

/// <summary>
/// Embedded-file-backed LanceDB client wrapper implementing <see cref="IVectorStore"/>.
/// Manages the <c>library</c> table with an HNSW cosine index and upserts/search/delete
/// via the LanceDB Rust-backed .NET SDK. Data persists under <see cref="StoragePath"/>.
/// </summary>
public sealed class LanceDbLocalClient : IVectorStore, IDisposable
{
    private const string TableName = "library";
    private const int DefaultVectorSize = 384;

    private readonly string _storagePath;
    private readonly int _vectorSize;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private Connection? _connection;
    private lancedb.Table? _table;
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Creates a local LanceDB client that persists data under <paramref name="storagePath"/>.
    /// </summary>
    public LanceDbLocalClient(string storagePath, int vectorSize = DefaultVectorSize)
    {
        _storagePath = storagePath ?? throw new ArgumentNullException(nameof(storagePath));
        _vectorSize = vectorSize;
    }

    private async Task<lancedb.Table> EnsureTableAsync(CancellationToken cancellationToken)
    {
        if (_initialized && _table is not null)
        {
            return _table;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized && _table is not null)
            {
                return _table;
            }

            Directory.CreateDirectory(_storagePath);

            _connection = new Connection();
            await _connection.Connect(_storagePath, new ConnectionOptions());

            var names = await _connection.TableNames();
            if (names.Contains(TableName))
            {
                _table = await _connection.OpenTable(TableName);
                // Dimension guard — first-request backstop per Q3
                await ValidateDimensionInternalAsync(_table, _vectorSize, cancellationToken);
                _initialized = true;
                return _table;
            }

            var vectorField = new Field("item", FloatType.Default, nullable: false);
            var vectorType = new FixedSizeListType(vectorField, _vectorSize);
            var schema = new Schema.Builder()
                .Field(new Field("id", StringType.Default, nullable: false))
                .Field(new Field("documentId", StringType.Default, nullable: false))
                .Field(new Field("text", StringType.Default, nullable: true))
                .Field(new Field("ordinal", Int32Type.Default, nullable: false))
                .Field(new Field("vector", vectorType, nullable: false))
                .Build();

            _table = await _connection.CreateTable(
                TableName,
                new CreateTableOptions { Schema = schema, ExistOk = true });

            // Best-effort HNSW index creation. Already indexed or empty tables are ignored.
            try
            {
                await _table.CreateIndex(
                    new[] { "vector" },
                    new HnswFlatIndex
                    {
                        DistanceType = DistanceType.Cosine,
                        NumEdges = 16,
                        EfConstruction = 128
                    },
                    waitTimeout: TimeSpan.FromSeconds(30));
            }
            catch
            {
                // Index may already exist or the table may be too small to index.
            }

            _initialized = true;
            return _table;
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

        var table = await EnsureTableAsync(cancellationToken);
        var batch = BuildBatch(vectors);

        if (batch.Length == 0)
        {
            return;
        }

        await table.MergeInsert("id")
            .WhenMatchedUpdateAll()
            .WhenNotMatchedInsertAll()
            .Execute(batch);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        float[] queryVector,
        int limit,
        string? documentIdFilter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryVector);

        if (queryVector.Length != _vectorSize)
        {
            throw new ArgumentException(
                $"Query vector size mismatch: expected {_vectorSize}, got {queryVector.Length}.",
                nameof(queryVector));
        }

        var table = await EnsureTableAsync(cancellationToken);

        var query = table.Query()
            .NearestTo(queryVector)
            .DistanceType(DistanceType.Cosine)
            .Limit(limit);

        if (!string.IsNullOrWhiteSpace(documentIdFilter))
        {
            query = query.Where(Expr.Col("documentId").Eq(Expr.Lit(documentIdFilter)));
        }

        var rows = await query.ToList(TimeSpan.FromSeconds(30), limit * 2)
            .WaitAsync(cancellationToken);

        return rows.Select(MapRow).ToList();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        var table = await EnsureTableAsync(cancellationToken);
        var filter = Expr.Col("documentId").Eq(Expr.Lit(documentId));
        await table.Delete(filter.ToSql());
    }

    /// <summary>
    /// Validates that the configured vector size matches the persisted collection dimension.
    /// Reads Arrow schema vector FixedSizeList size (R1 resolved probe).
    /// </summary>
    public async Task ValidateDimensionAsync(int configuredVectorSize, CancellationToken cancellationToken = default)
    {
        // No table yet → lazy-create allowed
        if (!Directory.Exists(_storagePath))
            return;

        // Quick probe without reusing _connection/_table to avoid init deadlock
        var probeConn = new Connection();
        try
        {
            await probeConn.Connect(_storagePath, new ConnectionOptions());
            var names = await probeConn.TableNames();
            if (!names.Contains(TableName))
                return;

            var table = await probeConn.OpenTable(TableName);
            await ValidateDimensionInternalAsync(table, configuredVectorSize, cancellationToken);
        }
        finally
        {
            probeConn.Dispose();
        }
    }

    private static async Task ValidateDimensionInternalAsync(lancedb.Table table, int configuredVectorSize, CancellationToken cancellationToken)
    {
        var schema = await table.Schema();
        var vectorField = schema.GetFieldByName("vector");
        if (vectorField is null)
            return;

        if (vectorField.DataType is FixedSizeListType fsl)
        {
            var stored = fsl.ListSize;
            if (stored != configuredVectorSize)
                throw new DimensionMismatchException(configuredVectorSize, stored);
        }
    }

    /// <summary>
    /// Returns the storage path used by this client.
    /// </summary>
    public string StoragePath => _storagePath;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _table?.Dispose();
        _connection?.Dispose();
        _initLock.Dispose();
        _disposed = true;
    }

    /// <inheritdoc />
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));
            var table = await EnsureTableAsync(cts.Token);
            _ = await table.CountRows();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private RecordBatch BuildBatch(IEnumerable<VectorRecord> vectors)
    {
        var vectorField = new Field("item", FloatType.Default, nullable: false);
        var vectorType = new FixedSizeListType(vectorField, _vectorSize);
        var schema = new Schema.Builder()
            .Field(new Field("id", StringType.Default, nullable: false))
            .Field(new Field("documentId", StringType.Default, nullable: false))
            .Field(new Field("text", StringType.Default, nullable: true))
            .Field(new Field("ordinal", Int32Type.Default, nullable: false))
            .Field(new Field("vector", vectorType, nullable: false))
            .Build();

        var idBuilder = new StringArray.Builder();
        var documentIdBuilder = new StringArray.Builder();
        var textBuilder = new StringArray.Builder();
        var ordinalBuilder = new Int32Array.Builder();
        var vectorBuilder = new FixedSizeListArray.Builder(vectorField, _vectorSize);
        var valueBuilder = (FloatArray.Builder)vectorBuilder.ValueBuilder;

        var count = 0;
        foreach (var vector in vectors)
        {
            if (vector.Vector.Length != _vectorSize)
            {
                throw new ArgumentException(
                    $"Vector size mismatch: expected {_vectorSize}, got {vector.Vector.Length}.",
                    nameof(vectors));
            }

            idBuilder.Append(vector.Id.ToString());
            documentIdBuilder.Append(GetPayloadString(vector.Payload, "documentId"));
            textBuilder.Append(GetPayloadString(vector.Payload, "text"));
            ordinalBuilder.Append(GetPayloadInt32(vector.Payload, "ordinal"));
            vectorBuilder.Append();
            foreach (var value in vector.Vector)
            {
                valueBuilder.Append(value);
            }

            count++;
        }

        return new RecordBatch(
            schema,
            new IArrowArray[]
            {
                idBuilder.Build(),
                documentIdBuilder.Build(),
                textBuilder.Build(),
                ordinalBuilder.Build(),
                vectorBuilder.Build()
            },
            count);
    }

    private static SearchResult MapRow(IReadOnlyDictionary<string, object?> row)
    {
        var id = Guid.Parse((string?)row["id"] ?? Guid.Empty.ToString());
        var documentId = (string?)row["documentId"] ?? string.Empty;
        var text = (string?)(row.TryGetValue("text", out var textValue) ? textValue : null) ?? string.Empty;
        var ordinal = (int?)(row.TryGetValue("ordinal", out var ordinalValue) ? ordinalValue ?? 0 : 0) ?? 0;

        // LanceDB returns cosine distance (0 = identical). Convert to similarity score.
        var distance = (float?)(row.TryGetValue("_distance", out var distanceValue) ? distanceValue ?? 0f : 0f) ?? 0f;
        var score = 1.0f - distance;

        return new SearchResult(id, documentId, text, ordinal, score);
    }

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
                uint ui => (int)ui,
                ulong ul => (int)ul,
                short s => s,
                _ => int.TryParse(value.ToString(), out var parsed) ? parsed : 0
            };
        }

        return 0;
    }
}
