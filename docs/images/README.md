# Presentation images — source map

SVG is canonical (scales in slides). Render with PlantUML tooling.
PNG optionally via `scripts/Render-PlantUml.ps1` (needs Java).

| Image | Source | Notes |
|---|---|---|
| `architecture.svg` | [`../architecture.puml`](../architecture.puml) | System topology |
| `query-sequence.svg` | [`../query-sequence.puml`](../query-sequence.puml) | Presentation query path (ask → citations) |
| `identity-sequence.svg` | [`../../specs/004-identity/docs/sequence.puml`](../../specs/004-identity/docs/sequence.puml) | Identity lifecycle; rendered under this name for README clarity |

Constraints: no `!theme` directive, no `[bracket]` components,
no `: trailing labels` on declarations in description diagrams.
Sequence diagrams need a `<style> sequenceDiagram { lifeLine { ... } }`
block — the engine emits lifelines with no stroke otherwise (invisible).
Database cans need `<style> componentDiagram { database { ... } }` —
same invisible-border problem for can body + rim.
