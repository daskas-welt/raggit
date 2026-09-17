# RAGGit — Your Company's Private AI Library

RAGGit turns your company's documents into a private library your people can
search and ask questions about — in plain language, with answers that always
show their sources.

**The short version for decision-makers:**

- **Private by design.** Everything runs on computers your company owns. Your
  documents are never sent to the cloud, not even to answer a question.
- **One company, one library.** Each installation serves exactly one company.
  There is no sharing between companies and no outside access.
- **Answers with proof.** Every answer names the documents and passages it
  came from. If nothing relevant exists, it says so instead of guessing.
- **Everyone gets their own account.** Admins manage documents and people;
  employees search and ask. People only ever see what they are allowed to see.

## How it works

1. **One powerful AI workstation** — a separate, capable machine on your
   company network (good CPU, 16GB+ RAM, a GPU helps, 10GB free disk) —
   stores your documents and runs the AI. No internet needed once it is set
   up. This machine does all the heavy work, so nothing else needs to be
   powerful.
2. **Ordinary Windows PCs** run a simple desktop app that talks to the
   workstation over your office network or VPN. Any modest office PC works —
   no AI hardware needed on desks.
3. **Admins upload documents** (PDF, Word, Excel, text...) from their PC.
   The workstation reads and indexes them automatically.
4. **Employees ask questions** in everyday language and get answers with
   citations they can click through to verify.

## Key promises

- **Works offline.** Pull the internet plug and searching still works —
  this is tested automatically on every change.
- **No guessing.** No relevant documents means a clear "no relevant content
  found", never a made-up answer.
- **No vendor lock-in on your data.** Documents and the library live in
  standard files on your own machines.

## Getting the app

- **Employees**: install from the company intranet page (link from IT),
  then sign in — full walkthrough: [Installing the RAGGit App](docs/install.md).
  The app updates itself; releases are signed with the company certificate
  and served internally (never downloaded from GitHub).
- **Operators**: release steps (sign → publish to intranet → version care):
  [Publishing guide](docs/publish.md). Note the manifest `Publisher` must
  match the release signing certificate.

## Trying it (developers)

You need .NET 8 and, for the AI part, [Ollama](https://ollama.com)
(free, runs locally). Roughly:

```powershell
git clone https://github.com/daskas-welt/raggit.git; cd raggit
dotnet build RAGGit.sln
ollama pull all-minilm; ollama pull phi3:mini
dotnet run --project src/RAGGit.Workstation.Api --urls https://localhost:5001
```

Full step-by-step (accounts, desktop app, offline check, MSIX install):
[Quickstart](specs/001-offline-mode/quickstart.md) ·
[Publishing guide](docs/publish.md) ·
[Operator guide](docs/operator-cli.md)

## Project map (for contributors)

- `src/RAGGit.Workstation.Api` — the workstation web service
- `src/RAGGit.Client.WinUI` — the Windows desktop app
- `src/RAGGit.Core`, `RAGGit.Ingest`, `RAGGit.Retrieval`, `RAGGit.Client.Core` — shared libraries
- `specs/` — feature specifications, plans, and verification records
- `docs/` — architecture diagrams, performance and publishing guides
- `.specify/memory/constitution.md` — the project's governing principles (v1.3.0)

Current release: `1.4.0`. Full version history and API details live in the
[specs](specs/) and [docs](docs/) folders, not in this file.

## License

Proprietary — all rights reserved. AI models stay on customer-owned
workstations and are never redistributed.
