# Contract: Admin Page Surface

**Feature**: `016-admin-redesign` | **Date**: 2026-09-18 | **Plan**: [plan.md](../plan.md)

No HTTP contract change: user-management endpoints, validation rules, and RBAC are untouched. This contract documents the **added/changed client surface** only.

Namespace note: shared types keep the 006 convention (`RAGGit.Client.Maui.*`).

---

## ViewModel additions (admin ViewModel, `RAGGit.Client.Core`)

```csharp
// New presentation state bound by AdminUsersPage:
// string? StatusMessage { get; }    // InfoBar line
// string StatusSeverity { get; }    // Informational/Success/Warning/Error
// bool HasStatus { get; }           // drives InfoBar.IsOpen
// void ClearStatus()/SetSuccess(...)/SetWarning(...)/SetError(...)
//
// Compat: string? ErrorMessage (existing) always matches StatusMessage
// on Warning/Error — pre-existing assertions hold unchanged.
//
// Behavior: every command (load/create/role/active/reset) sets a success
// confirmation; validation failures name the specific field; entered form
// values are preserved on failure; server policy messages surface verbatim.
```

**Rule (FR-006/FR-008)**: zero silent outcomes; action buttons gated while `IsBusy`.

---

## Page (`RAGGit.Client.WinUI.Views.AdminUsersPage`)

```text
AdminUsersPage(Page, admin-gated)
├── Layout: full-width root Grid with page padding, rows Header / InfoBar / List(*) / Forms(Auto)
├── Header: title + Refresh Button (SymbolIcon Refresh + text, AutomationId RefreshUsersButton)
├── Status InfoBar ← StatusMessage + StatusSeverity→InfoBarSeverity
│     (StatusSeverityConverter), IsClosable=False, LiveSetting Polite,
│     AutomationId AdminStatusBar
├── Users card: virtualized ListView (MinHeight=240, own scroll, empty state)
│   └── Row: username/displayname + role text Button ("Admin"/"Employee",
│       announces "Change role for {user}, currently…") + active text Button
│       ("Activate"/"Deactivate {user}") + lock FontIcon (visible only when
│       locked) + "Locked"/"Not locked" text; ProgressRing overlay while busy
├── Forms (own capped scroller, MaxHeight=380): Create Person card + Reset
│     Password card (Border cards; Description hints; PropertyChanged bindings;
│     44px buttons gated on IsBusy; reset keeps its ContentDialog confirms)
└── Narrow ≤720px: VisualStateManager/AdaptiveTrigger stacks form cards
```

**Rules**: `{ThemeResource}` brushes only; no `ScrollViewer` wrapping the list; lock stays display-only; existing `ContentDialog` confirms untouched; 401 mid-action follows `SessionExpiryNavigator` unchanged.

---

## Converters (`ViewConverters.cs`)

```text
RoleActionLabel / ActiveVerb / ActiveToggleLabel / LockedLabel / LockStatusLabel
    — per-row display strings (role text, activate/deactivate verb, lock text)
InverseBool — busy-gating helper
SelectedUserLabel — always returns a line (selection or "select a user" hint)
```

**Rule**: unused glyph/null converters left in place (no gratuitous deletion).
