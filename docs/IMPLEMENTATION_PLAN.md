# Execution Continuity Saver Implementation Plan

> **For implementers:** execute this plan with test-first state transitions. This document is a roadmap, not a commitment to a specific framework.

**Goal:** Build a Windows-local tool that preserves a prepared route, restores an exact return anchor, and captures interrupting thoughts without hijacking execution.

**Architecture:** Separate deterministic domain state and local persistence from the desktop presentation layer. The shell later renders a single guide action from the active route and delegates planning, capture, pause, stuck, and route-switching transitions to the domain layer.

**Tech Stack:** To be confirmed. Candidate desktop stacks and persistence options appear below; no candidate is a decided dependency.

---

## 1. Decision Status

### Confirmed Product Decisions

- The application is local-first and offline-capable through MVP-2.
- Many unfinished routes, projects, paused plans, inbox entries, and archives may be retained; `activeRouteId` is the sole source of truth for the one foreground route. Other previously started unfinished routes remain paused with retained return anchors. When `activeRouteId` is null, no route is active and no-active-route is a valid required state.
- Guide mode shows the active route's single next executable action.
- Captures are durable raw text plus timestamp and never hijack route state.
- Pauses persist a complete return anchor; the note is optional.
- Restart recovery is determined only by the nullable active route and that route's newest valid snapshot, never by a newer non-active route snapshot.
- Completion clears the active route and never auto-selects another route.
- Route choice and activation occur in planning mode and snapshot the old route first. Guide never lists routes or activates a replacement directly.
- Guide provides `Pause and choose another route`: it saves the complete return anchor before entering Planning, focuses paused routes, and leaves the original route active until the user explicitly selects a replacement.
- The Routes destination provides `By status` and `By project` grouping views. The first visit defaults to status; later visits restore the last selection. View changes never modify execution state.
- Each route belongs to at most one optional project. Project assignment is organizational only and never changes execution or recovery state.
- Paused route rows use progressive disclosure: a compact recognition summary by default and one read-only expansion level for complete return context.
- Capture occupies a stable bottom action bar across core states and opens a right drawer at desktop widths or a full-width panel in narrow windows. Save and cancel restore the exact page context; failed saves retain entered text.
- Inbox editing preserves immutable raw capture text and timestamp while exposing a separate editable organized version. Conversion and future AI assistance never overwrite the original record.
- Inbox uses list-and-detail at standard desktop widths and list-to-detail navigation at narrow widths, preserving list position and selection context.
- Guide and Planning use one fixed segmented control with a clear selected state. Planning-only navigation is hidden in Guide, and changing presentation mode never changes route execution state.
- Planning entry is context-sensitive only to the explicit entry point: ordinary entry restores Planning context, pause-and-choose focuses paused routes, and no-active entry opens Routes without automatic activation.
- With no active route, Planning prioritizes recently paused routes for explicit continuation and keeps route creation secondary; it never preselects or automatically resumes a route.
- Routes uses a grouped list and read-only-by-default detail workspace at desktop widths, with explicit editing and list-to-detail behavior at narrow widths. Destructive actions remain secondary and protected.
- Guide uses a constrained single-column focus layout with one dominant action, visible completion context, clear action hierarchy, and fallback replacement rather than simultaneous alternatives.
- Non-final step completion immediately advances after durable success and uses a short, restrained confirmation without interstitials, celebration, sound, or an extra continue command.
- Language follows Windows on first launch and supports remembered Settings-only overrides for Simplified Chinese and English. UI localization never rewrites user content or execution state.
- Planning destinations provide lightweight local search and limited explicit filters. Search preserves grouping, keeps Archive separate, and never infers priority or changes content state.
- Archiving is reversible; implicit deletion is prohibited and explicit deletion requires user confirmation.
- Planning edits cannot silently invalidate the active route's newest valid snapshot; an explicit deletion retains history and requires explicit recovery-path resolution.
- AI, if added, is user-triggered MVP-3 only and is not a prerequisite for MVP-0 through MVP-2.

### Confirmed Technical Decisions

- Local persistence is SQLite through `Microsoft.Data.Sqlite.Core` and `SQLitePCLRaw.bundle_winsqlite3`; the UI project references only the Persistence project and does not add a separate SQLite provider.
- The desktop shell is an unpackaged local-debug WinUI 3 application on .NET 10. MSIX remains deferred to MVP-2.

### Proposed Design

- Use a framework-independent domain state model for routes, steps, lifecycle state, captures, snapshots, and transitions.
- Use atomic, verified durable writes so success UI appears only after local storage confirms success. The approved storage choice may provide this through database transactions or atomic-file replacement.
- Keep guide mode intentionally narrow: one action plus capture, pause, complete, and stuck controls.
- Preserve inbox entries until the user converts, archives, or otherwise explicitly changes them in planning mode.
- Make paused routes easy to inspect and resume from their exact retained positions in planning mode.

### Open Technical Questions

| Decision | Candidates | Reason it remains open |
| --- | --- | --- |
| Packaging | MSIX; non-packaged installer | Distribution, signing, startup integration, and update policy are not specified. |
| Tray implementation | Framework-specific tray library; Win32 interop | Availability and maintenance depend on the chosen desktop stack. |
| Global shortcut | Win32 `RegisterHotKey`; framework abstraction | The shortcut must restore the current guide state, but implementation depends on the stack. |
| Login startup | User-controlled startup registration; no startup in early releases | The product requires consent and reversibility; packaging affects the implementation. |

## 2. MVP Delivery Phases

### MVP-0: Domain, Persistence, and Recovery

1. Define route persistence data, nullable application execution state (`activeRouteId`, `currentStepId`), immutable historical snapshots, captures, archives, and lifecycle transitions.
2. Write failing domain tests for the bidirectional `activeRouteId`/active-lifecycle invariant, final-step completion, snapshot completeness, capture with and without a snapshot, active-route-only recovery, fallback return semantics, protected snapshot references during planning edits, and planning-only route switching.
3. Implement the smallest local persistence adapter behind a storage interface, after resolving the SQLite-versus-JSON question.
4. Add integration tests that close/recreate the application state and verify capture and snapshot recovery.
5. Do not build a desktop UI, tray integration, or AI dependency before these state and recovery guarantees pass.

### MVP-1: Planning and Guided Execution

1. Add planning surfaces for creating/editing routes, ordered steps, completion standards, `do not do` boundaries, fallback actions, paused-route inspection/resume, and inbox conversion/archive.
2. Add guide mode that derives only the active route's current action.
3. Add durable capture in both active-route and no-active-route guide states, pause/resume, stuck/fallback, no-fallback single-sentence block, final route completion, and planning-owned route choice and activation.
4. Add UI and manual acceptance coverage for AC-01 through AC-28, excluding MVP-2 shell behavior.

### MVP-2: Windows Presence

1. Add the selected tray implementation that restores the current guide state.
2. Add the selected global shortcut implementation; it must not choose or switch routes.
3. Add optional, explicitly enabled, reversible login startup.
4. Validate AC-10 on supported Windows environments and repeat offline core-flow tests.

### MVP-3: User-Triggered AI Decomposition

1. Provide an explicit user command to request optional AI assistance while planning.
2. Keep output reviewable and non-automatic; it cannot alter the active route without explicit planning-mode confirmation.
3. Keep all earlier phases functional when the AI feature is disabled, unavailable, or offline.

## 3. Acceptance-to-Test Mapping

| Acceptance criteria | Required evidence |
| --- | --- |
| AC-01, AC-02 | Domain active-route invariant/selector tests; manual guide inspection |
| AC-03, AC-04 | Atomic, verified durable-write and restart integration tests |
| AC-05 | Snapshot-field unit test and restart restoration integration test |
| AC-06 | Stuck branch and fallback-return tests; UI/manual assertion of reduced choices |
| AC-07 | Final-step completion transition and retained-history tests |
| AC-08 | Switch-order persistence test and UI authorization test |
| AC-09 | Protected snapshot-reference domain, persistence, and manual planning tests |
| AC-10 | Dependency inspection plus offline workflow validation |
| AC-11 | Windows tray/shortcut integration and manual validation |
| AC-12 | No-active-route capture invariant, restart, and UI/manual validation |
| AC-13 | Paused-route visibility UI test and exact-anchor restoration evidence |
| AC-14 | Guide-to-Planning transition, cancel path, and failed-anchor-write evidence |
| AC-15 | Route-grouping preference UI test and proof that regrouping leaves domain execution state unchanged |
| AC-16 | Single optional project membership and project-operation state-isolation evidence |
| AC-17 | Collapsed/expanded paused-route UI coverage and proof that disclosure leaves execution state unchanged |
| AC-18 | Cross-state Capture placement, responsive surface, context restoration, and failed-save retention evidence |
| AC-19 | Immutable raw capture, editable organized version, conversion retention, and original-view UI evidence |
| AC-20 | Responsive Inbox list/detail behavior, scroll and selection restoration, and empty-state evidence |
| AC-21 | Stable Guide/Planning segmented navigation, visibility boundaries, and mode-switch state-isolation evidence |
| AC-22 | First/return Planning destination, pause-and-choose override, no-active entry, and no-auto-activation evidence |
| AC-23 | No-active continuation hierarchy, recent-pause ordering, empty state, and no-preselection evidence |
| AC-24 | Responsive Routes list/detail behavior, explicit edit state, context restoration, and protected management actions |
| AC-25 | Guide visual hierarchy, responsive focus layout, fallback replacement, and absence-of-competing-choice evidence |
| AC-26 | Immediate next-action transition, timed confirmation, failed-write retention, and final-step no-active evidence |
| AC-27 | System/default language, remembered override, live UI update, user-content preservation, and Settings-boundary evidence |
| AC-28 | Planning-local search/filter behavior, grouping preservation, archive separation, and state-isolation evidence |

## 4. Explicit Non-Goals

- Accounts, cloud synchronization, multi-device sync, collaboration, points, gamification, analytics dashboards, notification campaigns, automatic task selection, automatic AI, and medical/neurological inference.
- Any behavior that asks a user in guide mode to reprioritize, explain their state, or choose a route.
