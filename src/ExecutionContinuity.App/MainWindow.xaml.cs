using ExecutionContinuity.Domain;
using ExecutionContinuity.Persistence;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;
using WinRT.Interop;

namespace ExecutionContinuity.App;

public sealed partial class MainWindow : Window
{
    private const double CompactPlanningBreakpoint = 860;

    private readonly ExecutionSession _session;
    private readonly CaptureContextLock _captureContext = new();
    private static readonly SolidColorBrush SelectedButtonBrush = new(Colors.SeaGreen);
    private static readonly SolidColorBrush TransparentBrush = new(Colors.Transparent);
    private static readonly SolidColorBrush WhiteBrush = new(Colors.White);
    private static readonly SolidColorBrush TextBrush = new(Colors.Black);
    private DispatcherQueueTimer? _statusTimer;
    private bool? _modeIndicatorPlanningSelection;
    private bool _planningMode;
    private bool _settingsOpen;
    private bool _enteringBlock;
    private PlanningDestination _planningDestination = PlanningDestination.Routes;
    private Guid? _editingRouteId;
    private Guid? _editingStepId;
    private int? _editingStepIndex;
    private bool _editingStepWasCompleted;
    private Guid? _convertingCaptureId;
    private ResponsivePlanningPresentation _responsivePlanning = ResponsivePlanningPresentation.Create(WindowPresentation.DefaultWidth);
    private readonly List<Step> _draftSteps = new();
    private readonly HashSet<Guid> _expandedPausedRoutes = new();
    private InboxFilter _inboxFilter = InboxFilter.All;
    private string _inboxSearchText = string.Empty;
    private string _routeSearchText = string.Empty;
    private bool _groupRoutesByProject;
    private string _taskSearchText = string.Empty;
    private TasksView _tasksView = TasksView.Calendar;
    private TimeOnly? _editingStepPlannedTime;
    private bool _planningDestinationChosen;
    private RoutesSectionView _routesSectionView = RoutesSectionView.Routes;
    private TextBox? _organizedCaptureInput;
    private Guid? _organizedCaptureId;

    public MainWindow()
    {
        StartupDiagnostics.Trace("MainWindow constructor entered");
        StartupDiagnostics.Trace("MainWindow constructor before InitializeComponent");
        InitializeComponent();
        StartupDiagnostics.Trace("MainWindow constructor after InitializeComponent");
        this.Title = "Daily";
        StartupDiagnostics.Trace("MainWindow constructor after Title");
        SetDefaultWindowSize();
        StartupDiagnostics.Trace("MainWindow constructor before DatabaseLocator.Resolve");
        var databasePath = DatabaseLocator.Resolve();
        StartupDiagnostics.Trace("MainWindow constructor after DatabaseLocator.Resolve");
        IStateStore store = new SqliteStateStore(databasePath);
        if (string.Equals(
                Environment.GetEnvironmentVariable(DatabaseLocator.FailSaveEnvironmentVariable),
                "1",
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                Environment.GetEnvironmentVariable(DatabaseLocator.FailSaveEnvironmentVariable),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            store = new FailSaveStateStore(store);
        }

        StartupDiagnostics.Trace($"State store={store.GetType().Name}");
        _session = new ExecutionSession(store);
        Activated += MainWindow_Activated;
        StartupDiagnostics.Trace("MainWindow constructor completed");
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= MainWindow_Activated;
        await LoadAndRenderAsync();
    }

    private void SetDefaultWindowSize()
    {
        var windowId = Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Title = "Daily";
        appWindow.Resize(new SizeInt32(
            WindowPresentation.DefaultWidth,
            WindowPresentation.DefaultHeight));
    }

    private async Task LoadAndRenderAsync()
    {
        await RunAsync(async () => await _session.LoadAsync());
    }

    private void Render(string? confirmation = null)
    {
        ApplyLanguage();
        var presentation = GuidePresentation.From(_session.State);
        GuidePanel.Visibility = ToVisibility(!_settingsOpen && !_planningMode && presentation.Screen != GuideScreen.NoActiveRoute);
        NoActivePanel.Visibility = ToVisibility(!_settingsOpen && !_planningMode && presentation.Screen == GuideScreen.NoActiveRoute);
        PlanningPanel.Visibility = ToVisibility(!_settingsOpen && _planningMode);
        SettingsPanel.Visibility = ToVisibility(_settingsOpen);
        CaptureOverlay.Visibility = ToVisibility(_captureContext.IsOpen);
        AppHeader.IsHitTestVisible = !_captureContext.IsOpen;
        ContentGrid.IsHitTestVisible = !_captureContext.IsOpen;
        BottomActionBar.IsHitTestVisible = !_captureContext.IsOpen;
        SetSelected(GuideModeButton, !_settingsOpen && !_planningMode);
        SetSelected(PlanningModeButton, !_settingsOpen && _planningMode);
        UpdateModeSelectionIndicator();

        if (confirmation is not null)
        {
            ShowStatus(confirmation, InfoBarSeverity.Success);
        }

        if (_planningMode)
        {
            RenderPlanning();
            UpdatePlanningLayout(PlanningPanel.ActualWidth);
            return;
        }

        if (presentation.Screen == GuideScreen.NoActiveRoute)
        {
            return;
        }

        GuideHeading.Text = presentation.Screen switch
        {
            GuideScreen.Fallback => "预设备选动作",
            GuideScreen.Blocked => "已暂停在此处",
            _ => "下一步动作"
        };
        GuideRouteText.Text = presentation.RouteTitle ?? string.Empty;
        GuideProgressText.Text = presentation.StepProgress ?? string.Empty;
        GuideActionText.Text = presentation.Action ?? string.Empty;
        GuidancePanel.Visibility = ToVisibility(presentation.CompletionStandard is not null);
        CompletionText.Text = presentation.CompletionStandard ?? string.Empty;
        BoundaryText.Text = presentation.DoNotDo ?? string.Empty;
        NormalControls.Visibility = ToVisibility(presentation.Screen == GuideScreen.CurrentAction && !_enteringBlock);
        BlockEntryPanel.Visibility = ToVisibility(_enteringBlock);
        FallbackControls.Visibility = ToVisibility(presentation.CanCompleteFallback);
        BlockedControls.Visibility = ToVisibility(presentation.Screen == GuideScreen.Blocked);
        PausePanel.Visibility = ToVisibility(presentation.CanPause && !_enteringBlock && presentation.Screen != GuideScreen.Blocked);
    }

    private void RenderPlanning()
    {
        var routesSection = _planningDestination == PlanningDestination.Routes;
        var showingTaskFlows = routesSection && _routesSectionView == RoutesSectionView.TaskFlows;
        TasksWorkspace.Visibility = ToVisibility(_planningDestination == PlanningDestination.Tasks);
        RoutesWorkspace.Visibility = ToVisibility(routesSection && !showingTaskFlows);
        TaskFlowsWorkspace.Visibility = ToVisibility(showingTaskFlows);
        InboxWorkspace.Visibility = ToVisibility(_planningDestination == PlanningDestination.Inbox);
        ReviewWorkspace.Visibility = ToVisibility(_planningDestination == PlanningDestination.Review);
        ArchiveWorkspace.Visibility = ToVisibility(_planningDestination == PlanningDestination.Archive);
        SyncNavigationSelection();
        SetSelected(RoutesListViewButton, _routesSectionView == RoutesSectionView.Routes);
        SetSelected(TaskFlowsListViewButton, _routesSectionView == RoutesSectionView.TaskFlows);
        SetSelected(RoutesListSwitcherButton, _routesSectionView == RoutesSectionView.Routes);
        SetSelected(TaskFlowsListSwitcherButton, _routesSectionView == RoutesSectionView.TaskFlows);
        SetSelected(RoutesByStatusButton, !_groupRoutesByProject);
        SetSelected(RoutesByProjectButton, _groupRoutesByProject);
        SetSelected(TasksCalendarButton, _tasksView == TasksView.Calendar);
        SetSelected(TasksListButton, _tasksView == TasksView.List);
        RenderReviewTimeline();
        RenderTaskBoard();
        RenderTaskFlows();
        SetSelected(InboxAllButton, _inboxFilter == InboxFilter.All);
        SetSelected(InboxUnorganizedButton, _inboxFilter == InboxFilter.Unorganized);
        SetSelected(InboxOrganizedButton, _inboxFilter == InboxFilter.Organized);
        RenderDraftSteps();
        RouteListPanel.Children.Clear();
        var matchingRouteIds = RouteListPresentation.Search(_session.State, _routeSearchText)
            .Select(route => route.Id)
            .ToHashSet();
        var sections = (_groupRoutesByProject
                ? RouteListPresentation.GroupByProject(_session.State)
                : RouteListPresentation.GroupByStatus(_session.State))
            .Select(section => new RouteListSection(
                section.Title,
                section.Routes.Where(route => matchingRouteIds.Contains(route.Id)).ToArray()))
            .Where(section => section.Routes.Count > 0)
            .ToArray();
        foreach (var section in sections)
        {
            RouteListPanel.Children.Add(new TextBlock
            {
                Text = SectionText(section.Title),
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                Margin = new Thickness(0, 8, 0, 0)
            });

            foreach (var route in section.Routes)
            {
                var item = RouteListPresentation.Describe(_session.State, route);
                var line = new StackPanel { Spacing = 6 };
                line.Children.Add(new TextBlock
                {
                    Text = route.Title,
                    Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
                });
                line.Children.Add(new TextBlock
                {
                    Text = $"{item.NextAction} · {item.ProgressText} {T("步", "steps")}",
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.78
                });
                if (item.PausedAt is DateTimeOffset pausedAt)
                {
                    line.Children.Add(new TextBlock
                    {
                        Text = $"{T("暂停于", "Paused at")}：{pausedAt.LocalDateTime:g}",
                        Opacity = 0.62
                    });
                }

                var primary = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                primary.Children.Add(RowAction(T("查看", "View"), route.Id, OpenRouteDetailButton_Click));
                if (route.Lifecycle != RouteLifecycle.Archived)
                {
                    primary.Children.Add(RowAction(T("编辑", "Edit"), route.Id, EditRouteButton_Click));
                }

                line.Children.Add(primary);

                var secondary = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                if (route.Lifecycle is RouteLifecycle.Draft or RouteLifecycle.Paused && route.CurrentStep() is not null)
                {
                    secondary.Children.Add(RowAction(T("设为当前", "Make current"), route.Id, ActivateRouteButton_Click));
                }

                if (route.Lifecycle != RouteLifecycle.Archived && route.Steps.Count > 0)
                {
                    secondary.Children.Add(RowAction(T("存为任务流", "Save as task flow"), route.Id, SaveTaskFlowFromRouteButton_Click));
                }

                if (route.Id != _session.State.Execution.ActiveRouteId && route.Lifecycle != RouteLifecycle.Archived)
                {
                    secondary.Children.Add(RowAction(T("归档", "Archive"), route.Id, ArchiveRouteButton_Click));
                }

                if (secondary.Children.Count > 0)
                {
                    line.Children.Add(secondary);
                }

                if (item.IsPaused)
                {
                    var disclosure = new Button
                    {
                        Content = _expandedPausedRoutes.Contains(route.Id) ? T("收起返回上下文", "Hide return context") : T("展开返回上下文", "Show return context"),
                        Tag = route.Id.ToString(),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Background = TransparentBrush,
                        BorderBrush = TransparentBrush,
                        Padding = new Thickness(0, 4, 0, 4)
                    };
                    disclosure.Click += TogglePausedRouteButton_Click;
                    line.Children.Add(disclosure);
                    if (_expandedPausedRoutes.Contains(route.Id))
                    {
                        AddPausedContext(line, item);
                    }
                }

                RouteListPanel.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Colors.White),
                    BorderBrush = new SolidColorBrush(Colors.LightGray),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12),
                    Child = line
                });
            }
        }

        if (sections.Length == 0)
        {
            RouteListPanel.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(_routeSearchText)
                    ? T("尚无路线。", "No routes yet.")
                    : T("没有匹配的路线。", "No matching routes."),
                Opacity = 0.68
            });
        }

        var visibleCaptures = InboxPresentation.Filter(_session.State.Captures, _inboxFilter, _inboxSearchText);
        if (_responsivePlanning.Inbox.SelectedItemId is not Guid selectedCaptureId || visibleCaptures.All(capture => capture.Id != selectedCaptureId))
        {
            _responsivePlanning = _responsivePlanning with
            {
                Inbox = _responsivePlanning.Inbox with { SelectedItemId = visibleCaptures.FirstOrDefault()?.Id }
            };
        }

        InboxPanel.Children.Clear();
        foreach (var capture in visibleCaptures)
        {
            var content = new StackPanel { Spacing = 5 };
            var displayText = InboxPresentation.DisplayText(capture);
            var preview = displayText.Length > 72 ? $"{displayText[..72]}..." : displayText;
            content.Children.Add(new TextBlock { Text = preview, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = capture.CapturedAt.LocalDateTime.ToString("g"), Opacity = 0.62 });
            var select = new Button
            {
                Content = content,
                Tag = capture.Id.ToString(),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = capture.Id == _responsivePlanning.Inbox.SelectedItemId ? new SolidColorBrush(Colors.Honeydew) : TransparentBrush,
                BorderBrush = capture.Id == _responsivePlanning.Inbox.SelectedItemId ? SelectedButtonBrush : TransparentBrush,
                BorderThickness = new Thickness(capture.Id == _responsivePlanning.Inbox.SelectedItemId ? 1 : 0),
                Padding = new Thickness(12)
            };
            AutomationProperties.SetName(select, preview);
            select.Click += SelectCaptureButton_Click;
            InboxPanel.Children.Add(select);
        }

        if (visibleCaptures.Count == 0)
        {
            InboxPanel.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(_inboxSearchText) && _inboxFilter == InboxFilter.All
                    ? T("尚未捕捉到想法。", "No captured ideas yet.")
                    : T("没有匹配的想法。", "No matching ideas."),
                Opacity = 0.68
            });
        }

        RenderInboxDetail(visibleCaptures.SingleOrDefault(capture => capture.Id == _responsivePlanning.Inbox.SelectedItemId));
        RenderArchive();
        UpdatePlanningLayout(PlanningPanel.ActualWidth);
    }

    private void RenderArchive()
    {
        ArchivedRoutesPanel.Children.Clear();
        var archivedRoutes = _session.State.Routes
            .Where(route => route.Lifecycle == RouteLifecycle.Archived)
            .OrderBy(route => route.Title)
            .ToArray();
        foreach (var route in archivedRoutes)
        {
            var line = new StackPanel { Spacing = 5 };
            line.Children.Add(new TextBlock { Text = route.Title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            line.Children.Add(new TextBlock { Text = $"{T("下一动作", "Next action")}：{route.CurrentStep()?.Action ?? T("没有未完成的动作", "No unfinished action")}", Opacity = 0.72, TextWrapping = TextWrapping.Wrap });
            line.Children.Add(new TextBlock { Text = $"{T("恢复后状态", "Status after restore")}：{LifecycleText(route.LifecycleBeforeArchive ?? RouteLifecycle.Draft)}", Opacity = 0.62 });
            var restore = new Button { Content = T("恢复路线", "Restore route"), Tag = route.Id.ToString(), HorizontalAlignment = HorizontalAlignment.Left };
            restore.Click += RestoreArchivedRouteButton_Click;
            line.Children.Add(restore);
            ArchivedRoutesPanel.Children.Add(line);
        }

        if (archivedRoutes.Length == 0)
        {
            ArchivedRoutesPanel.Children.Add(new TextBlock { Text = T("没有已归档路线。", "No archived routes."), Opacity = 0.68 });
        }

        ArchivedCapturesPanel.Children.Clear();
        var archivedCaptures = _session.State.Captures
            .Where(capture => capture.IsArchived)
            .OrderByDescending(capture => capture.CapturedAt)
            .ToArray();
        foreach (var capture in archivedCaptures)
        {
            var line = new StackPanel { Spacing = 5 };
            var preview = capture.RawText.Length > 100 ? $"{capture.RawText[..100]}..." : capture.RawText;
            line.Children.Add(new TextBlock { Text = preview, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            line.Children.Add(new TextBlock { Text = $"{T("捕捉时间", "Captured at")}：{capture.CapturedAt.LocalDateTime:g}", Opacity = 0.62 });
            var restore = new Button { Content = T("恢复想法", "Restore idea"), Tag = capture.Id.ToString(), HorizontalAlignment = HorizontalAlignment.Left };
            restore.Click += RestoreArchivedCaptureButton_Click;
            line.Children.Add(restore);
            ArchivedCapturesPanel.Children.Add(line);
        }

        if (archivedCaptures.Length == 0)
        {
            ArchivedCapturesPanel.Children.Add(new TextBlock { Text = T("没有已归档收件箱条目。", "No archived inbox entries."), Opacity = 0.68 });
        }
    }

    private void RoutesViewButton_Click(object sender, RoutedEventArgs e)
    {
        _routesSectionView = RoutesSectionView.Routes;
        RenderPlanning();
    }

    private void TaskFlowsViewButton_Click(object sender, RoutedEventArgs e)
    {
        _routesSectionView = RoutesSectionView.TaskFlows;
        RenderPlanning();
    }

    private async void StartTaskFlowButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadTag(sender, out var taskFlowId))
        {
            return;
        }

        await RunAsync(
            () => _session.StartTaskFlowAsync(taskFlowId),
            "任务流已开始。",
            () => _planningMode = false);
    }

    private async void SaveTaskFlowFromRouteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadTag(sender, out var routeId))
        {
            return;
        }

        var title = _session.State.Route(routeId).Title;
        await RunAsync(
            () => _session.CreateTaskFlowFromRouteAsync(routeId, title),
            "已存为任务流。");
    }

    private void RenderTaskFlows()
    {
        TaskFlowsPanel.Children.Clear();
        var flows = TaskFlowPresentation.Flows(_session.State);
        if (flows.Count == 0)
        {
            TaskFlowsPanel.Children.Add(new TextBlock
            {
                Text = T(
                    "还没有任务流。可以在「路线」里把一条路线存为任务流。",
                    "No task flows yet. Save a route as a task flow from Routes."),
                Opacity = 0.68,
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var summary in flows)
        {
            var line = new StackPanel { Spacing = 6 };
            line.Children.Add(new TextBlock
            {
                Text = summary.Flow.Title,
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                TextWrapping = TextWrapping.Wrap
            });
            line.Children.Add(new TextBlock
            {
                Text = $"{summary.StepCount} {T("步", "steps")}",
                Opacity = 0.72
            });

            var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var start = new Button
            {
                Content = summary.HasUnfinishedInstance
                    ? T("继续本次", "Continue this run")
                    : T("开始整组", "Start the group"),
                Tag = summary.Flow.Id.ToString()
            };
            start.Click += StartTaskFlowButton_Click;
            controls.Children.Add(start);
            line.Children.Add(controls);

            TaskFlowsPanel.Children.Add(new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Colors.LightGray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Child = line
            });
        }
    }

    private void TaskSearchInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        _taskSearchText = TaskSearchInput.Text;
        if (_planningMode && _planningDestination == PlanningDestination.Tasks)
        {
            RenderPlanning();
        }
    }

    private void TasksViewButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string view } ||
            !Enum.TryParse<TasksView>(view, out var parsed))
        {
            return;
        }

        _tasksView = parsed;
        RenderPlanning();
    }

    private void RenderTaskBoard()
    {
        var state = _session.State;
        TaskBoardPanel.Children.Clear();
        var tasks = TaskBoardPresentation.Tasks(state, _taskSearchText);
        if (tasks.Count == 0)
        {
            TaskBoardPanel.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(_taskSearchText)
                    ? T("还没有任务。先在「路线」里准备步骤。", "No tasks yet. Prepare steps inside Routes first.")
                    : T("没有匹配的任务。", "No matching tasks."),
                Opacity = 0.68
            });
            return;
        }

        if (_tasksView == TasksView.List)
        {
            foreach (var task in tasks)
            {
                TaskBoardPanel.Children.Add(BuildTaskRow(task));
            }

            return;
        }

        foreach (var bucket in TaskBoardPresentation.CalendarBuckets(state, _taskSearchText))
        {
            TaskBoardPanel.Children.Add(new TextBlock
            {
                Text = bucket.Date is DateOnly date ? date.ToString("D") : T("无日期", "Unscheduled"),
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                Margin = new Thickness(0, 6, 0, 0)
            });
            foreach (var task in bucket.Tasks)
            {
                TaskBoardPanel.Children.Add(BuildTaskRow(task));
            }
        }
    }

    private Border BuildTaskRow(TaskProjection task)
    {
        var line = new StackPanel { Spacing = 4 };
        line.Children.Add(new TextBlock
        {
            Text = task.Action,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            TextWrapping = TextWrapping.Wrap
        });
        line.Children.Add(new TextBlock
        {
            Text = $"{task.RouteTitle} · {LifecycleText(task.RouteLifecycle)}",
            Opacity = 0.72
        });
        if (!task.IsReady)
        {
            line.Children.Add(new TextBlock
            {
                Text = T("尚未准备好（缺少完成标准）", "Not ready yet (no completion standard)"),
                Foreground = new SolidColorBrush(Colors.DarkOrange)
            });
        }

        return new Border
        {
            Background = new SolidColorBrush(Colors.White),
            BorderBrush = new SolidColorBrush(Colors.LightGray),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Child = line
        };
    }

    private DateOnly? ReadStepDate() =>
        StepDatePicker.Date is DateTimeOffset value ? DateOnly.FromDateTime(value.Date) : null;

    private void SetStepDate(DateOnly? value) =>
        StepDatePicker.Date = value is null
            ? null
            : new DateTimeOffset(value.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static Button RowAction(string text, Guid routeId, RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = text,
            Tag = routeId.ToString(),
            Padding = new Thickness(10, 4, 10, 4),
            FontSize = 13
        };
        button.Click += handler;
        return button;
    }

    private void RenderReviewTimeline()
    {
        ReviewTimelinePanel.Children.Clear();
        var entries = ReviewPresentation.Timeline(_session.State);
        if (entries.Count == 0)
        {
            ReviewTimelinePanel.Children.Add(new TextBlock
            {
                Text = T("还没有可用的历史记录。", "No recorded history yet."),
                Opacity = 0.68
            });
            return;
        }

        foreach (var entry in entries)
        {
            var line = new StackPanel { Spacing = 4 };
            line.Children.Add(new TextBlock
            {
                Text = $"{UiText.HistoryKindLabel(_session.State.LanguagePreference, entry.Kind)}：{entry.Summary}",
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                TextWrapping = TextWrapping.Wrap
            });
            line.Children.Add(new TextBlock
            {
                Text = entry.OccurredAt.LocalDateTime.ToString("g"),
                Opacity = 0.62
            });
            ReviewTimelinePanel.Children.Add(new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Colors.LightGray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Child = line
            });
        }
    }

    private static string SectionText(string title) => title switch
    {
        "Current" => "当前路线",
        "Paused" => "已暂停",
        "Draft" => "草稿",
        "Completed" => "已完成",
        RouteListPresentation.UnassignedSectionTitle => "未分配",
        _ => title
    };

    private static void AddPausedContext(StackPanel target, RouteListItem item)
    {
        var context = new StackPanel { Spacing = 3, Margin = new Thickness(0, 2, 0, 0) };
        AddContextLine(context, "完成标准", item.CompletionStandard);
        AddContextLine(context, "不要做", item.DoNotDo);
        AddContextLine(context, "暂停备注", item.PauseNote);
        AddContextLine(context, "预设备选动作", item.FallbackAction);
        target.Children.Add(new Border
        {
            Background = new SolidColorBrush(Colors.Honeydew),
            BorderBrush = new SolidColorBrush(Colors.LightGray),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = context
        });
    }

    private static void AddContextLine(StackPanel target, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        target.Children.Add(new TextBlock
        {
            Text = $"{label}：{value}",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.78
        });
    }

    private void RoutesByStatusButton_Click(object sender, RoutedEventArgs e)
    {
        _groupRoutesByProject = false;
        RenderPlanning();
    }

    private void RoutesByProjectButton_Click(object sender, RoutedEventArgs e)
    {
        _groupRoutesByProject = true;
        RenderPlanning();
    }

    private void RouteSearchInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        _routeSearchText = RouteSearchInput.Text;
        if (_planningMode && _planningDestination == PlanningDestination.Routes)
        {
            RenderPlanning();
        }
    }

    private void InboxSearchInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        _inboxSearchText = InboxSearchInput.Text;
        if (_planningMode && _planningDestination == PlanningDestination.Inbox)
        {
            RenderPlanning();
        }
    }

    private void InboxFilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string filter } ||
            !Enum.TryParse<InboxFilter>(filter, out var parsed))
        {
            return;
        }

        _inboxFilter = parsed;
        RenderPlanning();
    }

    private void TogglePausedRouteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadTag(sender, out var routeId))
        {
            return;
        }

        if (!_expandedPausedRoutes.Add(routeId))
        {
            _expandedPausedRoutes.Remove(routeId);
        }

        RenderPlanning();
    }

    private void RenderInboxDetail(CaptureEntry? capture)
    {
        InboxDetailPanel.Children.Clear();
        _organizedCaptureInput = null;
        _organizedCaptureId = null;
        if (capture is null)
        {
            InboxDetailPanel.Children.Add(new TextBlock { Text = T("选择一条已捕捉的想法进行查看。", "Select a captured idea to view it."), FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            InboxDetailPanel.Children.Add(new TextBlock { Text = T("底部栏随时可以捕捉想法。", "Capture is always available in the bottom bar."), Foreground = new SolidColorBrush(Colors.DimGray) });
            return;
        }

        InboxDetailPanel.Children.Add(new TextBlock { Text = T("已捕捉的想法", "Captured idea"), FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        InboxDetailPanel.Children.Add(new TextBlock { Text = $"{T("捕捉时间", "Captured at")}：{capture.CapturedAt.LocalDateTime:g}", Foreground = new SolidColorBrush(Colors.DimGray) });

        InboxDetailPanel.Children.Add(new TextBlock { Text = T("整理后内容", "Organized content"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 0) });
        _organizedCaptureId = capture.Id;
        _organizedCaptureInput = new TextBox
        {
            Text = capture.OrganizedText ?? string.Empty,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 110,
            PlaceholderText = T("可选：写下整理后的内容", "Optional: write an organized version")
        };
        InboxDetailPanel.Children.Add(_organizedCaptureInput);
        var saveOrganized = new Button
        {
            Content = T("保存整理内容", "Save organized content"),
            Tag = capture.Id.ToString(),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        saveOrganized.Click += SaveOrganizedCaptureButton_Click;
        InboxDetailPanel.Children.Add(saveOrganized);

        InboxDetailPanel.Children.Add(new TextBlock { Text = T("原始记录（不可变）", "Original record (immutable)"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 0) });
        var original = new Border
        {
            Background = new SolidColorBrush(Colors.White),
            BorderBrush = new SolidColorBrush(Colors.LightGray),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20),
            Child = new TextBlock { Text = capture.RawText, TextWrapping = TextWrapping.Wrap, FontSize = 18 }
        };
        InboxDetailPanel.Children.Add(original);
        var actions = new StackPanel { Spacing = 12, Margin = new Thickness(0, 18, 0, 0) };

        var convert = new Button
        {
            Tag = capture.Id.ToString(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(ColorHelper.FromArgb(255, 238, 247, 238)),
            BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 184, 210, 186)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 14)
        };
        var convertLayout = new Grid { ColumnSpacing = 14 };
        convertLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        convertLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var convertCopy = new StackPanel { Spacing = 4 };
        convertCopy.Children.Add(new TextBlock { Text = T("整理为路线草稿", "Organize into a draft route"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 16, Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 42, 90, 59)) });
        convertCopy.Children.Add(new TextBlock { Text = T("带入规划继续整理，原始想法仍会保留。", "Continue organizing in planning; the original idea is kept."), TextWrapping = TextWrapping.Wrap, Opacity = 0.72 });
        convertLayout.Children.Add(convertCopy);
        var convertIcon = new FontIcon { FontFamily = new FontFamily("Segoe Fluent Icons"), Glyph = "\uE8A7", FontSize = 20, Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 42, 90, 59)) };
        Grid.SetColumn(convertIcon, 1);
        convertLayout.Children.Add(convertIcon);
        convert.Content = convertLayout;
        AutomationProperties.SetName(convert, T("整理为路线草稿", "Organize into a draft route"));
        convert.Click += ConvertCaptureButton_Click;
        actions.Children.Add(convert);

        var archiveRow = new Grid { ColumnSpacing = 12, Padding = new Thickness(16, 6, 16, 6) };
        archiveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        archiveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var archiveCopy = new StackPanel { Spacing = 2 };
        archiveCopy.Children.Add(new TextBlock { Text = T("归档想法", "Archive idea"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        archiveCopy.Children.Add(new TextBlock { Text = T("暂时移出收件箱，可在归档中恢复。", "Move out of the inbox for now; restorable from Archive."), Opacity = 0.62, TextWrapping = TextWrapping.Wrap });
        archiveRow.Children.Add(archiveCopy);
        var archive = new Button
        {
            Tag = capture.Id.ToString(),
            Content = new FontIcon { FontFamily = new FontFamily("Segoe Fluent Icons"), Glyph = "\uE74D", FontSize = 18 },
            Background = new SolidColorBrush(Colors.Transparent),
            BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 216, 222, 215)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(11),
            MinWidth = 42,
            MinHeight = 42
        };
        ToolTipService.SetToolTip(archive, T("归档想法", "Archive idea"));
        Grid.SetColumn(archive, 1);
        archiveRow.Children.Add(archive);
        AutomationProperties.SetName(archive, T("归档想法", "Archive idea"));
        archive.Click += ArchiveCaptureButton_Click;
        actions.Children.Add(archiveRow);
        InboxDetailPanel.Children.Add(actions);
    }

    private void SelectCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        if (TryReadTag(sender, out var captureId))
        {
            _responsivePlanning = _responsivePlanning.OpenDetail(
                PlanningDestination.Inbox,
                captureId,
                InboxListScrollViewer.VerticalOffset);
            RenderPlanning();
            RestorePlanningListContext();
        }
    }

    private async void SaveOrganizedCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        if (_organizedCaptureId is not Guid captureId || _organizedCaptureInput is null)
        {
            return;
        }

        await RunAsync(
            () => _session.OrganizeCaptureAsync(captureId, _organizedCaptureInput.Text),
            T("整理内容已保存。", "Organized content saved."));
    }

    private void OpenRouteDetailButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadTag(sender, out var routeId))
        {
            return;
        }

        _responsivePlanning = _responsivePlanning.OpenDetail(
            PlanningDestination.Routes,
            routeId,
            RouteListScrollViewer.VerticalOffset);
        LoadRouteIntoEditor(routeId);
        RenderPlanning();
        RestorePlanningListContext();
    }

    private void RenderDraftSteps()
    {
        DraftStepsPanel.Children.Clear();
        for (var index = 0; index < _draftSteps.Count; index++)
        {
            var step = _draftSteps[index];
            var line = new StackPanel { Spacing = 3 };
            line.Children.Add(new TextBlock { Text = $"{index + 1}. {step.Action}", TextWrapping = TextWrapping.Wrap });
            line.Children.Add(new TextBlock { Text = $"完成标准：{step.CompletionStandard}", Opacity = 0.72, TextWrapping = TextWrapping.Wrap });
            var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var edit = new Button { Content = "编辑步骤", Tag = step.Id.ToString() };
            edit.Click += EditDraftStepButton_Click;
            controls.Children.Add(edit);
            var remove = new Button { Content = "移除步骤", Tag = step.Id.ToString() };
            remove.Click += RemoveDraftStepButton_Click;
            controls.Children.Add(remove);
            line.Children.Add(controls);
            DraftStepsPanel.Children.Add(line);
        }
    }

    private async void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        var origin = _captureContext.Origin;
        if (origin is null)
        {
            return;
        }

        var text = CaptureInput.Text;
        await RunAsync(
            () => _session.CaptureAsync(text),
            "想法已保存。",
            () =>
            {
                CaptureInput.Text = string.Empty;
                RestoreCaptureContext(_captureContext.CompleteSave());
            });

        if (!_captureContext.IsOpen)
        {
            RestoreCaptureScrollPosition(origin);
        }
    }

    private async void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        var note = PauseNoteInput.Text;
        await RunAsync(() => _session.PauseAsync(note), "返回点已保存。", () => PauseNoteInput.Text = string.Empty);
    }

    private async void CompleteCurrentButton_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => _session.CompleteCurrentStepAsync(), "动作已完成。");

    private void StuckButton_Click(object sender, RoutedEventArgs e)
    {
        var presentation = GuidePresentation.From(_session.State);
        if (presentation.CanStartFallback)
        {
            _ = RunAsync(() => _session.StartFallbackAsync(), "正在显示预设备选动作。");
            return;
        }

        _enteringBlock = true;
        Render();
    }

    private async void SaveBlockButton_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(
            () => _session.RecordBlockAndPauseAsync(BlockInput.Text),
            "返回点已保存。",
            () =>
            {
                BlockInput.Text = string.Empty;
                _enteringBlock = false;
            });

    private void CancelBlockButton_Click(object sender, RoutedEventArgs e)
    {
        BlockInput.Text = string.Empty;
        _enteringBlock = false;
        Render();
    }

    private async void CompleteFallbackButton_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => _session.CompleteFallbackAsync(), "已回到原动作。");

    private async void ReturnFromBlockedButton_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => _session.ReturnFromBlockedAsync(), "已回到当前动作。");

    private async void PauseFromBlockedButton_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => _session.PauseAsync(), "返回点已保存。");

    private void PlanningButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_captureContext.CanChangeUnderlyingContext)
        {
            return;
        }

        _settingsOpen = false;
        _planningMode = true;
        _enteringBlock = false;
        ApplyOrdinaryPlanningEntry();
        Render();
    }

    private void ApplyOrdinaryPlanningEntry()
    {
        if (_planningDestinationChosen)
        {
            return;
        }

        _planningDestination = _session.State.Execution.ActiveRouteId is null
            ? PlanningDestination.Routes
            : PlanningDestination.Tasks;
        _responsivePlanning = _responsivePlanning with
        {
            Destination = _planningDestination,
            Detail = PlanningDetail.None
        };
    }

    private void GuideButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_captureContext.CanChangeUnderlyingContext)
        {
            return;
        }

        _settingsOpen = false;
        _planningMode = false;
        Render();
    }

    private void OpenCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        if (_captureContext.IsOpen)
        {
            return;
        }

        var presentation = GuidePresentation.From(_session.State);
        _captureContext.Open(new CaptureContext(
            _planningMode,
            _settingsOpen,
            _planningDestination,
            _enteringBlock,
            _session.State.Execution.ActiveRouteId,
            _session.State.Execution.CurrentStepId,
            _session.State.Execution.Mode,
            presentation.Action,
            MainContentScrollViewer.VerticalOffset,
            RouteEditorScrollViewer.VerticalOffset));
        Render();
        CaptureInput.Focus(FocusState.Programmatic);
    }

    private void CloseCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_captureContext.IsOpen)
        {
            return;
        }

        var origin = _captureContext.Cancel();
        CaptureInput.Text = string.Empty;
        RestoreCaptureContext(origin);
        Render();
        RestoreCaptureScrollPosition(origin);
    }

    private async void PauseAndChooseAnotherRouteButton_Click(object sender, RoutedEventArgs e)
    {
        var note = PauseNoteInput.Text;
        await RunAsync(
            () => _session.PauseAsync(note),
            "返回点已保存。",
            () =>
            {
                PauseNoteInput.Text = string.Empty;
                _planningMode = true;
                _planningDestination = PlanningDestination.Routes;
                _planningDestinationChosen = true;
            });
    }

    private void TasksNavButton_Click(object sender, RoutedEventArgs e) =>
        NavigatePlanning(PlanningDestination.Tasks);

    private void RoutesNavButton_Click(object sender, RoutedEventArgs e) =>
        NavigatePlanning(PlanningDestination.Routes);

    private void InboxNavButton_Click(object sender, RoutedEventArgs e) =>
        NavigatePlanning(PlanningDestination.Inbox);

    private void ReviewNavButton_Click(object sender, RoutedEventArgs e) =>
        NavigatePlanning(PlanningDestination.Review);

    private void ArchiveNavButton_Click(object sender, RoutedEventArgs e) =>
        NavigatePlanning(PlanningDestination.Archive);

    private void NavigatePlanning(PlanningDestination destination)
    {
        if (!_captureContext.CanChangeUnderlyingContext)
        {
            return;
        }

        _planningDestination = destination;
        _planningDestinationChosen = true;
        _responsivePlanning = _responsivePlanning with
        {
            Destination = destination,
            Detail = PlanningDetail.None
        };
        Render();
    }

    private void RouteBackButton_Click(object sender, RoutedEventArgs e)
    {
        _responsivePlanning = _responsivePlanning.ReturnToList();
        RenderPlanning();
        RestorePlanningListContext();
    }

    private void InboxBackButton_Click(object sender, RoutedEventArgs e)
    {
        _responsivePlanning = _responsivePlanning.ReturnToList();
        RenderPlanning();
        RestorePlanningListContext();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_captureContext.CanChangeUnderlyingContext)
        {
            return;
        }

        _settingsOpen = true;
        Render();
    }

    private void CloseSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_captureContext.CanChangeUnderlyingContext)
        {
            return;
        }

        _settingsOpen = false;
        Render();
    }

    private async void LanguagePreferenceButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string value } ||
            !Enum.TryParse<LanguagePreference>(value, out var preference))
        {
            return;
        }

        await RunAsync(() => _session.SetLanguagePreferenceAsync(preference));
    }

    private async void CreateRouteButton_Click(object sender, RoutedEventArgs e)
    {
        var title = RouteTitleInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(title) || _draftSteps.Count == 0)
        {
            ShowStatus("需要路线标题和至少一个已准备的步骤。", InfoBarSeverity.Error);
            return;
        }

        var route = Route.Create(title, _draftSteps.ToArray());
        var projectName = ProjectNameInput.Text;
        var savingUpdate = _editingRouteId;
        var convertingCapture = _convertingCaptureId;
        await RunAsync(
            () => savingUpdate is Guid routeId
                ? _session.UpdateRouteAsync(routeId, title, _draftSteps, projectName)
                : convertingCapture is Guid captureId
                    ? _session.ConvertCaptureToRouteAsync(captureId, route, projectName)
                    : _session.AddRouteAsync(route, projectName),
            savingUpdate is not null ? "路线已更新。" : convertingCapture is not null ? "想法已转换为草稿路线。" : "草稿路线已保存。",
            ClearRouteEditor);
    }

    private void AddStepButton_Click(object sender, RoutedEventArgs e)
    {
        var action = RouteActionInput.Text.Trim();
        var completionStandard = CompletionStandardInput.Text.Trim();
        var doNotDo = DoNotDoInput.Text.Trim();
        var fallback = string.IsNullOrWhiteSpace(FallbackActionInput.Text) ? null : FallbackActionInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(completionStandard) || string.IsNullOrWhiteSpace(doNotDo))
        {
            ShowStatus("每个步骤都需要动作、完成标准和边界。", InfoBarSeverity.Error);
            return;
        }

        var id = _editingStepId ?? Guid.NewGuid();
        var step = new Step(
            id,
            _draftSteps.Count,
            action,
            completionStandard,
            doNotDo,
            fallback,
            _editingStepWasCompleted,
            ReadStepDate(),
            _editingStepPlannedTime);
        if (_editingStepIndex is int index)
        {
            _draftSteps.Insert(index, step);
        }
        else
        {
            _draftSteps.Add(step);
        }
        ClearStepEditor();
        RenderPlanning();
        UpdatePlanningLayout(PlanningPanel.ActualWidth);
    }

    private async void ClearStepButton_Click(object sender, RoutedEventArgs e)
    {
        if (_draftSteps.Count == 0 &&
            string.IsNullOrWhiteSpace(RouteActionInput.Text) &&
            string.IsNullOrWhiteSpace(CompletionStandardInput.Text) &&
            string.IsNullOrWhiteSpace(DoNotDoInput.Text) &&
            string.IsNullOrWhiteSpace(FallbackActionInput.Text))
        {
            return;
        }

        await ShowClearStepConfirmation();
    }

    private async Task ShowClearStepConfirmation()
    {
        var dialog = new ContentDialog
        {
            Title = "清空当前步骤？",
            Content = "这会清除当前正在编辑的步骤内容和未保存的草稿步骤。已保存的路线不会改变。",
            PrimaryButtonText = "清空步骤",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = RootGrid.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            ClearStepEditor();
            _draftSteps.Clear();
            RenderPlanning();
        }
    }

    private void EditDraftStepButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadTag(sender, out var stepId))
        {
            return;
        }

        var index = _draftSteps.FindIndex(step => step.Id == stepId);
        var step = _draftSteps[index];
        _draftSteps.RemoveAt(index);
        _editingStepId = step.Id;
        _editingStepIndex = index;
        _editingStepWasCompleted = step.IsCompleted;
        RouteActionInput.Text = step.Action;
        CompletionStandardInput.Text = step.CompletionStandard;
        DoNotDoInput.Text = step.DoNotDo;
        FallbackActionInput.Text = step.FallbackAction ?? string.Empty;
        SetStepDate(step.PlannedDate);
        _editingStepPlannedTime = step.PlannedTime;
        RenderPlanning();
    }

    private void RemoveDraftStepButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editingRouteId is not null)
        {
            ShowStatus("此 MVP 会保留已保存路线的步骤；删除需要在规划中明确处理。", InfoBarSeverity.Warning);
            return;
        }

        if (!TryReadTag(sender, out var stepId))
        {
            return;
        }

        _draftSteps.RemoveAll(step => step.Id == stepId);
        RenderPlanning();
    }

    private void EditRouteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadTag(sender, out var routeId))
        {
            return;
        }

        if (_responsivePlanning.IsCompact)
        {
            _responsivePlanning = _responsivePlanning.OpenDetail(
                PlanningDestination.Routes,
                routeId,
                RouteListScrollViewer.VerticalOffset);
        }

        LoadRouteIntoEditor(routeId);
        RenderPlanning();
        RestorePlanningListContext();
    }

    private void LoadRouteIntoEditor(Guid routeId)
    {
        var route = _session.State.Route(routeId);
        _editingRouteId = routeId;
        _convertingCaptureId = null;
        RouteTitleInput.Text = route.Title;
        ProjectNameInput.Text = RouteListPresentation.ProjectName(_session.State, route) ?? string.Empty;
        _draftSteps.Clear();
        _draftSteps.AddRange(route.Steps);
        ClearStepEditor();
        SaveRouteButton.Content = "保存路线修改";
    }

    private async void ArchiveRouteButton_Click(object sender, RoutedEventArgs e)
    {
        if (TryReadTag(sender, out var routeId))
        {
            await RunAsync(() => _session.ArchiveRouteAsync(routeId), "路线已归档。");
        }
    }

    private async void RestoreArchivedRouteButton_Click(object sender, RoutedEventArgs e)
    {
        if (TryReadTag(sender, out var routeId))
        {
            await RunAsync(() => _session.RestoreArchivedRouteAsync(routeId), "路线已恢复。");
        }
    }

    private void ConvertCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadTag(sender, out var captureId))
        {
            return;
        }

        var capture = _session.State.Captures.Single(item => item.Id == captureId);
        _editingRouteId = null;
        _convertingCaptureId = captureId;
        RouteTitleInput.Text = capture.RawText.Length > 60 ? capture.RawText[..60] : capture.RawText;
        ProjectNameInput.Text = string.Empty;
        _draftSteps.Clear();
        RouteActionInput.Text = capture.RawText;
        CompletionStandardInput.Text = string.Empty;
        DoNotDoInput.Text = string.Empty;
        FallbackActionInput.Text = string.Empty;
        SaveRouteButton.Content = "转换想法为草稿路线";
        ShowStatus("补全已准备的步骤并添加后，即可保存转换后的路线。", InfoBarSeverity.Informational);
        RenderPlanning();
    }

    private async void ArchiveCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        if (TryReadTag(sender, out var captureId))
        {
            await RunAsync(() => _session.ArchiveCaptureAsync(captureId), "想法已归档。");
        }
    }

    private async void RestoreArchivedCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        if (TryReadTag(sender, out var captureId))
        {
            await RunAsync(() => _session.RestoreArchivedCaptureAsync(captureId), "想法已恢复。");
        }
    }

    private void ClearRouteEditor()
    {
        _editingRouteId = null;
        _convertingCaptureId = null;
        _draftSteps.Clear();
        RouteTitleInput.Text = string.Empty;
        ProjectNameInput.Text = string.Empty;
        ClearStepEditor();
        SaveRouteButton.Content = "保存草稿路线";
    }

    private void ClearStepEditor()
    {
        _editingStepId = null;
        _editingStepIndex = null;
        _editingStepWasCompleted = false;
        _editingStepPlannedTime = null;
        RouteActionInput.Text = string.Empty;
        CompletionStandardInput.Text = string.Empty;
        DoNotDoInput.Text = string.Empty;
        FallbackActionInput.Text = string.Empty;
        SetStepDate(null);
    }

    private static bool TryReadTag(object sender, out Guid id)
    {
        id = Guid.Empty;
        return sender is Button { Tag: string text } && Guid.TryParse(text, out id);
    }

    private async void ActivateRouteButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadTag(sender, out var routeId))
        {
            ShowStatus("所选路线不可用。", InfoBarSeverity.Error);
            return;
        }

        await RunAsync(() => _session.ActivateRouteAsync(routeId), "当前路线已保存。");
    }

    private async Task RunAsync(Func<Task> command, string? confirmation = null, Action? afterSuccess = null)
    {
        RootGrid.IsHitTestVisible = false;
        StatusBar.IsOpen = false;
        try
        {
            await command();
            afterSuccess?.Invoke();
            Render(confirmation);
        }
        catch (Exception exception)
        {
            ShowStatus($"未做任何更改。{exception.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            RootGrid.IsHitTestVisible = true;
        }
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusBar.Message = message;
        StatusBar.Severity = severity;
        StatusBar.IsOpen = true;
        _statusTimer ??= DispatcherQueue.CreateTimer();
        _statusTimer.Tick -= StatusTimer_Tick;
        _statusTimer.Tick += StatusTimer_Tick;
        _statusTimer.Interval = WindowPresentation.StatusLifetime(severity == InfoBarSeverity.Error);
        _statusTimer.Start();
    }

    private void RestoreCaptureContext(CaptureContext origin)
    {
        _planningMode = origin.PlanningMode;
        _settingsOpen = origin.SettingsOpen;
        _planningDestination = origin.PlanningDestination;
        _enteringBlock = origin.EnteringBlock;
    }

    private void RestoreCaptureScrollPosition(CaptureContext origin)
    {
        MainContentScrollViewer.ChangeView(null, origin.MainScrollOffset, null, disableAnimation: true);
        RouteEditorScrollViewer.ChangeView(null, origin.RouteEditorScrollOffset, null, disableAnimation: true);
    }

    private void StatusTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        StatusBar.IsOpen = false;
    }

    private static Visibility ToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    private string T(string simplifiedChinese, string english) =>
        UiText.Choose(_session.State.LanguagePreference, simplifiedChinese, english);

    private void ApplyLanguage()
    {
        GuideModeButton.Content = T("引导", "Guide");
        PlanningModeButton.Content = T("规划", "Planning");
        ToolTipService.SetToolTip(SettingsButton, T("设置", "Settings"));
        PlanningTitleText.Text = T("规划", "Planning");
        PlanningSubtitleText.Text = T("管理", "Manage");
        TasksNavButton.Content = T("任务", "Tasks");
        RoutesNavButton.Content = T("路线", "Routes");
        RoutesListViewButton.Content = T("路线", "Routes");
        TaskFlowsListViewButton.Content = T("任务流", "Task flows");
        RoutesListSwitcherButton.Content = T("路线", "Routes");
        TaskFlowsListSwitcherButton.Content = T("任务流", "Task flows");
        TaskFlowsTitleText.Text = T("任务流", "Task flows");
        TaskFlowsDescriptionText.Text = T(
            "可复用的一组步骤；开始整组后生成一条路线。",
            "A reusable set of steps; starting the group creates one route.");
        InboxNavButton.Content = T("收件箱", "Inbox");
        ReviewNavButton.Content = T("回顾", "Review");
        ArchiveNavButton.Content = T("归档", "Archive");
        CompactTasksNavButton.Content = T("任务", "Tasks");
        CompactRoutesNavButton.Content = T("路线", "Routes");
        CompactInboxNavButton.Content = T("收件箱", "Inbox");
        CompactReviewNavButton.Content = T("回顾", "Review");
        CompactArchiveNavButton.Content = T("归档", "Archive");
        TasksTitleText.Text = T("任务", "Tasks");
        TaskSearchInput.PlaceholderText = T("搜索任务或路线", "Search tasks or routes");
        TasksCalendarButton.Content = T("日历", "Calendar");
        TasksListButton.Content = T("任务列表", "Task list");
        StepDatePicker.PlaceholderText = T("计划日期（可选，属于当前步骤）", "Planned date (optional, for this step)");
        ReviewTitleText.Text = T("回顾", "Review");
        ReviewDescriptionText.Text = T("回顾：只读的历史时间线，按时间倒序。", "Review: a read-only timeline of recorded facts, newest first.");
        ArchiveTitleText.Text = T("归档", "Archive");
        ArchiveDescriptionText.Text = T("归档内容会保留，并可在规划中恢复。", "Archived items are retained and can be restored while planning.");
        ArchivePlaceholderText.Text = T("当前版本会在路线和收件箱条目中显示归档状态。", "This build marks archive state on routes and inbox entries.");
        RoutesTitleText.Text = T("路线", "Routes");
        RouteSearchInput.PlaceholderText = T("搜索标题、动作或项目", "Search title, action, or project");
        RoutesByStatusButton.Content = T("按状态", "By status");
        RoutesByProjectButton.Content = T("按项目", "By project");
        ToolTipService.SetToolTip(RoutesByProjectButton, T("按项目分组", "Group routes by project"));
        ProjectNameInput.PlaceholderText = T("项目（可选）", "Project (optional)");
        InboxTitleText.Text = T("收件箱", "Inbox");
        InboxSearchInput.PlaceholderText = T("搜索原文或整理内容", "Search raw or organized text");
        InboxAllButton.Content = T("全部", "All");
        InboxUnorganizedButton.Content = T("未整理", "Unorganized");
        InboxOrganizedButton.Content = T("已整理", "Organized");
        CaptureFooterButton.Content = T("捕捉想法", "Capture idea");
        SettingsTitleText.Text = T("设置", "Settings");
        LanguageTitleText.Text = T("语言", "Language");
        LanguageSystemButton.Content = T("跟随系统", "Follow system");
        LanguageChineseButton.Content = T("简体中文", "Simplified Chinese");
        LanguageEnglishButton.Content = "English";
        LanguageCurrentText.Text = _session.State.LanguagePreference switch
        {
            LanguagePreference.FollowSystem => T("当前：跟随系统", "Current: Follow system"),
            LanguagePreference.SimplifiedChinese => T("当前：简体中文", "Current: Simplified Chinese"),
            LanguagePreference.English => T("当前：English", "Current: English"),
            _ => string.Empty
        };
        LanguageDescriptionText.Text = T(
            "界面语言可以立即切换。路线、步骤、备注和捕捉原文会保持原样，不会自动翻译。",
            "The interface language changes immediately. Routes, steps, notes, and raw captures stay exactly as written.");
        CloseSettingsButton.Content = T("返回", "Back");
        SetSelected(LanguageSystemButton, _session.State.LanguagePreference == LanguagePreference.FollowSystem);
        SetSelected(LanguageChineseButton, _session.State.LanguagePreference == LanguagePreference.SimplifiedChinese);
        SetSelected(LanguageEnglishButton, _session.State.LanguagePreference == LanguagePreference.English);
    }

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveShell(e.NewSize.Width);
        if (_planningMode)
        {
            UpdatePlanningLayout(PlanningPanel.ActualWidth);
        }
    }

    private void UpdateResponsiveShell(double availableWidth)
    {
        var compact = ResponsivePlanningPresentation.IsCompactWidth(availableWidth);
        CaptureDrawer.MaxWidth = compact ? double.PositiveInfinity : 410;
        CaptureDrawer.HorizontalAlignment = compact ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        CaptureDrawer.Padding = compact ? new Thickness(20) : new Thickness(24);
        NormalControls.Orientation = compact ? Orientation.Vertical : Orientation.Horizontal;
        BottomActionBar.Padding = compact ? new Thickness(16, 7, 16, 7) : new Thickness(24, 7, 24, 7);
    }

    private void PlanningPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePlanningLayout(e.NewSize.Width);
    }

    private void UpdatePlanningLayout(double availableWidth)
    {
        if (availableWidth <= 0 || ContentGrid.ActualHeight <= 0)
        {
            return;
        }

        // MainContentScrollViewer measures its child with unbounded height. Constrain the
        // planning grid to its viewport so compact detail surfaces receive a real star row.
        PlanningPanel.Height = ContentGrid.ActualHeight;
        _responsivePlanning = _responsivePlanning.WithWidth(availableWidth);
        var compact = _responsivePlanning.IsCompact;
        var routeDetail = _responsivePlanning.Detail == PlanningDetail.Route;
        var inboxDetail = _responsivePlanning.Detail == PlanningDetail.Inbox;

        PlanningNavigationColumn.Width = new GridLength(compact ? 0 : 196);
        PlanningNavigationSurface.Visibility = ToVisibility(!compact);
        CompactPlanningNavigation.Visibility = ToVisibility(compact);

        RouteListColumn.Width = compact
            ? routeDetail ? new GridLength(0) : new GridLength(1, GridUnitType.Star)
            : new GridLength(340);
        RouteEditorColumn.Width = compact
            ? routeDetail ? new GridLength(1, GridUnitType.Star) : new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
        RouteListRow.Height = new GridLength(1, GridUnitType.Star);
        RouteEditorRow.Height = new GridLength(0);
        Grid.SetColumn(RouteListSurface, 0);
        Grid.SetRow(RouteListSurface, 1);
        Grid.SetColumn(RouteEditorScrollViewer, 1);
        Grid.SetRow(RouteEditorScrollViewer, 1);
        RouteListSurface.Visibility = ToVisibility(!compact || !routeDetail);
        RouteEditorScrollViewer.Visibility = ToVisibility(!compact || routeDetail);
        RouteBackButton.Visibility = ToVisibility(compact && routeDetail);

        InboxListColumn.Width = compact
            ? inboxDetail ? new GridLength(0) : new GridLength(1, GridUnitType.Star)
            : new GridLength(300);
        InboxDetailColumn.Width = compact
            ? inboxDetail ? new GridLength(1, GridUnitType.Star) : new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
        InboxListRow.Height = new GridLength(1, GridUnitType.Star);
        InboxDetailRow.Height = new GridLength(0);
        Grid.SetColumn(InboxListSurface, 0);
        Grid.SetRow(InboxListSurface, 1);
        Grid.SetColumn(InboxDetailWorkspace, 1);
        Grid.SetRow(InboxDetailWorkspace, 1);
        InboxListSurface.Visibility = ToVisibility(!compact || !inboxDetail);
        InboxDetailWorkspace.Visibility = ToVisibility(!compact || inboxDetail);
        InboxBackButton.Visibility = ToVisibility(compact && inboxDetail);
        RoutesWorkspace.Padding = compact ? new Thickness(16, 12, 16, 16) : new Thickness(24, 20, 24, 20);
        InboxWorkspace.Padding = compact ? new Thickness(16, 12, 16, 16) : new Thickness(24, 20, 24, 20);
        TasksWorkspace.Padding = compact ? new Thickness(16, 12, 16, 16) : new Thickness(32, 24, 32, 24);
        TaskFlowsWorkspace.Padding = compact ? new Thickness(16, 12, 16, 16) : new Thickness(24, 20, 24, 20);
        ReviewWorkspace.Padding = compact ? new Thickness(16, 12, 16, 16) : new Thickness(32, 24, 32, 24);
        ArchiveWorkspace.Padding = compact ? new Thickness(16, 12, 16, 16) : new Thickness(32, 24, 32, 24);
    }

    private void RestorePlanningListContext()
    {
        if (!_responsivePlanning.IsCompact || _responsivePlanning.Detail != PlanningDetail.None)
        {
            return;
        }

        var context = _responsivePlanning.Destination == PlanningDestination.Routes
            ? _responsivePlanning.Routes
            : _responsivePlanning.Inbox;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_responsivePlanning.Destination == PlanningDestination.Routes)
            {
                RouteListScrollViewer.ChangeView(null, context.ScrollOffset, null, disableAnimation: true);
            }
            else if (_responsivePlanning.Destination == PlanningDestination.Inbox)
            {
                InboxListScrollViewer.ChangeView(null, context.ScrollOffset, null, disableAnimation: true);
            }
        });
    }

    private void UpdateModeSelectionIndicator()
    {
        ModeSelectionIndicator.Visibility = ToVisibility(!_settingsOpen);
        if (_settingsOpen)
        {
            return;
        }

        var planningSelected = _planningMode;
        var targetOffset = planningSelected ? 100d : 0d;
        if (_modeIndicatorPlanningSelection is null)
        {
            ModeSelectionTransform.X = targetOffset;
        }
        else if (_modeIndicatorPlanningSelection != planningSelected)
        {
            var animation = new DoubleAnimation
            {
                To = targetOffset,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(animation, ModeSelectionTransform);
            Storyboard.SetTargetProperty(animation, "X");
            var storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }

        _modeIndicatorPlanningSelection = planningSelected;
    }

    private static string LifecycleText(RouteLifecycle lifecycle) => lifecycle switch
    {
        RouteLifecycle.Draft => "草稿",
        RouteLifecycle.Active => "进行中",
        RouteLifecycle.Paused => "已暂停",
        RouteLifecycle.Completed => "已完成",
        RouteLifecycle.Archived => "已归档",
        _ => lifecycle.ToString()
    };

    private void SyncNavigationSelection()
    {
        SetSelected(TasksNavButton, _planningDestination == PlanningDestination.Tasks);
        SetSelected(RoutesNavButton, _planningDestination == PlanningDestination.Routes);
        SetSelected(InboxNavButton, _planningDestination == PlanningDestination.Inbox);
        SetSelected(ReviewNavButton, _planningDestination == PlanningDestination.Review);
        SetSelected(ArchiveNavButton, _planningDestination == PlanningDestination.Archive);
        SetSelected(CompactTasksNavButton, _planningDestination == PlanningDestination.Tasks);
        SetSelected(CompactRoutesNavButton, _planningDestination == PlanningDestination.Routes);
        SetSelected(CompactInboxNavButton, _planningDestination == PlanningDestination.Inbox);
        SetSelected(CompactReviewNavButton, _planningDestination == PlanningDestination.Review);
        SetSelected(CompactArchiveNavButton, _planningDestination == PlanningDestination.Archive);
    }

    private void SetSelected(Button button, bool selected)
    {
        var isModeButton = button == GuideModeButton || button == PlanningModeButton;
        button.Background = isModeButton ? TransparentBrush : selected ? SelectedButtonBrush : TransparentBrush;
        button.Foreground = selected ? WhiteBrush : TextBrush;
        button.BorderBrush = isModeButton ? TransparentBrush : selected ? SelectedButtonBrush : TransparentBrush;
    }
}
