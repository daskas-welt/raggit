# Quickstart: Remove Upload Nav Item Validation (013)

## Prerequisites

- .NET 8 SDK (`global.json` pins `8.0.425`); Windows 10 1809+ / 11 for visual checks.

## 1. Regression gate

```powershell
dotnet build RAGGit.sln
dotnet test RAGGit.sln --no-build
dotnet csharpier check .
```

Expect: 0 errors; unit 228, contract 78, integration 62 (one known load-dependent perf flake — confirm isolated green); csharpier clean; zero test assertion changes.

## 2. Nav check (admin + employee)

1. `dotnet run --project src/RAGGit.Client.WinUI -p:Platform=x64`, sign in as admin → pane shows Library, Ask, History, My Docs, Admin — no Upload.
2. Sign in as employee → same minus Admin — no Upload.
3. Walk every destination by keyboard; selection always reflects the current page.

## 3. Upload flow intact (admin)

On Library press Upload → dialog opens → upload a file → new row appears. Unchanged behavior.

## 4. Scope guard

```powershell
git status --short -- src/RAGGit.Client.Core tests
Select-String -Path 'src/RAGGit.Client.WinUI/MainWindow.xaml*' -Pattern 'UploadItem|upload'
```

Expect: no changes under Core/tests; the only `upload` hits are the Library-owned flow (header button, `LibraryPage.xaml.cs`, `UploadDialog`), never the nav.
