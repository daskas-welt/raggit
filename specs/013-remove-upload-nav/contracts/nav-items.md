# UI Contract: Nav Pane Items (013)

The nav pane contains exactly five items in this order. No Upload entry exists for any role.

```xml
<NavigationViewItem Content="Library" Tag="library" Icon="Folder" />
<NavigationViewItem Content="Ask" Tag="query" Icon="Message" />
<NavigationViewItem Content="History" Tag="history" Icon="Clock" />
<NavigationViewItem Content="My Docs" Tag="mine" Icon="Page" />
<NavigationViewItem Content="Admin" Tag="admin" Icon="People" />
```

## Invariants

- `Nav_SelectionChanged` handles exactly the tags `library`, `query`, `history`, `mine`, `admin` (admin-gated); no `upload` case.
- `RefreshAdminVisibility` gates `AdminItem` only.
- Upload flow (header button → `UploadDialog` → refresh) is owned solely by `LibraryPage`; `MainWindow` holds no upload code.
