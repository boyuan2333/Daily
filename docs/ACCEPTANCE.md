# Acceptance Criteria

## Test Levels

- **Domain test**: validates pure state transitions and invariants.
- **Persistence integration test**: writes local data, recreates the storage/session, and verifies recovery.
- **Desktop integration/UI test**: validates the Windows interaction boundary once a desktop shell exists.
- **Manual acceptance**: validates the visible workflow and absence of disallowed prompts.

## AC-01: Multiple Unfinished Routes, One Foreground Active Route

**Given** several unfinished routes exist, including previously started paused routes and routes that have never been started,
**when** planning mode selects route R as active,
**then** `activeRouteId` identifies R, R is the only route with lifecycle status `active`, every other previously started unfinished route remains `paused`, and guide mode derives its action only from R.

**Given** `activeRouteId` is null,
**then** no route has lifecycle status `active` and the no-active-route state is shown.

**Tests:** domain invariant test for both directions of the `activeRouteId`/lifecycle relationship; persistence integration test; manual planning-to-guide verification.

## AC-02: Guide Mode Shows One Executable Action

**Given** the active route has an unfinished current step,
**when** guide mode is opened or resumed,
**then** it shows only that step's single action and not paused routes, route choices, task lists, priority rankings, or advice.

**Tests:** domain selector test; later UI test; manual visual inspection.

## AC-03: Capture Is Durable and Non-Hijacking

**Given** guide mode displays an action on an active route and no pause snapshot exists,
**when** the user enters raw capture text and saves it,
**then** the entry has the original text and a timestamp before `Saved` is displayed; `activeRouteId`, `currentStepId`, route lifecycle, and step completion state are unchanged; and the original action is shown again.

**Given** guide mode displays an action on an active route and a persisted execution snapshot exists,
**when** the user enters raw capture text and saves it,
**then** the entry is durably stored, the same route state remains current, the existing snapshot is unchanged, and the original action is shown again.

**Tests:** persistence integration test that injects a write failure and verifies no success confirmation; domain invariant test; UI flow test.

## AC-04: Capture Survives Immediate Restart

**Given** a user saves a capture while an action is current,
**when** the program is immediately closed and restarted,
**then** the capture is present in the inbox and the same current action remains current.

**Tests:** persistence integration restart test; manual close-and-relaunch acceptance test.

## AC-05: Pause Always Preserves a Complete Return Anchor

**Given** an active current step,
**when** the user pauses with no note,
**then** persistence contains route ID, step ID, current action, completion standard, `do not do`, fallback action, timestamp, and an empty optional note.

**When** the program restarts,
**then** the user can directly resume the original step.

**Tests:** domain snapshot construction test; persistence integration restart test; manual acceptance test.

## AC-06: Stuck State Reduces Decisions

**Given** a current step with a fallback action,
**when** the user reports being stuck,
**then** guide mode shows that exact single fallback action; completing it returns to the original current step and does not complete, skip, or replace that step.

**Given** the original current step is shown after a completed fallback,
**when** the user has not explicitly completed it against its completion standard,
**then** the original step remains unfinished.

**Given** a current step without a fallback action,
**when** the user reports being stuck,
**then** the product accepts only one sentence describing the block, saves a pause anchor, and offers only return or pause-later controls.

**Tests:** domain branch tests; UI test asserting no extra choices; manual inspection for no diagnosis, recommendation list, reordering, or AI long-form output.

## AC-07: Completing a Route Does Not Choose the Next One

**Given** the active route's final step is current,
**when** it is completed,
**then** the route is marked completed, its data remains available, `activeRouteId` is cleared, no route remains active, and guide mode shows `Enter planning mode` as its primary action and `Capture idea` as an available secondary action.

**Tests:** domain completion and active-route invariant tests; persistence integration test; manual acceptance test confirming no automatic route activation and available no-active-route capture.

## AC-08: Route Switching Preserves Both Routes' Execution Positions

**Given** route A is active and route B is available,
**when** the user chooses B in planning mode,
**then** a durable complete snapshot for A is saved before B becomes active, A becomes paused, and A retains its current step and return anchor.

**Given** route B was previously paused with a valid return anchor,
**when** planning mode makes B active,
**then** B resumes at that retained position without asking the user to choose a step or reconstruct the next action.

**When** the user is in guide mode or capture flow,
**then** the interface does not show route choices and no control directly selects or activates a replacement route.

**Given** route A was active, a pause snapshot for A is later than any snapshot for route B, and planning mode switched the active route to B,
**when** the application restarts,
**then** recovery restores B rather than A, using B's newest valid snapshot or B's current unfinished step when B has no snapshot.

**Tests:** domain ordering/invariant tests; persistence integration restart test; later UI authorization test.

## AC-09: Protected Snapshot References During Planning Edits

**Given** the active route's newest valid execution snapshot references step S,
**when** planning mode edits the route,
**then** the edit cannot silently delete S or make the snapshot's `stepId` invalid.

**When** the user explicitly deletes S,
**then** the historical snapshot remains retained, the application does not crash or automatically recover to another route or unrelated step, and planning mode requires the user to explicitly resolve the recovery path.

**Tests:** domain edit-invariant test; persistence integration test retaining the snapshot; manual planning-mode recovery-path acceptance test.

## AC-10: Local and Offline MVP Behavior

**Given** network access is unavailable,
**when** the user creates routes, captures, pauses, resumes, becomes stuck, and completes a route in MVP-0 through MVP-2,
**then** the workflows remain available without a remote account or AI service.

**Tests:** dependency/configuration inspection; offline desktop integration test; manual blocked-network test.

## AC-11: Optional Windows Shell Features

**Given** MVP-2 is implemented and enabled by the user,
**when** the app is minimized or inactive,
**then** its tray entry and global shortcut return the user to the same guide state without selecting another route.

**Tests:** Windows desktop integration test and manual acceptance. Startup registration must be opt-in and reversible.

## AC-12: Capture Without an Active Route

**Given** `activeRouteId` is null and guide mode displays the no-active-route state,
**when** the user captures raw text,
**then** the capture is durably stored in the inbox, no route is created or activated, the application does not enter planning mode, and it remains in the no-active-route state.

**Tests:** domain invariant test; persistence integration restart test; later UI/manual acceptance test.

## AC-13: Planning Makes Paused Routes Easy to Resume

**Given** one route is active and one or more previously started unfinished routes are paused,
**when** the user opens the Routes destination in planning mode,
**then** paused routes are discoverable without scrolling through route-editing forms, and each paused route shows its title, retained next-action context, pause time, and a clear resume command.

**Given** the user resumes a paused route,
**when** the switch succeeds,
**then** guide mode shows that route's exact retained next action and no route-selection list remains visible.

**Tests:** UI test for paused-route visibility and displayed context; persistence integration test for exact anchor restoration; manual planning-to-guide verification.

## AC-14: Guide Can Safely Enter Route Choice

**Given** route A is active and Guide shows its current action,
**when** the user invokes `Pause and choose another route`,
**then** the application durably saves A's complete return anchor before entering Planning, opens Planning with paused routes in focus, and does not activate another route automatically.

**Given** the user enters Planning through that command but does not select another route,
**when** the user returns to Guide,
**then** A remains active and Guide shows the same current action.

**Given** saving A's return anchor fails,
**when** the command is invoked,
**then** the application remains in Guide, reports that the save failed, and does not change `activeRouteId`, `currentStepId`, route lifecycle, or the visible current action.

**Tests:** application-layer failure test; desktop UI transition test; manual save-success, cancel, and save-failure verification.

## AC-15: Route Grouping Views Do Not Change Execution State

**Given** the user opens the Routes destination for the first time,
**when** no grouping preference has been saved,
**then** routes are grouped by status into Current, Paused, and Draft sections.

**Given** the user switches between `By status` and `By project`,
**when** the list is regrouped,
**then** the same routes remain present and `activeRouteId`, `currentStepId`, route lifecycle, step order, and execution snapshots are unchanged.

**Given** the user leaves Planning after selecting a grouping view,
**when** the user later returns to the Routes destination,
**then** the most recently selected grouping view is restored.

**Given** a route has no project assignment,
**when** routes are grouped by project,
**then** that route appears in an `Unassigned` section and remains fully usable.

**Tests:** presentation-state test for the first-visit default and restored preference; UI test for both groupings; domain-state comparison before and after view switching; manual verification.

## AC-16: Project Membership Is Optional And Organizational Only

**Given** a route is unassigned,
**when** the user assigns it to a project,
**then** the route belongs to that one project and no execution state, lifecycle, step, return anchor, or Guide presentation changes.

**Given** a route already belongs to project A,
**when** the user moves it to project B,
**then** it no longer appears under A, appears once under B, and does not exist as duplicate route instances.

**Given** a route is removed from its project,
**when** routes are grouped by project,
**then** the route appears once under `Unassigned` and remains fully usable.

**Given** Guide is open,
**then** no project list, project assignment, or project-based route suggestion is displayed.

**Tests:** domain or application-state test for single optional project membership; UI test for assign, move, and unassign; manual verification that project operations do not alter execution state.

## AC-17: Paused Routes Use Progressive Context Disclosure

**Given** paused routes are visible in Planning,
**when** their rows are collapsed,
**then** each row shows its title, one or two lines of the retained next action, pause time, step progress, a resume command, and a disclosure control without showing the full route editor.

**Given** the user expands a paused route row,
**when** the additional context appears,
**then** it shows the retained completion standard, `do not do` boundary, pause note, and prepared fallback in a single read-only expansion level.

**When** the user expands or collapses a paused route row,
**then** `activeRouteId`, `currentStepId`, route lifecycle, step state, and execution snapshots remain unchanged.

**Tests:** desktop UI tests for collapsed and expanded content; state comparison before and after disclosure; manual scanability and no-edit verification.

## AC-18: Capture Is Stable, Context-Preserving, And Responsive

**Given** the user is in Guide, Planning, or the no-active-route state,
**when** the current page is displayed,
**then** the Capture control appears in the same fixed bottom action bar position and remains visually prominent without displacing the current Guide action as the strongest content element.

**Given** the window has standard desktop width,
**when** the user opens Capture,
**then** a right-side text-entry drawer opens and the underlying page remains visually recognizable.

**Given** the window is narrow,
**when** the user opens Capture,
**then** the entry surface uses the available full width without clipping text or controls.

**Given** the user successfully saves a capture,
**when** durable storage confirms the write,
**then** the entry surface closes and the same page, current Guide action, and scroll position are restored.

**Given** the durable write fails,
**when** the user attempts to save,
**then** the entry surface remains open, the entered text is preserved, the failure is reported, and all route and execution state remains unchanged.

**Given** the user cancels Capture,
**then** no inbox entry is added and the same page, action, and scroll position are restored.

**Tests:** desktop UI tests across Guide, Planning, no-active, standard-width, and narrow-width states; application-layer save-failure test; state and scroll-position comparison before and after save or cancel; manual visual inspection.

## AC-19: Inbox Editing Preserves The Original Capture

**Given** a capture contains raw text and an original capture timestamp,
**when** the user edits it for the first time,
**then** the application creates an organized version initialized from the raw text and leaves the raw text and timestamp unchanged.

**Given** an organized version exists,
**when** the user edits it again,
**then** only the organized version changes and `View original` still shows the exact raw text and original timestamp.

**Given** a capture has an organized version,
**when** the user converts it to a route or step or invokes future user-triggered AI organization,
**then** the operation uses or proposes changes to the organized version and does not overwrite, archive, or delete the original record implicitly.

**Given** an Inbox entry is displayed in a list,
**when** an organized version exists,
**then** the organized version is the primary displayed content and the entry clearly indicates that the original remains available.

**Tests:** domain or application tests for immutable raw capture fields and editable organized content; conversion retention test; UI tests for edit and `View original`; manual original-text comparison.

## AC-20: Inbox Supports Fast List-And-Detail Management

**Given** Inbox contains several entries and the window has standard desktop width,
**when** the user opens Inbox,
**then** a list pane shows each entry's text preview, capture time, and organized state while an adjacent detail pane can show the selected entry's complete content and management actions.

**Given** the user selects different entries,
**when** the detail content changes,
**then** the list remains visible and keeps its scroll position without opening a modal.

**Given** the window is narrow,
**when** the user opens an Inbox entry and then returns to the list,
**then** the interface uses a list-to-detail flow and restores the previous list scroll position and selection context.

**Given** Inbox is empty,
**when** the page is displayed,
**then** it shows a restrained empty state and the global Capture control remains available in its standard location.

**Tests:** responsive desktop UI tests for two-pane and list-to-detail states; scroll and selection restoration test; empty-state manual inspection.

## AC-21: Guide And Planning Use Stable Mode Navigation

**Given** Guide or Planning is visible,
**when** the user views the top application bar,
**then** the same `Guide | Planning` segmented control appears in the same location and clearly indicates the current mode.

**Given** Planning is selected,
**then** Routes, Inbox, and Archive navigation is available and Settings remains a separate application-level entry.

**Given** Guide is selected,
**then** Routes, Inbox, and Archive navigation is hidden while the global Capture control remains available.

**When** the user switches between Guide and Planning through the segmented control,
**then** the transition is brief, the control does not move, and no route is paused, completed, activated, or switched as a consequence of changing mode alone.

**Tests:** desktop UI layout tests for both selected states; state comparison before and after mode switching; manual transition and control-position inspection.

## AC-22: Planning Entry Respects The Explicit Entry Point

**Given** the user selects Planning normally for the first time,
**then** Planning opens Routes.

**Given** the user previously used a Planning destination and its local position remains valid,
**when** the user later selects Planning normally,
**then** that destination, scroll position, and local selection are restored.

**Given** the user invokes `Pause and choose another route` from Guide,
**when** the return anchor is saved successfully,
**then** Planning opens Routes with Paused in focus regardless of the last ordinary Planning destination.

**Given** no route is active,
**when** the user enters Planning,
**then** Planning opens Routes with resume-or-create actions available and does not select or activate a route automatically.

**Tests:** presentation-state tests for first entry and restored ordinary entry; UI test for the pause-and-choose override; no-active-route UI/manual verification; route-state comparison before and after navigation.

## AC-23: No-Active Planning Prioritizes Continuation Without Choosing

**Given** no route is active and one or more paused routes exist,
**when** the user enters Planning,
**then** Routes shows `Continue a previous route` as its primary section, orders paused routes by most recently paused, and shows `Create new route` as a visible secondary action.

**When** that Planning state is displayed,
**then** no paused route is preselected, automatically resumed, or ranked by inferred importance, and `activeRouteId` remains null until the user explicitly chooses a route.

**Given** no active or paused route exists,
**when** the user enters Planning,
**then** route creation becomes the primary available action without creating a route automatically.

**Tests:** UI tests for paused-present and paused-absent no-active states; ordering test by pause time; domain-state assertion that opening Planning leaves `activeRouteId` null; manual hierarchy inspection.

## AC-24: Routes Uses Explicit List-And-Detail Management

**Given** Routes is open at standard desktop width,
**when** the user selects routes from the grouped list,
**then** the list remains visible and an adjacent wider detail pane shows the selected route's identity, lifecycle, project, ordered steps, retained return context, and management actions.

**Given** a route detail is displayed,
**when** the user has not invoked Edit,
**then** route fields and step order are read-only.

**Given** the user invokes Edit and saves valid changes,
**then** the detail returns to read-only presentation and the grouped list reflects the saved route without losing its position.

**Given** the window is narrow,
**when** the user opens a route detail and returns,
**then** Routes uses a list-to-detail flow and restores the previous grouping, scroll position, expanded rows, and selected route where still valid.

**Given** route management actions are displayed,
**then** Archive is reversible and secondary, Delete is placed in overflow and requires confirmation, and a protected return-anchor step cannot be silently removed.

**Tests:** responsive Routes UI tests; read-only/edit-state tests; list context restoration test; archive/delete authorization tests; protected-step recovery-path manual verification.

## AC-25: Guide Maintains One Visual And Execution Focus

**Given** an active route has a normal current step,
**when** Guide is displayed,
**then** a single-column layout shows a quiet route-and-progress context line, one visually dominant current action, its completion standard, a directly visible `do not do` boundary, one primary `Complete action` command, and secondary stuck and pause-and-choose commands.

**When** the normal Guide state is displayed,
**then** no dashboard grid, competing status cards, fallback content, route list, or replacement suggestion is visible, and Capture remains separate in the fixed bottom action bar.

**Given** the user explicitly enters the fallback flow,
**when** fallback content is displayed,
**then** the fallback occupies the central focus area instead of appearing beside the normal action, and completing it returns to the original action without completing that action.

**Given** the window narrows,
**when** Guide reflows,
**then** the same information hierarchy and command priority remain intact without clipping, overlap, or horizontal scrolling.

**Tests:** desktop UI hierarchy tests for normal, fallback, and narrow states; domain assertion for fallback return; manual visual inspection for one dominant action and absence of competing choices.

## AC-26: Step Completion Advances Without Added Friction

**Given** a non-final current step,
**when** its completion is durably persisted,
**then** Guide immediately presents the next action without a continue command or completion interstitial and shows a compact `Step completed` confirmation near progress for approximately two seconds.

**When** completion feedback is displayed,
**then** it contains no celebration animation, points, score, praise dialog, or sound.

**Given** persisting completion fails,
**when** the user invokes `Complete action`,
**then** the original action remains visible and unchanged, no success confirmation appears, and the next action is not shown.

**Given** the completed step is the route's final step,
**when** completion is durably persisted,
**then** Guide enters the no-active-route state and does not display or choose another route's action.

**Tests:** application-layer success and write-failure tests; desktop UI transition and timed-confirmation tests; final-step state test; manual no-celebration inspection.

## AC-27: Language Is Configurable Outside The Execution Surface

**Given** the application launches with no saved language override,
**when** Windows uses Simplified Chinese or English,
**then** the application interface follows that system language.

**Given** the user opens Settings,
**when** the user selects `Follow system`, `Simplified Chinese`, or `English`,
**then** interface text updates without restart and the selected override behavior is retained for later launches.

**Given** user-authored routes, steps, notes, projects, or captures exist,
**when** the interface language changes,
**then** that content remains byte-for-byte unchanged and all route and execution state remains unchanged.

**Given** timestamp presentation changes with locale,
**then** only its visible formatting changes and the stored timestamp value remains unchanged.

**When** Guide or another core execution surface is displayed,
**then** language selection controls are not present there and remain available only through Settings.

**Tests:** localization resource/UI tests for Chinese and English; persisted preference test; user-content and state comparison across language changes; timestamp-value test; manual Settings-boundary verification.

## AC-28: Planning Search Filters Locally Without Reprioritizing

**Given** Routes contains active, paused, draft, and project-assigned routes,
**when** the user searches by route title, retained next action, or project name,
**then** matching routes remain inside the selected `By status` or `By project` grouping and no route lifecycle, order, anchor, or execution state changes.

**Given** Inbox contains raw-only and organized entries,
**when** the user searches or selects `All`, `Unorganized`, or `Organized`,
**then** matching normal Inbox entries are filtered using both raw and organized text as applicable and archived entries remain excluded.

**Given** the user searches Archive,
**then** archived content is searched separately and does not reappear in normal Routes or Inbox results.

**Given** no content matches,
**then** a restrained no-results state appears without recommendations, inferred ranking, automatic state changes, or content deletion.

**Tests:** local search and filter UI tests; archived-content separation test; domain-state comparison before and after search; manual no-results and no-recommendation inspection.
