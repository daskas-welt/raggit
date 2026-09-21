# Quickstart: Multi-File Upload (Picker + Drag-and-Drop)

**Feature**: `017-multi-file-upload` | **Date**: 2026-09-18 | **Plan**: [plan.md](plan.md)

Validation guide proving the feature end-to-end. Contracts: [contracts/ui-contracts.md](contracts/ui-contracts.md). Data model: [data-model.md](data-model.md). No server changes — the workstation API runs as usual over LAN.

## Prerequisites

- Workstation API running locally (`POST /api/documents` reachable); admin account available.
- WinUI client built (`src/RAGGit.Client.WinUI`), logged in as admin on the Library page.
- Test files: 3 small supported files (e.g. `.txt`), 1 unsupported (e.g. `.exe` or renamed), 1 folder, 1 shortcut (`.lnk`).

## Scenario 1 — One-pass multi-select (spec US1)

1. Open Upload from the Library header.
2. Choose Add files → select 3 supported files in one picker pass.
3. Expect: all 3 queued with name + size; "3 files ready. Press Upload to start."
4. Start upload → expect per-file progress, "3 of 3 done", success outcome, auto-close, library shows 3 new documents.

## Scenario 2 — Drag-and-drop (spec US2)

1. Open Upload; drag 2 supported files from Explorer onto the drop area.
2. Expect: highlight while hovering; both queued on drop without touching the picker.
3. Start upload → expect identical progress/outcome/refresh behavior as Scenario 1.

## Scenario 3 — Mixed intake + invalid files (spec US3, FR-003)

1. Add 1 file via picker, drop 1 supported + 1 unsupported + 1 folder + 1 shortcut.
2. Expect: supported files queued; each rejected item named with its reason; start stays enabled (valid files present).
3. Remove 1 queued file → expect it leaves the queue and counts update.
4. Start → expect only remaining valid files upload; library shows exactly those.

## Scenario 4 — Queue lock during upload (clarified)

1. Queue 2 files, start upload, immediately try Add files and drag another file over the dialog.
2. Expect: picker action disabled; drop area disabled with "add more after this run" hint; nothing new queued.
3. After completion, open Upload again → expect intake re-enabled.

## Scenario 5 — Unit + regression

1. `dotnet test tests/unit/RAGGit.Tests.Unit.csproj --filter UploadViewModelTests` — all green, including new multi-add, mixed-rejection, lock, and virtual-reject tests.
2. Full `dotnet build` + unit suite green; existing upload/contract suites unchanged (no server change).

## Expected outcomes

- SC-001: 5 files queued via one pass/drop in < 30 s unaided, 90% first-attempt trials.
- SC-002: every rejected file named with its reason in 100% of mixed trials.
- SC-003: zero orphans / duplicate-starts across 20 trials.
- SC-004: keyboard-only run of both paths + Light/Dark/High Contrast check, zero unreachable/illegible controls.
