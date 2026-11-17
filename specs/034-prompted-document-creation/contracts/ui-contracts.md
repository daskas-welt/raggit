# UI Contracts: Description-Based Document Creation

**Feature**: `034-prompted-document-creation` | **Date**: 2026-11-17

The WPF client is a thin client. It sends a description and selected format to the workstation, then saves returned bytes only through the employee's OS save dialog.

## Entry point and page

- Add **Create Document** as a navigation item visible to signed-in Employees and Admins; keep Ask and its answer-export controls unchanged.
- The page contains a multiline description input, a format selector with exactly **Word (DOCX)**, **Excel (XLSX)**, **PDF**, and **Text (TXT)**, a **Create document** action, and a status/progress region.
- No format is selected initially. The create action is disabled until the description is nonblank and exactly one format is selected.
- The format selector is explicit; the page does not infer a format from prose.
- The first version does not offer individual source-file selection. The workstation retrieves relevant library material from the description. The resulting file lists sources used, or states that no RAGGit sources were used.

## States and actions

| State | Presentation |
|-------|--------------|
| Incomplete request | Create action disabled; missing description/format is identified |
| Generating | Progress indicator and Cancel action; form submission disabled |
| Saving | OS save dialog shown with suggested filename and correct format filter |
| Save cancelled | No error; no file written; page returns to idle |
| Saved | Show saved filename and an Open action using the OS handler |
| No library sources used | The generated file contains a clear no-source notice |
| Unsupported format / invalid request | Explain the four supported formats or missing/overlong description; no file offered |
| Local model/workstation unavailable | Existing user-safe workstation error vocabulary; no file offered |
| Too large / generation or renderer failure | Clear message; no partial file |

- Only one generation/save operation can run at a time.
- Cancel or page navigation cancels the in-flight request. No old bytes or saved path are restored on a new page/login session.
- Existing-file overwrite confirmation is provided by the standard OS save dialog.

## Layout and accessibility

- Use WPF-UI Fluent controls where available (`ui:Button`, `ui:TextBlock`, `ui:InfoBar`, `ui:ProgressRing`). Use the standard WPF `TextBox` and `ComboBox` for text and format input because WPF-UI does not provide replacement controls for these; the application theme supplies their Fluent styling.
- Keep the page margin-free and space content with explicit Grid tracks and gutter tracks.
- All interactive controls are keyboard reachable, have accessible names, and use stable `AutomationProperties.AutomationId` values.
- The page must load with the real application resources and produce no XAML parse or binding errors in the client screen tests.
- Use theme tokens for all colors; support Light, Dark, and High Contrast.

## Automation IDs

| Element | AutomationId |
|---------|--------------|
| Description input | `DocumentCreationDescription` |
| Format selector | `DocumentCreationFormat` |
| Create action | `CreateDocumentButton` |
| Cancel action | `CancelDocumentCreationButton` |
| Progress ring | `DocumentCreationProgressRing` |
| Status InfoBar | `DocumentCreationStatusInfoBar` |
| Saved-file Open action | `OpenCreatedDocumentButton` |
| Navigation item | `NavCreateDocument` |

## Non-effects

- Do not add the prompt, generated document, or source list to Ask conversation/history, My Documents, the shared library, or the workstation content store.
- Do not change any of the frozen 022/023 automation IDs or 033 Ask/Query Detail export behavior.
