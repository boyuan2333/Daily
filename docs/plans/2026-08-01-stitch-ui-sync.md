# Stitch UI Sync Implementation Plan

> **For Codex:** Execute this plan task-by-task with test-first changes.

**Goal:** Apply the supplied Stitch visual system to the existing WinUI 3 application without changing route and execution semantics.

**Architecture:** Keep `ExecutionSession` and domain transitions as the sole source of state. Replace the prototype single-column surface with a shared header, Guide focus surface, Planning navigation/workspace, and fixed capture bar in `MainWindow.xaml`; extend only presentation data needed to render existing state.

**Tech Stack:** .NET 10, WinUI 3 XAML, xUnit, existing SQLite persistence.

---

### Task 1: Expose Guide context for the shared focused layout

**Files:**
- Modify: `src/ExecutionContinuity.App/GuidePresentation.cs`
- Test: `tests/ExecutionContinuity.App.Tests/ExecutionSessionTests.cs`

**Step 1:** Add a failing test that an active Guide presentation includes route title and current-step progress.

**Step 2:** Run the app test project and verify the assertion fails because the presentation does not expose context.

**Step 3:** Add the minimal presentation fields and populate them from the active route.

**Step 4:** Re-run the app test project and verify it passes.

### Task 2: Replace the prototype visual shell

**Files:**
- Modify: `src/ExecutionContinuity.App/App.xaml`
- Modify: `src/ExecutionContinuity.App/MainWindow.xaml`
- Modify: `src/ExecutionContinuity.App/MainWindow.xaml.cs`

**Step 1:** Add an app-level light palette and reusable button, section, and input styles matching the reviewed Stitch exports.

**Step 2:** Build a stable top bar with brand, `Guide | Planning` segmented controls, and application settings entry.

**Step 3:** Rebuild Guide as a single focused action surface with step context, completion and boundary panels, execution controls, an explicit Planning transition, and a fixed Capture action bar.

**Step 4:** Rebuild no-active Guide as a restrained centered state with Planning as primary and Capture as secondary.

**Step 5:** Rebuild Planning as a rail plus content workspace, with separate Routes and Inbox destinations and the supplied list/detail treatment.

### Task 3: Preserve command behavior through the new surface

**Files:**
- Modify: `src/ExecutionContinuity.App/MainWindow.xaml.cs`
- Test: `tests/ExecutionContinuity.App.Tests/ExecutionSessionTests.cs`

**Step 1:** Add a failing test for Guide context values while in fallback and no-active states.

**Step 2:** Wire visibility and text rendering for the new named XAML elements without changing `ExecutionSession` transition calls.

**Step 3:** Keep capture persistence confirmations after `CaptureAsync` succeeds and preserve current Guide context after the drawer closes.

**Step 4:** Run the focused test project and verify all tests pass.

### Task 4: Build and visual smoke test

**Files:**
- Verify: `ExecutionContinuity.slnx`

**Step 1:** Run the full test suite.

**Step 2:** Build the solution in Release configuration.

**Step 3:** Start the desktop application and inspect the no-active, Guide, Planning Routes, and Planning Inbox surfaces at desktop width.

**Step 4:** Report exact verification evidence and any limitations.
