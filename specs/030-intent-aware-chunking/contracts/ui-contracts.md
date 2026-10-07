# UI Contracts: Intent-Aware Chunking (U1)

**Feature**: `030-intent-aware-chunking` | **Date**: 2026-10-07 | **Spec**: [spec.md](spec.md)

Behavioral contract for the one client surface this feature adds: the intent-mode selector on the
Ask surface. Each clause is observable without reading implementation. Where an earlier contract
(023 design tokens, 028/029 shared header/status/automation-ID freeze, WPF-UI control policy) already
governs a surface, it still governs and is not restated here.

Server contract for the same behaviour: [api.yaml](api.yaml) (`POST /api/queries` `mode`).

## U1 — Ask surface intent-mode selector (FR-007, US3, SC-004)

Applies to the Ask (query) surface.

1. The Ask surface shows a three-way selector with the options **Auto**, **Broad**, and **Specific**,
   labelled so the choice's meaning is visible without a tooltip. Its default, and the state after
   every app restart, is **Auto**.
2. When the user asks a question, the selected mode is sent with the request. **Auto** lets the
   server classify; **Broad** forces broad-context (synthesis/comparison) retrieval; **Specific**
   forces granular (fact) retrieval.
3. The selector's state is retained while the user stays on the Ask surface and across questions
   within the session; it resets to **Auto** on a fresh app start. Changing the mode does not modify
   or resend an already-answered question — the user asks again to apply a new mode.
4. The control carries a stable `AutomationProperties.AutomationId` (frozen once introduced) and an
   accessible name; it is reachable and operable by keyboard alone.
5. The control is composed from Fluent controls consistent with the client's existing selector
   pattern (the `PageSizeCombo` `ComboBox` idiom); no stock chrome beyond that idiom, no hard-coded
   colour, and no `Opacity=` emphasis. Legible in light, dark, and high-contrast themes.
6. Selecting a mode never changes the grounding guarantee: an answer is still citation-grounded, or
   exactly "no relevant content found". The selector adds no new answer state.

**Optional (non-blocking)**: the answer may show which mode was used (the server echoes the
effective intent); if shown, it is a caption, never the only indication of a result's scope, and it
does not alter the citation rendering.

**Prohibited**: a selector that requires a choice before the user can ask (Auto must always be
valid); a mode that silently changes or resends a prior question; a control that is discoverable
only by hover; any colour-only indication of the selected mode.
