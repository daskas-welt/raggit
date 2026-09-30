# Tasks: Application Icon

**Input**: Design documents from `/specs/024-client-app-icon/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/, quickstart.md

**Tests**: No new test tasks. This feature adds an asset and two client edit points with no library logic to
test; correctness is proven by the identity audits and the asset/screenshot review in
[quickstart.md](quickstart.md), plus the existing suites with zero assertion changes — the approach
recorded in plan.md's Constitution Check (VI) and established by feature `023`.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- Single project layout: `src/RAGGit.Client.WPF/` (the desktop client) and `specs/024-client-app-icon/` (this feature).
- The identity lives with the client that ships it: `src/RAGGit.Client.WPF/Assets/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: The asset's home and the single definition of the mark's parameters.

- [X] T001 Create `src/RAGGit.Client.WPF/Assets/` and the generator script `src/RAGGit.Client.WPF/Assets/generate-app-icon.ps1` declaring, once at the top, the brand colour `#0F6CBD` and the frame set `16`, `20`, `24`, `32`, `40`, `48`, `64`, `256` px — per FR-008, plan structure decision

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The mark and the asset. Every user story needs these; nothing else may start first.

- [X] T002 Draw the mark's geometry in `src/RAGGit.Client.WPF/Assets/generate-app-icon.ps1`: a filled rounded-square tile in the brand colour `#0F6CBD` carrying a simplified document/library silhouette in white, derived from the client's `*24` icon vocabulary, with a reduced-detail variant for frames below 24 px (same tile, same colour, same silhouette) — per FR-002, FR-003, FR-004, FR-006
- [X] T003 Emit `src/RAGGit.Client.WPF/Assets/RaggitIcon.ico` from the generator with exactly the frames `16`, `20`, `24`, `32`, `40`, `48`, `64`, `256` px (no missing, no extra), 32-bit with alpha, the 256 px frame PNG-compressed and the smaller frames uncompressed, and commit the asset beside the generator — per FR-001, FR-003, FR-005

---

## Phase 3: User Story 1 - The application is recognisable in the Windows shell (Priority: P1) 🎯 MVP

**Goal**: The mark replaces the generic placeholder on every surface Windows shows the client on.

**Independent Test**: Launch the client and inspect the file system, the taskbar button, the Alt-Tab entry and the shell's custom title bar; confirm each shows the mark.

- [X] T004 [US1] Declare the executable icon and the asset resource in `src/RAGGit.Client.WPF/RAGGit.Client.WPF.csproj`: `ApplicationIcon` pointing at `Assets\RaggitIcon.ico` and a `Resource` item for the same file so the shell can reference it by pack URI — per FR-001, FR-005
- [X] T005 [US1] Set the window icon in `src/RAGGit.Client.WPF/Views/MainWindow.xaml` from the committed asset, leaving the window title text unchanged — per FR-001, FR-007
- [X] T006 [US1] Add the mark to the shell's custom title bar in `src/RAGGit.Client.WPF/Views/MainWindow.xaml` through the control's **nested** `Icon` element (`Wpf.Ui.Controls.IconElement`; a string attribute does not render — the trap AGENTS.md documents for buttons), sitting beside the existing product wordmark rather than replacing it — per FR-001, FR-002, FR-007
- [X] T007 [US1] Run audit A3 from `specs/024-client-app-icon/quickstart.md` against the built client and confirm the executable's associated icon is present **and** contains the brand colour `#0F6CBD` (a bare "has an icon" check passes with the placeholder that ships today) — per SC-001
- [X] T008 [US1] Run the client, capture the window and the screen (taskbar in frame) using the recipe and the forced-repaint rule in `specs/024-client-app-icon/quickstart.md`, and confirm the file, pinned, taskbar, Alt-Tab and title-bar surfaces all show the mark with no generic placeholder — per SC-001

---

## Phase 4: User Story 2 - The icon reads at every size and background the shell asks for (Priority: P2)

**Goal**: The mark is identifiable at the shell's smallest rendering, clean at the largest, and legible on
light and dark shells without relying on colour.

**Independent Test**: Generate the review sheet (every size over light and dark swatches), read it, and re-check the captured taskbar rendering.

- [X] T009 [US2] Add a review-sheet mode to `src/RAGGit.Client.WPF/Assets/generate-app-icon.ps1` that renders every frame over a light (`#F3F3F3`) and a dark (`#202020`) swatch to a caller-supplied path — per SC-002, contract I3
- [X] T010 [US2] Tune the small-size geometry in `src/RAGGit.Client.WPF/Assets/generate-app-icon.ps1` so the mark stays identifiable at 16 px (fewer interior details below 24 px, same tile, colour and silhouette) and regenerate `src/RAGGit.Client.WPF/Assets/RaggitIcon.ico` — per FR-003
- [X] T011 [US2] Review the sheet written to `%TEMP%\icon-review.png` against contract I3 and re-capture the taskbar/title-bar surfaces at that size, confirming the mark reads on both backgrounds and that the tile plus silhouette carry the identity when colour is ignored; record the outcome — per SC-002

---

## Phase 5: User Story 3 - The identity travels with the product (Priority: P3)

**Goal**: The published build shows the same identity on a machine that has never run the client.

**Independent Test**: Publish the client, re-run audit A3 against the published executable, and inspect its file icon.

- [X] T012 [US3] Publish the client (`dotnet publish src/RAGGit.Client.WPF -c Release -p:Platform=x64 -o ./out/desktop`) and re-run audit A3 against `./out/desktop/RAGGit.Client.WPF.exe`, confirming the published executable carries the brand colour — per FR-005, SC-003
- [X] T013 [US3] Confirm the file icon of `./out/desktop/RAGGit.Client.WPF.exe` matches the running application's and that no per-machine step is needed to see the identity; record the evidence — per SC-003
- [X] T014 [P] [US3] Note in `docs/publish.md`, under the desktop build steps, that the published executable carries the application identity — per FR-005

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Keep the reference set truthful and prove the behaviour freeze.

- [X] T015 Record the fixed brand colour in `specs/023-design-system-refinement/design-system.md` as a short, clearly scoped line: the application icon (feature 024) carries the product's first fixed brand colour `#0F6CBD`, owned by `specs/024-client-app-icon/`, deliberately not a theme token and applied to the icon only — per FR-008, contract I4
- [X] T016 [P] Run the gates in `specs/024-client-app-icon/quickstart.md` (`dotnet csharpier check .`, `dotnet build RAGGit.sln -c Release -p:Platform=x64`, and the unit/contract/integration suites) and confirm zero assertion changes — per SC-005, contract I5
- [X] T017 [P] Run the `023` design audits unchanged (`specs/023-design-system-refinement/quickstart.md`: colour literals, `Opacity=`, icon names, `ThemeResource` keys, frozen automation IDs) to prove the identity added no UI drift — per FR-007, contract I5
- [X] T018 [P] Run audits A2 and A4 from `specs/024-client-app-icon/quickstart.md` and confirm one brand colour defined once with nothing inlined on a surface, and that `src/RAGGit.Client.WPF/Assets/` holds only the asset and its generator — per SC-004, contract I4
- [X] T019 Write the Implementation Record and Caveats into `specs/024-client-app-icon/tasks.md`: what changed, the verification evidence (audits, review sheet, captures, published-build check), and any residual limitation — per FR-005, SC-003

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: needs T001; blocks every user story — the mark and the asset are what the stories apply and judge.
- **User Story 1 (Phase 3)**: needs T003. Delivers the MVP on its own.
- **User Story 2 (Phase 4)**: needs T003; its tuning (T010) rewrites the asset, so US3's verification must follow it.
- **User Story 3 (Phase 5)**: needs US1 (the executable icon) and the final asset from US2.
- **Polish (Phase 6)**: after the stories; T015 records the colour, T016–T018 verify, T019 records the outcome.

### Within each story

- US1: the project declaration (T004) → the window icon (T005) → the title-bar mark (T006, same file as T005) → the audit (T007) → the captures (T008).
- US2: the sheet (T009) → the tuning plus regeneration (T010, same file as T009) → the review (T011).
- US3: publish and audit (T012) → the file-icon confirmation (T013); the documentation note (T014) is independent.

### Notes for execution

- The identity's generator runs under either PowerShell 5.1 or PowerShell 7 (its audits use GDI+ only); the
  `023` reflection checks, by contrast, need `pwsh`.
- Audit A3 must assert the icon's **content**: the built executable already returns a default icon, so a
  presence-only check would pass with the placeholder still in place.
- The mark is applied beside the product wordmark; window titles and the 22+ frozen automation IDs do not
  change.
- Screenshots can return a stale frame for a surface captured immediately after a theme or navigation
  change — force a repaint, as the `023` guide records.

---

## Parallel Example: Phase 6

```text
# After the three stories are implemented, these three verifications are independent:
Task: "T016 Run the gates in specs/024-client-app-icon/quickstart.md"
Task: "T017 Run the 023 design audits unchanged to prove no UI drift"
Task: "T018 Run audits A2 and A4 to confirm one brand colour and a clean asset folder"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Setup (T001) → Foundational (T002, T003).
2. US1 (T004–T008): the identity appears in the shell and the placeholder is gone — this is the request
   itself ("use an app icon"), and it is demonstrable on its own.
3. Stop and validate US1 by its independent test before continuing.

### Incremental Delivery

1. US1 → the app is recognisable (MVP).
2. US2 → the mark holds up at taskbar size and on both shell backgrounds.
3. US3 → the identity survives publishing and is documented for distribution.
4. Polish → the reference set stays truthful (one recorded brand colour) and the behaviour freeze is proven.

---

## Implementation Record (2026-09-30)

All 19 tasks complete. The client now presents the product's own identity instead of the generic
placeholder, with no behaviour change.

### What changed

- **The mark** — `src/RAGGit.Client.WPF/Assets/generate-app-icon.ps1` defines it once: the fixed brand colour `#0F6CBD`, a rounded-square tile carrying a white page with a turned-back corner, the frame set `16 · 20 · 24 · 32 · 40 · 48 · 64 · 256`, and the rule that below 24 px the fold is dropped while the tile, colour and silhouette stay identical. Two colours only, at every size.
- **The asset** — `src/RAGGit.Client.WPF/Assets/RaggitIcon.ico` (45 KB, 8 frames: the 256 px frame PNG-compressed, the smaller frames uncompressed 32-bit with alpha), committed beside its generator.
- **The client** — `RAGGit.Client.WPF.csproj` declares the executable icon and embeds the asset as a resource; `Views/MainWindow.xaml` sets the window icon and gives the shell's custom title bar an `ImageIcon` through its **nested** `Icon` element (the attribute form renders nothing — the trap the contributor guide documents for buttons). The title-bar wordmark and every window title are unchanged.
- **The reference** — `specs/023-design-system-refinement/design-system.md` records the brand colour as the one colour outside the theme roles, scoped to the icon and never used on a UI surface; `docs/publish.md` notes that the published executable carries the identity.
- **The guide** — `quickstart.md` gained audit A5 (read the window's own small and large icons back) and refinements to A2 and the capture recipe.

### Verification

| Check | Result |
|-------|--------|
| A1 asset frames | ✅ exactly 8: 16/20/24/32/40/48/64/256 |
| A2 one brand colour, not inlined | ✅ defined once in the generator, 0 hits in `*.xaml`/`*.cs` |
| A3 executable icon | ✅ 32×32, 649 brand-colour pixels (the old placeholder shows none) |
| A4 asset inventory | ✅ the folder holds only `RaggitIcon.ico` and `generate-app-icon.ps1` |
| A5 window icons (taskbar/Alt-Tab source) | ✅ small 16×16 with 154 brand pixels, large 32×32 with 649 |
| Title bar | ✅ captured with the mark beside the wordmark (`…\Temp\opencode\converge-023\identity-window.png`) |
| Taskbar button | ✅ captured as the active entry of the vertical taskbar (`identity-taskbar-active.png`) |
| Published build | ✅ `out\desktop\RAGGit.Client.WPF.exe` gives the same 32×32 / 649 result |
| Legibility | ✅ review sheet over light `#F3F3F3` and dark `#202020` at every size and a 4× zoom of 16/20 |
| Gates | ✅ csharpier clean, release build 0 errors, unit 340/340, contract 86/86, integration 62/62 |
| `023` audits | ✅ 0 colour literals and 0 `Opacity=` in the client, all 22 frozen automation IDs present |

### Caveats

- **Alt-Tab** was verified through the icon the shell draws from (A5: the window's large icon) rather than as a switcher screenshot, which cannot be captured without holding a key down.
- **The taskbar on the verifying machine is vertical at the right edge**, so the strip capture targeted that edge; the guide now says to capture the strip where the taskbar actually is.
- **One integration run failed** (61/62) before passing twice consecutively; the failing test's name was not captured. The suite references no client project, so the identity cannot be its cause — the same transient seen during feature `023`.
- **High Contrast** is a shell-theme concern rather than an icon one: the mark carries its own contrast inside the tile and does not depend on the shell's mode.
- **`Window.Icon` frame selection** was an open risk (WPF resizing a large frame for the taskbar): A5 settles it — the shell receives a true 16×16 icon, i.e. the small hand-tuned frame, not a downscale.

---

## Phase 7: Convergence

- [X] T020 Give the application's second window the same identity, and make the model match the code: `src/RAGGit.Client.WPF/Views/Dialogs/UploadDialog.xaml` is a plain `Window` (`Title="Upload Documents"`, centre-owner, default chrome) with no `Icon`, so its native title bar carries no mark while `specs/024-client-app-icon/data-model.md`'s Surface table claims dialogs and owned windows are "inherited from the window" — WPF does not inherit `Window.Icon`, so that row describes something the code does not do. Set the same pack-URI icon on that window as the shell uses and correct the Surface row's wording to what the code actually does — per FR-001 (partial)
- [X] T021 Capture the Alt-Tab switcher with the client's entry in frame and record the PNG: US1's independent test asks to inspect the Alt-Tab entry, which is currently proven only through the icon the switcher renders from (`quickstart.md` audit A5 reads the window's large 32×32 icon) — drive Alt (key down) and Tab with `SendInput`, capture while the switcher is held open, then release — per US1/AC2 (partial)
### Convergence follow-up (2026-09-30, T020/T021)

- **T020** — `src/RAGGit.Client.WPF/Views/Dialogs/UploadDialog.xaml` now sets the same pack-URI icon as the
  shell, so the upload window's native title bar carries the mark
  (`…\Temp\opencode\converge-023\identity-upload-dialog.png`), and the Surface table in `data-model.md`
  states what the code does instead of claiming `Window.Icon` is inherited.
- **T021** — the Alt-Tab switcher is captured with the client's entry in frame and the mark on it
  (`identity-alt-tab.png`), closing the caveat above about Alt-Tab being proven only through the icon the
  switcher consumes.
- Gates re-run after both: csharpier clean, build 0 errors, unit 340/340, contract 86/86, integration
  62/62; the asset audits are unchanged (8 frames; the folder still holds only the asset and its generator).

### Review follow-up (2026-09-30)

The four findings from the review of this change set, all addressed:

- **The guide's justification for the content-based check was time-bound** — `quickstart.md` now gives the timeless reason (an executable carrying the framework's placeholder icon would pass a presence-only check).
- **No audit decoded the 256 px frame** — A1 now decodes it and re-runs the generator, asserting the committed asset is byte-for-byte what the script produces.
- **`biSizeImage` included the AND mask** — `ConvertTo-DibBytes` now writes the XOR image size (1024 / 4096 / 16384 at 16 / 32 / 64 px), leaving the mask offset unambiguous.
- **The largest entry carried non-zero plane and bit-count fields** — the PNG entry now writes the conventional zero for both.

Re-verified after the change: the asset still holds exactly the 8 frames; the executable's icon is 32×32 with 649 brand-colour pixels; the window's own icons are 16×16 with 154 and 32×32 with 649; the build is warning-free; and the asset stays reproducible across `pwsh` 7 and Windows PowerShell 5.1 (identical SHA256 for all three copies).

A fifth item was outside this feature: the `023` capture-pitfall note attributed ≈150 KB to a 1200×800 capture when that figure came from the 2514×1456 captures. Corrected in `specs/023-design-system-refinement/quickstart.md` in the same pass.
