# Active Tasks: RAGGit Offline-Mode Single-Tenant RAG Library

- [ ] T005 [P] Initialize `tests/unit`, `tests/contract`, `tests/integration` (xUnit) with `FluentAssertions`, `Microsoft.AspNetCore.Mvc.Testing`
- [ ] T006 [P] Configure `Directory.Build.props`, `editorconfig`, `dotnet format`, `.gitignore` (`/data/`, `/models/*.gguf`, `/models/*.onnx`)
- [ ] T007 Setup SQLite `rag.db` schema and migrations for `Documents`, `Chunks`, `Queries`, `Library(singleton)` per data-model.md
- [ ] T008 [P] Add `Qdrant.Client` package ref to `src/RAGGit.Ingest` and implement Qdrant embedded wrapper `src/RAGGit.Ingest/Vector/QdrantLocalClient.cs`
- [ ] T009 [P] Add `OllamaSharp` / `LLamaSharp` / `Microsoft.ML.OnnxRuntime` package refs to `src/RAGGit.Ingest` and `src/RAGGit.Retrieval`; implement `OllamaEmbedder.cs`, `OnnxEmbedder.cs`, `OllamaLlmClient.cs`
- [ ] T010 [P] Implement auth/RBAC `src/RAGGit.Workstation.Api/Auth/ApiKeyAuthHandler.cs` — `X-Api-Key` → `Admin` vs `Employee`, `AllowAnonymous` for `/health`
- [ ] T011 Setup API routing and middleware in `src/RAGGit.Workstation.Api/Program.cs`
- [ ] T012 Create base models `src/RAGGit.Core/Models/Document.cs`, `Chunk.cs`, `Query.cs`, `Library.cs` with validation
- [ ] T013 Configure env secrets (`dotnet user-secrets` `Api:Key`, `Onnx:EmbeddingModelPath`) and health endpoint `GET /health`
- [ ] T014 [P] [US1] Contract test `POST /api/documents` 201 + `GET /api/documents` 200
- [ ] T015 [P] [US1] Integration test `Upload → Index → Searchable <5min`
- [ ] T016 [P] [US1] Unit test chunking `512/50` and hash SHA-256 dedupe
- [ ] T017 [P] [US1] Implement chunking `src/RAGGit.Ingest/Chunker.cs`
- [ ] T018 [US1] Implement ingest service `src/RAGGit.Ingest/IngestService.cs`
- [ ] T019 [US1] Implement `POST /api/documents` + `GET /api/documents` in `DocumentsController.cs`
- [ ] T020 [US1] Implement client `LibraryView` + `UploadView` in `src/RAGGit.Client.Maui/Views/`
- [ ] T021 [US1] Add validation, logging, and `DELETE` purge stub
- [ ] T022 [P] [US2] Contract test `POST /api/query` 200 `{answer,citations}` and `NoRelevantContent` branch
- [ ] T023 [P] [US2] Integration test `WAN-disabled query`
- [ ] T024 [P] [US2] Eval harness 50 Q/A set
- [ ] T025 [P] [US2] Implement retrieval `src/RAGGit.Retrieval/RetrievalService.cs`
- [ ] T026 [US2] Implement generation `src/RAGGit.Retrieval/GenerationService.cs`
- [ ] T027 [US2] Implement `POST /api/query` in `QueryController.cs`
- [ ] T028 [US2] Implement client `QueryView.xaml`
- [ ] T029 [US2] Instrument latency `latencyMs` in `Queries` table
- [ ] T030 [P] [US3] Contract test `DELETE /api/documents/{id}` 204
- [ ] T031 [P] [US3] Integration test `Delete → purge → query excludes`
- [ ] T032 [US3] Implement `DELETE /api/documents/{id}`
- [ ] T033 [US3] Update client `LibraryView` delete button
- [ ] T034 [P] [US4] Contract test RBAC `GET 200` vs `POST/DELETE 403`
- [ ] T035 [P] [US4] Integration test `Employee browse read-only`
- [ ] T036 [US4] Enforce `[Authorize(Roles="Admin")]` on `POST/DELETE`; client role-aware UI
- [ ] T037 [P] Add Serilog structured logging + `X-Request-Id` + error problem details
- [ ] T038 [P] Harden `POST /api/documents` with magic-byte mime check + virus-scan hook stub
- [ ] T039 [P] Performance: index batching, Qdrant HNSW tuning, chunk cache
- [ ] T040 [P] Security: single-tenant proprietary signing docs + `data/` + `models/` gitignored
- [ ] T041 [P] Add `src/RAGGit.Client.Maui` publish builds + workstation `docker-compose`
- [ ] T042 [P] Extra unit tests for edge cases
- [ ] T043 Run `quickstart.md` validation
- [ ] T044 [P] Add CI workflow `.github/workflows/ci.yml`

## Follow-ups / Notes

- [ ] [Priority: Med] Convert `src/RAGGit.Client.Maui` from classlib fallback to a full `dotnet new maui` project once the .NET 8 MAUI workload/template is available; remove the `BuildingInsideVisualStudio` conditional and keep the required TFMs (`net8.0-windows10.0.19041.0;net8.0-ios;net8.0-android`). (Ref: `src/RAGGit.Client.Maui/RAGGit.Client.Maui.csproj`)
- [ ] [Priority: Low] Remove placeholder `Class1.cs` files from each classlib as real types are added in T002+. (Ref: `src/RAGGit.Core/Class1.cs`, `src/RAGGit.Ingest/Class1.cs`, `src/RAGGit.Retrieval/Class1.cs`, `src/RAGGit.Client.Maui/Class1.cs`)
- [ ] [Priority: Low] Ensure `.gitignore` for `bin/`, `obj/`, `data/`, and `models/` is added in T006 so build artifacts are not tracked. (Ref: `specs/001-offline-mode/tasks.md`)
