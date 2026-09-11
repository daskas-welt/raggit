# Quickstart: Ingest Breadth (XLSX)

**Feature**: `003-ingest-breadth` | **Branch**: `003-ingest-breadth` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Extends the offline RAG library (001 + 002 real bring-up) with `.xlsx` ingest: visible-sheet streaming, header-repeat, cached formula values, 100k cell cap, hidden skip, PK deep-validation. No new NuGet beyond `DocumentFormat.OpenXml` already in the repo.

## Prereqs

- .NET 8 SDK (`dotnet --version` ≥8.0), Git
- Ollama installed for real embed/LLM path (`ollama --version`) — optional for xlsx extract demo (extract + guards work without Ollama); 10GB free disk
- `data/lancedb` + `rag.db` are .gitignored (created on first run); `models/` GGUF/ONNX not needed for this feature
- Workstation API is ASP.NET Core 8 (`src/RAGGit.Workstation.Api`); thin MAUI client unchanged for this feature

## 1. Build (no new package)

```powershell
git clone <repo> raggit; cd raggit
dotnet build RAGGit.sln -v q
# Expect: 0 Error(s) — DocumentFormat.OpenXml already referenced, no restore of new packages.
```

## 2. Run workstation API (real LanceDB file-backed)

```powershell
# Fresh DB for first xlsx run (or after any VectorSize swap — not needed for this feature):
Remove-Item -Recurse -Force ./data/lancedb -ErrorAction SilentlyContinue
Remove-Item -Force ./data/rag.db -ErrorAction SilentlyContinue  # if you want a clean library

dotnet user-secrets set "Api:AdminKey" "dev-admin-key" --project src/RAGGit.Workstation.Api
dotnet user-secrets set "Api:EmployeeKey" "dev-employee-key" --project src/RAGGit.Workstation.Api

dotnet run --project src/RAGGit.Workstation.Api --urls http://localhost:5001
# Health:
curl http://localhost:5001/health
# → 200 { "vectorDb":"ok", "llm":"ok|down", "qdrant":"ok", "version":"1.2.0" }
```

If Ollama is not running, `llm:down` is expected — xlsx upload + extract still works (only `POST /api/query` needs Ollama/LLM).

## 3. Create sample workbooks (Python openpyxl — any generator works)

Requires `pip install openpyxl` locally.

```powershell
# 3-sheet workbook (header + 10 rows each, known sentence on Sheet2 for SC-001)
python - << 'PY'
import openpyxl
wb = openpyxl.Workbook()
headers = ["Name","Dept","Refund Policy"]
rows = [[f"User{i}", "Sales", f"row {i}"] for i in range(10)]
# Sheet 1
ws1 = wb.active; ws1.title="Sheet1"
ws1.append(headers); [ws1.append(r) for r in rows]
# Sheet 2 — contains the searchable sentence
ws2 = wb.create_sheet("Sheet2")
ws2.append(headers)
for r in rows: ws2.append(r)
ws2["B5"] = "refund policy: 30-day full refund with receipt"
# inject a formula cell whose cached value is 42.50 on Sheet2 (openpyxl stores cached value as v when you set it)
ws2["C12"] = 42.50  # simulate cached formula result; real Excel stores <f> + <v>42.50</v>
# Sheet 3
ws3 = wb.create_sheet("Sheet3")
ws3.append(headers); [ws3.append(r) for r in rows]
wb.save("sample-3sheet.xlsx")
print("wrote sample-3sheet.xlsx")
PY

# Hidden-sheet workbook (2 visible + 1 hidden with token hidden-token-xyz)
python - << 'PY'
import openpyxl
wb = openpyxl.Workbook()
ws1 = wb.active; ws1.title="VisibleA"; ws1.append(["H1","H2"]); ws1.append(["a","b"])
ws2 = wb.create_sheet("VisibleB"); ws2.append(["H1","H2"]); ws2.append(["c","d"])
ws3 = wb.create_sheet("HiddenC"); ws3.append(["H1","H2"]); ws3.append(["hidden-token-xyz","secret"])
ws3.sheet_state = "hidden"  # or "veryHidden"
wb.save("sample-hidden.xlsx")
print("wrote sample-hidden.xlsx")
PY

# Over-cap workbook (>100k cells) — 5 sheets x 20k cells each
python - << 'PY'
import openpyxl
wb = openpyxl.Workbook()
for s in range(5):
    ws = wb.active if s==0 else wb.create_sheet(f"S{s+1}")
    ws.title = f"S{s+1}"
    # 20 columns x 1000 rows = 20k per sheet, 5*20k=100k exactly at cap+1 overflow needs 100001
    # do 20 cols x 1000 rows x 5 sheets + 1 extra row = 100k+20 overflow
    for r in range(1000):
        ws.append([f"c{c}" for c in range(20)])
ws_extra = wb.create_sheet("Overflow")
ws_extra.append(["x"]*20)  # +20 pushes over 100k
wb.save("sample-overcap.xlsx")
print("wrote sample-overcap.xlsx (over 100k cells)")
PY

# Renamed docx as .xlsx (PK zip but not xlsx — contains word/document.xml)
python - << 'PY'
import zipfile, io
# use any valid docx if you have one, or synthesize a minimal docx zip
buf = io.BytesIO()
with zipfile.ZipFile(buf, "w") as z:
    z.writestr("[Content_Types].xml", '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main"/></Types>')
    z.writestr("word/document.xml", "<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'><w:body><w:p><w:r><w:t>docx content</w:t></w:r></w:p></w:body></w:document>")
open("fake-xlsx-from-docx.xlsx","wb").write(buf.getvalue())
print("wrote fake-xlsx-from-docx.xlsx")
PY

# Corrupt xlsx (truncate after PK header)
python - << 'PY'
open("corrupt.xlsx","wb").write(open("sample-3sheet.xlsx","rb").read()[:20])
print("wrote corrupt.xlsx (truncate)")
PY
```

## 4. Upload xlsx + verify extract (SC-001, SC-003, SC-005)

```powershell
# Valid 3-sheet upload (Admin):
curl -X POST http://localhost:5001/api/documents -H "X-Api-Key: dev-admin-key" -F "file=@sample-3sheet.xlsx"
# → 201 { id:"<uuid>", filename:"sample-3sheet.xlsx", mime:"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", size:..., hash:"...", status:"Indexing" }
# Save the id:
$docId = (curl -s http://localhost:5001/api/documents -H "X-Api-Key: dev-admin-key" | ConvertFrom-Json)[0].id
# or capture from the 201 response
# List:
curl http://localhost:5001/api/documents -H "X-Api-Key: dev-admin-key"
# → [{ status:"Ready" }] — must appear in <30s on dev laptop
# When Ollama is running with all-minilm/phi3:mini, status flows Indexing→Ready; without Ollama, text extraction still succeeds (chunk rows visible in DB), query for citations requires Ollama.

# Query the known sentence on Sheet2:
curl -X POST http://localhost:5001/api/query -H "X-Api-Key: dev-employee-key" -H "Content-Type: application/json" -d '{"query":"refund policy 30-day full refund"}'
# → 200 { "answer":"...", "citations":[{ "documentId":"<docId>", "chunkId":"...", "text":"...refund policy... | ...", "ordinal":... }], "latencyMs":... }
# Expect: at least 1 citation whose documentId == docId and whose text contains the Sheet2 row. Verify:
#   text contains "[Sheet: Sheet2]" or "refund policy"

# Query cached formula value 42.50 (SC-003):
curl -X POST http://localhost:5001/api/query -H "X-Api-Key: dev-employee-key" -H "Content-Type: application/json" -d '{"query":"42.50"}'
# → citation containing "42.50", not "=SUM" or formula text

# Verify hidden-sheet token is never returned (SC-002):
curl -X POST http://localhost:5001/api/documents -H "X-Api-Key: dev-admin-key" -F "file=@sample-hidden.xlsx"
curl -X POST http://localhost:5001/api/query -H "X-Api-Key: dev-employee-key" -H "Content-Type: application/json" -d '{"query":"hidden-token-xyz"}'
# → 200 { "answer":"no relevant content found", "citations":[] }  — 0 hits

# Deep-validation / renamed-docx guard (FR-006):
curl -X POST http://localhost:5001/api/documents -H "X-Api-Key: dev-admin-key" -F "file=@fake-xlsx-from-docx.xlsx"
# → 400 { "error":"content does not match type" }  — no Document retained; GET /api/documents does not list it
curl http://localhost:5001/api/documents -H "X-Api-Key: dev-admin-key"  # count unchanged

# Corrupt/truncated xlsx:
curl -X POST http://localhost:5001/api/documents -H "X-Api-Key: dev-admin-key" -F "file=@corrupt.xlsx"
# → 400 { "error":"corrupted xlsx" or "content does not match type" }  — no partial index

# Cell-cap guard (FR-005 — 413):
curl -X POST http://localhost:5001/api/documents -H "X-Api-Key: dev-admin-key" -F "file=@sample-overcap.xlsx"
# → 413 { "error":"Spreadsheet exceeds 100,000 cell limit (found 100020 cells)" }  — no Document retained

# Empty / hidden-only workbook:
python - << 'PY'
import openpyxl
wb = openpyxl.Workbook(); ws = wb.active; ws.title="HiddenOnly"; ws.sheet_state="hidden"
wb.save("empty-hidden-only.xlsx")
PY
curl -X POST http://localhost:5001/api/documents -H "X-Api-Key: dev-admin-key" -F "file=@empty-hidden-only.xlsx"
# → 400 { "error":"no extractable content" }

# Dedupe (same bytes hash):
curl -X POST http://localhost:5001/api/documents -H "X-Api-Key: dev-admin-key" -F "file=@sample-3sheet.xlsx"
# → 200 with existing Document id (not 201), not re-indexed — same as pdf/docx
```

## 5. GET documents / MIME verification (contract 1.2.0)

```powershell
curl http://localhost:5001/api/documents -H "X-Api-Key: dev-admin-key" | ConvertFrom-Json | Format-List
# Each xlsx doc shows mime:"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
curl http://localhost:5001/api/auth/me -H "X-Api-Key: dev-admin-key"
# → 200 { "identityType":"ApiKey", "role":"Admin" }  (from 1.1.0, unchanged)
```

## 6. Guard ordering demo (413 vs 400)

```powershell
# 1) >100MB is checked first — even a renamed docx >100MB returns 413 before 400
# (create a >100MB file: fsutil file createnew big.xlsx 104857601 on Windows, but do not use real .xlsx bytes — handler checks size before zip probe)
# 2) Renamed docx with 200k physical <c> tags would still return 400 "content does not match type", not 413 cell-cap — deep validation precedes cap per plan R6
# 3) Hidden-only vs over-cap: hidden cells excluded from cap count — a workbook with 80k visible + 50k hidden returns 201 Ready, not 413
```

## 7. Opt-in tests (still gated as in 002)

```powershell
# 3a. Fakes-only (no Ollama) — must stay green:
dotnet test -v q

# 3b. Real-Ollama opt-in (requires ollama serve with all-minilm + phi3:mini, fresh data/lancedb):
dotnet test --filter "Trait=RequiresOllama" -v q
# or exclusion:
dotnet test --filter "RequiresOllama!=true" -v q
```

Unit extractor tests (visible sheets, hidden skip, formula cached, inline/shared string, date 1900/1904, header-repeat, over-cap, empty, renamed docx) live under `tests/unit/RAGGit.Ingest` and run in the fakes-only path (no Ollama).

## 8. Troubleshooting

- `DocumentFormat.OpenXml` missing → `dotnet restore` (already in `RAGGit.Ingest.csproj` since 002); never add a new package.
- `InvalidDataException` on upload → truncated zip; returns 400 as above, not 500.
- `Spreadsheet exceeds 100,000 cell limit` → reduce visible cells (hidden sheets excluded; delete hidden sheet or split workbook).
- `content does not match type` on valid xlsx → ensure file is real `.xlsx` (Office 2007+ OOXML), not `.xls` (BIFF) — `.xls` is out of scope and rejected.
- Date shows as `44561` → cell's `NumberFormatId` not date; best-effort fallback emits raw cached value (expected per Assumptions).
- MAUI client shows no xlsx difference → correct: MAUI is thin; xlsx is workstation-only (Library/Query views already show citations with sheet prefix).

