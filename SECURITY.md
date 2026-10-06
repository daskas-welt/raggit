# Security & Distribution Policy

RAGGit is a **single-tenant, on-premises** RAG system. This document describes
how deployments stay secure and how secrets are handled.

## Single-Tenant Model

- One AI Workstation serves exactly one company.
- There is no `company_id`, no shared tenancy, and no cross-company data flow.
- Document metadata and vectors are stored only on the workstation under
  `./data/` and `./models/`.

## Secrets

- No secrets are committed to source control.
- API keys, model paths, and certificates are supplied via:
  - `dotnet user-secrets` in development,
  - environment variables in production,
  - or a company-specific secret manager.
- `appsettings.json` contains only empty placeholders for `Api:AdminKey` and
  `Api:EmployeeKey`.

## Distribution

- **Windows desktop (10 1809+ / 11)**: published as a self-contained executable
  from `src/RAGGit.Client.WPF` (`net10.0-windows10.0.17763.0`) and distributed
  privately inside the single-tenant environment — no Store/MSIX step. Publish
  with:
  ```powershell
  dotnet publish src/RAGGit.Client.WPF -c Release -p:Platform=x64 -o ./out/desktop
  ```

## Data Exclusion

The following directories must never be committed and are ignored by
`.gitignore`:

- `/data/` — SQLite `rag.db` and LanceDB vector files.
- `/models/*.gguf`, `/models/*.onnx` — local model weights.

## Reporting

If you discover a security issue in RAGGit, contact the project owner directly.
Do not open a public issue for undisclosed vulnerabilities.
