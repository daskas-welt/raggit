# UI Contracts: Ask-View Document Output

**Feature**: `033-ask-document-generation` | **Date**: 2026-11-16

The desktop remains a thin client: it renders the answer the workstation returns and asks the workstation to render a file. It performs no AI work and opens no document library.

## Ask surface

### Asking for a document

- The employee types an ordinary request that names one supported format (Word/docx, Excel/xlsx/spreadsheet, PDF, text/txt) and a document-creation verb (create, make, write, generate, export, save, put/list/table, or the Greek equivalents).
- The question is sent exactly as typed. The answer is displayed exactly as today, with citations.
- After the terminal answer arrives:
  - **One supported format detected and the answer is eligible** → the client requests that file from the workstation and opens the OS save dialog with the suggested filename.
  - **No format detected** → no file; the answer is unchanged (FR-007).
  - **More than one distinct format detected** → no automatic file; the answer remains, and the per-message "Save as" menu is available. No format is silently chosen.
  - **An unsupported format is detected** (PowerPoint/presentation/image/email) → no file; an InfoBar states the four supported formats while the answer remains visible (FR-024).
  - **The answer is exactly `no relevant content found`** → no file and no save action (FR-018).
  - **The answer has no citations** → no save action; the document cannot list sources (FR-019).
- If the employee cancels the save dialog, no file is written and the conversation is unchanged (FR-013).
- A second save request while one is in flight is ignored; the save controls are disabled.

### Any finished answer

- Every assistant message with a query id and at least one citation shows a **Save as** action; the same applies to answers reopened from History.
- The action opens a flyout/menu with exactly four entries: **Word**, **Excel**, **PDF**, **Text**.
- Choosing an entry requests that format, then opens the OS save dialog with the suggested filename. The employee may rename or choose any folder; overwrite confirmation is the OS dialog's responsibility.
- After a successful save, the message shows a **Saved** state with the filename and an **Open** action that launches the saved file with the OS handler.
- Refusal answers show no Save as action.

### Errors and states

| State | Presentation |
|-------|--------------|
| Requesting a document | Inline progress ring on the message; save controls disabled |
| Saving | Save controls disabled; progress ring remains until the dialog closes |
| Save cancelled | No error; state returns to idle |
| Query not owned / missing | InfoBar: "This answer is no longer available." |
| Refusal or no citations | No action; if forced, InfoBar: "This answer has no sources to document." |
| Too large | InfoBar: "This document is too large. Narrow the request and try again." |
| Model/workstation unreachable | Same vocabulary as today: "cannot reach AI workstation" / "AI workstation unavailable" |
| Renderer failure | InfoBar: "Document generation failed. No file was saved." |
| Unsupported format | InfoBar lists Word, Excel, PDF and text; no file |

No partial file is left after a failure or cancellation. The client never writes a temp file for a generated document; the only write is the employee's chosen path.

## Layout and controls

- Use WPF-UI (Fluent) controls only: `ui:Button` with `ui:SymbolIcon`, `ui:Flyout`/menu, `ui:InfoBar`, `ui:ProgressRing`, `ui:TextBlock`.
- Keep the client margin-free: space the save row with explicit `Grid` rows/columns and gutter tracks, as the Dashboard page does.
- The save action must be keyboard reachable and have an accessible name; do not rely on colour or opacity alone.

## Automation IDs

Add these IDs without changing the 22 frozen IDs from features 022/023:

| Element | AutomationId |
|---------|--------------|
| Per-message Save as button | `SaveAsButton_{AutomationKey}` |
| Save as flyout/menu | `SaveAsMenu_{AutomationKey}` |
| Word entry | `SaveAsDocx_{AutomationKey}` |
| Excel entry | `SaveAsXlsx_{AutomationKey}` |
| PDF entry | `SaveAsPdf_{AutomationKey}` |
| Text entry | `SaveAsTxt_{AutomationKey}` |
| Saved-state Open action | `SavedDocumentOpen_{AutomationKey}` |
| Document progress ring | `DocumentBusyRing_{AutomationKey}` |
| Document error/status InfoBar | `DocumentStatusInfoBar` |
| Ask sending button (existing) | `SendQuestionButton` |

`AutomationKey` is the existing per-message key in `ChatMessage`; IDs are stable for the life of the message.

## History and conversation cache

- History messages carry the persisted query id and use the same Save as action.
- The **Query Detail** page (an answer reopened from History) shows the same Save as menu using `QueryDetail.Id`; the resulting file matches the displayed answer and sources.
- The existing `ConversationStore` keeps messages across page navigation; `QueryId` and the last saved path travel with the message.
- A new login clears the conversation and saved paths as today (single-tenant, per-user isolation).
- Nothing about the generated file is added to History, the library, or any server-side store.
