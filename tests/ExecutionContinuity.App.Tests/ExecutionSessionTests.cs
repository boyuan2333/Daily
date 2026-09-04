using ExecutionContinuity.App;
using ExecutionContinuity.Domain;
using ExecutionContinuity.Persistence;
using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace ExecutionContinuity.App.Tests;

public sealed class ExecutionSessionTests
{
    [Fact]
    public void Startup_diagnostics_are_available_to_the_generated_entry_point_and_unhandled_exception_path()
    {
        var diagnosticsType = typeof(App).Assembly.GetType("ExecutionContinuity.App.StartupDiagnostics");

        Assert.NotNull(diagnosticsType);
        Assert.NotNull(diagnosticsType.GetMethod(
            "Record",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));

        var format = diagnosticsType.GetMethod(
            "Format",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(format);

        var details = Assert.IsType<string>(format.Invoke(
            null,
            [new InvalidOperationException("startup failed"), new DateTimeOffset(2026, 8, 3, 8, 0, 0, TimeSpan.Zero)]));
        Assert.Contains("OccurredAtUtc: 2026-08-03T08:00:00.0000000+00:00", details);
        Assert.Contains("System.InvalidOperationException: startup failed", details);

        var write = diagnosticsType.GetMethod(
            "Write",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(write);

        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"execution-continuity-diagnostics-{Guid.NewGuid():N}");
        var temporaryPath = Path.Combine(temporaryDirectory, "startup-error.txt");
        try
        {
            Directory.CreateDirectory(temporaryDirectory);
            File.WriteAllText(temporaryPath, "stale diagnostics");

            write.Invoke(
                null,
                [new InvalidOperationException("fresh failure"), new DateTimeOffset(2026, 8, 3, 9, 0, 0, TimeSpan.Zero), temporaryPath]);

            var written = File.ReadAllText(temporaryPath);
            Assert.DoesNotContain("stale diagnostics", written);
            Assert.Contains("OccurredAtUtc: 2026-08-03T09:00:00.0000000+00:00", written);
            Assert.Contains("System.InvalidOperationException: fresh failure", written);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Startup_trace_records_opt_in_fixture_stages()
    {
        var diagnosticsType = typeof(App).Assembly.GetType("ExecutionContinuity.App.StartupDiagnostics");
        Assert.NotNull(diagnosticsType);
        var trace = diagnosticsType.GetMethod(
            "Trace",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(trace);

        var tracePath = Path.Combine(Path.GetTempPath(), $"execution-continuity-startup-{Guid.NewGuid():N}.log");
        try
        {
            trace.Invoke(null, ["Program.Main entered", tracePath]);

            var traceText = File.ReadAllText(tracePath);
            Assert.Contains("Stage=Program.Main entered", traceText);
        }
        finally
        {
            File.Delete(tracePath);
        }

        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        Assert.Contains("Program.Main", File.ReadAllText(Path.Combine(root, "src", "ExecutionContinuity.App", "Program.cs")));
        Assert.Contains("App constructor", File.ReadAllText(Path.Combine(root, "src", "ExecutionContinuity.App", "App.xaml.cs")));
        Assert.Contains("OnLaunched", File.ReadAllText(Path.Combine(root, "src", "ExecutionContinuity.App", "App.xaml.cs")));
        Assert.Contains("MainWindow constructor", File.ReadAllText(Path.Combine(root, "src", "ExecutionContinuity.App", "MainWindow.xaml.cs")));
        Assert.Contains("DatabaseLocator.Resolve", File.ReadAllText(Path.Combine(root, "src", "ExecutionContinuity.App", "DatabaseLocator.cs")));
        Assert.Contains("Activate", File.ReadAllText(Path.Combine(root, "src", "ExecutionContinuity.App", "App.xaml.cs")));
    }

    [Fact]
    public void Startup_window_probe_requires_the_Daily_window_title()
    {
        var scriptPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "tests", "Verify-ReleaseWindow.ps1"));
        var script = File.ReadAllText(scriptPath);

        Assert.Contains("[string]$ExpectedWindowTitle = \"Daily\"", script);
        Assert.Contains("[ReleaseWindowProbe]::TryFindWindow", script);
        Assert.Contains("$found -and $windowHandle -ne [IntPtr]::Zero", script);
        Assert.Contains("EnumWindows", script);
        Assert.Contains("GetWindowText", script);
        Assert.Contains("Title='$windowTitle'", script);
    }

    [Fact]
    public void Startup_window_probe_can_keep_the_verified_fixture_process_running()
    {
        var scriptPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "tests", "Verify-ReleaseWindow.ps1"));
        var script = File.ReadAllText(scriptPath);

        Assert.Contains("[switch]$KeepRunning", script);
        Assert.Contains("[string]$DatabasePath", script);
        Assert.Contains("[string]$StartupTracePath", script);
        Assert.Contains("$env:EXECUTION_CONTINUITY_DATABASE = $resolvedDatabasePath", script);
        Assert.Contains("$env:EXECUTION_CONTINUITY_STARTUP_TRACE = $resolvedStartupTracePath", script);
        Assert.Contains("$process = Start-Process -FilePath $resolvedExecutable -PassThru", script);
        Assert.Contains("DatabasePath='$resolvedDatabasePath'", script);
        Assert.Contains("StartupTracePath='$resolvedStartupTracePath'", script);
        Assert.Contains("if ($KeepRunning)", script);
        Assert.Contains("if (-not $KeepRunning", script);
        Assert.Contains("$process.Kill()", script);
    }

    [Fact]
    public void Main_window_explicitly_sets_the_native_window_title()
    {
        var codePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ExecutionContinuity.App", "MainWindow.xaml.cs"));
        var code = File.ReadAllText(codePath);

        Assert.Contains("this.Title = \"Daily\"", code);
        Assert.Contains("appWindow.Title = \"Daily\"", code);
    }

    [Fact]
    public void Database_locator_accepts_an_explicit_isolated_path()
    {
        var locatorType = typeof(MainWindow).Assembly.GetType("ExecutionContinuity.App.DatabaseLocator");
        Assert.NotNull(locatorType);

        var resolve = locatorType.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(resolve);

        var path = Path.Combine(Path.GetTempPath(), "ui004-fixture", "execution-continuity.db");
        var resolved = Assert.IsType<string>(resolve.Invoke(null, [path]));
        Assert.Equal(Path.GetFullPath(path), resolved);
    }

    [Fact]
    public void Window_presentation_uses_a_compact_desktop_default_and_transient_status_lifetimes()
    {
        Assert.Equal(960, WindowPresentation.DefaultWidth);
        Assert.Equal(680, WindowPresentation.DefaultHeight);
        Assert.Equal(TimeSpan.FromSeconds(3), WindowPresentation.StatusLifetime(isError: false));
        Assert.Equal(TimeSpan.FromSeconds(5), WindowPresentation.StatusLifetime(isError: true));
    }

    [Fact]
    public void Planning_surface_uses_crisp_stitch_inputs_and_reflows_when_narrow()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ExecutionContinuity.App", "MainWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);
        var codePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ExecutionContinuity.App", "MainWindow.xaml.cs"));
        var code = File.ReadAllText(codePath);

        Assert.Contains("x:Name=\"RouteTitleInputSurface\"", xaml);
        Assert.Contains("x:Name=\"BlockInputSurface\"", xaml);
        Assert.Contains("x:Name=\"PauseNoteInputSurface\"", xaml);
        Assert.Contains("x:Name=\"CaptureInputSurface\"", xaml);
        Assert.Contains("Background=\"#FFFFFF\" BorderBrush=\"#B8C4B9\" BorderThickness=\"1\" CornerRadius=\"8\"", xaml);
        Assert.Contains("x:Name=\"RouteTitleInput\" Background=\"Transparent\" BorderThickness=\"0\"", xaml);
        Assert.Equal(8, xaml.Split("Background=\"Transparent\" BorderThickness=\"0\" Padding=\"14,10\" FontFamily=\"Microsoft YaHei UI\" FontSize=\"15\"").Length - 1);
        Assert.Contains("x:Name=\"RouteListColumn\"", xaml);
        Assert.Contains("x:Name=\"RouteEditorRow\"", xaml);
        Assert.Contains("SizeChanged=\"PlanningPanel_SizeChanged\"", xaml);
        Assert.Contains("private void PlanningPanel_SizeChanged", code);
        Assert.Contains("const double CompactPlanningBreakpoint = 860", code);
    }

    [Fact]
    public void Compact_planning_uses_list_to_detail_state_and_restores_the_list_context()
    {
        var stateType = typeof(MainWindow).Assembly.GetType("ExecutionContinuity.App.ResponsivePlanningPresentation");
        Assert.NotNull(stateType);

        var create = stateType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
        var withWidth = stateType.GetMethod("WithWidth", BindingFlags.Public | BindingFlags.Instance);
        var openDetail = stateType.GetMethod("OpenDetail", BindingFlags.Public | BindingFlags.Instance);
        var returnToList = stateType.GetMethod("ReturnToList", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(create);
        Assert.NotNull(withWidth);
        Assert.NotNull(openDetail);
        Assert.NotNull(returnToList);

        var state = create.Invoke(null, [960d]);
        state = withWidth.Invoke(state, [520d]);
        var routeId = Guid.NewGuid();
        state = openDetail.Invoke(state, [PlanningDestination.Routes, routeId, 184d]);

        Assert.Equal("Route", stateType.GetProperty("Detail")?.GetValue(state)?.ToString());
        Assert.Equal(routeId, stateType.GetProperty("Routes")?.GetValue(state)?.GetType().GetProperty("SelectedItemId")?.GetValue(stateType.GetProperty("Routes")?.GetValue(state)));

        state = returnToList.Invoke(state, null);

        Assert.Equal("None", stateType.GetProperty("Detail")?.GetValue(state)?.ToString());
        var routes = stateType.GetProperty("Routes")?.GetValue(state);
        Assert.Equal(184d, routes?.GetType().GetProperty("ScrollOffset")?.GetValue(routes));
        Assert.Equal(routeId, routes?.GetType().GetProperty("SelectedItemId")?.GetValue(routes));
    }

    [Fact]
    public void Narrow_planning_shell_exposes_explicit_back_surfaces_and_responsive_width_constraints()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ExecutionContinuity.App", "MainWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);
        var codePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ExecutionContinuity.App", "MainWindow.xaml.cs"));
        var code = File.ReadAllText(codePath);

        Assert.Contains("x:Name=\"RouteBackButton\"", xaml);
        Assert.Contains("x:Name=\"InboxBackButton\"", xaml);
        Assert.Contains("x:Name=\"RouteListScrollViewer\"", xaml);
        Assert.Contains("x:Name=\"InboxListScrollViewer\"", xaml);
        Assert.Contains("x:Name=\"CompactRoutesNavigation\"", xaml);
        Assert.Contains("x:Name=\"CompactInboxNavigation\"", xaml);
        Assert.Contains("x:Name=\"CompactArchiveNavigation\"", xaml);
        Assert.Contains("MaxWidth=\"760\"", xaml);
        Assert.Contains("MaxWidth=\"520\"", xaml);
        Assert.Contains("MaxWidth=\"410\"", xaml);
        Assert.Contains("RouteBackButton_Click", code);
        Assert.Contains("InboxBackButton_Click", code);
        Assert.Contains("AutomationProperties.SetName(select, preview)", code);
        Assert.Contains("CompactRoutesNavigation.Visibility", code);
        Assert.Contains("CompactInboxNavigation.Visibility", code);
        Assert.Contains("CompactArchiveNavigation.Visibility", code);
        Assert.Contains("ResponsivePlanningPresentation", code);
    }

    [Fact]
    public void Compact_detail_layout_constrains_the_planning_grid_to_the_visible_content_viewport()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ExecutionContinuity.App", "MainWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);
        var codePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ExecutionContinuity.App", "MainWindow.xaml.cs"));
        var code = File.ReadAllText(codePath);

        Assert.Contains("<ScrollViewer x:Name=\"MainContentScrollViewer\"", xaml);
        Assert.Contains("x:Name=\"PlanningPanel\"", xaml);
        Assert.Contains("PlanningPanel.Height = ContentGrid.ActualHeight", code);
        Assert.Contains("RouteEditorScrollViewer.Visibility = ToVisibility(!compact || routeDetail)", code);
        Assert.Contains("InboxDetailWorkspace.Visibility = ToVisibility(!compact || inboxDetail)", code);
    }

    [Fact]
    public void Compact_detail_surfaces_stay_in_the_star_sized_detail_column()
    {
        var codePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ExecutionContinuity.App", "MainWindow.xaml.cs"));
        var code = File.ReadAllText(codePath);

        Assert.Contains("Grid.SetColumn(RouteEditorScrollViewer, 1);", code);
        Assert.Contains("Grid.SetColumn(InboxDetailWorkspace, 1);", code);
        Assert.DoesNotContain("Grid.SetColumn(RouteEditorScrollViewer, compact ? 0 : 1);", code);
        Assert.DoesNotContain("Grid.SetColumn(InboxDetailWorkspace, compact ? 0 : 1);", code);
    }

    [Fact]
    public void Planning_lists_give_inner_scroll_viewers_a_constrained_grid_row()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ExecutionContinuity.App", "MainWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("<ScrollViewer x:Name=\"RouteListScrollViewer\" Grid.Row=\"1\"", xaml);
        Assert.Contains("<ScrollViewer x:Name=\"InboxListScrollViewer\" Grid.Row=\"2\"", xaml);
    }

    [Fact]
    public void Segmented_control_uses_one_corner_radius_and_a_sliding_indicator()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ExecutionContinuity.App", "MainWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);
        var normalizedXaml = xaml.ReplaceLineEndings("\n");
        var codePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ExecutionContinuity.App", "MainWindow.xaml.cs"));
        var code = File.ReadAllText(codePath);

        Assert.Contains("x:Name=\"ModeSelectionIndicator\"", xaml);
        Assert.Contains("<Border Grid.Column=\"1\" Background=\"#F0F3EE\" CornerRadius=\"18\"", xaml);
        Assert.Contains("x:Name=\"ModeSelectionIndicator\"\n                            Width=\"100\"\n                            Background=\"#4A845E\"\n                            CornerRadius=\"18\"", normalizedXaml);
        Assert.Contains("x:Name=\"ModeSelectionTransform\"", xaml);
        Assert.Contains("TimeSpan.FromMilliseconds(300)", code);
        Assert.Contains("Storyboard.SetTargetProperty(animation, \"X\")", code);
        Assert.Contains("<Button x:Name=\"GuideModeButton\" Width=\"100\" HorizontalAlignment=\"Left\"", xaml);
        Assert.Contains("<Button x:Name=\"PlanningModeButton\" Width=\"100\" HorizontalAlignment=\"Right\"", xaml);
        Assert.Equal(2, xaml.Split("CornerRadius=\"18\" Content=\"").Length - 1);
    }

    [Fact]
    public void Guide_action_card_uses_the_shared_content_card_padding()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ExecutionContinuity.App", "MainWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains(
            "<Border Background=\"#FFFFFF\" CornerRadius=\"8\" BorderBrush=\"#4A845E\" BorderThickness=\"4,1,1,1\" Padding=\"20\">",
            xaml);
    }

    [Fact]
    public void Capture_context_lock_preserves_the_origin_while_the_surface_is_open()
    {
        var routeId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        var origin = new CaptureContext(
            PlanningMode: true,
            SettingsOpen: false,
            PlanningDestination.Inbox,
            EnteringBlock: false,
            routeId,
            stepId,
            ExecutionMode.Normal,
            CurrentAction: "Keep working from this exact action",
            MainScrollOffset: 184,
            RouteEditorScrollOffset: 36);
        var contextLock = new CaptureContextLock();

        contextLock.Open(origin);

        Assert.True(contextLock.IsOpen);
        Assert.False(contextLock.CanChangeUnderlyingContext);
        Assert.Equal(origin, contextLock.Origin);
    }

    [Fact]
    public void Capture_context_lock_releases_the_origin_only_for_success_or_explicit_cancel()
    {
        var origin = new CaptureContext(
            PlanningMode: false,
            SettingsOpen: false,
            PlanningDestination.Routes,
            EnteringBlock: true,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ExecutionMode.Normal,
            CurrentAction: "Original action",
            MainScrollOffset: 80,
            RouteEditorScrollOffset: 0);
        var contextLock = new CaptureContextLock();
        contextLock.Open(origin);

        var restoredAfterSave = contextLock.CompleteSave();

        Assert.Equal(origin, restoredAfterSave);
        Assert.False(contextLock.IsOpen);
        Assert.True(contextLock.CanChangeUnderlyingContext);

        contextLock.Open(origin);
        var restoredAfterCancel = contextLock.Cancel();

        Assert.Equal(origin, restoredAfterCancel);
        Assert.False(contextLock.IsOpen);
    }

    [Fact]
    public void Capture_surface_is_modal_across_the_entire_window()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ExecutionContinuity.App", "MainWindow.xaml"));
        var xaml = File.ReadAllText(xamlPath);
        var codePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "ExecutionContinuity.App", "MainWindow.xaml.cs"));
        var code = File.ReadAllText(codePath);

        Assert.Contains("x:Name=\"CaptureOverlay\" Grid.RowSpan=\"3\"", xaml);
        Assert.Contains("x:Name=\"CaptureOverlay\" Grid.RowSpan=\"3\" Visibility=\"Collapsed\"", xaml);
        Assert.Contains("x:Name=\"AppHeader\"", xaml);
        Assert.Contains("x:Name=\"BottomActionBar\"", xaml);
        Assert.Contains("TabFocusNavigation=\"Cycle\"", xaml);
        Assert.True(
            xaml.IndexOf("x:Name=\"StatusBar\"", StringComparison.Ordinal) >
            xaml.IndexOf("x:Name=\"CaptureOverlay\"", StringComparison.Ordinal));
        Assert.Contains("x:Name=\"StatusBar\" IsOpen=\"False\" IsClosable=\"False\" Grid.Row=\"1\"", xaml);
        Assert.Contains("Canvas.ZIndex=\"30\"", xaml);
        Assert.Contains("AppHeader.IsHitTestVisible = !_captureContext.IsOpen", code);
        Assert.Contains("ContentGrid.IsHitTestVisible = !_captureContext.IsOpen", code);
        Assert.Contains("BottomActionBar.IsHitTestVisible = !_captureContext.IsOpen", code);
    }

    [Fact]
    public void Planning_route_editor_distinguishes_primary_secondary_and_destructive_step_actions()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var xaml = File.ReadAllText(Path.Combine(root, "src", "ExecutionContinuity.App", "MainWindow.xaml"));
        var resourcesPath = Path.Combine(root, "src", "ExecutionContinuity.App", "App.xaml");
        var resources = File.ReadAllText(resourcesPath);
        var code = File.ReadAllText(Path.Combine(root, "src", "ExecutionContinuity.App", "MainWindow.xaml.cs"));
        var document = XDocument.Load(resourcesPath);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace xamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
        XNamespace controls = "using:Microsoft.UI.Xaml.Controls";

        var applicationResources = Assert.Single(document.Root!.Elements(presentation + "Application.Resources"));
        var resourceDictionary = Assert.Single(applicationResources.Elements(presentation + "ResourceDictionary"));
        var mergedDictionaries = Assert.Single(resourceDictionary.Elements(presentation + "ResourceDictionary.MergedDictionaries"));
        Assert.Single(mergedDictionaries.Elements(controls + "XamlControlsResources"));
        Assert.Equal(
            ["PlanningDestructiveButtonStyle", "PlanningPrimaryButtonStyle", "PlanningSecondaryButtonStyle"],
            resourceDictionary.Elements(presentation + "Style")
                .Select(style => (string)style.Attribute(xamlNamespace + "Key")!)
                .OrderBy(key => key)
                .ToArray());

        Assert.Contains("Style=\"{StaticResource PlanningSecondaryButtonStyle}\"", xaml);
        Assert.Contains("Style=\"{StaticResource PlanningDestructiveButtonStyle}\"", xaml);
        Assert.Contains("Style=\"{StaticResource PlanningPrimaryButtonStyle}\"", xaml);
        Assert.Contains("Resources=\"{StaticResource PlanningSecondaryButtonResources}\"", xaml);
        Assert.Contains("Resources=\"{StaticResource PlanningDestructiveButtonResources}\"", xaml);
        Assert.Contains("Resources=\"{StaticResource PlanningPrimaryButtonResources}\"", xaml);
        Assert.Contains("Glyph=\"&#xE710;\"", xaml);
        Assert.Contains("ClearStepButton_Click", xaml);
        Assert.Contains("ShowClearStepConfirmation", code);
        Assert.Contains("x:Key=\"PlanningPrimaryButtonStyle\"", resources);
        Assert.Contains("x:Key=\"PlanningSecondaryButtonStyle\"", resources);
        Assert.Contains("x:Key=\"PlanningDestructiveButtonStyle\"", resources);
        Assert.Contains("PointerOver", resources);
        Assert.Contains("#427A56", resources);
        Assert.Contains("#386A49", resources);
        Assert.Contains("FocusRing", resources);
        Assert.DoesNotContain("#FFFFFF\" />\n                            </VisualState.Setters>", resources);
    }

    [Fact]
    public void Route_list_groups_visible_routes_by_lifecycle_and_excludes_archived_routes()
    {
        var currentStep = Step.Create("Current action", "Current done", "Current boundary");
        var pausedStep = Step.Create("Paused action", "Paused done", "Paused boundary");
        var draftStep = Step.Create("Draft action", "Draft done", "Draft boundary");
        var completedStep = Step.Create("Completed action", "Completed done", "Completed boundary") with
        {
            IsCompleted = true
        };

        var current = Route.Create("Current route", currentStep) with { Lifecycle = RouteLifecycle.Active };
        var paused = Route.Create("Paused route", pausedStep) with { Lifecycle = RouteLifecycle.Paused };
        var draft = Route.Create("Draft route", draftStep);
        var completed = Route.Create("Completed route", completedStep) with { Lifecycle = RouteLifecycle.Completed };
        var archived = Route.Create("Archived route", Step.Create("Archived action", "Done", "Boundary")) with
        {
            Lifecycle = RouteLifecycle.Archived
        };
        var state = AppState.Restore(
            [current, paused, draft, completed, archived],
            new ExecutionState(current.Id, current.Steps[0].Id),
            [],
            []);

        var sections = RouteListPresentation.GroupByStatus(state);

        Assert.Equal(["Current", "Paused", "Draft", "Completed"], sections.Select(section => section.Title));
        Assert.Equal("Current route", sections[0].Routes.Single().Title);
        Assert.Equal("Paused route", sections[1].Routes.Single().Title);
        Assert.Equal("Draft route", sections[2].Routes.Single().Title);
        Assert.Equal("Completed route", sections[3].Routes.Single().Title);
        Assert.DoesNotContain(sections.SelectMany(section => section.Routes), route => route.Id == archived.Id);
    }

    [Fact]
    public void Route_list_orders_paused_routes_by_newest_valid_return_anchor()
    {
        var olderStep = Step.Create("Older action", "Done", "Boundary");
        var newerStep = Step.Create("Newer action", "Done", "Boundary");
        var older = Route.Create("Older paused", olderStep) with { Lifecycle = RouteLifecycle.Paused };
        var newer = Route.Create("Newer paused", newerStep) with { Lifecycle = RouteLifecycle.Paused };
        var state = AppState.Restore(
            [older, newer],
            new ExecutionState(null, null),
            [
                new ExecutionSnapshot(Guid.NewGuid(), older.Id, olderStep.Id, olderStep.Action, olderStep.CompletionStandard, olderStep.DoNotDo, olderStep.FallbackAction, new DateTimeOffset(2026, 9, 3, 8, 0, 0, TimeSpan.Zero), "older"),
                new ExecutionSnapshot(Guid.NewGuid(), newer.Id, newerStep.Id, newerStep.Action, newerStep.CompletionStandard, newerStep.DoNotDo, newerStep.FallbackAction, new DateTimeOffset(2026, 9, 4, 8, 0, 0, TimeSpan.Zero), "newer")
            ],
            []);

        var pausedSection = RouteListPresentation.GroupByStatus(state).Single(section => section.Title == "Paused");

        Assert.Equal(["Newer paused", "Older paused"], pausedSection.Routes.Select(route => route.Title));
    }

    [Fact]
    public void Route_list_item_uses_the_paused_anchor_and_reports_progress_and_read_only_context()
    {
        var completed = Step.Create("Already done", "Done", "Boundary") with { IsCompleted = true };
        var current = Step.Create("Resume this action", "Ship the small change", "Do not redesign", "Try the prepared fallback");
        var route = Route.Create("Paused route", completed, current) with { Lifecycle = RouteLifecycle.Paused };
        var pausedAt = new DateTimeOffset(2026, 9, 4, 8, 30, 0, TimeSpan.Zero);
        var state = AppState.Restore(
            [route],
            new ExecutionState(null, null),
            [new ExecutionSnapshot(Guid.NewGuid(), route.Id, current.Id, current.Action, current.CompletionStandard, current.DoNotDo, current.FallbackAction, pausedAt, "Waiting for review")],
            []);

        var item = RouteListPresentation.Describe(state, route);

        Assert.Equal("Resume this action", item.NextAction);
        Assert.Equal("1/2", item.ProgressText);
        Assert.Equal(pausedAt, item.PausedAt);
        Assert.Equal("Ship the small change", item.CompletionStandard);
        Assert.Equal("Do not redesign", item.DoNotDo);
        Assert.Equal("Waiting for review", item.PauseNote);
        Assert.Equal("Try the prepared fallback", item.FallbackAction);
    }

    [Fact]
    public void Routes_ui_exposes_status_grouping_and_declares_project_grouping_unavailable_without_project_data()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var xaml = File.ReadAllText(Path.Combine(root, "src", "ExecutionContinuity.App", "MainWindow.xaml"));
        var code = File.ReadAllText(Path.Combine(root, "src", "ExecutionContinuity.App", "MainWindow.xaml.cs"));

        Assert.Contains("x:Name=\"RoutesByStatusButton\"", xaml);
        Assert.Contains("x:Name=\"RoutesByProjectButton\"", xaml);
        Assert.Contains("IsEnabled=\"False\"", xaml);
        Assert.Contains("领域模型尚未提供项目归属", xaml);
        Assert.Contains("RouteListPresentation.GroupByStatus", code);
        Assert.Contains("RouteListPresentation.Describe", code);
        Assert.Contains("TogglePausedRouteButton_Click", code);
    }

    [Fact]
    public void Guide_presentation_exposes_only_the_commands_allowed_by_the_execution_mode()
    {
        var fallbackRoute = Route.Create("Fallback", Step.Create("Action", "Done", "Boundary", "Fallback action"));
        var fallbackState = StateTransitions.StartFallback(
            StateTransitions.SelectActiveRoute(AppState.Create(fallbackRoute), fallbackRoute.Id));
        var fallback = GuidePresentation.From(fallbackState);

        Assert.Equal(GuideScreen.Fallback, fallback.Screen);
        Assert.Equal("Fallback action", fallback.Action);
        Assert.True(fallback.CanCompleteFallback);
        Assert.False(fallback.CanCompleteCurrentStep);
        Assert.False(fallback.CanStartFallback);

        var blockedRoute = Route.Create("Blocked", Step.Create("Action", "Done", "Boundary"));
        var blockedState = StateTransitions.RecordBlockAndPause(
            StateTransitions.SelectActiveRoute(AppState.Create(blockedRoute), blockedRoute.Id),
            "I cannot find the file",
            DateTimeOffset.Now);
        var blocked = GuidePresentation.From(blockedState);

        Assert.Equal(GuideScreen.Blocked, blocked.Screen);
        Assert.True(blocked.CanReturnFromBlocked);
        Assert.True(blocked.CanPause);
        Assert.False(blocked.CanCompleteCurrentStep);

        var idle = GuidePresentation.From(AppState.Create());
        Assert.Equal(GuideScreen.NoActiveRoute, idle.Screen);
        Assert.True(idle.CanCapture);
    }

    [Fact]
    public void Guide_presentation_includes_route_and_step_context_for_an_active_route()
    {
        var route = Route.Create(
            "Prepare quarterly report",
            Step.Create("Collect source numbers", "The source workbook is complete", "Do not format the report yet"),
            Step.Create("Write the outline", "The sections are named", "Do not polish the prose yet"));
        var state = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id);

        var presentation = GuidePresentation.From(state);

        Assert.Equal("Prepare quarterly report", presentation.RouteTitle);
        Assert.Equal("1/2 步", presentation.StepProgress);
    }

    [Fact]
    public async Task Failed_capture_keeps_the_visible_and_durable_state_unchanged()
    {
        var path = NewDatabasePath();
        try
        {
            var route = Route.Create("Route", Step.Create("Action", "Done", "Boundary"));
            var initial = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id);
            await new SqliteStateStore(path).SaveAsync(initial);
            var session = new ExecutionSession(new SqliteStateStore(
                path,
                beforeCommit: () => throw new InvalidOperationException("injected write failure")));
            await session.LoadAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => session.CaptureAsync("interruption"));

            Assert.Empty(session.State.Captures);
            Assert.Equal(initial.Execution, session.State.Execution);
            Assert.Empty((await new ExecutionSession(new SqliteStateStore(path)).LoadAsync()).Captures);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task Failed_final_completion_does_not_show_the_no_active_route_state()
    {
        var path = NewDatabasePath();
        try
        {
            var route = Route.Create("Route", Step.Create("Action", "Done", "Boundary"));
            var initial = StateTransitions.SelectActiveRoute(AppState.Create(route), route.Id);
            await new SqliteStateStore(path).SaveAsync(initial);
            var session = new ExecutionSession(new SqliteStateStore(
                path,
                beforeCommit: () => throw new InvalidOperationException("injected write failure")));
            await session.LoadAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => session.CompleteCurrentStepAsync());

            Assert.Equal(route.Id, session.State.Execution.ActiveRouteId);
            Assert.False(session.State.Route(route.Id).Steps.Single().IsCompleted);
            var reloaded = await new ExecutionSession(new SqliteStateStore(path)).LoadAsync();
            Assert.Equal(route.Id, reloaded.Execution.ActiveRouteId);
            Assert.False(reloaded.Route(route.Id).Steps.Single().IsCompleted);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task Failed_route_switch_keeps_the_original_active_route_after_restart()
    {
        var path = NewDatabasePath();
        try
        {
            var oldRoute = Route.Create("Old", Step.Create("Old action", "Done", "Boundary"));
            var newRoute = Route.Create("New", Step.Create("New action", "Done", "Boundary"));
            var initial = StateTransitions.SelectActiveRoute(AppState.Create(oldRoute, newRoute), oldRoute.Id);
            await new SqliteStateStore(path).SaveAsync(initial);
            var session = new ExecutionSession(new SqliteStateStore(
                path,
                beforeCommit: () => throw new InvalidOperationException("injected write failure")));
            await session.LoadAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => session.ActivateRouteAsync(newRoute.Id));

            Assert.Equal(oldRoute.Id, session.State.Execution.ActiveRouteId);
            var reloaded = await new ExecutionSession(new SqliteStateStore(path)).LoadAsync();
            Assert.Equal(oldRoute.Id, reloaded.Execution.ActiveRouteId);
            Assert.Empty(reloaded.Snapshots);
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task Fallback_and_blocked_guide_states_survive_session_recreation()
    {
        var fallbackPath = NewDatabasePath();
        var blockedPath = NewDatabasePath();
        try
        {
            var fallbackRoute = Route.Create("Fallback", Step.Create("Action", "Done", "Boundary", "Fallback action"));
            var fallback = new ExecutionSession(new SqliteStateStore(fallbackPath));
            await fallback.LoadAsync();
            await fallback.AddRouteAsync(fallbackRoute);
            await fallback.ActivateRouteAsync(fallbackRoute.Id);
            await fallback.StartFallbackAsync();

            var recoveredFallback = await new ExecutionSession(new SqliteStateStore(fallbackPath)).LoadAsync();
            Assert.Equal(ExecutionMode.Fallback, recoveredFallback.Execution.Mode);
            Assert.Equal(fallbackRoute.Steps.Single().Id, recoveredFallback.Execution.CurrentStepId);

            var blockedRoute = Route.Create("Blocked", Step.Create("Action", "Done", "Boundary"));
            var blocked = new ExecutionSession(new SqliteStateStore(blockedPath));
            await blocked.LoadAsync();
            await blocked.AddRouteAsync(blockedRoute);
            await blocked.ActivateRouteAsync(blockedRoute.Id);
            await blocked.RecordBlockAndPauseAsync("I cannot find the file");

            var recoveredBlocked = new ExecutionSession(new SqliteStateStore(blockedPath));
            await recoveredBlocked.LoadAsync();
            Assert.Equal(ExecutionMode.Blocked, recoveredBlocked.State.Execution.Mode);
            await recoveredBlocked.ReturnFromBlockedAsync();
            Assert.Equal(ExecutionMode.Normal, recoveredBlocked.State.Execution.Mode);
        }
        finally
        {
            DeleteDatabase(fallbackPath);
            DeleteDatabase(blockedPath);
        }
    }

    private static string NewDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"execution-continuity-app-{Guid.NewGuid():N}.db");

    private static void DeleteDatabase(string path)
    {
        foreach (var candidate in new[] { path, $"{path}-wal", $"{path}-shm" })
        {
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
    }
}
