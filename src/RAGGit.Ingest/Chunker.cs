using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using RAGGit.Core.Models;
using UglyToad.PdfPig;

namespace RAGGit.Ingest;

/// <summary>
/// Extracts plain text from supported document formats and splits it into
/// overlapping token windows per data-model.md.
/// </summary>
public static class Chunker
{
    /// <summary>
    /// Extracts raw text from a stream based on the document MIME type.
    /// </summary>
    public static async Task<string> ExtractTextAsync(Stream stream, DocumentMimeType mime)
    {
        return mime switch
        {
            DocumentMimeType.Pdf => ExtractPdfText(stream),
            DocumentMimeType.Docx => ExtractDocxText(stream),
            DocumentMimeType.Txt or DocumentMimeType.Md => await ExtractPlainTextAsync(stream),
            _ => throw new NotSupportedException($"Unsupported MIME type: {mime}"),
        };
    }

    /// <summary>
    /// Splits text into chunks of <paramref name="chunkSize"/> tokens with
    /// <paramref name="overlap"/> tokens overlapping between consecutive chunks.
    /// Token counting is a whitespace-split approximation for v1.
    /// </summary>
    public static IReadOnlyList<Chunk> ChunkText(
        string text,
        Guid documentId,
        int chunkSize = 512,
        int overlap = 50
    )
    {
        ArgumentNullException.ThrowIfNull(text);

        if (chunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chunkSize),
                "Chunk size must be positive."
            );
        }

        if (overlap < 0 || overlap >= chunkSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(overlap),
                "Overlap must be non-negative and less than chunk size."
            );
        }

        var tokens = text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return new List<Chunk>();
        }

        var step = chunkSize - overlap;
        var chunks = new List<Chunk>();
        var ordinal = 0;

        for (var start = 0; start < tokens.Length; start += step)
        {
            var length = Math.Min(chunkSize, tokens.Length - start);
            var chunkTokens = tokens[start..(start + length)];
            var chunkText = string.Join(' ', chunkTokens);

            chunks.Add(
                new Chunk
                {
                    Id = Guid.NewGuid(),
                    DocumentId = documentId,
                    Ordinal = ordinal++,
                    Text = chunkText,
                    TokenCount = length,
                }
            );
        }

        return chunks;
    }

    /// <summary>
    /// Computes a SHA-256 hash for the stream contents and resets the stream
    /// position to zero when it is seekable.
    /// </summary>
    public static async Task<string> ComputeHashAsync(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return Convert.ToHexString(hash);
    }

    private static string ExtractPdfText(Stream stream)
    {
        var builder = new StringBuilder();
        using var document = PdfDocument.Open(stream);
        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        return builder.ToString();
    }

    private static string ExtractDocxText(Stream stream)
    {
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document.Body;
        if (body is null)
        {
            return string.Empty;
        }

        var paragraphs = body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
            .Select(p => p.InnerText)
            .Where(t => !string.IsNullOrWhiteSpace(t));

        return string.Join(Environment.NewLine, paragraphs);
    }

    private static async Task<string> ExtractPlainTextAsync(Stream stream)
    {
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true
        );
        return await reader.ReadToEndAsync();
    }
}
