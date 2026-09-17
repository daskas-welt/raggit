# Data Model: Remove Upload Nav Item (013)

No persistent data. The "model" is the nav item inventory:

## Nav pane items (post-change)

| Tag | Page | Visible to |
|---|---|---|
| `library` | `LibraryPage` | All roles |
| `query` | `QueryPage` | All roles |
| `history` | `HistoryPage` | All roles |
| `mine` | `DocumentsMinePage` | All roles |
| `admin` | `AdminUsersPage` | Admin only |

- Removed: `upload` tag (dialog opener) — no page type ever existed for it.
- Validation rule: exactly these five items in this order; no sixth entry for any role.
- Upload entry point (unchanged): Library header `Upload` button → `UploadDialog` → list refresh; admin-only gating unchanged.
