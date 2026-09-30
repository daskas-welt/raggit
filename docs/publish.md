# Publishing RAGGit

This guide covers building the workstation API and the WPF desktop client for
distribution inside a single-tenant environment.

## Prerequisites

- .NET 10 SDK (`global.json` pins the SDK)
- Windows 10 1809+ / 11 (no extra workloads;
  clean-checkout `dotnet build RAGGit.sln -p:Platform=x64` must succeed)
- Docker (optional, for the workstation container)

## AI Workstation

### Native run

```powershell
dotnet publish src/RAGGit.Workstation.Api -c Release -o ./out/workstation
./out/workstation/RAGGit.Workstation.Api.exe
```

### Docker Compose (optional)

A `docker-compose.yml` is provided at the repository root. It mounts local
`./data` and `./models` directories so the container remains stateless while
persisting the library and model cache.

```powershell
docker compose up --build
```

The API listens on port `5001` and expects the Ollama daemon to be reachable
at `http://host.docker.internal:11434` by default. Override with the
`OLLAMA__URL` environment variable.

## WPF Desktop Client

Single project `src/RAGGit.Client.WPF` (`net10.0-windows10.0.17763.0`) styled
with WPF-UI. The client is a thin HttpClient-only app; no model weights are
bundled. One binary covers Windows 10 1809+ and Windows 11 (x64).

### Debug — F5 loop

```powershell
dotnet run --project src/RAGGit.Client.WPF -p:Platform=x64
```

### Release — self-contained executable

```powershell
dotnet publish src/RAGGit.Client.WPF `
  -c Release `
  -p:Platform=x64 `
  -o ./out/desktop
```

Distribute the contents of `./out/desktop` privately inside the
single-tenant environment (no Store/MSIX step).

### Release the desktop client to employees

1. Get the desktop build from the CI `RAGGit.Client.WPF-desktop` artifact
   (or build it with the publish command above).
2. Copy the published folder to the internal distribution location.
   Employees run `RAGGit.Client.WPF.exe` directly
   ([employee guide](./install.md)).

The published executable carries the application identity: the Raggit icon (feature
`024-client-app-icon`) rides with the build, so the file, the taskbar button and Alt-Tab need no
per-machine setup.

## LAN Discovery

Clients locate the workstation through one of the following mechanisms:

1. **DNS / mDNS**: resolve `ai-workstation.local` to the workstation IP.
2. **Static IP / DHCP reservation**: admin pre-configures the workstation URL.
3. **Site VPN for remote Windows desktops**: off-site machines connect to the
   company VPN and reach the workstation as if on the same LAN.

There is no cloud relay. The client fails fast with `cannot reach AI workstation`
when the LAN/VPN path is unavailable.

## First-Run Checklist

- [ ] Workstation Ollama models cached (`ollama pull nomic-embed-text` and chat model).
- [ ] API keys configured via environment variables or secret manager.
- [ ] `./data` and `./models` excluded from backups you do not want to keep.
- [ ] Client desktop build published and distributed privately.
- [ ] WAN disabled for the workstation and query flow verified.
