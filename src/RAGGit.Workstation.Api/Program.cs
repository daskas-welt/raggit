using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Memory;
using RAGGit.Core.Abstractions;
using RAGGit.Core.Abstractions.Repositories;
using RAGGit.Core.Data;
using RAGGit.Core.Models;
using RAGGit.Ingest;
using RAGGit.Ingest.Ai;
using RAGGit.Ingest.Vector;
using RAGGit.Retrieval;
using RAGGit.Retrieval.Ai;
using RAGGit.Workstation.Api;
using RAGGit.Workstation.Api.Auth;
using RAGGit.Workstation.Api.Cli;
using RAGGit.Workstation.Api.Config;
using RAGGit.Workstation.Api.Middleware;
using Serilog;

// -----------------------------------------------------------------------------
// Operator CLI
// -----------------------------------------------------------------------------

// Intercept before the web host is built so provisioning can run offline with no
// plaintext secret in configuration.
if (args.Length > 0 && string.Equals(args[0], "user", StringComparison.OrdinalIgnoreCase))
{
    var cliConnectionString = BuildCliConnectionString(args);

    var dataDir =
        Path.GetDirectoryName(cliConnectionString.Replace("Data Source=", "")) ?? "./data";
    Directory.CreateDirectory(dataDir);

    await using var cliDb = new RagDbContext(cliConnectionString);
    await cliDb.EnsureCreatedAsync();

    IUserRepository cliStore = new SqliteUserRepository(cliDb);
    var cli = new OperatorCli();
    var exitCode = await cli.RunAsync(FilterOperatorArgs(args), cliStore);
    Environment.Exit(exitCode);
}

// -----------------------------------------------------------------------------
// Host and logging
// -----------------------------------------------------------------------------

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

// -----------------------------------------------------------------------------
// Cache
// -----------------------------------------------------------------------------

var cacheEnabled = builder.Configuration.GetValue<bool>("Cache:Enabled");
var embedCap = builder.Configuration.GetValue<int?>("Cache:EmbedCap") ?? 10000;
var embedTtl = builder.Configuration.GetValue<int?>("Cache:EmbedTTLHours") ?? 24;

builder.Services.AddMemoryCache(options => options.SizeLimit = embedCap);
builder.Services.AddResponseCaching();

// -----------------------------------------------------------------------------
// CORS
// -----------------------------------------------------------------------------

// Allow any LAN origin; auth is header-based so credentials are not required.
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

// -----------------------------------------------------------------------------
// Authentication and authorization
// -----------------------------------------------------------------------------

var jwtSigningKeyPath = builder.Configuration["Auth:JwtSigningKeyPath"] ?? "./data/auth.key";
var tokenLifetimeHours = builder.Configuration.GetValue<int?>("Auth:TokenLifetimeHours") ?? 8;
var pbkdf2Iterations = builder.Configuration.GetValue<int?>("Auth:Pbkdf2Iterations") ?? 310_000;
var lockoutThreshold = builder.Configuration.GetValue<int?>("Auth:LockoutThreshold") ?? 5;
var lockoutMinutes = builder.Configuration.GetValue<int?>("Auth:LockoutMinutes") ?? 15;

var jwtKey = AuthKeyLoader.LoadOrCreateKey(jwtSigningKeyPath);

builder.Services.AddSingleton<IUserRepository, SqliteUserRepository>();
builder.Services.Configure<JwtTokenServiceOptions>(options =>
{
    options.SigningKey = jwtKey;
    options.TokenLifetimeHours = tokenLifetimeHours;
});
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.Configure<LockoutPolicyOptions>(options =>
{
    options.Threshold = lockoutThreshold;
    options.Minutes = lockoutMinutes;
});
builder.Services.AddSingleton<LockoutPolicy>();

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
        options => ConfigureJwtBearer(options, jwtKey)
    );

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(ApiKeyAuthOptions.Scheme, JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
});

// -----------------------------------------------------------------------------
// Configuration and validation
// -----------------------------------------------------------------------------

var vectorDbPath = builder.Configuration["VectorDb:Path"] ?? "./data/lancedb";
var vectorDbVectorSize = builder.Configuration.GetValue<int?>("VectorDb:VectorSize") ?? 384;
var ollamaUrl = builder.Configuration["Ollama:Url"] ?? "http://localhost:11434";
var embedModel = builder.Configuration["Ollama:EmbedModel"] ?? "all-minilm";
var chatModel = builder.Configuration["Ollama:ChatModel"] ?? "phi3:mini";
var ollamaTimeoutMs = builder.Configuration.GetValue<int?>("Ollama:TimeoutMs") ?? 5000;
Log.Information("Ollama timeout configured: {TimeoutMs}ms", ollamaTimeoutMs);
var connectionString =
    builder.Configuration.GetConnectionString("RagDb") ?? "Data Source=./data/rag.db";

ValidateConfiguration(vectorDbVectorSize, embedModel, vectorDbPath);

// -----------------------------------------------------------------------------
// Data and persistence
// -----------------------------------------------------------------------------

// RagDbContext is the Unit of Work / connection factory + schema bootstrap only.
// All aggregate persistence goes through the repositories below (one repository
// per aggregate root per the MS persistence-layer design).
builder.Services.AddSingleton(new RagDbContext(connectionString));
builder.Services.AddSingleton<IQueryRepository, SqliteQueryRepository>();
builder.Services.AddSingleton<IDocumentRepository, SqliteDocumentRepository>();
var contentDir = Path.Combine(
    Path.GetDirectoryName(connectionString.Replace("Data Source=", "")) ?? "./data",
    "documents"
);
builder.Services.AddSingleton<IDocumentContentStore>(new FileDocumentContentStore(contentDir));

// -----------------------------------------------------------------------------
// AI services
// -----------------------------------------------------------------------------

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

builder.Services.AddSingleton<ILlmClient>(sp => new OllamaLlmClient(
    ollamaUrl,
    chatModel,
    ollamaTimeoutMs,
    sp.GetRequiredService<ILogger<OllamaLlmClient>>()
));
builder.Services.Configure<IngestOptions>(builder.Configuration.GetSection("Ingest"));
builder.Services.Configure<RetrievalOptions>(builder.Configuration.GetSection("Retrieval"));
builder.Services.Configure<GenerationOptions>(builder.Configuration.GetSection("Generation"));
builder.Services.AddSingleton<IngestWorkQueue>();
builder.Services.AddHostedService<IngestWorker>();
builder.Services.AddHostedService<LanceDbMaintenanceWorker>();
builder.Services.AddHostedService<ReindexBackfillService>();
builder.Services.AddSingleton<RetrievalService>();
builder.Services.AddSingleton<GenerationService>();
builder.Services.AddSingleton<IngestService>();
builder.Services.AddSingleton<IVirusScanner, NoOpVirusScanner>();

// -----------------------------------------------------------------------------
// HTTP API
// -----------------------------------------------------------------------------

builder
    .Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new DocumentMimeTypeConverter());
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()
        );
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "RAGGit.Workstation.Api",
            Version = "v1",
        }
    );
});

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

// -----------------------------------------------------------------------------
// Application pipeline
// -----------------------------------------------------------------------------

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

// -----------------------------------------------------------------------------
// Startup tasks
// -----------------------------------------------------------------------------

// Ensure SQLite schema exists.
await EnsureSchemaAsync(app);

// Startup dimension guard (Q3 normative) — fail fast before accepting traffic per FR-001/SC-004.
await EnsureVectorDimensionsAsync(app, vectorDbVectorSize);

app.Run();

// -----------------------------------------------------------------------------
// Local helpers
// -----------------------------------------------------------------------------

static string BuildCliConnectionString(string[] args) =>
    new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
        .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
        .AddEnvironmentVariables()
        .AddCommandLine(args)
        .Build()
        .GetConnectionString("RagDb")
    ?? "Data Source=./data/rag.db";

static string[] FilterOperatorArgs(string[] args)
{
    var result = new List<string>(args.Length);
    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (
            arg.StartsWith("--connectionstrings:", StringComparison.OrdinalIgnoreCase)
            || arg.StartsWith("--ConnectionStrings:", StringComparison.OrdinalIgnoreCase)
        )
        {
            // If the value was supplied as a separate token, skip it too.
            if (!arg.Contains('=') && i + 1 < args.Length)
            {
                i++;
            }
            continue;
        }
        result.Add(arg);
    }
    return result.ToArray();
}

static void ValidateConfiguration(int vectorSize, string embedModel, string? vectorDbPath)
{
    // Validation per T005 / FR-001.
    var validation = WorkstationConfigValidator.Validate(vectorSize, embedModel, vectorDbPath);
    if (!validation.IsValid)
    {
        throw new InvalidOperationException(validation.Error);
    }
    foreach (var w in validation.Warnings)
    {
        Log.Warning("Config warning: {Warning}", w);
    }
}

static void ConfigureJwtBearer(JwtBearerOptions options, byte[] signingKey)
{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = "raggit-workstation",
        ValidAudience = "raggit-workstation",
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(signingKey),
        ClockSkew = TimeSpan.FromMinutes(5),
    };

    options.Events = new JwtBearerEvents
    {
        OnChallenge = context =>
        {
            // Suppress the default JWT challenge write so the default ApiKey handler
            // can produce the single 401 JSON body for missing/invalid credentials.
            context.HandleResponse();
            return Task.CompletedTask;
        },
        OnTokenValidated = HandleTokenValidated,
    };
}

static async Task HandleTokenValidated(TokenValidatedContext context)
{
    var subClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier);
    if (subClaim is null || !Guid.TryParse(subClaim.Value, out var userId))
    {
        context.Fail("Invalid subject claim.");
        return;
    }

    var store = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
    try
    {
        var user = await store.GetByIdAsync(userId, context.HttpContext.RequestAborted);
        if (user is null || !user.IsActive || user.IsLockedOut)
        {
            context.Fail("Account is inactive or locked.");
            return;
        }

        // Refresh mutable claims from the DB so role/displayName changes take
        // effect on the very next request (FR-005, SC-006).
        var identity = context.Principal?.Identities.FirstOrDefault();
        if (identity is not null)
        {
            var roleClaim = identity.FindFirst(ClaimTypes.Role);
            if (roleClaim is not null)
            {
                identity.RemoveClaim(roleClaim);
                identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));
            }

            var displayNameClaim = identity.FindFirst("displayName");
            if (displayNameClaim is not null)
            {
                identity.RemoveClaim(displayNameClaim);
                identity.AddClaim(new Claim("displayName", user.DisplayName));
            }

            var usernameClaim = identity.FindFirst("username");
            if (usernameClaim is not null)
            {
                identity.RemoveClaim(usernameClaim);
                identity.AddClaim(new Claim("username", user.Username));
            }
        }
    }
    catch
    {
        context.Fail("Identity store unavailable.");
    }
}

static async Task EnsureSchemaAsync(WebApplication app)
{
    await using var db = app.Services.GetRequiredService<RagDbContext>();
    await db.EnsureCreatedAsync();
}

static async Task EnsureVectorDimensionsAsync(WebApplication app, int vectorSize)
{
    var vectorStore = app.Services.GetRequiredService<IVectorStore>();
    if (vectorStore is not LanceDbLocalClient lance)
    {
        return;
    }

    try
    {
        await lance.ValidateDimensionAsync(vectorSize);
    }
    catch (DimensionMismatchException ex)
    {
        Log.Fatal(ex, "Startup dimension guard failed: {Message}", ex.Message);
        throw;
    }
}

public partial class Program { }
