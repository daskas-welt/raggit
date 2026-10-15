# Quickstart Validation: Reasoning Progress While Waiting

**Feature**: `032-reasoning-progress` | **Date**: 2026-10-15 | **Spec**: [spec.md](spec.md)

Runnable checks that prove the feature end-to-end. Event shapes live in
[contracts/api.yaml](contracts/api.yaml); entities and the reasoning lifecycle live in
[data-model.md](data-model.md); UI clauses live in [contracts/ui-contracts.md](contracts/ui-contracts.md).
This guide does not restate them.

## Prerequisites

- Windows, .NET SDK pinned by `global.json` (10.0.401). `dotnet tool restore` once.
- Ollama running on `http://localhost:11434` with the configured embed + chat models pulled
  (`Ollama:EmbedModel` `snowflake-arctic-embed2`, `Ollama:ChatModel` `qwen2.5:3b` in dev).
- A local person account for the signed-in checks
  (`user add --username me --display-name Me --role Admin --password 'Passw0rd!'`, run **from**
  `src/RAGGit.Workstation.Api` because the SQLite path is CWD-relative).
- A small non-empty library (upload 2–3 documents through the client, or reuse an existing
  `./data/lancedb`).
- For the UI checks: the client's output `appsettings.json` must carry **both** `Workstation:Url` and
  `Workstation:ApiKey` (a config with no key is treated as invalid and sign-in fails).

> Note on identity: `X-Api-Key` requests authenticate as `admin`/`employee` and have **no `sub` claim**, so
> they can exercise the stream but cannot read `/api/queries/history`. Use a signed-in person session for
> the history assertions (V6/V7).

## 1. Automated gates

```powershell
# Formatting (CSharpier formats XAML too — run it after touching .xaml)
dotnet tool restore
dotnet csharpier check .

# Unit + contract (no Ollama needed)
dotnet test tests/unit/RAGGit.Tests.Unit.csproj -c Release
dotnet test tests/contract/RAGGit.Tests.Contract.csproj -c Release

# Integration (RequiresOllama tests skip when Ollama is absent; plain `dotnet test` must stay green)
dotnet test tests/integration/RAGGit.Tests.Integration.csproj -c Release

# Offline invariant gate — must remain green (FR-011, SC-008)
dotnet test tests/integration/RAGGit.Tests.Integration.csproj -c Release `
  --filter "QueryOfflineTests|OfflineIdentityTests|OfflineHistoryTests|CrossUserIsolationTests"

# Full solution incl. the WPF client (Windows only)
dotnet build RAGGit.sln -c Release -p:Platform=x64
```

Add `-c Release --no-build` for repeat runs; a running API or open Visual Studio locks `bin/Debug` DLLs.

**Expected**: all green. The new unit tests assert stage order and the honesty rules (no `sources` when
nothing was retrieved, no line exposing prompt/chunk text), progress-text language, NDJSON parsing of
partial/split lines, and the `QueryViewModel` reasoning lifecycle (populated → cleared on answer / on
error / never repopulated from history). The new contract tests assert `application/x-ndjson`, event
ordering, `400`/`401`/`403`/`503` before the first byte, and **parity**: for the same fakes, the
`answer` event equals the `POST /api/queries` `200` body modulo `latencyMs`.

## 2. Raw stream inspection (server only)

Start the API (http on `5142`, https on `5001`) and watch the events arrive line by line — this is the
fastest way to confirm events are *flushed live* rather than buffered to the end.

```powershell
$key = (Get-Content src/RAGGit.Workstation.Api/appsettings.Development.json | ConvertFrom-Json).Api.EmployeeKey
# or set Api:EmployeeKey yourself and start the API with it

$body = '{"query":"What is the renewal term?"}'
Invoke-WebRequest -Uri https://localhost:5001/api/queries/stream -Method Post `
  -Headers @{ 'X-Api-Key' = $key } -ContentType 'application/json' -Body $body |
  Select-Object -ExpandProperty Content
```

For line-by-line timing, read the response stream instead of buffering it:

```powershell
$req = [System.Net.Http.HttpRequestMessage]::new('Post','https://localhost:5001/api/queries/stream')
$req.Headers.Add('X-Api-Key', $key)
$req.Content = [System.Net.Http.StringContent]::new($body, [System.Text.Encoding]::UTF8, 'application/json')
$http = [System.Net.Http.HttpClient]::new([System.Net.Http.HttpClientHandler]::new())
$resp = $http.Send($req, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead)
$reader = [System.IO.StreamReader]::new($resp.Content.ReadAsStream())
while (($line = $reader.ReadLine()) -ne $null) { "[{0:HH:mm:ss.fff}] {1}" -f (Get-Date), $line }
```

**Expected**
- `Content-Type: application/x-ndjson`; every line is a self-contained JSON object.
- Ordering: `progress(understanding)` → `progress(searching)` → optional `progress(sources)` →
  `progress(composing)` → `answer` (or a terminal `error`).
- Timestamps move apart in step with the work — not all at the end (SC-001/SC-002).
- `progress(sources)` appears only when the library actually contains a match, and its `sourceCount` /
  `documentNames` agree with the `answer` event's citations.
- No line contains prompt text, chunk bodies, or internal identifiers (FR-007).
- A question with no match produces **no** `sources` line and an `answer` of exactly
  `no relevant content found` (US2.2, US3.2).

## 3. Signed-in end-to-end (client)

1. Start the API, then start `src/RAGGit.Client.WPF` (Release output) with a valid
   `Workstation:Url` + `Workstation:ApiKey`, and sign in as the local account.
2. Ask a question whose answer takes several seconds.

**Expected (US1, SC-001…SC-004)**
- Reasoning lines appear within ~1 s of submitting and **change at least twice** before the answer lands.
- When the answer arrives, the reasoning region is gone and the answer bubble with its **Sources**
  expander is exactly as before (US1.3, US3).
- Submitting the same question again while waiting is not possible (existing busy rules), and the user
  has no reason to re-submit (SC-003).

3. Ask a question in Greek (e.g. `Ποιος είναι ο χρόνος ανανέωσης;`).

**Expected**: every reasoning line is Greek, matching the eventual answer's language (FR-006, US2.1).

4. Ask a question that matches nothing in the library.

**Expected**: no "sources found" line; the answer is exactly `no relevant content found` (US2.2).

5. With a question in flight, stop Ollama (or point `Ollama:Url` at a dead port) and ask again.

**Expected**: the same failure message as today (`model unavailable offline` / `AI workstation
unavailable: …`), and **no** lingering reasoning line (FR-012, US2.5). Doing this *mid-answer* exercises
the in-band `error` event; doing it *before* the first byte exercises the ordinary `503`.

6. Navigate away from Ask (and back) while an answer is in flight.

**Expected**: no stale reasoning reappears; the in-flight question is abandoned the same way it is today
(US1.4, R10).

7. Re-open the finished exchange from the conversation and from History → detail.

**Expected**: identical prompt, answer, citations, and mode to today; **no** reasoning text anywhere
(FR-010, FR-014, US3.4). The question still appears in history (the streaming path persists it).

## 4. UI automation checks

Drive the app with `winapp ui` (see `.opencode/skills/winui-ui-testing`) against a signed-in client and a
deliberately slow answer.

**Expected**
- `ReasoningProgressList` is present and non-empty while the question is in flight.
- After the answer arrives, `ReasoningProgressList` is absent/empty while the assistant bubble and its
  `SourcesExpander_*` are present.
- All pre-existing automation IDs from 022/023 (`ChatInputBox`, `SendQuestionButton`, `CopyMessageButton`,
  `QueryModeCombo`, `QueryClearConversationButton`, …) still resolve.

## 5. Failure triage

- **Nothing streams (the whole response arrives at once)**: a buffering layer is swallowing the per-event
  flush — check `Response.Body.FlushAsync` is awaited per event and that no caching/compression policy
  applies to the route.
- **A page "won't open" / no reasoning appears at all**: read `%LOCALAPPDATA%\RAGGit\crash.log` first
  (page-construction XAML errors are swallowed by `NavigationView`).
- **Reasoning lines render with the wrong key or blank text**: an unknown `ui:ThemeResource` key or a
  `SymbolRegular` member that does not exist fails at load, not at compile.
- **Stream truncated with no terminal event**: re-run against a healthy Ollama; the client is required to
  treat a missing terminal event as a failure (R10), not as an empty answer.
