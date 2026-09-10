# Publishing RAGGit

This guide covers building the workstation API and the .NET MAUI client for
distribution inside a single-tenant environment.

## Prerequisites

- .NET 8 SDK
- .NET MAUI workload (for client builds):
  ```powershell
  dotnet workload install maui
  ```
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

## .NET MAUI Client

Build for each target framework. The client is a thin HttpClient-only app;
no model weights are bundled.

### Windows 11 — MSIX

```powershell
dotnet publish src/RAGGit.Client.Maui `
  -c Release `
  -f net8.0-windows10.0.19041.0 `
  -p:RuntimeIdentifierOverride=win10-x64
```

Sign the resulting MSIX with a company code-signing certificate before
enterprise distribution.

### Android — Sideload / Private MDM

```powershell
dotnet publish src/RAGGit.Client.Maui `
  -c Release `
  -f net8.0-android `
  -p:AndroidPackageFormat=apk
```

Distribute the APK through your private MDM or sideload onto managed devices.

### iOS — Ad-Hoc / Enterprise

Requires a Mac build host with Xcode and an Apple Developer Enterprise account.

```bash
dotnet publish src/RAGGit.Client.Maui \
  -c Release \
  -f net8.0-ios \
  -p:RuntimeIdentifier=ios-arm64 \
  -p:CodesignKey="iPhone Distribution: Your Company" \
  -p:CodesignProvision="RAGGit Enterprise"
```

## LAN Discovery

Clients locate the workstation through one of the following mechanisms:

1. **DNS / mDNS**: resolve `ai-workstation.local` to the workstation IP.
2. **Static IP / DHCP reservation**: admin pre-configures the workstation URL.
3. **Site VPN for mobile**: iOS/Android devices connect to the company VPN and
   reach the workstation as if on the same LAN.

There is no cloud relay. The client fails fast with `cannot reach AI workstation`
when the LAN/VPN path is unavailable.

## First-Run Checklist

- [ ] Workstation Ollama models cached (`ollama pull nomic-embed-text` and chat model).
- [ ] API keys configured via environment variables or secret manager.
- [ ] `./data` and `./models` excluded from backups you do not want to keep.
- [ ] Client MSIX/APK/IPA signed and distributed privately.
- [ ] WAN disabled for the workstation and query flow verified.
