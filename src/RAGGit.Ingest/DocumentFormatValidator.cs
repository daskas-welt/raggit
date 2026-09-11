using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RAGGit.Core.Models;

namespace RAGGit.Ingest;

/// <summary>
/// Validates uploaded file content by inspecting leading magic bytes.
/// PDF and docx have reliable signatures; plain text/markdown fall back
/// to the declared MIME type / extension per FR-006.
/// </summary>
public static class DocumentFormatValidator
{
    private static readonly byte[] PdfMagic = "PDF"u8.ToArray();
    private static readonly byte[] PkZipMagic = "PK"u8.ToArray();

    /// <summary>
    /// Reads enough of the stream to verify the content matches the declared
    /// MIME type. The returned stream is positioned at 0 and is seekable.
    /// </summary>
    public static async Task<Stream> ValidateAndRewindAsync(
        Stream stream,
        DocumentMimeType declaredMime,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(stream);

        const int headerSize = 8;
        var buffer = ArrayPool<byte>.Shared.Rent(headerSize);
        try
        {
            int read;
            if (stream.CanSeek && stream.Position != 0)
            {
                stream.Position = 0;
            }

            read = await stream.ReadAsync(buffer.AsMemory(0, headerSize), cancellationToken);
            var actual = read > 0 ? buffer.AsMemory(0, read).ToArray() : Array.Empty<byte>();

            if (!ValidateMagic(actual, declaredMime))
            {
                throw new InvalidDataException(
                    $"File content does not match declared type {declaredMime}."
                );
            }

            if (stream.CanSeek)
            {
                stream.Position = 0;
                return stream;
            }

            // Non-seekable stream: buffer entire content so downstream consumers can re-read.
            var memoryStream = new MemoryStream();
            if (actual.Length > 0)
            {
                await memoryStream.WriteAsync(actual.AsMemory(), cancellationToken);
            }

            await stream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            return memoryStream;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static bool ValidateMagic(byte[] header, DocumentMimeType declaredMime)
    {
        if (header.Length == 0)
        {
            // Empty files are accepted as text/markdown only; binary types require magic.
            return declaredMime is DocumentMimeType.Txt or DocumentMimeType.Md;
        }

        return declaredMime switch
        {
            DocumentMimeType.Pdf => StartsWith(header, "%PDF"u8.ToArray()),
            DocumentMimeType.Docx => StartsWith(header, PkZipMagic),
            DocumentMimeType.Txt or DocumentMimeType.Md => IsUtf8Like(header),
            _ => false,
        };
    }

    private static bool StartsWith(byte[] header, byte[] magic)
    {
        if (header.Length < magic.Length)
        {
            return false;
        }

        for (var i = 0; i < magic.Length; i++)
        {
            if (header[i] != magic[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Treats content as text-like when the first bytes are printable ASCII
    /// or common UTF-8 BOM markers. This is intentionally permissive for v1.
    /// </summary>
    private static bool IsUtf8Like(byte[] header)
    {
        // UTF-8 BOM
        if (header.Length >= 3 && header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF)
        {
            return true;
        }

        // UTF-16 LE BOM
        if (header.Length >= 2 && header[0] == 0xFF && header[1] == 0xFE)
        {
            return true;
        }

        // UTF-16 BE BOM
        if (header.Length >= 2 && header[0] == 0xFE && header[1] == 0xFF)
        {
            return true;
        }

        for (var i = 0; i < Math.Min(header.Length, 4); i++)
        {
            var b = header[i];
            if (b is < 0x09 or (> 0x0D and < 0x20) and >= 0x80)
            {
                return false;
            }
        }

        return true;
    }
}
