# UI Contracts: Application Icon

**Feature**: `024-client-app-icon` | **Date**: 2026-09-30 | **Plan**: [plan.md](../plan.md)

Desktop application, no external API. The observable contracts are the identity invariants below, checked
by the static audits and the asset/screenshot review in [quickstart.md](../quickstart.md).

## I1 — Asset Integrity (FR-001, FR-003, FR-005)

1. The built executable carries an icon; a machine that has never run the product sees it in the file
   system.
2. The committed asset carries exactly the documented frames: `16`, `20`, `24`, `32`, `40`, `48`, `64`,
   `256` px — no missing frame, no extra frame.
3. One container carries the identity; the asset is committed with the generator that produced it, so a
   reviewer can read both the shipped binary and the source of its geometry.

**Prohibited**: a single-size asset the shell must upscale; loose per-size image files standing in for the
identity; an asset that exists only as a build output with no committed source.

## I2 — Surface Coverage (FR-001, FR-002, FR-005, SC-001, SC-003)

1. Every surface listed in the data model's Surface table shows the mark: the application file, the pinned
   entry, the taskbar button, the Alt-Tab entry, and the shell's custom title bar.
2. No listed surface shows a generic placeholder.
3. The published build shows the same identity on a clean machine with no manual setup.

**Prohibited**: an identity only in the title bar; an identity only in the file system; any per-machine or
per-user step needed to see the mark.

## I3 — Legibility (FR-003, FR-004, SC-002)

1. The mark stays identifiable at the smallest shell size and renders cleanly at the largest.
2. The mark stays distinguishable over light and dark backgrounds, by its own contrast rather than the
   shell's.
3. Identity survives when colour is ignored: the tile and the silhouette carry it.

**Prohibited**: a transparent silhouette that disappears on one of the two shell backgrounds; a small-size
frame that is a downscale of the large one; identity conveyed by colour alone.

## I4 — Brand Consistency (FR-006, FR-008, SC-004)

1. One fixed brand colour, identical on every machine; the value is defined in exactly one place and
   recorded where the design system documents colour roles.
2. The mark is consistent with the product name (`Raggit`) and the client's existing glyph vocabulary.
3. No third-party, supplied-reference, or unrelated artwork is reproduced.

**Prohibited**: the operating-system accent as the icon's colour; a second brand colour invented elsewhere;
copying a supplied reference's logo; introducing imagery that requires network access or installed fonts.

## I5 — Behaviour Freeze (FR-007, SC-005)

1. Every automation identifier frozen by `022`/`023` remains present and unchanged.
2. Window titles are unchanged; the mark is added beside the product wordmark.
3. No change to behaviour, workflows, the shared behaviour layer, the workstation service, or any
   contract; the existing suites pass with zero assertion changes and the formatter gate stays clean.

**Prohibited**: renaming or removing an automation identifier; replacing the wordmark with the mark;
adding a dependency, endpoint, or setting in the name of the icon.
