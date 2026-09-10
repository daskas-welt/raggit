# Completed Tasks: RAGGit Offline-Mode Single-Tenant RAG Library

- [x] T001 Create solution `RAGGit.sln` and projects `src/RAGGit.Core`, `src/RAGGit.Ingest`, `src/RAGGit.Retrieval`, `src/RAGGit.Workstation.Api`, `src/RAGGit.Client.Maui` per plan.md Project Structure [DONE: 2026-09-10] [By: coder]
- [x] T002 [P] Initialize `src/RAGGit.Core` as plain net8.0 classlib with `Microsoft.Data.Sqlite` package ref only, and define abstractions `IVectorStore`, `IEmbedder`, `ILlmClient` in `src/RAGGit.Core/Abstractions/` — no Qdrant.Client/OllamaSharp/LLamaSharp/Microsoft.ML.OnnxRuntime in Core [DONE: 2026-09-10] [By: coder]
- [x] T003 [P] Initialize `src/RAGGit.Workstation.Api` (ASP.NET Core 8) with `Swashbuckle.AspNetCore`, `Serilog` and reference `RAGGit.Core/Ingest/Retrieval` [DONE: 2026-09-10] [By: coder]
- [x] T004 [P] Initialize `src/RAGGit.Client.Maui` (.NET MAUI .NET 8; TFMs `net8.0-windows10.0.19041.0`, `net8.0-ios`, `net8.0-android`) with `HttpClient`, `CommunityToolkit.Mvvm` and reference `RAGGit.Core` [DONE: 2026-09-10] [By: coder]
- [x] T005 [P] Initialize `tests/unit`, `tests/contract`, `tests/integration` (xUnit) with `FluentAssertions`, `Microsoft.AspNetCore.Mvc.Testing` [DONE: 2026-09-10] [By: coder]
- [x] T006 [P] Configure `Directory.Build.props`, `editorconfig`, `dotnet format`, `.gitignore` (`/data/`, `/models/*.gguf`, `/models/*.onnx`) [DONE: 2026-09-10] [By: coder]
- [x] T007 Setup SQLite `rag.db` schema and migrations for `Documents`, `Chunks`, `Queries`, `Library(singleton)` per data-model.md [DONE: 2026-09-10] [By: coder]
