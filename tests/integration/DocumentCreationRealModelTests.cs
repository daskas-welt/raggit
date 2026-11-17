using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using RAGGit.Ingest.Ai;
using RAGGit.Ingest.Vector;
using RAGGit.Retrieval.Ai;
using RAGGit.Tests.Integration.Helpers;
using RAGGit.Workstation.Api.Auth;
using Xunit;

namespace RAGGit.Tests.Integration;

/// <summary>
/// 034 T039: opt-in real-model evidence for source use, irrelevant-source
/// exclusion, same-template person isolation and conflict disclosure. Returns
/// early when Ollama is unavailable so the default suite stays green.
/// </summary>
[Trait("RequiresOllama", "true")]
public sealed class DocumentCreationRealModelTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        "raggit-034-real-" + Guid.NewGuid().ToString("N")
    );

    public DocumentCreationRealModelTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException) { }
    }

    [Fact]
    public async Task RealModel_CreationUsesRelevantSourcesAndDisclosesConflicts()
    {
        var ollamaUrl = OllamaProbe.DefaultUrl;
        if (!await OllamaProbe.IsAvailableAsync(ollamaUrl, 2000))
        {
            return;
        }

        var chatModel =
            Environment.GetEnvironmentVariable("OLLAMA_CHAT_MODEL") ?? "qwen3:4b-instruct-2507";
        var embedModel =
            Environment.GetEnvironmentVariable("OLLAMA_EMBED_MODEL") ?? "snowflake-arctic-embed2";
        var vectorSize =
            embedModel.Contains("nomic", StringComparison.OrdinalIgnoreCase) ? 768
            : embedModel.Contains("minilm", StringComparison.OrdinalIgnoreCase)
            || embedModel.Contains("bge-micro", StringComparison.OrdinalIgnoreCase)
                ? 384
            : 1024;
        var timeoutMs = int.TryParse(
            Environment.GetEnvironmentVariable("OLLAMA_TIMEOUT_MS"),
            out var configuredTimeout
        )
            ? configuredTimeout
            : 900_000;

        using var factory = new RealFactory(
            Path.Combine(_tempDir, "rag.db"),
            Path.Combine(_tempDir, "lancedb"),
            ollamaUrl,
            embedModel,
            chatModel,
            vectorSize,
            timeoutMs
        );
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Api-Key", factory.AdminKey);

        var documents = new Dictionary<string, string>
        {
            ["refund-policy"] =
                "The refund policy allows returns within 30 days with a full refund.",
            ["parking-policy"] = "The office parking policy provides 12 spaces for employees.",
            ["person-maria"] = "Name: Maria Papadopoulou\nRole: Analyst\nDepartment: Finance",
            ["person-giorgos"] = "Name: Giorgos Nikolaou\nRole: Manager\nDepartment: Operations",
            ["retention-five"] = "The data retention period is 5 years.",
            ["retention-seven"] = "The data retention period is 7 years.",
        };

        foreach (var (name, text) in documents)
        {
            var form = new MultipartFormDataContent();
            var file = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(text)));
            file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            form.Add(file, "file", $"{name}-{Guid.NewGuid():N}.txt");
            (await admin.PostAsync("/api/documents", form)).EnsureSuccessStatusCode();
        }

        var settled = await factory.WaitForSettledAsync(admin);
        settled.Should().HaveCount(documents.Count);
        settled.Should().OnlyContain(document => document.Status == DocumentStatus.Ready);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", factory.EmployeeKey);

        var refund = await CreateTextAsync(client, "Create a note about the refund policy.");
        refund.Should().Contain("30 days").And.Contain("refund-policy");

        var parking = await CreateTextAsync(
            client,
            "Create a note about the office parking policy."
        );
        parking.Should().Contain("12 spaces").And.NotContain("30 days");

        var maria = await CreateTextAsync(client, "Create a note about Maria Papadopoulou.");
        maria
            .Should()
            .Contain("Maria Papadopoulou")
            .And.NotContain("Giorgos")
            .And.NotContain("Manager");

        var retention = await CreateTextAsync(
            client,
            "Create a note about the data retention period."
        );
        retention.Should().Contain("5 years").And.Contain("7 years");
    }

    private static async Task<string> CreateTextAsync(HttpClient client, string description)
    {
        var response = await client.PostAsJsonAsync(
            "/api/documents/generate",
            new { description, format = "txt" }
        );
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync());
    }

    private sealed class RealFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath;
        private readonly string _lancePath;
        private readonly string _ollamaUrl;
        private readonly string _embedModel;
        private readonly string _chatModel;
        private readonly int _vectorSize;
        private readonly int _timeoutMs;

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new DocumentMimeTypeConverter(),
                new System.Text.Json.Serialization.JsonStringEnumConverter(),
            },
        };

        public string AdminKey { get; } = "admin-034-real";
        public string EmployeeKey { get; } = "employee-034-real";

        public async Task<List<Document>> WaitForSettledAsync(
            HttpClient adminClient,
            TimeSpan? timeout = null
        )
        {
            var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromMinutes(10));
            while (true)
            {
                var response = await adminClient.GetAsync("/api/documents");
                response.EnsureSuccessStatusCode();
                var documents =
                    JsonSerializer.Deserialize<List<Document>>(
                        await response.Content.ReadAsStringAsync(),
                        JsonOptions
                    ) ?? new List<Document>();

                if (
                    documents.TrueForAll(document =>
                        document.Status is DocumentStatus.Ready or DocumentStatus.Failed
                    )
                    || DateTime.UtcNow >= deadline
                )
                {
                    return documents;
                }

                await Task.Delay(500);
            }
        }

        public RealFactory(
            string dbPath,
            string lancePath,
            string ollamaUrl,
            string embedModel,
            string chatModel,
            int vectorSize,
            int timeoutMs
        )
        {
            _dbPath = dbPath;
            _lancePath = lancePath;
            _ollamaUrl = ollamaUrl;
            _embedModel = embedModel;
            _chatModel = chatModel;
            _vectorSize = vectorSize;
            _timeoutMs = timeoutMs;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.Configure<ApiKeyAuthOptions>(
                    ApiKeyAuthOptions.Scheme,
                    options =>
                    {
                        options.AdminApiKey = AdminKey;
                        options.EmployeeApiKey = EmployeeKey;
                    }
                );
                services.AddSingleton(new RagDbContext($"Data Source={_dbPath}"));
                services.AddSingleton<IVectorStore>(
                    new LanceDbLocalClient(_lancePath, _vectorSize)
                );
                services.AddSingleton<IEmbedder>(
                    new OllamaEmbedder(_ollamaUrl, _embedModel, _timeoutMs)
                );
                services.AddSingleton<ILlmClient>(
                    new OllamaLlmClient(_ollamaUrl, _chatModel, _timeoutMs)
                );
            });
        }
    }
}
