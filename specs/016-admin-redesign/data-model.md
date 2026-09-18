# Data Model: Admin Page

**Feature**: `016-admin-redesign` | **Date**: 2026-09-18 | **Plan**: [plan.md](plan.md)

No persistence, no schema migration, no server change. User accounts are server-owned (`UserAccountDto`); this page adds transient presentation state only.

## UserAccountDto (existing, server-owned — read-only here)

| Field | Meaning |
|-------|---------|
| Username | Unique login name |
| DisplayName | Shown in the users list |
| Role | `Admin` / `Employee` |
| IsActive | Account enabled flag |
| LockedOut | Lock display flag (display-only on this page) |

Validation rules and last-admin safeguards (if any) are server decisions surfaced verbatim; the page never invents policy.

## Admin presentation state (added to the existing admin ViewModel)

| Field | Type | Meaning |
|-------|------|---------|
| StatusMessage | string? | Summary outcome line for the `InfoBar` |
| StatusSeverity | string | `Informational` / `Success` / `Warning` / `Error` → `InfoBarSeverity` via `StatusSeverityConverter`; string so Core stays UI-framework-free |
| HasStatus | bool (derived) | Drives `InfoBar.IsOpen` |
| ErrorMessage | string? (existing, compat) | Always matches `StatusMessage` on Warning/Error; all pre-existing assertions hold |
| SelectedUser | existing | Drives the reset form target + selection hint label |
| IsBusy | existing | Drives the `ProgressRing` overlay + busy-gating of action buttons |

Helpers: `ClearStatus()` / `SetSuccess(...)` / `SetWarning(...)` / `SetError(...)` — every command sets a success confirmation; validation failures name the specific field; entered form values are preserved on failure.

## Create-person draft (existing fields, unchanged rules)

| Field | Rule |
|-------|------|
| NewUsername | Required, unique (uniqueness enforced server-side; conflict surfaces the server message) |
| NewDisplayName | Required |
| NewRole | `Admin` / `Employee` selection |
| NewPassword | Initial password, required |

## Reset-password draft (existing fields, unchanged rules)

| Field | Rule |
|-------|------|
| SelectedUser | Required — action explains itself when nothing is selected |
| ResetPassword | New password, required |
| ResetMustChangePassword | Must-change flag |

## State transitions

```text
Idle → Busy (command executes; buttons gated) → Success (confirmation + list refresh)
                                              → Failure (reason + next step; form values kept)
```

Destructive/security-sensitive actions (role change, password reset) pass through the existing `ContentDialog` confirms before executing — unchanged.
