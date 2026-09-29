using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Models;

namespace RAGGit.Core.Data;

/// <summary>
/// SQLite implementation of <see cref="IDocumentRepository"/> — the single
/// persistence channel for the <see cref="Document"/> aggregate root, per the
/// MS persistence-layer design (one repository per aggregate root).
/// <see cref="Chunk"/> rows are children of the Document aggregate.
/// Offline invariant: pure Microsoft.Data.Sqlite, no Ollama/LanceDB/HTTP.
/// </summary>
public class SqliteDocumentRepository : IDocumentRepository
{
    private readonly RagDbContext _dbContext;

    public SqliteDocumentRepository(RagDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Lists the caller's own documents (CreatedBy == sub, legacy excluded),
    /// ordered CreatedAt DESC, Id DESC. Offline: SQLite-only, no Ollama/LanceDB/HTTP.
    /// </summary>
    public async Task<DocumentsMinePage> ListMineAsync(
        string sub,
        int? limit,
        int? offset,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);

        // Same pagination envelope as history: clamp, COUNT, page.
        var clampedLimit = SqliteQueryRepository.ClampLimit(limit);
        var clampedOffset = SqliteQueryRepository.ClampOffset(offset);

        // offline: SQLite-only read over the Documents table.
        await using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        int total;
        using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = CountSql;
            countCommand.Parameters.AddWithValue("@sub", sub);
            total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
        }

        var items = new List<DocumentMineItem>();
        using (var listCommand = connection.CreateCommand())
        {
            listCommand.CommandText = ListSql;
            listCommand.Parameters.AddWithValue("@sub", sub);
            listCommand.Parameters.AddWithValue("@limit", clampedLimit);
            listCommand.Parameters.AddWithValue("@offset", clampedOffset);

            await using var reader = await listCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(
                    new DocumentMineItem
                    {
                        Id = Guid.Parse(reader.GetString(0)),
                        Filename = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        Size = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                        Status = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        CreatedAt = DateTime.Parse(
                            reader.GetString(4),
                            null,
                            System.Globalization.DateTimeStyles.RoundtripKind
                        ),
                    }
                );
            }
        }

        return new DocumentsMinePage
        {
            Items = items,
            Total = total,
            Limit = clampedLimit,
            Offset = clampedOffset,
        };
    }

    /// <summary>
    /// Finds a document by id, or null when absent.
    /// </summary>
    public async Task<Document?> FindByIdAsync(
        Guid documentId,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            @"
            SELECT Id, Filename, Mime, Size, Hash, Status, CreatedBy, CreatedByName, CreatedAt, FailureReason
            FROM Documents
            WHERE Id = @id
            LIMIT 1;";
        command.Parameters.AddWithValue("@id", documentId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapDocument(reader);
        }

        return null;
    }

    /// <summary>
    /// Lists all documents in the singleton library, newest first.
    /// </summary>
    public async Task<IReadOnlyList<Document>> ListAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            @"
            SELECT Id, Filename, Mime, Size, Hash, Status, CreatedBy, CreatedByName, CreatedAt, FailureReason
            FROM Documents
            ORDER BY CreatedAt DESC;";

        var documents = new List<Document>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            documents.Add(MapDocument(reader));
        }

        return documents;
    }

    /// <summary>
    /// Finds a document by its SHA-256 hash.
    /// </summary>
    public async Task<Document?> FindByHashAsync(
        string hash,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        await using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            @"
            SELECT Id, Filename, Mime, Size, Hash, Status, CreatedBy, CreatedByName, CreatedAt, FailureReason
            FROM Documents
            WHERE Hash = @hash
            LIMIT 1;";
        command.Parameters.AddWithValue("@hash", hash);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapDocument(reader);
        }

        return null;
    }

    /// <summary>
    /// Inserts a new document row.
    /// </summary>
    public async Task AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        await using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            @"
            INSERT INTO Documents (Id, Filename, Mime, Size, Hash, Status, CreatedBy, CreatedByName, CreatedAt, FailureReason)
            VALUES (@id, @filename, @mime, @size, @hash, @status, @createdBy, @createdByName, @createdAt, @failureReason);";

        command.Parameters.AddWithValue("@id", document.Id.ToString());
        command.Parameters.AddWithValue("@filename", document.Filename);
        command.Parameters.AddWithValue("@mime", document.Mime.GetContentType());
        command.Parameters.AddWithValue("@size", document.Size);
        command.Parameters.AddWithValue("@hash", document.Hash);
        command.Parameters.AddWithValue("@status", document.Status.ToString());
        command.Parameters.AddWithValue("@createdBy", document.CreatedBy);
        command.Parameters.AddWithValue(
            "@createdByName",
            document.CreatedByName is null ? DBNull.Value : document.CreatedByName
        );
        command.Parameters.AddWithValue("@createdAt", document.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue(
            "@failureReason",
            document.FailureReason is null ? DBNull.Value : document.FailureReason
        );

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Inserts chunk rows for a document in a single transaction (Unit of Work).
    /// </summary>
    public async Task AddChunksAsync(
        IEnumerable<Chunk> chunks,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(chunks);

        await using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var chunk in chunks)
            {
                using var command = connection.CreateCommand();
                command.Transaction = (SqliteTransaction)transaction;
                command.CommandText =
                    @"
                    INSERT INTO Chunks (Id, DocumentId, Ordinal, Text, TokenCount)
                    VALUES (@id, @documentId, @ordinal, @text, @tokenCount);";

                command.Parameters.AddWithValue("@id", chunk.Id.ToString());
                command.Parameters.AddWithValue("@documentId", chunk.DocumentId.ToString());
                command.Parameters.AddWithValue("@ordinal", chunk.Ordinal);
                command.Parameters.AddWithValue("@text", chunk.Text);
                command.Parameters.AddWithValue("@tokenCount", chunk.TokenCount);

                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Updates a document's processing status and failure explanation in a
    /// single statement (a retry clears the previous reason by passing null).
    /// </summary>
    public async Task UpdateStatusAsync(
        Guid documentId,
        DocumentStatus status,
        string? failureReason = null,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            "UPDATE Documents SET Status = @status, FailureReason = @failureReason WHERE Id = @id;";
        command.Parameters.AddWithValue("@status", status.ToString());
        command.Parameters.AddWithValue(
            "@failureReason",
            failureReason is null ? DBNull.Value : failureReason
        );
        command.Parameters.AddWithValue("@id", documentId.ToString());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Deletes a document and its chunks in a single transaction (Unit of Work).
    /// Returns <c>true</c> when the document existed and was deleted,
    /// <c>false</c> when it was not found.
    /// </summary>
    public async Task<bool> DeleteAsync(
        Guid documentId,
        CancellationToken cancellationToken = default
    )
    {
        await using var connection = _dbContext.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            using var existsCommand = connection.CreateCommand();
            existsCommand.Transaction = (SqliteTransaction)transaction;
            existsCommand.CommandText = "SELECT COUNT(*) FROM Documents WHERE Id = @id;";
            existsCommand.Parameters.AddWithValue("@id", documentId.ToString());
            var count = Convert.ToInt64(await existsCommand.ExecuteScalarAsync(cancellationToken));
            if (count == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            using var deleteChunksCommand = connection.CreateCommand();
            deleteChunksCommand.Transaction = (SqliteTransaction)transaction;
            deleteChunksCommand.CommandText = "DELETE FROM Chunks WHERE DocumentId = @id;";
            deleteChunksCommand.Parameters.AddWithValue("@id", documentId.ToString());
            await deleteChunksCommand.ExecuteNonQueryAsync(cancellationToken);

            using var deleteDocumentCommand = connection.CreateCommand();
            deleteDocumentCommand.Transaction = (SqliteTransaction)transaction;
            deleteDocumentCommand.CommandText = "DELETE FROM Documents WHERE Id = @id;";
            deleteDocumentCommand.Parameters.AddWithValue("@id", documentId.ToString());
            await deleteDocumentCommand.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return true;
    }

    internal static Document MapDocument(SqliteDataReader reader)
    {
        var mimeText = reader.GetString(2);
        var mime = mimeText switch
        {
            "application/pdf" => DocumentMimeType.Pdf,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" =>
                DocumentMimeType.Docx,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" =>
                DocumentMimeType.Xlsx,
            "text/plain" => DocumentMimeType.Txt,
            "text/markdown" => DocumentMimeType.Md,
            _ => throw new InvalidOperationException($"Unknown MIME type in database: {mimeText}"),
        };

        if (!Enum.TryParse<DocumentStatus>(reader.GetString(5), out var status))
        {
            throw new InvalidOperationException(
                $"Unknown status in database: {reader.GetString(5)}"
            );
        }

        return new Document
        {
            Id = Guid.Parse(reader.GetString(0)),
            Filename = reader.GetString(1),
            Mime = mime,
            Size = reader.GetInt64(3),
            Hash = reader.GetString(4),
            Status = status,
            CreatedBy = reader.GetString(6),
            CreatedByName = reader.IsDBNull(7) ? null : reader.GetString(7),
            CreatedAt = DateTime.Parse(reader.GetString(8)),
            FailureReason = reader.IsDBNull(9) ? null : reader.GetString(9),
        };
    }

    internal const string ListSql =
        @"
            SELECT Id, Filename, Size, Status, CreatedAt
            FROM Documents
            WHERE CreatedBy = @sub AND CreatedBy NOT IN ('admin','employee')
            ORDER BY CreatedAt DESC, Id DESC
            LIMIT @limit OFFSET @offset;";

    internal const string CountSql =
        @"
            SELECT COUNT(*)
            FROM Documents
            WHERE CreatedBy = @sub AND CreatedBy NOT IN ('admin','employee');";
}
