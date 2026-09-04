# UI-006 Inbox And Language Controls Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the Inbox and language-setting placeholders with durable, testable controls while preserving raw captures and all execution state.

**Architecture:** Add an optional organized text field to `CaptureEntry` and a persisted `LanguagePreference` to `AppState`. Keep filtering/search as pure presentation helpers, and route all edits and preference changes through `ExecutionSession` so the existing atomic SQLite commit boundary remains the source of truth.

**Tech Stack:** C#/.NET 10, WinUI 3 XAML, SQLite JSON state document, xUnit.

---

### Task 1: Define the UI-006 behavior in tests

**Files:**
- Modify: `tests/ExecutionContinuity.App.Tests/ExecutionSessionTests.cs`
- Create: `src/ExecutionContinuity.App/InboxPresentation.cs`
- Modify: `src/ExecutionContinuity.Domain/Models.cs`

**Step 1: Write failing tests**

Cover organized capture editing, raw-text preservation, Inbox All/Unorganized/Organized filtering, case-insensitive search across raw and organized text, archived exclusion, route title/next-action search, and language preference persistence.

**Step 2: Run focused tests**

Run: `dotnet test tests/ExecutionContinuity.App.Tests/ExecutionContinuity.App.Tests.csproj --no-restore --filter FullyQualifiedName~Ui006 --verbosity minimal`

Expected: FAIL because the new model and presentation APIs do not exist yet.

### Task 2: Add backward-compatible state fields

**Files:**
- Modify: `src/ExecutionContinuity.Domain/Models.cs`
- Modify: `src/ExecutionContinuity.Persistence/SqliteStateStore.cs`

**Step 1: Implement the smallest model change**

Add nullable `OrganizedText` after the existing `CaptureEntry` fields and add `LanguagePreference` with `FollowSystem`, `SimplifiedChinese`, and `English`; default missing persisted values to `FollowSystem`.

**Step 2: Run focused tests**

Run the UI-006 filter and persistence tests. Expected: model/persistence tests pass; session and presentation tests may still fail until Task 3.

### Task 3: Add session commands and pure presentation helpers

**Files:**
- Modify: `src/ExecutionContinuity.App/ExecutionSession.cs`
- Modify: `src/ExecutionContinuity.Domain/Models.cs`
- Modify: `src/ExecutionContinuity.App/InboxPresentation.cs`
- Modify: `src/ExecutionContinuity.App/RouteListPresentation.cs`

**Step 1: Implement durable commands**

Add `OrganizeCaptureAsync` and `SetLanguagePreferenceAsync`, using the same candidate-save-then-publish pattern as existing commands. Preserve raw text and execution state.

**Step 2: Implement presentation filtering**

Return non-archived captures for Inbox, apply the selected organization filter, and search both raw and organized text. Search Routes by title and retained next action; keep project search explicitly unsupported because `Route` has no project field.

**Step 3: Run focused tests**

Run: `dotnet test tests/ExecutionContinuity.App.Tests/ExecutionContinuity.App.Tests.csproj --no-restore --filter FullyQualifiedName~Ui006 --verbosity minimal`

Expected: PASS.

### Task 4: Replace Inbox and Settings placeholders

**Files:**
- Modify: `src/ExecutionContinuity.App/MainWindow.xaml`
- Modify: `src/ExecutionContinuity.App/MainWindow.xaml.cs`

**Step 1: Add controls**

Add an Inbox search box, All/Unorganized/Organized segmented buttons, an organized-content editor, save action, and a raw-text view action. Add a three-option language selector in Settings.

**Step 2: Wire immediate rendering**

Keep the selected Inbox item and list scroll position where possible. Update UI strings immediately after a language selection, without altering user-authored route, step, note, or raw capture text.

**Step 3: Run full tests and build**

Run the solution build and test commands from the repository root. Expected: no build errors and all tests pass.

### Task 5: Document acceptance and model blocker

**Files:**
- Modify: `docs/MANUAL_ACCEPTANCE.md`
- Modify: `docs/UI_FIX_BACKLOG.md`

Record the UI-006 manual acceptance sequence, automated test counts, build evidence, and the explicit project-search blocker.

### Task 6: Verify and commit

Run sequentially:

```powershell
dotnet build ExecutionContinuity.slnx --no-restore --verbosity minimal
dotnet test ExecutionContinuity.slnx --no-build --verbosity minimal
git diff --check
git status --short --branch
```

Commit only the verified UI-006 changes with a focused message. Do not merge or push; leave the merge decision to the user.
