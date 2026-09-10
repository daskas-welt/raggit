using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Core.Models;
using RAGGit.Ingest;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="DocumentFormatValidator"/> magic-byte validation.
/// </summary>
public sealed class DocumentFormatValidatorTests
{
    [Theory]
    [InlineData(DocumentMimeType.Pdf, "%PDF-1.4\n1 0 obj")]
    [InlineData(DocumentMimeType.Docx, "PK\x03\x04")]
    [InlineData(DocumentMimeType.Txt, "This is plain text.")]
    [InlineData(DocumentMimeType.Md, "# Markdown header")]
    public async Task ValidateAndRewindAsync_MatchingMagic_ReturnsSeekableStream(DocumentMimeType mime, string content)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var result = await DocumentFormatValidator.ValidateAndRewindAsync(stream, mime);

        result.CanSeek.Should().BeTrue();
        result.Position.Should().Be(0);
    }

    [Fact]
    public async Task ValidateAndRewindAsync_PdfMissingMagic_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Not a PDF"));

        var act = async () => await DocumentFormatValidator.ValidateAndRewindAsync(stream, DocumentMimeType.Pdf);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task ValidateAndRewindAsync_DocxMissingPkSignature_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Not a zip archive"));

        var act = async () => await DocumentFormatValidator.ValidateAndRewindAsync(stream, DocumentMimeType.Docx);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task ValidateAndRewindAsync_NonSeekableStream_ReturnsBufferedSeekableStream()
    {
        var bytes = Encoding.UTF8.GetBytes("Plain text content");
        var stream = new NonSeekableStream(bytes);

        var result = await DocumentFormatValidator.ValidateAndRewindAsync(stream, DocumentMimeType.Txt);

        result.CanSeek.Should().BeTrue();
        result.Position.Should().Be(0);

        using var reader = new StreamReader(result, Encoding.UTF8);
        var text = await reader.ReadToEndAsync();
        text.Should().Be("Plain text content");
    }

    private sealed class NonSeekableStream : Stream
    {
        private readonly byte[] _data;
        private int _position;

        public NonSeekableStream(byte[] data)
        {
            _data = data;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _data.Length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= _data.Length)
            {
                return 0;
            }

            var read = Math.Min(count, _data.Length - _position);
            Buffer.BlockCopy(_data, _position, buffer, offset, read);
            _position += read;
            return read;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
