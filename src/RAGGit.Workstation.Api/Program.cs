using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using RAGGit.Ingest;
using RAGGit.Ingest.Ai;
using RAGGit.Ingest.Vector;
using RAGGit.Retrieval.Ai;
using RAGGit.Workstation.Api.Auth;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Cache
builder.Services.AddMemoryCache();
builder.Services.AddResponseCaching();

// CORS: allow any LAN origin; auth is header-based so credentials are not required.
builder.Services.AddCors(options =>
{
    options.AddPolicy("Lan", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Auth / RBAC
builder.Services.AddAuthentication(ApiKeyAuthOptions.Scheme)
    .AddScheme<ApiKeyAuthOptions, ApiKeyAuthHandler>(ApiKeyAuthOptions.Scheme, options =>
    {
        options.AdminApiKey = builder.Configuration["Api:AdminKey"] ?? string.Empty;
        options.EmployeeApiKey = builder.Configuration["Api:EmployeeKey"] ?? string.Empty;
    });

builder.Services.AddAuthorization();

// Configuration
var vectorDbPath = builder.Configuration["VectorDb:Path"] ?? "./data/lancedb";
var vectorDbVectorSize = builder.Configuration.GetValue<int?>("VectorDb:VectorSize") ?? 384;
var ollamaUrl = builder.Configuration["Ollama:Url"] ?? "http://localhost:11434";
var embedModel = builder.Configuration["Ollama:EmbedModel"] ?? "nomic-embed-text";
var chatModel = builder.Configuration["Ollama:ChatModel"] ?? "llama3.2:3b";
var connectionString = builder.Configuration.GetConnectionString("RagDb") ?? "Data Source=./data/rag.db";

// Backward compatibility: fall back to legacy Qdrant:Path if VectorDb:Path is missing.
if (!builder.Configuration.GetSection("VectorDb:Path").Exists() &&
    !string.IsNullOrEmpty(builder.Configuration["Qdrant:Path"]))
{
    vectorDbPath = builder.Configuration["Qdrant:Path"]!;
}

// Data
builder.Services.AddSingleton(new RagDbContext(connectionString));

// AI services
builder.Services.AddSingleton<IVectorStore>(new LanceDbLocalClient(vectorDbPath, vectorDbVectorSize));
builder.Services.AddSingleton<IEmbedder>(new OllamaEmbedder(ollamaUrl, embedModel));
builder.Services.AddSingleton<ILlmClient>(new OllamaLlmClient(ollamaUrl, chatModel));
builder.Services.AddSingleton<RAGGit.Ingest.IngestService>();

// API
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new RAGGit.Core.Models.DocumentMimeTypeConverter());
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = DocumentValidation.MaxFileSizeBytes + 1024;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors("Lan");
app.UseResponseCaching();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Ensure SQLite schema exists.
await using (var db = app.Services.GetRequiredService<RagDbContext>())
{
    await db.EnsureCreatedAsync();
}

app.Run();

public partial class Program { }
