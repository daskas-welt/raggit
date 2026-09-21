using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Core.Models;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace RAGGit.Tests.Contract;

/// <summary>
/// 018-allowed-upload-types: the intake allow-list is exactly
/// pdf/docx/xlsx/txt. New `.md` (and other) uploads are rejected with 400.
/// </summary>
public sealed class UploadAllowListContractTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    public UploadAllowListContractTests(TestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);
    }

    [Fact]
    public async Task Post_ValidTxt_Accepted()
    {
        var response = await PostBytesAsync(
            Encoding.UTF8.GetBytes("Allow-list contract txt content."),
            "text/plain",
            "allowlist.txt"
        );

        response.IsSuccessStatusCode.Should().BeTrue();
        var doc = await DeserializeDocumentAsync(response);
        doc.Mime.Should().Be(DocumentMimeType.Txt);
    }

    [Fact]
    public async Task Post_ValidPdf_Accepted()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(612, 792);
        page.AddText("Allow-list contract pdf content.", 12, new PdfPoint(50, 700), font);

        var response = await PostBytesAsync(builder.Build(), "application/pdf", "allowlist.pdf");

        response.IsSuccessStatusCode.Should().BeTrue();
        var doc = await DeserializeDocumentAsync(response);
        doc.Mime.Should().Be(DocumentMimeType.Pdf);
    }

    [Fact]
    public async Task Post_ValidDocx_Accepted()
    {
        var response = await PostBytesAsync(
            BuildMinimalDocx("Allow-list contract docx content."),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "allowlist.docx"
        );

        response.IsSuccessStatusCode.Should().BeTrue();
        var doc = await DeserializeDocumentAsync(response);
        doc.Mime.Should().Be(DocumentMimeType.Docx);
    }

    [Fact]
    public async Task Post_ValidXlsx_Accepted()
    {
        var bytes = await LoadIntegrationFixtureAsync(Path.Combine("xlsx", "sample-3sheet.xlsx"));
        var response = await PostBytesAsync(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "allowlist.xlsx"
        );

        response.IsSuccessStatusCode.Should().BeTrue();
        var doc = await DeserializeDocumentAsync(response);
        doc.Mime.Should().Be(DocumentMimeType.Xlsx);
    }

    [Fact]
    public async Task Post_Markdown_Rejected400NamingFile()
    {
        var response = await PostBytesAsync(
            Encoding.UTF8.GetBytes("# Allow-list contract markdown"),
            "text/markdown",
            "notes.md"
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("unsupported type");
        body.Should().Contain(".md");
    }

    [Fact]
    public async Task Post_MarkdownByExtension_Rejected400()
    {
        var response = await PostBytesAsync(
            Encoding.UTF8.GetBytes("# Allow-list contract markdown"),
            "application/octet-stream",
            "notes.md"
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("unsupported type");
    }

    [Fact]
    public async Task Post_LegacyDoc_Rejected400()
    {
        var response = await PostBytesAsync(
            Encoding.UTF8.GetBytes("fake legacy doc content"),
            "application/msword",
            "legacy.doc"
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("unsupported type");
    }

    [Fact]
    public async Task Post_DocxRenamedToXlsx_Rejected400ContentMismatch()
    {
        var bytes = await LoadIntegrationFixtureAsync(
            Path.Combine("xlsx", "fake-xlsx-from-docx.xlsx")
        );
        var response = await PostBytesAsync(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "fake.xlsx"
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync())
            .Should()
            .Contain("content does not match type");
    }

    private async Task<HttpResponseMessage> PostBytesAsync(
        byte[] bytes,
        string contentType,
        string fileName
    )
    {
        var file = new StreamContent(new MemoryStream(bytes));
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent();
        form.Add(file, "file", fileName);
        return await _client.PostAsync("/api/documents", form);
    }

    private static byte[] BuildMinimalDocx(string paragraphText)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(
                archive,
                "[Content_Types].xml",
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
                </Types>
                """
            );
            WriteEntry(
                archive,
                "_rels/.rels",
                """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
                </Relationships>
                """
            );
            var escaped = System.Security.SecurityElement.Escape(paragraphText);
            WriteEntry(
                archive,
                "word/document.xml",
                $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                  <w:body><w:p><w:r><w:t>{escaped}</w:t></w:r></w:p></w:body>
                </w:document>
                """
            );
        }
        return output.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    private static async Task<byte[]> LoadIntegrationFixtureAsync(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "tests",
                "integration",
                "fixtures",
                relativePath
            );
            if (File.Exists(candidate))
                return await File.ReadAllBytesAsync(candidate);
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"Fixture {relativePath} not found");
    }

    private async Task<Document> DeserializeDocumentAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Document>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize Document response.");
    }
}
