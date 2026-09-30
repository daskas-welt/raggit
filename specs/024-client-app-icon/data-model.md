# Data Model: Application Icon

**Feature**: `024-client-app-icon` | **Date**: 2026-09-30 | **Plan**: [plan.md](plan.md)

No persisted data and no interface contract. The "model" is the identity the client presents to Windows:
the mark, the asset that carries it, the surfaces that must show it, and the behaviour that must not move.

## Entity: Brand mark

Drives FR-002, FR-004, FR-006, FR-008. One mark, one colour, every surface.

| Field | Value | Source |
|-------|-------|--------|
| Product name | `Raggit` | `020-raggit-branding-refresh` (FR-001) |
| Brand colour | `#0F6CBD`, fixed — never the operating system accent | FR-008, the resolution recorded in [spec.md](spec.md) |
| Treatment | A filled rounded-square tile in the brand colour carrying a simplified product glyph in white | FR-004 — contrast comes from inside the mark, so it works on light and dark shells |
| Glyph | A simplified document/library silhouette derived from the client's `*24` icon vocabulary | `023-design-system-refinement` (one concept, one symbol) |
| Small-size variant | Fewer interior details below 24 px; same tile, same colour, same silhouette | FR-003 — the mark must survive the smallest shell rendering |
| Definition site | The generator script, exactly one place | FR-008 — every size variant and any future in-app use draw on one value |

**Validation rules**: exactly one brand colour; the mark is identical on every surface (sizes vary the
geometry, never the identity); identity is readable when colour is ignored; no third-party or
supplied-reference artwork is reproduced.

## Entity: Icon asset

Drives FR-001, FR-003, FR-005.

| Field | Value |
|-------|-------|
| Container | One multi-frame `.ico` at `src/RAGGit.Client.WPF/Assets/RaggitIcon.ico` |
| Frames | `16`, `20`, `24`, `32`, `40`, `48`, `64`, `256` px |
| Encoding | 32-bit with alpha; the 256 px frame PNG-compressed, the smaller frames uncompressed |
| Generator | `src/RAGGit.Client.WPF/Assets/generate-app-icon.ps1` — draws the mark as vector paths, writes the asset, and can emit a review sheet (all sizes over light and dark swatches) |
| Consumers | The client project (executable icon + pack-URI resource) and the shell window (window icon, title-bar icon element) |

**Validation rules**: the frame set is exactly as listed (no missing, no extra); the asset is committed
alongside the generator, so the shipped identity is both reviewable and reproducible; loose per-size image
files are not used as the identity.

## Entity: Surface

Drives FR-001, FR-002, FR-005, SC-001, SC-003.

| Surface | Fed by | Required state |
|---------|--------|----------------|
| Application file in the file system | the executable's icon resource | shows the mark |
| Pinned taskbar entry | the executable's icon resource | shows the mark |
| Taskbar button while running | the window's icon | shows the mark |
| Alt-Tab entry | the window's icon | shows the mark |
| Shell's custom title bar | the title bar control's icon element | shows the mark beside the existing product wordmark |
| Dialogs and owned windows | set explicitly on each window — WPF does not inherit `Window.Icon` | the upload window's title bar shows the mark; owned windows open no taskbar button |

**Validation rules**: every listed surface shows the mark rather than a generic placeholder, on a machine
that has never run the product; the published build shows the same identity with no per-machine setup.

## Entity: Frozen behaviour

Drives FR-007, SC-005.

| Field | Value |
|-------|-------|
| Automation identifiers | All identifiers frozen by `022`/`023` remain present and unchanged |
| Window titles | Unchanged (the identity is added beside the wordmark, not instead of it) |
| Shared behaviour layer, workstation service, contracts | Untouched |
| Suites | Existing suites pass with zero assertion changes; the formatter gate stays clean |

## State Transitions

Not applicable — the identity is a static asset applied at build and start-up; no runtime state, no user
interaction, and no transition between states.
