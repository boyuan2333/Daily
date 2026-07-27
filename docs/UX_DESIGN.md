# UX Design Direction

## 1. Product Mental Model

The product is an execution continuity tool, not a task dashboard.

- A user may have several unfinished routes.
- `Active` means the one route currently in the foreground and shown by Guide.
- Previously started unfinished routes that are not in the foreground are `paused`.
- Each paused route retains its exact return position and next action.
- Guide reduces choice; Planning contains route choice and content management.

## 2. Confirmed Visual Priority

When an active route exists, the current action has the highest visual priority. `Capture idea` is prominent and always easy to reach, but remains secondary to the current action.

This separates two kinds of importance:

- **Execution priority:** the prepared current action remains the focal point.
- **Availability priority:** capture remains persistently accessible so an interrupting thought can be stored without taking over the route.

The product name is visually quiet. It must not compete with the current action or capture control.

## 3. Information Architecture

### Guide

Guide contains only:

- the active route's one current action;
- its completion standard and `do not do` boundary;
- complete, stuck, pause, and capture controls;
- transient save or transition feedback.

Guide does not show a route list, paused-route preview, priority comparison, or suggested replacement route.

### Guide Focus Layout

Guide uses one centered, constrained single column:

- A quiet context line shows the active route title and step progress.
- The one current executable action is the strongest and largest content element.
- The completion standard follows with clear but lower visual weight.
- The `Do not do` boundary remains directly visible and visually distinct without becoming a competing card.
- `Complete action` is the primary execution button.
- `I'm stuck` and `Pause and choose another route` are secondary controls.
- The global Capture action remains separate in the fixed bottom action bar.

Guide does not use a dashboard grid or several competing status cards. When the user explicitly enters the stuck flow, the fallback replaces the normal action in the central focus area. It does not appear alongside the normal action or expose additional suggestions.

### Step Completion Feedback

For a non-final step, durable completion immediately replaces the old action with the next action. The old action may fade out briefly and the next action appears without an interstitial or additional command. A compact `Step completed` message remains near progress for about two seconds.

The feedback is informational rather than celebratory: no confetti, points, sound, praise dialog, or score. If persistence fails, the original action stays visible and no success feedback appears. Completing the final step enters the specified no-active-route state rather than inventing a next action.

### Planning Workspace

Planning is a structured workspace rather than one long page. Its primary destinations are:

- **Routes:** active, paused, and draft route management;
- **Inbox:** captured ideas and their management actions;
- **Archive:** reversible access to archived routes and captures;
- **Settings:** language and other application preferences, separate from core execution controls.

Settings must support at least Simplified Chinese and English. Language selection must not share the main execution surface with Guide or route controls.

Language behavior is:

- first launch follows the Windows display language;
- Settings offers `Follow system`, `Simplified Chinese`, and `English`;
- a manual selection is remembered and overrides later system-language changes;
- interface copy updates without an application restart;
- user-authored content is never automatically translated or rewritten;
- timestamp presentation may follow locale while the stored timestamp remains unchanged.

## 4. Paused Route Experience

Planning provides a scan-friendly paused-routes section near the top of the Routes destination. Each paused route row exposes:

- route title;
- one or two lines of the exact retained next action;
- the time it was paused;
- step progress;
- one clear `Resume` command;
- a disclosure control for additional return context.

The disclosure expands one inline level and reveals the completion standard, `do not do` boundary, pause note, and prepared fallback. It is read-only in this context. Expanding or collapsing a row does not edit the route, move the return anchor, or affect which route is active.

Resuming a paused route restores its retained position. The user is not asked to select a step, rebuild context, or decide what to do next.

If another route is active, choosing `Resume` first persists the active route's complete return anchor. The selected paused route becomes active only after that save succeeds.

### Route Grouping Views

The Routes destination provides a stable segmented control with two views:

- **By status:** show Current, Paused, and Draft sections. Paused routes are the primary section and are ordered by most recently paused.
- **By project:** show one section per project. Within each project, show the active route first, then paused routes, then drafts. Routes without a project appear in an `Unassigned` section.

The first visit defaults to `By status`. Later visits remember the user's most recent selection. Switching views only changes grouping and presentation; it never changes route state, ordering within the route, return anchors, or the active route.

Each route belongs to at most one optional project. Moving a route between projects or leaving it unassigned only changes organization. A route never appears as duplicate instances across several project sections, and project membership never affects Guide or route recovery.

### Route List And Detail

At standard desktop widths, Routes uses a two-pane workspace:

- The left pane retains the selected `By status` or `By project` grouped list.
- The wider right pane shows route title, lifecycle, project, ordered steps, retained return context, and management actions.
- The detail pane is read-only by default. An explicit `Edit` command enables field and step changes.
- Selecting another route updates the detail pane without opening a modal or moving the list.
- Archive is a visible secondary action. Delete is inside an overflow menu and requires confirmation.
- A step protected by an active return anchor cannot be silently removed; the interface must explain that the recovery path needs explicit resolution.

At narrow widths, Routes becomes a list-to-detail flow. Returning to the list restores the previous grouping, scroll position, expanded rows, and selected route where still valid.

Routes includes a Planning-only local search field. It matches route title, retained next action, and project name, then filters the currently selected `By status` or `By project` view without replacing its grouping structure.

## 5. Capture Experience

Capture is a global quick action, not a peer mode to Planning.

- It remains in a fixed bottom action bar across Guide, Planning, and the no-active-route state.
- Its button is larger and more prominent than secondary utilities, while the current Guide action retains the highest visual priority.
- It opens a focused right-side drawer over the current context. In narrow windows, the drawer becomes a full-width panel.
- The underlying page remains visually recognizable so the user does not lose their place.
- Successful save feedback appears only after durable storage confirms the write.
- Successful save closes the drawer and returns to the same page, Guide action, and scroll position.
- A failed save keeps the drawer open, preserves the entered text, and leaves route context unchanged.
- Cancelling closes the drawer without storing an entry or changing context.
- In Planning, Inbox is a first-level destination rather than a section at the bottom of the page.

Inbox management supports an immutable original record and a separate editable organized version:

- The first edit copies the raw text into an organized-content field.
- Later edits change only the organized version.
- Lists show the organized version first when it exists and mark that the entry has been organized.
- `View original` reveals the raw text and original capture time.
- Conversion to a route or step uses the organized version when present while retaining the original capture.
- Archiving is reversible; deletion is secondary, explicit, and confirmed.
- Later user-triggered AI actions may propose or revise organized content in Planning only. They never overwrite the original record and are not required for Capture or Guide.

### Inbox Layout

At standard desktop widths, Inbox uses a two-pane workspace:

- The left pane is a scan-friendly list showing a text preview, capture time, and organized-state indicator.
- The right pane shows the complete organized content, access to the original record, editing, conversion, archive, explicit delete, and future user-triggered AI actions.
- Selecting another entry updates the detail pane without opening a modal or moving the list.

At narrow widths, the same information becomes a list-to-detail flow. Returning from detail restores the previous list scroll position and selected entry. An empty Inbox uses a restrained empty state and keeps the global Capture action available.

Inbox includes a local search field and a compact `All | Unorganized | Organized` filter. Search matches both immutable raw text and organized content. Results never include archived entries; Archive has its own search. Empty search results explain that no match was found without suggesting, prioritizing, or modifying content.

## 6. Navigation And Mode Feedback

Guide and Planning use a fixed `Guide | Planning` segmented control. It occupies the same top-bar location in both environments and clearly indicates the selected mode.

- In Planning, an internal navigation rail exposes Routes, Inbox, and Archive.
- In Guide, the internal Planning navigation is hidden so route and content choices do not compete with the current action.
- Settings uses a separate gear entry at application level. Language controls remain inside Settings rather than the execution surface.
- The global bottom Capture action remains available in both modes.

Mode changes use brief, restrained feedback. The transition must make the destination clear without delaying capture or execution.

Changing the selected mode only changes presentation. It never pauses, completes, activates, or switches a route by itself.

### Planning Entry Destinations

Planning chooses its initial destination from the explicit entry point:

- A first ordinary selection of `Planning` opens Routes.
- Later ordinary selections restore the most recent Planning destination, scroll position, and local selection where still valid.
- `Pause and choose another route` always opens Routes with Paused in focus.
- With no active route, entering Planning opens Routes and makes resume-or-create actions immediately available.

These rules never select a route automatically and do not infer intent from user behavior or personal state.

### No-Active Planning Hierarchy

When no route is active but paused routes exist, Routes leads with `Continue a previous route`. Recently paused routes appear first and provide clear resume commands. `Create new route` remains visible but uses secondary visual weight. The interface does not preselect, recommend, rank by inferred importance, or automatically resume a route.

`Capture idea` remains visually larger and more prominent than secondary utility commands, while the current action remains the strongest element when a route is active.

## 7. Visual Character

The application should feel calm, focused, and purpose-built rather than like an unstyled Windows settings form.

- Use a deliberate typography scale and spacing rhythm.
- Reserve strong contrast for the current action, capture, and confirmed state changes.
- Avoid decorative dashboards, nested cards, and large branding.
- Keep controls visually stable between states.
- Use accessible Windows interaction behavior without relying on default component styling as the complete visual identity.

## 8. Confirmed Route-Switch Entry

Guide provides one `Pause and choose another route` command. It behaves as follows:

- First persist the active route's complete return anchor.
- Enter Planning only after the save succeeds.
- Open Planning with the paused-routes section in focus.
- Keep the original route active until the user explicitly selects another route.
- If the user returns without selecting, restore the same Guide action with no route-state change.
- If saving fails, remain in Guide and show a concise error.

The command is a safe transition into route choice, not a route choice itself. Guide never shows paused-route options or switches automatically.

## 9. Deferred Design Questions

The following UX details still require discussion before producing a Stitch prompt:

- the visual reference direction and desired level of departure from native WinUI styling.
