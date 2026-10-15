# UI Contracts: Reasoning Progress While Waiting (U1)

**Feature**: `032-reasoning-progress` | **Date**: 2026-10-15 | **Spec**: [spec.md](spec.md)

Behavioral contract for the one client surface this feature touches: the reasoning region on the Ask
surface. Each clause is observable without reading implementation. Where an earlier contract (022/023
design tokens, 025 Fluent system icons, 028/029 shared header/status/automation-ID freeze, WPF-UI control
policy) already governs a surface, it still governs and is not restated here.

Server contract for the same behavior: [api.yaml](api.yaml) (`POST /api/queries/stream`).

## U1 — Ask-surface reasoning region (FR-001…FR-014, US1–US3, SC-001, SC-002, SC-004)

Applies to the Ask (query) surface, in the conversation area where the static busy line sits today.

1. While a question is in flight, the region shows an ordered list of short, plain-language lines
   describing the work being done — reading the question, searching the library, the relevant sources
   found, and writing the answer. Lines appear as that work actually happens and are never reordered or
   rewritten.
2. Each new line appears before the previous one is replaced, so a question that takes several seconds
   shows **more than one distinct line over time** rather than one frozen message (SC-001). No artificial
   delay or minimum display time is introduced to achieve this.
3. The lines are rendered from the workstation's own text, so they are in the same language as the
   question (Greek question ⇒ Greek lines) without the client detecting language itself.
4. The region is **absent** once the answer or an error is presented: it is cleared before the answer
   bubble is added, so a finished exchange shows the answer and its citations exactly as today, with no
   residue (FR-009, US1.3, US3.4).
5. When a failure is reported (unreachable workstation, model unavailable offline, timeout, or a stream
   that ends without a result), the region is cleared and the employee sees the **same** status message
   as today; no "still working" line is left behind (FR-012, US2.5).
6. The region never claims work that did not happen: no "sources found" line when the answer is
   "no relevant content found", and no line for a stage that has not been reached (FR-005, US2.2).
7. No line exposes prompt text, chunk bodies beyond what the answer's own Sources view already shows,
   internal identifiers, model configuration, or another person's data (FR-007, US2.3, US2.4).
8. When a question is answered too quickly to show progress, the region simply never appears — no
   flicker, no contradictory flash, and no placeholder line that the server contradicts (FR-013).
9. The region is reachable by accessibility tooling: its container carries a stable
   `AutomationProperties.AutomationId` (`ReasoningProgressList`) and an accessible name, and its rows are
   exposed as readable text.
10. The region is composed from Fluent controls and existing tokens only — no stock chrome beyond the
    `ListBox` idiom already used by the chat, no `Opacity=` emphasis, no hard-coded colour, and no icon
    glyph that has not been verified to exist in `SymbolRegular`. It is legible in light, dark, and
    high-contrast themes.
11. No 022/023 automation ID is renamed, removed, or reassigned by this feature. The send button, its
    in-flight spinner, and the input box keep their current IDs and behavior.
12. The reasoning region is decorative-with-meaning, not interactive: it adds no control, no focus stop,
    and no keyboard target of its own.

**Replaces**: the static line "Searching the library and writing your answer…" is removed rather than
kept alongside the region — the region is the single, live indication of work in progress.

**Prohibited**: a client-side timer or pre-scripted sequence that advances lines without server events; a
progress *percentage* or ETA; a "sources found" line derived from anything other than actual retrieval; a
line that survives into the finished answer; a per-token or per-character answer preview (the answer
arrives once, complete); any indication of progress conveyed by colour alone.
