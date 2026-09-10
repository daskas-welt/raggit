# Security & Distribution Policy

RAGGit is a **single-tenant, on-premises** RAG system. This document describes
how deployments stay proprietary and how secrets are handled.

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

- **Windows 11 desktop**: packaged as a signed MSIX using a company-owned code
  signing certificate. Publish with:
  ```powershell
  dotnet publish src/RAGGit.Client.Maui -c Release -f net8.0-windows10.0.19041.0
  ```
- **Android**: sideloaded or distributed via private MDM; never via public app
  stores.
- **iOS**: enterprise or Ad-Hoc distribution; requires a Mac build host and
  company Apple Developer Enterprise certificate.

## Data Exclusion

The following directories must never be committed and are ignored by
`.gitignore`:

- `/data/` — SQLite `rag.db` and LanceDB vector files.
- `/models/*.gguf`, `/models/*.onnx` — local model weights.

## Reporting

If you discover a security issue in RAGGit, contact the project owner directly.
Do not open a public issue for undisclosed vulnerabilities.
