using System;
using System.Collections.Generic;
using RAGGit.Core.Models;

namespace RAGGit.Documents;

/// <summary>A rendered document held only in memory until the HTTP response.</summary>
public sealed class GeneratedDocument
{
    public DocumentOutputFormat Format { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public byte[] Bytes { get; init; } = Array.Empty<byte>();
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<DocumentSource> Sources { get; init; } = Array.Empty<DocumentSource>();
    public int RowCount { get; init; }
    public int ByteCount => Bytes.Length;
}

/// <summary>One numbered source in a document's appendix.</summary>
public sealed class DocumentSource
{
    public int Index { get; init; }
    public Guid DocumentId { get; init; }
    public string? DocumentName { get; init; }
    public Guid ChunkId { get; init; }
    public int Ordinal { get; init; }
    public string Excerpt { get; init; } = string.Empty;
}

/// <summary>A spreadsheet section: either record rows or titled prose rows.</summary>
public sealed class DocumentSection
{
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<DocumentRow> Rows { get; init; } = Array.Empty<DocumentRow>();
    public bool IsRecordShape { get; init; }
}

/// <summary>One spreadsheet row.</summary>
public sealed class DocumentRow
{
    public IReadOnlyList<DocumentCell> Cells { get; init; } = Array.Empty<DocumentCell>();
}

/// <summary>One spreadsheet cell with its detected kind and source references.</summary>
public sealed class DocumentCell
{
    public string Header { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public DocumentCellKind Kind { get; init; } = DocumentCellKind.Text;
    public double? NumberValue { get; init; }
    public DateTime? DateValue { get; init; }
    public string? NumberFormat { get; init; }
    public IReadOnlyList<int> SourceIndexes { get; init; } = Array.Empty<int>();
}

public enum DocumentCellKind
{
    Text,
    Number,
    Date,
}

/// <summary>Result of mapping the answer's citation GUIDs to numbered sources.</summary>
public sealed class DocumentCitationMap
{
    public string Body { get; init; } = string.Empty;
    public IReadOnlyList<DocumentSource> Sources { get; init; } = Array.Empty<DocumentSource>();
}
