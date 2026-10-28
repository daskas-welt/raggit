# Bug Assessment: Page-level empty states are not styled consistently

- **Slug**: empty-state-consistency
- **Created**: 2026-10-27
- **Source**: pasted text (follow-up to the Dashboard empty-state fix in this session)
- **Verdict**: valid
- **Severity**: low

## Report (verbatim or summarized)

> Empty states should be styled the same

Context: the Dashboard's whole-page empty state was just changed (working tree, `DashboardPage.xaml`) to
a bounded, bordered card centered in the space below the metric cards. The same conceptual state on
Library, My Documents, History and Admin People still renders differently — some as a faint default
`ui:Card`, some as a bare panel with no border, background or padding at all.

Scope agreed with the reporter: **all page-level empty states**, not the nested in-card placeholders.

## Symptom

"No records" and "no matching …" empty states look different from page to page (and even within one
page): the Dashboard box has a visible 1 px border and is centered in the page, the others either rely on
the near-invisible default card stroke (`#0F000000`, ~6 % black) or show no card chrome at all, so their
text sits unpadded in an otherwise empty region.

Expected: every page-level empty state is the same surface — one card style, same padding, same visible
border, same centering — so an empty page reads as one deliberate pattern.

## Reproduction

1. Sign in as an admin against the workstation API.
2. Open **Dashboard** with an empty library: bordered, width-capped empty card centered below the metric cards.
3. Open **Document Library** with an empty library, then type a search that matches nothing: the first
   state is a faint default card; the "No matching documents" state is a bare panel with no chrome.
4. Repeat on **My Documents** and **History**: same two variants, both borderless when filtering.
5. Open **Admin → People** with no users / a filter that matches nobody: cards again, but with
   `Padding="24"`/`"20"` and 8 px/12 px gap mixes that differ from the other pages.

## Suspected Code Paths

- `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml:729` — whole-page empty state; already carries
  `MaxWidth="480"`, `Padding="{StaticResource CardPaddingLarge}"`,
  `BorderBrush="{ui:ThemeResource SurfaceStrokeColorDefaultBrush}"`, `BorderThickness="1"` and
  `BasedOn="{StaticResource {x:Type ui:Card}}"` (`:744`). This is the reference look.
- `src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml:363` — "No documents yet": plain `ui:Card`
  (implicit style, default 6 % stroke). `:423` — "No matching documents": local
  `<Style TargetType="ui:Card">` at `:431` **without** `BasedOn`, so the card template is lost.
- `src/RAGGit.Client.WPF/Views/Pages/DocumentsMinePage.xaml:193` and `:239` (local style at `:247`) —
  same pair.
- `src/RAGGit.Client.WPF/Views/Pages/HistoryPage.xaml:221` and `:267` (local style at `:275`) — same pair.
- `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml:478` — "No people yet" with inline
  `Padding="24"`; `:512` — "No matching people" with inline `Padding="20"`, 8 px gaps and
  `FontTypography="BodyStrong"` instead of the 24 px / 12 px / `Title` used elsewhere.
- `src/RAGGit.Client.WPF/App.xaml:41` — `EmptyStateGlyph` is the only shared empty-state resource;
  `:56` has `CardPaddingLarge`, `:63` has `PageHeaderCard`. There is no shared empty-state surface style.
- Related, out of scope: `DashboardPage.xaml:967` / `:1131` (recent documents/questions placeholders
  inside panel cards), `QueryDetailPage.xaml:250` ("No citations", inside the citations card),
  `ChatControl.xaml:250`, `Views/Dialogs/UploadDialog.xaml:362` ("No files yet").

## Root Cause Hypothesis

**Confidence: high.** WPF applies an implicit style (`<Style TargetType="ui:Card">` inside WPF-UI's
`ControlsDictionary`) only when the element has no local `Style`. WPF-UI's `Card` is a plain
`ContentControl` (no `DefaultStyleKey` override), so its template, background, padding handling and
card stroke all live in that implicit style. Any page that sets `<ui:Card.Style><Style
TargetType="ui:Card">…` to hold a visibility trigger silently replaces it, and the card degrades to the
default `ContentControl` template: **no `Border` in the visual tree, so `Padding`, `Background` and
`BorderBrush` are inert**. A probe against WPF-UI 4.3.0 confirmed it: local style → "no Border
(template=set)"; `BasedOn="{StaticResource {x:Type ui:Card}}"` → template border present with padding
applied. The same trap makes every "no match" state on Library / My Documents / History a bare panel.

The remaining differences are plain hard-coded values (Admin's 20/8/BodyStrong) and the Dashboard's new
explicit border, which no other surface shares.

A related finding of the same root cause: `App.xaml:63` `PageHeaderCard` is a keyed `ui:Card` style
without `BasedOn`, so every page header card is also template-less today (probe: "no Border").

## Proposed Remediation

**Preferred**: introduce one shared empty-state surface in `App.xaml` and point every page-level empty
state at it.

```xml
<Style
    x:Key="EmptyStateCard"
    TargetType="ui:Card"
    BasedOn="{StaticResource {x:Type ui:Card}}"
>
    <Setter Property="Padding" Value="{StaticResource CardPaddingLarge}" />
    <Setter Property="MaxWidth" Value="480" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="BorderBrush" Value="{ui:ThemeResource SurfaceStrokeColorDefaultBrush}" />
</Style>
```

- Cards that need no trigger (Dashboard whole-page, Library/My Documents/History "no records",
  Admin "no people") use `Style="{StaticResource EmptyStateCard}"`; keep their inline `Visibility`
  bindings as they are.
- Cards that need a visibility trigger (the three "no match" states) keep a local
  `<Style TargetType="ui:Card" BasedOn="{StaticResource EmptyStateCard}">` with only the trigger.
  A probe verified this chain (`BasedOn` a keyed `BasedOn` style) keeps template, padding and border.
- Normalise the Admin states onto the same tokens: drop inline `Padding="24"`/`"20"`, use 12 px content
  gaps and `Title` for the heading.
- Optionally remove the now-duplicated local setters in `DashboardPage.xaml` (padding, max width,
  border) so the Dashboard consumes the same style.

**Alternatives**:
- A custom `EmptyStateControl` (glyph + title + body + optional action) would remove the copy-pasted
  seven-row grids, but it is a larger change than the reported inconsistency and would need its own
  design/contracts; keep it as a follow-up if empty states keep growing.
- Wrapping each empty state in a `Grid` that owns the trigger instead of styling the `Card` leaves the
  markup inconsistent and keeps four copies of the trigger; rejected.

**Files likely to change**:
- `src/RAGGit.Client.WPF/App.xaml`
- `src/RAGGit.Client.WPF/Views/Pages/DashboardPage.xaml`
- `src/RAGGit.Client.WPF/Views/Pages/LibraryPage.xaml`
- `src/RAGGit.Client.WPF/Views/Pages/DocumentsMinePage.xaml`
- `src/RAGGit.Client.WPF/Views/Pages/HistoryPage.xaml`
- `src/RAGGit.Client.WPF/Views/Pages/AdminUsersPage.xaml`

**Tests to add or update**:
- No WPF UI test host exists (unit/contract/integration projects do not reference `RAGGit.Client.WPF`
  or `System.Windows`), so the mechanical regression guard is a text-level XAML test in
  `tests/unit` (same shape as `ClientLicenseIndependenceTests`): every `<Style TargetType="ui:Card">`
  in `Views/Pages/*.xaml` must declare `BasedOn` (or be replaced by the shared key), and every
  page-level empty-state card must reference `EmptyStateCard`.
- Manual UI check with `winapp ui` (or on screen) of all five pages in both empty variants; the
  `LibraryNoMatchState`, `DocumentsMineNoMatchState`, `HistoryNoMatchState`,
  `DashboardEmptyState` automation IDs already exist for this.

## Risks & Considerations

- Visible change on Library / My Documents / History / Admin: the no-match states gain a border,
  background and padding for the first time, and the "no records" cards get a stronger stroke
  (`SurfaceStroke #66757575` vs the default `#0F000000`). Intended, but it is the whole app's empty look.
- `MaxWidth="480"` in the shared style caps every page-level empty state; the Admin body text is short,
  but Library's non-admin body wraps, so verify at narrow widths (≤720 px responsive breakpoint).
- Custom probe used `SurfaceStrokeColorDefaultBrush`; if a softer look is preferred, the single style is
  the only place to change it.
- Leaving `PageHeaderCard` template-less is now a known, separate inconsistency; fixing it changes every
  page header's chrome and should be its own decision.
- No automated visual coverage: a broken `BasedOn` degrades silently (page still loads, box just
  disappears), which is how this bug arrived.

## Open Questions

- [NEEDS CLARIFICATION: should page headers (`PageHeaderCard`, same root cause) be fixed in the same
  change, or tracked separately?]
- [NEEDS CLARIFICATION: is 480 px the desired cap for all page-level empty states, or should Admin keep
  its narrower auto-sized card?]
- [NEEDS CLARIFICATION: should "no matching …" states keep their `Search24` glyph and add a "clear
  filters" action where the page has one, matching Admin's filtered state?]
