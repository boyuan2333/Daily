# Product Specification: Execution Continuity Saver

> **Planning mode writes the route. Guide mode only reads, executes, pauses, and captures. Guide mode does not re-plan, re-choose, or require the user to explain their state.**

## 1. Purpose

This Windows-local, always-available tool preserves execution continuity. When a user is able to think clearly, they define goals, their order, and their fallback actions. Several unfinished routes may coexist. When the user later loses context, freezes, or becomes distracted, the product presents the prepared next action from the one foreground route and lets them capture a new thought without breaking that route.

It is not a conventional to-do list, a productivity scoring system, a medical product, or a system that detects a person's brain state. Guide mode is an interaction mode entered explicitly by the user or entered by the application during recovery; it is not a medical state. The program must not infer or automatically determine Guide mode from behavior, medical information, brain state, or other personal signals.

## 2. Core Product Rules

1. The system can store multiple unfinished routes, projects, paused plans, inbox entries, and archived material. A route that has been started may remain unfinished while another route is in the foreground.
2. `activeRouteId` is the sole source of truth for the foreground execution focus. At any moment it either identifies one route or is null. When it identifies route R, R is the only route with lifecycle status `active`; other previously started unfinished routes have lifecycle status `paused` and retain their return anchors. A route that has never been started may remain `draft`. When `activeRouteId` is null, no route may have lifecycle status `active`, and the no-active-route state is valid and required.
3. Guide mode shows one executable next action from the active route. It does not ask the user to choose a route, reprioritize work, or inspect a list.
4. Planning mode is where the user creates, edits, reviews, archives, converts inbox material, quickly inspects paused routes, and explicitly chooses which route becomes the foreground active route.
5. The user may capture an idea from guide mode, but capture never changes the active route, current step, or execution snapshot.
6. A pause always retains a return anchor. A user note may enrich it but cannot be required.
7. Stuck handling must reduce decisions. It never diagnoses, ranks, or generates a list of advice.
8. User data is retained by default. Archiving is reversible, and deletion requires explicit user action and confirmation.

## 3. Domain Model

### Route Persistence Data

A route is an ordered execution plan. It has a stable ID, title, lifecycle status (`draft`, `active`, `paused`, `completed`, or `archived`), ordered steps, and retained history. `Active` means foreground and currently guided; it does not mean that this is the user's only unfinished undertaking. A previously started unfinished route that is not in the foreground is `paused`. A route that has never been started may remain `draft`. Routes, projects, paused plans, and archives may coexist; only one route may be active.

### Project Organization

A project is an optional organizational container for routes. Each route belongs to at most one project and may remain unassigned. Project membership has no execution meaning: assigning, moving, renaming, or removing a route from a project must not change `activeRouteId`, route lifecycle, step state, return anchors, or Guide behavior. Projects do not appear in Guide.

### Step

Each step belongs to one route and has an ordered position. A step contains:

- one current executable action;
- a completion standard;
- a `do not do` boundary;
- one optional pre-written fallback action for a stuck state; and
- completion/history metadata.

The next unfinished step of the active route is the guide-mode current step. The displayed action is the step's normal action unless the user explicitly enters its stuck branch.

### Application Execution State

Application execution state is separate from route persistence data and from historical snapshots. It contains nullable `activeRouteId` and nullable `currentStepId`. `activeRouteId` is the sole source of truth for the foreground route: when it identifies route R, R must be the only route whose lifecycle status is `active`; other previously started unfinished routes remain `paused`. When it is null, no route may have lifecycle status `active`. When `activeRouteId` is null, `currentStepId` must also be null and guide mode is in its no-active-route state. When a route is active, `currentStepId` identifies that route's current unfinished step or is recalculated from its retained route data during recovery.

### Historical Execution Snapshots

Execution snapshots are immutable historical return anchors associated with a route. They are not live application state and do not replace a route's stored step lifecycle. Each paused route retains the newest valid anchor required to resume at its previous position. A new pause or route switch adds a new snapshot rather than mutating an existing one.

### Execution Snapshot and Pause Anchor

Pausing automatically persists a complete execution snapshot with:

- current route ID;
- current step ID;
- current action;
- completion standard;
- `do not do` boundary;
- pre-written fallback action, if any;
- pause timestamp; and
- optional user note.

The snapshot is the return anchor. It is required even when the optional note is empty and must restore after a program restart.

### Capture Inbox

A capture is deliberately not required to be a well-formed task. It stores immutable raw user text and its original timestamp. Planning may also store a separate editable organized version. The first edit begins from a copy of the raw text; later edits update only the organized version. Inbox lists prefer the organized version when present and always provide access to the original record.

Planning mode must let the user review a capture, edit its organized version, convert it to a route or step, retain it as reference, archive it, or explicitly delete it with confirmation. Conversion and future user-triggered AI assistance use the organized version when present, but they never overwrite or discard the raw record. Captures must remain discoverable until the user explicitly changes their lifecycle.

At standard desktop widths, Inbox uses a list-and-detail layout. The list exposes a text preview, capture time, and organized state without requiring the user to open each entry. Selecting an entry shows its full content and management actions in the adjacent detail pane. At narrow widths, Inbox becomes a list-to-detail navigation flow; returning from detail restores the previous list position and selection context.

Capturing may only add an inbox entry. It must not modify `activeRouteId`, `currentStepId`, route lifecycle, step completion state, or any existing execution snapshot.

### Data Protection

Archiving a route or inbox entry is reversible. Inbox entries, their raw records, routes, steps, and execution snapshots must not be implicitly deleted. A deletion must be explicitly triggered and confirmed by the user; interface simplification, editing an organized capture, AI output, automatic cleanup, and state transitions cannot discard user data.

## 4. Modes and Flows

### Planning Mode

Planning mode is the only place to create or edit routes, steps, completion standards, `do not do` boundaries, and fallback actions. It is also the only place where routes are listed and where the user chooses a paused or draft route to become active. Paused routes must be quickly discoverable. Their default rows show the route title, one or two lines of the retained next action, pause time, step progress, and a clear resume command. A single expandable context area may reveal the completion standard, `do not do` boundary, pause note, and prepared fallback without changing route or execution state.

Guide and Planning are selected through one fixed `Guide | Planning` segmented control. It occupies the same location in both modes and visibly indicates the current mode. Planning exposes internal destinations for Routes, Inbox, and Archive. Guide hides those internal destinations. Settings remains a separate application-level entry and does not compete with core execution controls.

Settings supports `Follow system`, `Simplified Chinese`, and `English`. On first launch, the application follows the Windows display language. A manual selection overrides the system choice and is remembered. Changing language updates application interface text without requiring restart and must not translate or rewrite user-authored route, step, note, project, or capture content. Locale presentation may change how stored timestamps are displayed, but it must not change their stored values or route state.

Planning entry is context-preserving and determined only by the explicit entry point. The first ordinary entry opens Routes; later ordinary entries restore the user's most recent Planning destination and position. `Pause and choose another route` always opens Routes with paused routes in focus. When no route is active and paused routes exist, entering Planning opens Routes with `Continue a previous route` as the primary section and route creation as a secondary action. Paused routes are ordered by most recently paused. These destination and hierarchy rules do not select or activate a route automatically.

The Routes destination supports two presentation views: grouping by lifecycle status and grouping by the route's optional single project. Unassigned routes appear in a dedicated `Unassigned` section. The first visit defaults to status grouping; later visits restore the user's most recently selected view. Changing the presentation view only reorganizes the visible list. It must not modify `activeRouteId`, route lifecycle, step order, return anchors, or any execution state.

Planning provides lightweight local search within each content destination. Routes searches route title, retained next action, and project name while preserving the selected status-or-project grouping. Inbox searches raw and organized capture text and offers `All`, `Unorganized`, and `Organized` filters. Archive searches archived content separately and archived records do not appear in normal results. Search and filtering only change visible presentation; they do not rank by inferred importance, recommend content, or change persisted route or capture state.

At standard desktop widths, Routes uses a grouped list and an adjacent route-detail workspace. The list remains visible while the detail area shows route identity, lifecycle, project, ordered steps, retained return context, and management actions. Details are read-only by default and require an explicit Edit command before fields or step order can change. At narrow widths, Routes becomes a list-to-detail flow and restores list position on return. Archive is a reversible secondary action. Delete is placed in an overflow menu, requires confirmation, and must respect protected return-anchor references.

When switching routes, the system first saves a complete pause snapshot for the old active route. The old route becomes `paused` and retains its current step and return anchor. Only after that write is confirmed may the selected route become active. If the selected route was paused, it resumes from its newest valid return anchor; if it has no valid anchor, it resumes from its retained current unfinished step. Guide mode never lists routes, chooses a replacement, or directly changes `activeRouteId`.

Guide mode includes a single `Pause and choose another route` shortcut. It first persists the active route's complete return anchor. Only after that write succeeds does the application enter Planning and focus the paused-routes section. The shortcut does not display route choices in Guide or activate another route automatically. The original route remains active until the user explicitly selects a replacement in Planning. If the user returns to Guide without selecting one, the original route and current action remain unchanged. If saving the return anchor fails, the application remains in Guide and reports the failure without changing route state.

Planning edits must not silently delete a step or make the active route's newest valid snapshot point to an invalid `stepId`. If a user explicitly deletes such a step, the historical snapshot remains retained. The application must not crash or automatically recover to another route or unrelated step; planning mode must require the user to explicitly resolve the recovery path before it can be used again.

### Guide Mode

Guide mode is an explicit user interaction mode or the recovery mode entered on application launch. If an active route has an unfinished step, it displays exactly that one action and its prepared context. Other paused routes remain hidden. Guide may offer direct controls to complete, capture, report stuck, or pause, but it must not display competing tasks, routes, rankings, or suggestions. The product must never infer Guide mode from user behavior or personal/medical data.

Guide uses a single-column focus layout. A quiet context line may identify the active route and step progress. The current action is the strongest visual element, followed by the completion standard and visible `do not do` boundary. `Complete action` is the primary execution command; `I'm stuck` and `Pause and choose another route` are secondary. Capture remains separate in the fixed bottom action bar. Fallback content does not share the normal-action surface and appears in the central focus area only after the user explicitly reports being stuck.

Switching between Guide and Planning changes presentation context only. It must not itself complete, pause, activate, or switch a route. A brief transition may reinforce the mode change without delaying access or moving the segmented control.

After a non-final step is durably completed, Guide immediately presents the next action. The previous action may use a brief restrained exit transition and a compact `Step completed` confirmation may remain near progress for approximately two seconds. No continue command, completion interstitial, celebratory animation, score, or sound is added. Success feedback must not appear before the durable write succeeds. A failed completion keeps the original action visible and unchanged.

### Capture Flow

The capture control remains in a stable bottom action bar across Guide, Planning, and the no-active-route state. It is visually more prominent than secondary utility actions while the current Guide action remains the strongest content element. Activating capture opens a right-side text-entry drawer; in a narrow window it becomes a full-width panel. The underlying page and current action remain visually identifiable.

Saving uses an atomic, verified durable write of raw text and timestamp before the UI confirms `Saved`. After a successful save, the capture surface closes and the system returns immediately to the same page, current action, and scroll position. If the write fails, the capture surface remains open with the entered text intact and the route context unchanged. Cancelling closes the capture surface without writing. Capture must not modify `activeRouteId`, `currentStepId`, route lifecycle, step completion state, or a persisted execution snapshot. The eventual storage implementation may use database transactions or atomic-file replacement after the storage choice is approved.

### Pause and Resume Flow

Pause writes the complete snapshot automatically using an atomic, verified durable write. A user may optionally add a note. On relaunch, recovery follows this exact order:

1. If `activeRouteId` is null, show the no-active-route state.
2. If `activeRouteId` exists, restore only that active route's newest valid execution snapshot.
3. If the active route has no pause snapshot, recover from its current unfinished step.
4. Never restore a non-active paused route merely because its snapshot is newer.

An absent note cannot block recovery.

### Stuck Flow

If the current step has a pre-written fallback, guide mode shows that single fallback action. Completing the fallback returns to the original current step; it never completes, skips, or replaces it. Only an explicit completion against the original step's completion standard completes the original step.

If there is no fallback, the system permits only a one-sentence record of the block, automatically saves the pause anchor, and offers only:

- `Return to current action`;
- `Pause now and return here later`.

It must not produce a diagnostic, advice list, priority reorder, or AI-generated essay.

### Route Completion and No-Active-Route State

When the final step of an active route is completed, the route becomes `completed` and all route, step, and history data remain retained. The system clears `activeRouteId`.

Guide mode then displays a safe no-active-route state with a primary direction, `Enter planning mode to select or create the next route`, and an always-available secondary action, `Capture idea`. In this state capture durably stores raw text and timestamp in the inbox without creating or activating a route, entering planning mode, or leaving the no-active-route state. The product must not auto-activate another route or infer a choice from priority, deadlines, or AI.

## 5. MVP Scope

MVP-0 provides the domain state model, local durable persistence, and restart recovery. MVP-1 adds route editing, one-next-action guide mode, capture, pause, and stuck behavior. MVP-2 adds tray access, a global shortcut, and optional user-controlled startup. MVP-3 may add user-triggered AI decomposition.

MVP-0 through MVP-2 must work without AI or network access. Accounts, cloud synchronization, gamification, points, statistics, automated prioritization, notification campaigns, medical claims, and automatic mental-state recognition are out of scope.
