# Remove Query Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete the Auto/Broad/Specific mode from the WPF client, the HTTP contract, and persistence, while keeping automatic intent classification inside retrieval.

**Architecture:** The client stops choosing or sending a mode (Task 1), the server stops persisting the effective mode (Task 2), then the `QueryMode` type and every wire/pipeline field carrying it are deleted and `RetrievalService` collapses to one classifier-driven `RetrieveAsync` (Task 3). Each task leaves the tree compiling and its own tests green.

**Tech Stack:** .NET 10 (`net10.0`), ASP.NET Core, WPF + WPF-UI, SQLite (`Microsoft.Data.Sqlite`), xUnit + FluentAssertions, CSharpier 1.3.0.

**Spec:** `docs/superpowers/specs/2026-10-27-remove-query-mode-design.md`

## Global Constraints

- SDK pinned by `global.json` (10.0.401); do not change target frameworks or package versions.
- TDD is mandatory: write the red test before the implementation, then make it green.
- CSharpier formats C# **and** XAML: `dotnet csharpier format .` after touching `.xaml`.
- WPF client builds only on Windows: `dotnet build RAGGit.sln -c Release -p:Platform=x64`. Server-only: `dotnet build RAGGit.Server.slnf -c Release`.
- Do not add a `DROP COLUMN` migration. Fresh databases omit `Queries.Mode`; existing ones keep the orphan column and its values.
- Do not reject a legacy `mode` request field. `System.Text.Json` ignores it; the classifier decides.
- The rule-based `QueryIntentClassifier` stays and runs on every query. Broad retrieval must remain reachable.
- Client XAML is margin-free with property-element grid tracks; use `ui:*` controls only. Keep `AutomationProperties.AutomationId` on every remaining interactive element.

## Review Focus

1. A legacy client still posting `mode` (any value, including `banana`) gets a normal answer, never a 400 — covered by Task 3's `Post_LegacyModeField_IsIgnored`.
2. A broad-trigger question sent with no mode still retrieves parent chunks — covered by the existing `IntentRetrievalTests.BroadSynthesisQuery_ReturnsParentContextCoveringAdjacentChildren`, rerun in Task 3.
3. A fact question with no mode still cites a child chunk — covered by the existing `IntentRetrievalTests.GranularFactQuery_ReturnsExactFact_WithChildChunkCitation`, rerun in Task 3.
4. A pre-existing database that still has `Queries.Mode` boots and accepts new queries — covered by Task 2's `ExistingDatabaseWithModeColumn_StillAcceptsQueries`.
5. A fresh database has no `Queries.Mode` column — covered by Task 2's `QueriesTable_HasNoModeColumn`.

---

### Task 1: Client stops choosing and sending mode

**Files:**
- Modify: `src/RAGGit.Client.Core/Services/QueryApiClient.cs:32-50` and `:97-112`
- Modify: `src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs:36-42` and `:449`
- Modify: `src/RAGGit.Client.WPF/Views/Pages/QueryPage.xaml:8-9`, `:15-27`, `:44-100`
- Test: `tests/unit/QueryStreamClientTests.cs:152-177`
- Delete: `tests/unit/QueryModeClientTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces:
  - `public async Task<QueryResponse> QueryAsync(string query, int topK = 5, CancellationToken cancellationToken = default)` in `QueryApiClient`.
  - `public async IAsyncEnumerable<QueryStreamEvent> StreamAsync(string query, int topK = 5, [EnumeratorCancellation] CancellationToken cancellationToken = default)` in `QueryApiClient`.
  - `QueryViewModel` no longer has a `QueryMode` property.
  - `QueryPage.xaml` no longer references `RAGGit.Core.Models.QueryMode` or `System.Enum`.

- [ ] **Step 1: Write the failing client tests**

Replace `StreamAsync_PostsToTheStreamEndpoint_WithTheSelectedMode` in `tests/unit/QueryStreamClientTests.cs` with the endpoint/query test below, and add the API-shape test (`QueryRequest.Mode` still defaults to `Auto` until Task 3, so the body-absence assertion moves there):

```csharp
[Fact]
public async Task StreamAsync_PostsToTheStreamEndpoint()
{
    // ... existing capturing handler ...
    await CollectAsync(client.StreamAsync("renewal term"));

    uri!.AbsolutePath.Should().Be("/api/queries/stream");
    using var json = System.Text.Json.JsonDocument.Parse(body!);
    json.RootElement.GetProperty("query").GetString().Should().Be("renewal term");
}

[Fact]
public void ClientApi_NoLongerAcceptsOrExposesAMode()
{
    typeof(QueryApiClient)
        .GetMethod(nameof(QueryApiClient.StreamAsync))!
        .GetParameters()
        .Select(parameter => parameter.Name)
        .Should()
        .NotContain("mode");
    typeof(QueryApiClient)
        .GetMethod(nameof(QueryApiClient.QueryAsync))!
        .GetParameters()
        .Select(parameter => parameter.Name)
        .Should()
        .NotContain("mode");
    typeof(QueryViewModel).GetProperty("QueryMode").Should().BeNull();
}
```

Add `using System.Linq;` and `using RAGGit.Client.Core.ViewModels;`.

- [ ] **Step 2: Delete the mode-plumbing test file and run the new test to see it fail**

```bash
git rm tests/unit/QueryModeClientTests.cs
dotnet test tests/unit/RAGGit.Tests.Unit.csproj --filter "FullyQualifiedName~QueryStreamClientTests.ClientApi_NoLongerAcceptsOrExposesAMode"
```

Expected: FAIL — the client method still has a `mode` parameter / the ViewModel still has `QueryMode`.

- [ ] **Step 3: Drop the mode argument from `QueryApiClient`**

In both `QueryAsync` and `StreamAsync`, delete the `QueryMode mode = QueryMode.Auto` parameter, delete `Mode = mode,` from the `QueryRequest` initializer, and update the XML comments so they no longer mention a selected/override mode. Keep `_jsonOptions` unchanged (it still serializes `QueryStage`).

- [ ] **Step 4: Remove mode from the ViewModel**

Delete the `[ObservableProperty] private QueryMode _queryMode = QueryMode.Auto;` field and its doc comment. Change the stream call in `AskQuestionAsync` to:

```csharp
await foreach (var streamEvent in _apiClient.StreamAsync(question))
```

- [ ] **Step 5: Remove the selector from `QueryPage.xaml`**

- Delete `xmlns:sys="clr-namespace:System;assembly=System.Runtime"` (keep `xmlns:models`; it is still used by the `PersonSuggestion` template).
- Delete the `ObjectDataProvider x:Key="QueryModes"` resource and the `Page.Resources` entry's trailing comma structure.
- In the header card, delete the inner actions `Grid` (its `Mode` label, `ComboBox`, `QueryModeCombo` automation id, and the two 8px gutter columns) and place the existing Clear `ui:Button` directly in the header grid at `Grid.Column="1"` with `VerticalAlignment="Center"`, keeping `x:Name`, automation id, name, tooltip, content, icon, and `Click`.

- [ ] **Step 6: Format, build, and run the tests**

```bash
dotnet csharpier format src/RAGGit.Client.Core/Services/QueryApiClient.cs src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs src/RAGGit.Client.WPF/Views/Pages/QueryPage.xaml tests/unit/QueryStreamClientTests.cs
dotnet csharpier check src/RAGGit.Client.Core/Services/QueryApiClient.cs src/RAGGit.Client.Core/ViewModels/QueryViewModel.cs src/RAGGit.Client.WPF/Views/Pages/QueryPage.xaml tests/unit/QueryStreamClientTests.cs
dotnet test tests/unit/RAGGit.Tests.Unit.csproj --filter "FullyQualifiedName~QueryStreamClientTests"
dotnet build RAGGit.sln -c Release -p:Platform=x64
```

Expected: the filtered tests PASS and the WPF build succeeds.

- [ ] **Step 7: Commit**

```bash
git add src/RAGGit.Client.Core src/RAGGit.Client.WPF/Views/Pages/QueryPage.xaml tests/unit/QueryStreamClientTests.cs
git commit -m "refactor(client): remove query mode selector and request field"
```

---

### Task 2: Stop persisting the effective mode

**Files:**
- Modify: `src/RAGGit.Core/Models/Query.cs:30-35`
- Modify: `src/RAGGit.Core/Data/RagDbContext.cs:89-96` and `:208-219`
- Modify: `src/RAGGit.Core/Data/SqliteQueryRepository.cs:262-282`
- Modify: `src/RAGGit.Workstation.Api/Controllers/QueryController.cs:146-155`, `:269-293`, `:424-449`
- Test: `tests/unit/RagDbContextQueriesSchemaTests.cs` (create)
- Test: `tests/integration/IntentRetrievalTests.cs:186-192` and `:241-260`
- Test: `tests/integration/QueryStreamTests.cs:218-224` and `:350-378`

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces:
  - `Query` has no `Mode` property.
  - `Queries` fresh DDL has columns `Id, UserId, Prompt, RetrievedChunkIds, Answer, CitationIds, LatencyMs, CreatedAt` only.
  - `QueryController.SaveQueryAsync(string userId, string prompt, IReadOnlyList<Guid> retrievedChunkIds, string? answer, IReadOnlyList<Guid> citationIds, int latencyMs, CancellationToken cancellationToken)`.
  - `intent` from retrieval is still produced (used by the response until Task 3) but never saved.

- [ ] **Step 1: Write the failing schema test**

Create `tests/unit/RagDbContextQueriesSchemaTests.cs`, modelled on `RagDbContextUsersSchemaTests`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using RAGGit.Core.Data;
using Xunit;

namespace RAGGit.Tests.Unit;

/// <summary>
/// The Queries table no longer carries the removed query-mode audit column.
/// </summary>
public sealed class RagDbContextQueriesSchemaTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly RagDbContext _db;

    public RagDbContextQueriesSchemaTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"raggit-queries-{Guid.NewGuid()}.db");
        _db = new RagDbContext($"Data Source={_dbPath}");
    }

    public async Task InitializeAsync() => await _db.EnsureCreatedAsync();

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        try
        {
            File.Delete(_dbPath);
        }
        catch
        {
            // best effort
        }
    }

    [Fact]
    public async Task QueriesTable_HasNoModeColumn()
    {
        await using var connection = _db.CreateConnection();
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM pragma_table_info('Queries');";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        columns.Should().NotContain("Mode");
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

```bash
dotnet test tests/unit/RAGGit.Tests.Unit.csproj --filter "FullyQualifiedName~RagDbContextQueriesSchemaTests"
```

Expected: FAIL — the fresh table (and the `EnsureColumnAsync` upgrade) still adds `Mode`.

- [ ] **Step 3: Add the upgrade-compatibility test**

In the same test class, add this fact. It creates a legacy `Queries` table with the old `Mode` column before bootstrap, then proves new rows insert and read without it:

```csharp
[Fact]
public async Task ExistingDatabaseWithModeColumn_StillAcceptsQueries()
{
    var legacyDbPath = Path.Combine(Path.GetTempPath(), $"raggit-legacy-{Guid.NewGuid()}.db");
    await using (var legacy = new SqliteConnection($"Data Source={legacyDbPath}"))
    {
        await legacy.OpenAsync();
        using var create = legacy.CreateCommand();
        create.CommandText =
            @"
            CREATE TABLE Queries (
                Id TEXT PRIMARY KEY,
                UserId TEXT NOT NULL,
                Prompt TEXT NOT NULL,
                RetrievedChunkIds TEXT,
                Answer TEXT,
                CitationIds TEXT,
                LatencyMs INTEGER,
                CreatedAt TEXT NOT NULL,
                Mode TEXT
            );";
        await create.ExecuteNonQueryAsync();
    }

    try
    {
        var legacyDb = new RagDbContext($"Data Source={legacyDbPath}");
        await legacyDb.EnsureCreatedAsync();
        var repository = new SqliteQueryRepository(legacyDb);
        var query = new RAGGit.Core.Models.Query
        {
            Id = Guid.NewGuid(),
            UserId = "legacy-user",
            Prompt = "still works?",
            CreatedAt = DateTime.UtcNow,
        };

        await repository.AddAsync(query);

        var page = await repository.ListHistoryAsync("legacy-user", 10, 0);
        page.Items.Should().ContainSingle().Which.PromptPreview.Should().Be("still works?");
        await legacyDb.DisposeAsync();
    }
    finally
    {
        try
        {
            File.Delete(legacyDbPath);
        }
        catch
        {
            // best effort
        }
    }
}
```

Run the class and expect both facts to FAIL until Step 4 (the first on the missing removal, the second only if the insert path is broken).

- [ ] **Step 4: Remove the column from the entity, schema, and insert**

- `Query.cs`: delete the `Mode` property and its doc comment.
- `RagDbContext.cs`: delete the `EnsureColumnAsync(..., "Queries", "Mode", "TEXT", ...)` block and remove `Mode TEXT` from the `Queries` DDL (drop the trailing comma on `CreatedAt`).
- `SqliteQueryRepository.cs`: remove `, Mode` from the column list, `, @mode` from the values, and the `@mode` parameter assignment.
- `QueryController.cs`: remove the `QueryIntent? mode` parameter and `Mode = mode,` from `SaveQueryAsync`; remove the `outcome.Mode,` argument at both call sites. Leave `QueryOutcome.Mode` in place for Task 3.

- [ ] **Step 5: Update the two SQL row readers**

- `IntentRetrievalTests.LoadLatestQueryRowAsync`: select only `RetrievedChunkIds, CitationIds`; return the two-element tuple; delete the `mode.Should().Be("Broad");` assertion (and the comment above it).
- `QueryStreamTests.LoadLatestQueryRowAsync`: select `Prompt, Answer, RetrievedChunkIds, CitationIds`; drop `Mode` from the tuple and the `reader.IsDBNull(4)` line; delete `row.Mode!.ToLowerInvariant().Should().Be("granular");`.

- [ ] **Step 6: Format, build, and run the tests**

```bash
dotnet csharpier format src/RAGGit.Core/Models/Query.cs src/RAGGit.Core/Data/RagDbContext.cs src/RAGGit.Core/Data/SqliteQueryRepository.cs src/RAGGit.Workstation.Api/Controllers/QueryController.cs tests/unit/RagDbContextQueriesSchemaTests.cs tests/integration/IntentRetrievalTests.cs tests/integration/QueryStreamTests.cs
dotnet test tests/unit/RAGGit.Tests.Unit.csproj --filter "FullyQualifiedName~RagDbContextQueriesSchemaTests"
dotnet test tests/integration/RAGGit.Tests.Integration.csproj --filter "FullyQualifiedName~IntentRetrievalTests|FullyQualifiedName~QueryStreamTests"
dotnet build RAGGit.Server.slnf -c Release
```

Expected: all PASS and the server build succeeds.

- [ ] **Step 7: Commit**

```bash
git add src/RAGGit.Core src/RAGGit.Workstation.Api tests/unit/RagDbContextQueriesSchemaTests.cs tests/integration/IntentRetrievalTests.cs tests/integration/QueryStreamTests.cs
git commit -m "refactor(db): stop persisting query mode"
```

---

### Task 3: Remove mode from the HTTP contract and retrieval

**Files:**
- Modify: `src/RAGGit.Core/Models/QueryIntent.cs` (delete `QueryMode`)
- Modify: `src/RAGGit.Core/Models/QueryStage.cs:8` (comment references the old convention)
- Modify: `src/RAGGit.Core/Models/QueryRequest.cs:19-25`
- Modify: `src/RAGGit.Core/Models/QueryRequest.cs` (`QueryResponse.Mode`, lines 58-63)
- Modify: `src/RAGGit.Core/Models/QueryStreamEvent.cs:55-56`, `:105`, `:140`
- Modify: `src/RAGGit.Retrieval/QueryOutcome.cs:35-43`, `:53`
- Modify: `src/RAGGit.Retrieval/RetrievalService.cs:55-123`
- Modify: `src/RAGGit.Retrieval/QueryPipeline.cs:85-90`, `:100-106`, `:166-176`
- Modify: `tests/contract/QueryModeContractTests.cs` → `tests/contract/QueryModeRemovalContractTests.cs`
- Modify: `tests/contract/QueryStreamContractTests.cs:103-108`
- Modify: `tests/integration/QueryStreamTests.cs:205-224`, `:264-272`
- Modify: `tests/integration/IntentRetrievalTests.cs:53-57`, `:125-129`, `:153-178`
- Modify: `tests/integration/IntentGroundingTests.cs:20-64`, `:66-104`
- Modify: `tests/integration/EvalTests.cs:321`, `:428`
- Modify: `tests/unit/RetrievalRoutingTests.cs`
- Modify: `tests/unit/QueryPipelineTests.cs:132-140`
- Modify: `tests/unit/QueryStreamEventTests.cs:128`, `:136`, `:158`
- Modify: `tests/unit/QueryStreamClientTests.cs` (add the body-absence assertion now that the field is gone)
- Modify: `AGENTS.md` (Two-level chunks bullet)

**Interfaces:**
- Consumes: Task 1's client signatures; Task 2's `Query`/schema/`SaveQueryAsync` without mode.
- Produces:
  - `QueryMode` type deleted; `QueryIntent` (`Granular`/`Broad`) remains.
  - `QueryRequest` has `Query`, `TopK` only.
  - `QueryResponse` has `Answer`, `Citations`, `RetrievedChunkIds`, `LatencyMs`, `SuggestedPersons` only.
  - `QueryStreamEvent` has no `Mode` property.
  - `QueryOutcome` has `Answer`, `Citations`, `CitationIds`, `RetrievedChunkIds`, `LatencyMs`, `SuggestedPersons` only.
  - `public async Task<IReadOnlyList<SearchResult>> RetrieveAsync(string query, int topK = 5, CancellationToken cancellationToken = default)` is the only retrieval entry point; it classifies internally and branches to broad or granular.

- [ ] **Step 1: Rewrite the mode contract test as a removal contract**

```bash
git mv tests/contract/QueryModeContractTests.cs tests/contract/QueryModeRemovalContractTests.cs
```

Replace the class body with two facts (keep the existing constructor, `_jsonOptions`, `SeedRefundChunk`, and fake accessors):

```csharp
[Fact]
public async Task Post_Response_HasNoMode()
{
    SeedRefundChunk(out _);
    var llm = GetLlmClient();
    llm.Healthy = true;
    llm.ResponseText = "Customers can return items within 30 days.";

    var response = await _client.PostAsJsonAsync(
        "/api/queries",
        new { query = "What is the renewal term?" },
        _jsonOptions
    );

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
    json.TryGetProperty("mode", out _).Should().BeFalse();
    json.GetProperty("answer").GetString().Should().NotBeNullOrWhiteSpace();
}

[Fact]
public async Task Post_LegacyModeField_IsIgnored()
{
    SeedRefundChunk(out _);
    var llm = GetLlmClient();
    llm.Healthy = true;
    llm.ResponseText = "Customers can return items within 30 days.";

    var response = await _client.PostAsJsonAsync(
        "/api/queries",
        new { query = "refund policy", mode = "banana" },
        _jsonOptions
    );

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var json = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
    json.GetProperty("answer").GetString().Should().NotBeNullOrWhiteSpace();
}
```

Rename the class to `QueryModeRemovalContractTests` and update the summary comment.

- [ ] **Step 2: Run it to verify it fails**

```bash
dotnet test tests/contract/RAGGit.Tests.Contract.csproj --filter "FullyQualifiedName~QueryModeRemovalContractTests"
```

Expected: FAIL — the first fact finds a `mode` property; the second gets 400 for `banana`.

- [ ] **Step 3: Delete the wire and pipeline fields**

- `QueryIntent.cs`: delete the `QueryMode` enum and its comment; keep `QueryIntent`.
- `QueryStage.cs`: reword the summary comment that cites the `QueryMode`/`QueryIntent` wire-value convention so it cites `QueryIntent` only.
- `QueryRequest.cs`: delete `Mode`; delete `QueryResponse.Mode` and its comment.
- `QueryStreamEvent.cs`: delete the `Mode` property, `Mode = response.Mode,` in `FromAnswer`, and `Mode = Mode,` in `ToResponse`.
- `QueryOutcome.cs`: delete `Mode`, `RequestedMode`, and `Mode = Mode,` in `ToResponse`.
- `QueryPipeline.cs`: replace the tuple call with `var chunks = await _retrievalService.RetrieveAsync(query, Math.Clamp(request.TopK, 1, 5), cancellationToken);` and remove `Mode = intent` / `RequestedMode = request.Mode` from both outcome constructions.

- [ ] **Step 4: Collapse `RetrievalService` to one classifier-driven method**

Replace lines 55-123 with one public method:

```csharp
public async Task<IReadOnlyList<SearchResult>> RetrieveAsync(
    string query,
    int topK = 5,
    CancellationToken cancellationToken = default
)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(query);

    return QueryIntentClassifier.Classify(query) == QueryIntent.Broad
        ? await RetrieveBroadAsync(query, topK, cancellationToken)
        : await RetrieveGranularAsync(query, topK, cancellationToken);
}
```

Delete `ResolveIntent`, the `QueryMode` overload, and `RetrieveWithIntentAsync`. Update the class/XML comments that mention overrides.

- [ ] **Step 5: Update every affected test**

- `QueryStreamContractTests`: delete the `streamed.Mode.Should().NotBeNull();` / `classicJson.GetProperty("mode")` block.
- `QueryStreamTests`: remove `mode = "specific"` from the request; delete the `row.Mode` assertion; delete the `streamed.Mode` parity block.
- `IntentRetrievalTests`: remove `mode = "auto"` / `mode = "broad"` from the three payloads and the `json.GetProperty("mode")` assertion; rename `BroadAnswer_PersistsParentCitationIds_AndEchoesBroad` to `BroadAnswer_PersistsParentCitationIds`.
- `IntentGroundingTests`: collapse the four-row theory to one `[Fact] NoRelevantContent_ReturnsExactMessage_ZeroCitations()` with `new { query = "unrelated topic quasar" }`; delete the `mode` payload branch and the final `mode` assertion; update the summary comment.
- `EvalTests`: delete both `json.GetProperty("mode")` assertions.
- `RetrievalRoutingTests`: drop the `QueryMode` argument from every `RetrieveAsync` call; delete `Retrieve_SpecificForcesGranular_EvenForBroadText` (the override no longer exists); update the file summary comment. The remaining broad cases already use trigger phrases (`Summarize…`, `Give me an overview`, `Compare the plans`).
- `QueryPipelineTests`: construct `new QueryRequest { Query = "q" }`; delete the `outcome.Mode` and `outcome.RequestedMode` assertions.
- `QueryStreamEventTests`: delete `Mode = QueryIntent.Broad,` and the `roundTripped.Mode` assertion in the round-trip fact; construct the classic response as `new QueryResponse { Answer = "a" }`.
- `QueryStreamClientTests.StreamAsync_PostsToTheStreamEndpoint`: add `json.RootElement.TryGetProperty("mode", out _).Should().BeFalse();` — the shared request model no longer serializes it.

- [ ] **Step 6: Update the documented behavior**

In `AGENTS.md`, in the **Two-level chunks (030)** bullet, replace the sentence describing `mode: auto|broad|specific` and the echoed effective mode with: retrieval granularity is classified automatically per question (broad trigger phrases, otherwise granular); there is no client or API mode switch and no persisted mode.

- [ ] **Step 7: Format, build, and run all affected suites**

```bash
dotnet csharpier format src/RAGGit.Core/Models src/RAGGit.Retrieval src/RAGGit.Workstation.Api/Controllers/QueryController.cs tests/contract tests/unit/RetrievalRoutingTests.cs tests/unit/QueryPipelineTests.cs tests/unit/QueryStreamEventTests.cs tests/integration/IntentRetrievalTests.cs tests/integration/IntentGroundingTests.cs tests/integration/QueryStreamTests.cs tests/integration/EvalTests.cs
dotnet csharpier check .
dotnet test tests/contract/RAGGit.Tests.Contract.csproj --filter "FullyQualifiedName~QueryModeRemovalContractTests|FullyQualifiedName~QueryStreamContractTests|FullyQualifiedName~QueryContractTests"
dotnet test tests/unit/RAGGit.Tests.Unit.csproj --filter "FullyQualifiedName~RetrievalRoutingTests|FullyQualifiedName~QueryPipelineTests|FullyQualifiedName~QueryStreamClientTests|FullyQualifiedName~QueryStreamEventTests"
dotnet test tests/integration/RAGGit.Tests.Integration.csproj --filter "FullyQualifiedName~IntentRetrievalTests|FullyQualifiedName~IntentGroundingTests|FullyQualifiedName~QueryStreamTests|FullyQualifiedName~EvalTests"
dotnet build RAGGit.Server.slnf -c Release
dotnet build RAGGit.sln -c Release -p:Platform=x64
```

Expected: every command succeeds; the legacy `mode:"banana"` request returns 200; broad and granular retrieval tests still pass without a mode field.

- [ ] **Step 8: Commit**

```bash
git add AGENTS.md src/RAGGit.Core src/RAGGit.Retrieval src/RAGGit.Workstation.Api tests/contract tests/unit tests/integration
git commit -m "refactor(api): remove query mode from contract and retrieval"
```
