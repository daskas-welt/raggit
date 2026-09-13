using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using RAGGit.Ingest;
using RAGGit.Ingest.Ai;
using RAGGit.Ingest.Vector;
using RAGGit.Retrieval;
using RAGGit.Retrieval.Ai;
using RAGGit.Workstation.Api.Auth;
using RAGGit.Workstation.Api.Middleware;
using Serilog;
using Serilog.Enrichers;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{RequestId}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

builder.Host.UseSerilog();

// Cache
var cacheEnabled = builder.Configuration.GetValue<bool>("Cache:Enabled");
var embedCap = builder.Configuration.GetValue<int?>("Cache:EmbedCap") ?? 10000;
var embedTtl = builder.Configuration.GetValue<int?>("Cache:EmbedTTLHours") ?? 24;

builder.Services.AddMemoryCache(options => options.SizeLimit = embedCap);
builder.Services.AddResponseCaching();

// CORS: allow any LAN origin; auth is header-based so credentials are not required.
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "Lan",
        policy =>
        {
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        }
    );
});

// Auth / RBAC
var jwtSigningKeyPath = builder.Configuration["Auth:JwtSigningKeyPath"] ?? "./data/auth.key";
var tokenLifetimeHours = builder.Configuration.GetValue<int?>("Auth:TokenLifetimeHours") ?? 8;
var pbkdf2Iterations = builder.Configuration.GetValue<int?>("Auth:Pbkdf2Iterations") ?? 310_000;
var lockoutThreshold = builder.Configuration.GetValue<int?>("Auth:LockoutThreshold") ?? 5;
var lockoutMinutes = builder.Configuration.GetValue<int?>("Auth:LockoutMinutes") ?? 15;

var jwtKey = AuthKeyLoader.LoadOrCreateKey(jwtSigningKeyPath);

builder.Services.AddSingleton<UserStore>();
builder.Services.Configure<JwtTokenServiceOptions>(options =>
{
    options.SigningKey = jwtKey;
    options.TokenLifetimeHours = tokenLifetimeHours;
});
builder.Services.AddSingleton<JwtTokenService>();

builder
    .Services.AddAuthentication(ApiKeyAuthOptions.Scheme)
    .AddScheme<ApiKeyAuthOptions, ApiKeyAuthHandler>(
        ApiKeyAuthOptions.Scheme,
        options =>
        {
            options.AdminApiKey = builder.Configuration["Api:AdminKey"] ?? string.Empty;
            options.EmployeeApiKey = builder.Configuration["Api:EmployeeKey"] ?? string.Empty;
        }
    )
    .AddJwtBearer(
        JwtBearerDefaults.AuthenticationScheme,
        options =>
        {
            options.TokenValidationParameters =
                new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = "raggit-workstation",
                    ValidAudience = "raggit-workstation",
                    IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                        jwtKey
                    ),
                    ClockSkew = TimeSpan.FromMinutes(5),
                };

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var subClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier);
                    if (subClaim is null || !Guid.TryParse(subClaim.Value, out var userId))
                    {
                        context.Fail("Invalid subject claim.");
                        return;
                    }

                    var store = context.HttpContext.RequestServices.GetRequiredService<UserStore>();
                    try
                    {
                        var user = await store.GetByIdAsync(
                            userId,
                            context.HttpContext.RequestAborted
                        );
                        if (user is null || !user.IsActive || user.IsLockedOut)
                        {
                            context.Fail("Account is inactive or locked.");
                        }
                    }
                    catch
                    {
                        context.Fail("Identity store unavailable.");
                    }
                },
            };
        }
    );

builder.Services.AddAuthorization();

// Configuration
var vectorDbPath = builder.Configuration["VectorDb:Path"] ?? "./data/lancedb";
var vectorDbVectorSize = builder.Configuration.GetValue<int?>("VectorDb:VectorSize") ?? 384;
var ollamaUrl = builder.Configuration["Ollama:Url"] ?? "http://localhost:11434";
var embedModel = builder.Configuration["Ollama:EmbedModel"] ?? "all-minilm";
var chatModel = builder.Configuration["Ollama:ChatModel"] ?? "phi3:mini";
var ollamaTimeoutMs = builder.Configuration.GetValue<int?>("Ollama:TimeoutMs") ?? 5000;
Log.Information("Ollama timeout configured: {TimeoutMs}ms", ollamaTimeoutMs);
var connectionString =
    builder.Configuration.GetConnectionString("RagDb") ?? "Data Source=./data/rag.db";

// Backward compatibility: fall back to legacy Qdrant:Path if VectorDb:Path is missing.
if (
    !builder.Configuration.GetSection("VectorDb:Path").Exists()
    && !string.IsNullOrEmpty(builder.Configuration["Qdrant:Path"])
)
{
    vectorDbPath = builder.Configuration["Qdrant:Path"]!;
}

// Validation per T005 / FR-001
{
    var qdrantPath = builder.Configuration["Qdrant:Path"];
    var validation = RAGGit.Workstation.Api.Config.WorkstationConfigValidator.Validate(
        vectorDbVectorSize,
        embedModel,
        vectorDbPath,
        qdrantPath
    );
    if (!validation.IsValid)
    {
        throw new InvalidOperationException(validation.Error);
    }
    foreach (var w in validation.Warnings)
    {
        Log.Warning("Config warning: {Warning}", w);
    }
}

// Data
builder.Services.AddSingleton(new RagDbContext(connectionString));

// AI services
builder.Services.AddSingleton<IVectorStore>(
    new LanceDbLocalClient(vectorDbPath, vectorDbVectorSize)
);

if (cacheEnabled)
{
    builder.Services.AddSingleton<IEmbedder>(sp => new CachedEmbedder(
        new OllamaEmbedder(ollamaUrl, embedModel, ollamaTimeoutMs),
        sp.GetRequiredService<IMemoryCache>(),
        embedModel,
        embedCap,
        embedTtl
    ));
}
else
{
    builder.Services.AddSingleton<IEmbedder>(
        new OllamaEmbedder(ollamaUrl, embedModel, ollamaTimeoutMs)
    );
}

builder.Services.AddSingleton<ILlmClient>(
    new OllamaLlmClient(ollamaUrl, chatModel, ollamaTimeoutMs)
);
builder.Services.Configure<IngestOptions>(builder.Configuration.GetSection("Ingest"));
builder.Services.AddSingleton<RetrievalService>();
builder.Services.AddSingleton<GenerationService>();
builder.Services.AddSingleton<RAGGit.Ingest.IngestService>();
builder.Services.AddSingleton<IVirusScanner, NoOpVirusScanner>();

// API
builder
    .Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new RAGGit.Core.Models.DocumentMimeTypeConverter()
        );
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()
        );
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// RFC7807 problem details for consistent error shapes.
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance =
            $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
        if (
            context.HttpContext.Items.TryGetValue(RequestIdMiddleware.HeaderName, out var requestId)
        )
        {
            context.ProblemDetails.Extensions["requestId"] = requestId;
        }
    };
});

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

app.UseMiddleware<RequestIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseCors("Lan");
app.UseHttpsRedirection();
app.UseResponseCaching();
app.UseAuthentication();
app.UseAuthorization();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapControllers();

// Ensure SQLite schema exists.
await using (var db = app.Services.GetRequiredService<RagDbContext>())
{
    await db.EnsureCreatedAsync();
}

// Startup dimension guard (Q3 normative) — fail fast before accepting traffic per FR-001/SC-004.
{
    var vectorStore = app.Services.GetRequiredService<IVectorStore>();
    if (vectorStore is LanceDbLocalClient lance)
    {
        try
        {
            await lance.ValidateDimensionAsync(vectorDbVectorSize);
        }
        catch (DimensionMismatchException ex)
        {
            Log.Fatal(ex, "Startup dimension guard failed: {Message}", ex.Message);
            throw;
        }
    }
}

app.Run();

public partial class Program { }
