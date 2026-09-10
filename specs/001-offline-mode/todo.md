# Active Tasks: RAGGit Offline-Mode Single-Tenant RAG Library

- [ ] T030 [P] [US3] Contract test `DELETE /api/documents/{id}` 204
- [ ] T031 [P] [US3] Integration test `Delete → purge → query excludes`
- [ ] T032 [US3] Implement `DELETE /api/documents/{id}`
- [ ] T033 [US3] Update client `LibraryView` delete button
- [ ] T034 [P] [US4] Contract test RBAC `GET 200` vs `POST/DELETE 403`
- [ ] T035 [P] [US4] Integration test `Employee browse read-only`
- [ ] T036 [US4] Enforce `[Authorize(Roles="Admin")]` on `POST/DELETE`; client role-aware UI
- [ ] T037 [P] Add Serilog structured logging + `X-Request-Id` + error problem details
- [ ] T038 [P] Harden `POST /api/documents` with magic-byte mime check + virus-scan hook stub
- [ ] T039 [P] Performance: index batching, LanceDB HNSW tuning, chunk cache
- [ ] T040 [P] Security: single-tenant proprietary signing docs + `data/` + `models/` gitignored
- [ ] T041 [P] Add `src/RAGGit.Client.Maui` publish builds + workstation `docker-compose`
- [ ] T042 [P] Extra unit tests for edge cases
- [ ] T043 Run `quickstart.md` validation
- [ ] T044 [P] Add CI workflow `.github/workflows/ci.yml`

## Follow-ups / Notes

- [ ] [Priority: Med] `OnnxEmbedder` uses a basic WordPiece tokenizer; validate against bge-micro-v2 ONNX output and replace with a proper HuggingFace tokenizer (e.g. `Microsoft.ML.Tokenizers`) if needed. (Ref: `src/RAGGit.Ingest/Ai/OnnxEmbedder.cs`)
- [ ] [Priority: Med] Convert `src/RAGGit.Client.Maui` from classlib fallback to a full `dotnet new maui` project once the .NET 8 MAUI workload/template is available; remove the `BuildingInsideVisualStudio` conditional and keep the required TFMs (`net8.0-windows10.0.19041.0;net8.0-ios;net8.0-android`). (Ref: `src/RAGGit.Client.Maui/RAGGit.Client.Maui.csproj`)
- [ ] [Priority: Low] Ensure `.gitignore` for `bin/`, `obj/`, `data/`, and `models/` is added in T006 so build artifacts are not tracked. (Ref: `specs/001-offline-mode/tasks.md`)
- [ ] [Priority: Med] Wire up `DocumentsApiClient` base address/API key and register MAUI views in `AppShell` / DI so the client can actually reach the workstation. (Ref: `src/RAGGit.Client.Maui/Services/DocumentsApiClient.cs`, `src/RAGGit.Client.Maui/AppShell.xaml`)
- [ ] [Priority: Med] Validate the MAUI XAML views build for the real target frameworks (`net8.0-windows10.0.19041.0;net8.0-ios;net8.0-android`) and wire `LibraryViewModel.UploadCommand` navigation. (Ref: `src/RAGGit.Client.Maui/Views/LibraryView.xaml`, `src/RAGGit.Client.Maui/Views/UploadView.xaml`)
- [ ] [Priority: Med] Harden `POST /api/documents` so corrupted/invalid PDFs and docx files return 400 instead of 500, preserving the no-partial-index invariant per FR-006. (Ref: `src/RAGGit.Workstation.Api/Controllers/DocumentsController.cs`, `src/RAGGit.Ingest/Chunker.cs`)
