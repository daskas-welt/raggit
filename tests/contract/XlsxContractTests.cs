using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FluentAssertions;
using RAGGit.Core.Models;
using Xunit;

namespace RAGGit.Tests.Contract;

/// <summary>
/// T004 contract tests for api.yaml 1.2.0 xlsx acceptance shape.
/// Requires T006-T008 to pass.
/// </summary>
public sealed class XlsxContractTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DocumentMimeTypeConverter(), new JsonStringEnumConverter() },
    };

    public XlsxContractTests(TestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);
    }

    [Fact]
    public async Task Post_Xlsx_WithSpreadsheetmlContentType_Returns201()
    {
        var bytes = await LoadFixtureAsync("sample-3sheet.xlsx");
        var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(new MemoryStream(bytes));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(fileContent, "file", "sample-3sheet.xlsx");

        var response = await _client.PostAsync("/api/documents", form);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var doc = await DeserializeDocumentAsync(response);
        doc.Mime.Should().Be(DocumentMimeType.Xlsx);
    }

    [Fact]
    public async Task Post_Xlsx_ExtensionFallback_Maps_OctetStream()
    {
        var bytes = await LoadFixtureAsync("sample-3sheet.xlsx");
        var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(new MemoryStream(bytes));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", "fallback.xlsx");

        var response = await _client.PostAsync("/api/documents", form);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Get_Documents_Serializes_Xlsx_Mime_As_Spreadsheetml_String()
    {
        var bytes = await LoadFixtureAsync("sample-3sheet.xlsx");
        var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(new MemoryStream(bytes));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(fileContent, "file", "serialize-check.xlsx");
        var post = await _client.PostAsync("/api/documents", form);
        post.StatusCode.Should().Be(HttpStatusCode.Created);

        var get = await _client.GetAsync("/api/documents");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await get.Content.ReadAsStringAsync();
        body.Should().Contain("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Fact]
    public void ApiYaml_Mime_Enum_Includes_Spreadsheetml()
    {
        var candidates = new[]
        {
            Path.Combine("specs", "003-ingest-breadth", "contracts", "api.yaml"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "specs", "003-ingest-breadth", "contracts", "api.yaml"),
            Path.Combine(AppContext.BaseDirectory, "specs", "003-ingest-breadth", "contracts", "api.yaml"),
        };
        string? yaml = null;
        foreach (var p in candidates)
            if (File.Exists(p)) { yaml = File.ReadAllText(p); break; }
        if (yaml == null)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var cand = Path.Combine(dir.FullName, "specs", "003-ingest-breadth", "contracts", "api.yaml");
                if (File.Exists(cand)) { yaml = File.ReadAllText(cand); break; }
                dir = dir.Parent;
            }
        }
        yaml.Should().NotBeNull();
        yaml.Should().Contain("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        yaml.Should().Contain("1.2.0");
    }

    private static async Task<byte[]> LoadFixtureAsync(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine("fixtures", "xlsx", fileName),
            Path.Combine(AppContext.BaseDirectory, "fixtures", "xlsx", fileName),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "fixtures", "xlsx", fileName),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "integration", "fixtures", "xlsx", fileName),
        };
        foreach (var p in candidates)
            if (File.Exists(p)) return await File.ReadAllBytesAsync(p);
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var cand = Path.Combine(dir.FullName, "tests", "integration", "fixtures", "xlsx", fileName);
            if (File.Exists(cand)) return await File.ReadAllBytesAsync(cand);
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"Fixture {fileName} not found");
    }

    private async Task<Document> DeserializeDocumentAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Document>(json, _jsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize Document");
    }
}
