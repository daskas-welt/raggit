# Presentation images — source map

SVG is canonical (scales in slides). Render with the official PlantUML MCP
server (`npx -y @plantuml/mcp-js@0.2.2`, tools `check_syntax` + `render_diagram`).
PNG optionally via `scripts/Render-PlantUml.ps1` (needs Java).

| Image | Source | Notes |
|---|---|---|
| `architecture.svg` | [`../architecture.puml`](../architecture.puml) | System topology; rectangle/actor/package dialect for the JS engine |
| `query-sequence.svg` | [`../query-sequence.puml`](../query-sequence.puml) | Presentation query path (ask → citations) |
| `identity-sequence.svg` | [`../../specs/004-identity/docs/sequence.puml`](../../specs/004-identity/docs/sequence.puml) | Identity lifecycle; rendered under this name for README clarity |

Constraints (verified 2026-09-24 against `@plantuml/mcp-js@0.2.2`):
no `!theme` directive (hangs `render_diagram`), no `[bracket]` components,
no `: trailing labels` on declarations in description diagrams.
Sequence diagrams need a `<style> sequenceDiagram { lifeLine { ... } }`
block — the engine emits lifelines with no stroke otherwise (invisible).
Database cans need `<style> componentDiagram { database { ... } }` —
same invisible-border problem for can body + rim.
