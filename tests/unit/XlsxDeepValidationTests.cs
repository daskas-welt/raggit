using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Core.Models;
using RAGGit.Ingest;
using Xunit;

namespace RAGGit.Tests.Unit;

public sealed class XlsxDeepValidationTests
{
    private static Stream OpenFixture(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine("fixtures", "xlsx", fileName),
            Path.Combine(AppContext.BaseDirectory, "fixtures", "xlsx", fileName),
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "integration",
                "fixtures",
                "xlsx",
                fileName
            ),
        };
        foreach (var p in candidates)
            if (File.Exists(p))
                return File.OpenRead(p);
        throw new FileNotFoundException($"Fixture {fileName} not found");
    }

    [Fact]
    public async Task Genuine_Xlsx_Passes_Probe_And_Rewinds()
    {
        await using var fs = OpenFixture("sample-3sheet.xlsx");
        var originalLength = fs.Length;
        var result = await DocumentFormatValidator.ValidateAndRewindAsync(
            fs,
            DocumentMimeType.Xlsx
        );
        result.Should().NotBeNull();
        result.CanSeek.Should().BeTrue();
        result.Position.Should().Be(0);
        result.Length.Should().Be(originalLength);
    }

    [Fact]
    public async Task Fake_Xlsx_From_Docx_Rejected_With_ContentDoesNotMatchType()
    {
        await using var fs = OpenFixture("fake-xlsx-from-docx.xlsx");
        var act = async () =>
            await DocumentFormatValidator.ValidateAndRewindAsync(fs, DocumentMimeType.Xlsx);
        var ex = await Assert.ThrowsAsync<CorruptDocumentException>(act);
        ex.Message.Should().Contain("content does not match type");
    }

    [Fact]
    public async Task Corrupt_Xlsx_Truncated_Returns400Mappable_Error()
    {
        await using var fs = OpenFixture("corrupt.xlsx");
        try
        {
            await DocumentFormatValidator.ValidateAndRewindAsync(fs, DocumentMimeType.Xlsx);
            Assert.Fail("Expected exception for corrupt xlsx");
        }
        catch (CorruptDocumentException ex)
        {
            ex.Message.ToLowerInvariant()
                .Should()
                .ContainAny("content does not match type", "corrupted");
        }
        catch (InvalidDataException)
        {
            // Also acceptable mapping — controller turns InvalidDataException into 400
        }
    }

    [Fact]
    public async Task NonSeekable_Stream_Buffered_To_Seekable_MemoryStream()
    {
        await using var fs = OpenFixture("sample-3sheet.xlsx");
        var bytes = new byte[fs.Length];
        await fs.ReadAsync(bytes);
        using var nonSeekable = new NonSeekableStream(new MemoryStream(bytes));
        var result = await DocumentFormatValidator.ValidateAndRewindAsync(
            nonSeekable,
            DocumentMimeType.Xlsx
        );
        result.CanSeek.Should().BeTrue();
        result.Position.Should().Be(0);
        result.Should().BeOfType<MemoryStream>();
    }

    [Fact]
    public async Task Docx_Declared_As_Docx_Still_Passes_Existing_Magic_Path()
    {
        // Use fake-xlsx-from-docx's docx content as docx — PK magic should still pass
        await using var fs = OpenFixture("fake-xlsx-from-docx.xlsx");
        // Declared as Docx, the PK magic check should pass (no deep xlsx probe)
        var result = await DocumentFormatValidator.ValidateAndRewindAsync(
            fs,
            DocumentMimeType.Docx
        );
        result.Should().NotBeNull();
        result.Position.Should().Be(0);
    }

    private sealed class NonSeekableStream : Stream
    {
        private readonly Stream _inner;

        public NonSeekableStream(Stream inner) => _inner = inner;

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => _inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) =>
            _inner.Write(buffer, offset, count);
    }
}
