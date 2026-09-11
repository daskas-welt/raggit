using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace RAGGit.Tests.Contract;

public sealed class XlsxRejectionContractTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;
    private readonly HttpClient _client;

    public XlsxRejectionContractTests(TestApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);
    }

    [Fact]
    public async Task Overcap_413_Body_Matches_ApiYaml_Shape()
    {
        var bytes = await LoadFixtureAsync("sample-overcap.xlsx");
        var form = new MultipartFormDataContent();
        var file = new StreamContent(new MemoryStream(bytes));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", "sample-overcap.xlsx");
        var resp = await _client.PostAsync("/api/documents", form);
        resp.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("error");
        body.Should().Contain("100,000");
        body.Should().MatchRegex(@".*Spreadsheet exceeds 100,000 cell limit.*found [\d,]+ cells.*");
    }

    [Fact]
    public async Task FakeXlsx_400_Body_Matches_ContentDoesNotMatchType()
    {
        var bytes = await LoadFixtureAsync("fake-xlsx-from-docx.xlsx");
        var form = new MultipartFormDataContent();
        var file = new StreamContent(new MemoryStream(bytes));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", "fake.xlsx");
        var resp = await _client.PostAsync("/api/documents", form);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("content does not match type");
    }

    [Fact]
    public async Task EmptyHiddenOnly_400_NoExtractableContent_Shape()
    {
        var bytes = await LoadFixtureAsync("empty-hidden-only.xlsx");
        var form = new MultipartFormDataContent();
        var file = new StreamContent(new MemoryStream(bytes));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", "empty-hidden-only.xlsx");
        var resp = await _client.PostAsync("/api/documents", form);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resp.Content.ReadAsStringAsync()).ToLowerInvariant().Should().Contain("no extractable content");
    }

    private static async Task<byte[]> LoadFixtureAsync(string name)
    {
        var candidates = new[]
        {
            Path.Combine("fixtures", "xlsx", name),
            Path.Combine(AppContext.BaseDirectory, "fixtures", "xlsx", name),
        };
        foreach (var p in candidates) if (File.Exists(p)) return await File.ReadAllBytesAsync(p);
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var cand = Path.Combine(dir.FullName, "tests", "integration", "fixtures", "xlsx", name);
            if (File.Exists(cand)) return await File.ReadAllBytesAsync(cand);
            dir = dir.Parent;
        }
        throw new FileNotFoundException(name);
    }
}
