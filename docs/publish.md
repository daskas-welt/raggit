# Publishing RAGGit

This guide covers building the workstation API and the WinUI 3 client for
distribution inside a single-tenant environment.

## Prerequisites

- .NET 8 SDK (`global.json` pins `8.0.425`)
- Windows 10 1809+ / 11 with the Windows App SDK 1.5 runtime (no MAUI workload;
  clean-checkout `dotnet build RAGGit.sln` must succeed with no MAUI installed)
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

## WinUI 3 Client

Single project `src/RAGGit.Client.WinUI` (`net8.0-windows10.0.17763.0`, Windows
App SDK 1.5). The client is a thin HttpClient-only app; no model weights are
bundled. One binary covers Windows 10 1809+ and Windows 11 (x64/x86/ARM64).

### Debug — unpackaged F5 loop

```powershell
dotnet run --project src/RAGGit.Client.WinUI -p:Platform=x64
```

Debug builds set `WindowsPackageType=None` (self-contained App Runtime) so F5
works on machines where the store-framework lookup fails.

### Release — signed MSIX sideload

```powershell
dotnet publish src/RAGGit.Client.WinUI `
  -c Release `
  -p:Platform=x64 `
  -p:WindowsPackageType=MSIX
```

Sign the resulting MSIX with the company code-signing certificate (local dev
test certificate is fine for sideload testing) before private enterprise
distribution. The MSIX installs with double-click on clean Win10 1809+ and
Win11 machines — no extra runtime install step.

### Release the desktop client to employees

1. Get the unsigned package from the CI `RAGGit.Client.WinUI-msix` artifact
   (or build it with the command above plus
   `-p:AppxPackageSigningEnabled=false -p:GenerateAppxPackageOnBuild=true`).
2. Sign it: `signtool sign /fd SHA256 /f <company-cert>.pfx /p <password>
   RAGGit.Client.WinUI_*_x64.msix`. The cert subject MUST match the
   `Publisher` in `Package.appxmanifest` (and in `RAGGit.appinstaller`).
3. In `src/RAGGit.Client.WinUI/RAGGit.appinstaller`: replace the example
   URLs with the real internal HTTPS location, and bump `Version`
   (appinstaller + manifest + package must all agree).
4. Copy the signed `.msix` and the edited `.appinstaller` to that internal
   location. Employees install once via the `.appinstaller` link
   ([employee guide](./install.md)); updates then arrive automatically.

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
- [ ] Client MSIX signed and distributed privately.
- [ ] WAN disabled for the workstation and query flow verified.
