# Data Model: Admin People Management Redesign

**Feature**: `026-admin-people-redesign` | **Date**: 2026-10-01 | **Plan**: [plan.md](plan.md)

No schema or user DTO fields are added. The feature uses existing account fields plus transient view
state and a repository update outcome.

## Existing entity: User account

| Field | Existing source | Use in redesigned surface |
|-------|-----------------|---------------------------|
| `Id` | `UserAccountDto` | Stable row, detail, and reset target identity |
| `Username` | `UserAccountDto` | Primary identity; search and sort key |
| `DisplayName` | `UserAccountDto` | Secondary identity; search and sort key |
| `Role` | `UserRole` (`Admin`, `Employee`) | Row dropdown and detail value |
| `IsActive` | `UserAccountDto` | Active/Disabled status and separate action |
| `LockedOut` | `UserAccountDto` | Locked chip only when true; read-only |
| `CreatedAt` | `UserAccountDto` | Detail metadata |
| `LastSignInAt` | `UserAccountDto?` | Detail metadata, with an explicit never/unknown state when null |
| `MustChangePassword` | Existing reset request | Existing reset dialog checkbox |

Password hashes remain server-only and are never present in the client model.

## Entity: Directory view state (transient)

Owned by the page, not persisted and not included in API requests.

| Field | Values / rule |
|-------|---------------|
| Search text | Case-insensitive substring match over username or display name |
| Role filter | All, Admin, Employee |
| Active filter | All, Active (`IsActive=true`), Disabled (`IsActive=false`) |
| Sort | Visible column and direction; default Username ascending |
| Selected account | Existing account object keyed by `Id`; cleared if filtering removes it |
| Layout mode | Wide master-detail or compact stacked, derived from content width |

## Entity: Create-person draft (transient)

Existing fields and server validation remain authoritative.

| Field | Existing validation |
|-------|---------------------|
| Username | Trimmed length 3–64; case-insensitive uniqueness |
| Display name | Required; trimmed length 1–100 |
| Role | Admin or Employee |
| Initial password | At least 10 characters |

On error the dialog retains values; on success it closes and the returned account enters the directory.

## Entity: Password-reset draft (transient)

| Field | Existing validation / behavior |
|-------|-------------------------------|
| Target account | Captured by `Id` when dialog opens; shown by username/display name |
| New password | At least 10 characters |
| Must-change flag | Existing boolean request behavior |

## Entity: User patch outcome (transient)

Returned by the atomic user repository patch operation; not persisted.

| Outcome | Meaning | Controller response |
|---------|---------|---------------------|
| Updated | Patch persisted; updated User returned | 200 with existing User DTO |
| NotFound | Target no longer exists | 404 |
| LastActiveAdmin | Patch would remove the final persisted active Admin | 409 with standard Error body |

## State transitions

- Row selected → detail panel shows that row's `Id` account.
- Role ComboBox selection → confirmation → cancel resets selection / confirm sends explicit target role → server response updates or restores row.
- Active action → confirmation → cancel leaves state / confirm submits inverse active flag → response updates row or displays server refusal.
- Search/filter excludes selected row → selection clears and detail actions become unavailable.
- Create/reset draft → cancel closes without request / failure retains draft / success refreshes and closes.
- Concurrent last-admin changes are serialized with the persisted check/update; at least one active Admin remains.
