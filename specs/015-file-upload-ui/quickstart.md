# Quickstart: File Upload UI Refresh

**Feature**: `015-file-upload-ui` | **Date**: 2026-09-18 | **Plan**: [plan.md](plan.md) | **Contract**: [contracts/upload-queue.md](contracts/upload-queue.md)

Validation guide proving the feature works end-to-end. No implementation code here — see the contract and data model for details.

## Prerequisites

- .NET 10 SDK, Windows 10 1809+ / 11 (WinUI 3, Windows App SDK 2.4.0)
- Workstation API running on LAN (upload target); an admin session for upload tests
- Repo root: `C:\Users\mcaib\Documents\Projects\raggit`

## 1. Build

```powershell
dotnet build src\RAGGit.Client.WinUI\RAGGit.Client.WinUI.csproj -p:Platform=x64
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

## 2. Unit tests (queue behavior)

```powershell
dotnet test tests\unit\RAGGit.Tests.Unit.csproj --filter "FullyQualifiedName~UploadViewModelTests"
```

Expected: `Passed! - Failed: 0, Passed: 18` (9 pre-existing single-file + 9 queue: gating, per-file progress, pick-time rejection, partial success, cancel, re-upload rewind).

## 3. Manual walkthrough (admin, Library page)

1. Press **Upload** in the Library header → dialog opens, focus lands on Add-files, supported types caption visible, Upload disabled.
2. Add two valid files → both rows appear with name + size; Upload enables; overall count reads "0 of 2 done".
3. Try adding an unsupported type (e.g. `.exe`) → blocked with a message naming the file; queue unchanged; Upload still gated on the valid entries only.
4. Press **Upload** → per-file bars advance, count ticks "1 of 2 done" → "2 of 2 done", success `InfoBar` shows briefly, dialog auto-closes, both rows appear in the library.
5. Repeat with one file that fails (or press **Cancel** mid-run) → dialog stays open with per-file outcomes; library shows only successes.
6. Esc during upload → in-flight request cancels, library unchanged.

## 4. Accessibility + themes (manual)

- Keyboard only: Tab order Add-files → per-row Remove → Upload/Cancel → Close; Enter activates; Esc cancels then closes; focus returns to the Library Upload button.
- Screen reader (Narrator): open, queue additions, progress milestones, success/failure/cancel, and close are announced.
- Light / Dark / HighContrast: all rows, bars, InfoBars, and focus indicators legible; narrow window (≤720px) reflows with nothing clipped.
- Employee session: no Upload button anywhere; dialog cannot be opened.

## References

- Data model (queue fields, transitions, validation): [data-model.md](data-model.md)
- Binding + behavior rules: [contracts/upload-queue.md](contracts/upload-queue.md)
- Decisions and rejected alternatives: [research.md](research.md)
