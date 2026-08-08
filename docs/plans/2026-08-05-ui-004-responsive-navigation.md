# UI-004 Responsive Navigation Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make the WinUI shell responsive at narrow widths and provide list-to-detail navigation for Routes and Inbox without changing domain execution state.

**Architecture:** Keep responsive layout and narrow-navigation state in a small framework-independent presentation model. The window maps that model to XAML visibility and grid layout; existing `ExecutionSession` state remains read-only for navigation and selection. Desktop keeps the two-pane layout, while compact mode shows one list or one detail surface at a time with an explicit back command.

**Tech Stack:** C#/.NET 10, WinUI 3 XAML, xUnit, existing `ExecutionContinuity.App` test project.

---

### Task 1: Establish UI-004 state contracts

**Files:**
- Modify: `docs/UI_FIX_BACKLOG.md`
- Create: `src/ExecutionContinuity.App/ResponsivePlanningPresentation.cs`
- Test: `tests/ExecutionContinuity.App.Tests/ExecutionSessionTests.cs`

Write failing tests for compact breakpoint behavior, Routes/Inbox list-detail transitions, explicit back transitions, and restoration of grouping/selection/scroll offsets without domain-state changes.

### Task 2: Implement presentation state

Implement the smallest immutable state transition model needed by the window. It must preserve the list context while a detail view is open and return the exact prior context on back.

### Task 3: Map the state to the WinUI shell

**Files:**
- Modify: `src/ExecutionContinuity.App/MainWindow.xaml`
- Modify: `src/ExecutionContinuity.App/MainWindow.xaml.cs`

Use max-width constraints and wrapping for Guide, no-active, Settings, status, Capture, and bottom actions. In compact mode hide the planning rail and show either the Routes/Inbox list or its detail surface. Add explicit back buttons and persist/restore list scroll, grouping, and selection context in presentation state only.

### Task 4: Verify

Run the focused tests, solution build, solution tests, `git diff --check`, and the release-window probe. Launch the real executable at default, compact, and wide sizes and retain screenshots showing no horizontal clipping and correct list/detail/back behavior.

### Task 5: Complete the backlog record

Record modified files, exact verification commands/results, manual evidence, and any residual risk. Mark UI-004 complete only when the compact and desktop acceptance paths have real evidence.
