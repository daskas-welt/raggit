# Quickstart: Allowed Upload Types

**Feature**: `018-allowed-upload-types` | **Date**: 2026-09-18

Validates the 4-type allow-list end to end. See [contracts/upload-allowlist.md](contracts/upload-allowlist.md) for exact messages and [data-model.md](data-model.md) for validation rules.

## Prerequisites

- Workstation API running and reachable over LAN; admin session available.
- Desktop client built from this branch (`dotnet build RAGGit.sln` green).
- Sample files: one valid `.pdf`, `.docx`, `.txt`, `.xlsx`; one file each of `.md`, `.doc`, `.png`, `.zip`, `.pptx`; one image renamed to `.pdf`; one `.PDF` (uppercase).

## Validation scenarios

### QS-1 — Each supported type uploads (SC-001, US1)

1. Open the upload dialog from the Library header.
2. Pick the valid `.docx` → confirm it queues with name + size → Upload → success → dialog auto-closes → library shows the new document.
3. Repeat for `.pdf`, `.txt`, `.xlsx` (one dialog open each).
4. Query a known sentence from each file → citation returned.

**Expected**: 4/4 succeed; each found in the library within 1 minute.

### QS-2 — Unsupported types blocked, picker + drop (SC-002, US2)

1. Open the upload dialog; pick the `.md` file → blocked with `'<name>' isn't supported. Choose PDF, DOCX, XLSX, TXT files.`; queue stays empty; Upload stays disabled.
2. Repeat the pick attempt for `.doc`, `.png`, `.zip`, `.pptx`.
3. Drag-and-drop the same 5 files onto the drop target → identical messages, nothing queued.
4. Multi-select all 5 unsupported + 1 valid file in one picker pass → exactly the valid file queues; 5 named reasons shown.

**Expected**: 100% blocked before queueing; every message names the file and lists the 4 types.

### QS-3 — Types advertised up front (US3)

1. Open the upload dialog → hint reads `PDF, DOCX, XLSX, TXT supported · up to 100 MB.`
2. Open the system picker → filter offers the 4 supported types.

**Expected**: No trial-and-error needed; 9/10 walkthrough participants name all 4 types unaided (SC-004).

### QS-4 — Mismatched content rejected (SC-003, FR-006)

1. Upload the image-renamed-to-`.pdf` → 400 `content does not match type`; no document in the library.
2. Upload a `.docx` renamed to `.xlsx` → same outcome.

**Expected**: Zero documents created across mismatch trials; each shows the mismatch reason.

### QS-5 — Case and edge handling

1. Upload `SAMPLE.PDF` and `notes.TxT` → both accepted.
2. Attempt `report.pdf.exe` and an extensionless file → both rejected as unsupported.
3. Attempt a 0-byte `.txt` → rejected with no-extractable-content message.

**Expected**: Case-insensitive accept; double/no extension rejected; empty rejected.

### QS-6 — Existing `.md` documents unaffected (FR-008)

1. Using a workstation seeded with an `.md` document (pre-change data), `GET /api/documents` still lists it and a query for its text returns a citation.

**Expected**: Stored `.md` docs remain readable/searchable; only new `.md` uploads are blocked.

## Regression gate

```powershell
dotnet build RAGGit.sln
dotnet test RAGGit.sln
```

Full suite green, including untouched `DocumentMimeTypeXlsxTests`, `DocumentFormatValidatorTests` (`Md` data), and `DocumentDisplayTests` (`Md`).
