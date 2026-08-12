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
        _session = new ExecutionSession(new SqliteStateStore(databasePath));
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
        RoutesWorkspace.Visibility = ToVisibility(_planningDestination == PlanningDestination.Routes);
        InboxWorkspace.Visibility = ToVisibility(_planningDestination == PlanningDestination.Inbox);
        ArchiveWorkspace.Visibility = ToVisibility(_planningDestination == PlanningDestination.Archive);
        SetSelected(RoutesNavButton, _planningDestination == PlanningDestination.Routes);
        SetSelected(InboxNavButton, _planningDestination == PlanningDestination.Inbox);
        SetSelected(ArchiveNavButton, _planningDestination == PlanningDestination.Archive);
        RenderDraftSteps();
        RouteListPanel.Children.Clear();
        foreach (var route in _session.State.Routes
            .Where(route => route.Lifecycle != RouteLifecycle.Archived)
            .OrderBy(route => route.Title))
        {
            var line = new StackPanel { Spacing = 4 };
            line.Children.Add(new TextBlock { Text = route.Title, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] });
            line.Children.Add(new TextBlock { Text = $"{LifecycleText(route.Lifecycle)}：{route.CurrentStep()?.Action ?? "没有未完成的动作"}", TextWrapping = TextWrapping.Wrap, Opacity = 0.72 });
            var select = new Button { Content = "查看路线", Tag = route.Id.ToString(), HorizontalAlignment = HorizontalAlignment.Left };
            select.Click += OpenRouteDetailButton_Click;
            line.Children.Add(select);
            if (route.Lifecycle is RouteLifecycle.Draft or RouteLifecycle.Paused && route.CurrentStep() is not null)
            {
                var activate = new Button { Content = "设为当前路线", Tag = route.Id.ToString(), HorizontalAlignment = HorizontalAlignment.Left };
                activate.Click += ActivateRouteButton_Click;
                line.Children.Add(activate);
            }

            var edit = new Button { Content = "编辑路线", Tag = route.Id.ToString(), HorizontalAlignment = HorizontalAlignment.Left };
            edit.Click += EditRouteButton_Click;
            line.Children.Add(edit);

            if (route.Id != _session.State.Execution.ActiveRouteId)
            {
                var archive = new Button { Content = "归档路线", Tag = route.Id.ToString(), HorizontalAlignment = HorizontalAlignment.Left };
                archive.Click += ArchiveRouteButton_Click;
                line.Children.Add(archive);
            }

            RouteListPanel.Children.Add(line);
        }

        if (_session.State.Routes.All(route => route.Lifecycle == RouteLifecycle.Archived))
        {
            RouteListPanel.Children.Add(new TextBlock { Text = "尚无路线。", Opacity = 0.68 });
        }

        var visibleCaptures = _session.State.Captures
            .Where(capture => !capture.IsArchived)
            .OrderByDescending(capture => capture.CapturedAt)
            .ToArray();
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
            var preview = capture.RawText.Length > 72 ? $"{capture.RawText[..72]}..." : capture.RawText;
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

        if (visibleCaptures.Length == 0)
        {
            InboxPanel.Children.Add(new TextBlock { Text = "尚未捕捉到想法。", Opacity = 0.68 });
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
            line.Children.Add(new TextBlock { Text = $"下一动作：{route.CurrentStep()?.Action ?? "没有未完成的动作"}", Opacity = 0.72, TextWrapping = TextWrapping.Wrap });
            line.Children.Add(new TextBlock { Text = $"恢复后状态：{LifecycleText(route.LifecycleBeforeArchive ?? RouteLifecycle.Draft)}", Opacity = 0.62 });
            var restore = new Button { Content = "恢复路线", Tag = route.Id.ToString(), HorizontalAlignment = HorizontalAlignment.Left };
            restore.Click += RestoreArchivedRouteButton_Click;
            line.Children.Add(restore);
            ArchivedRoutesPanel.Children.Add(line);
        }

        if (archivedRoutes.Length == 0)
        {
            ArchivedRoutesPanel.Children.Add(new TextBlock { Text = "没有已归档路线。", Opacity = 0.68 });
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
            line.Children.Add(new TextBlock { Text = $"捕捉时间：{capture.CapturedAt.LocalDateTime:g}", Opacity = 0.62 });
            var restore = new Button { Content = "恢复想法", Tag = capture.Id.ToString(), HorizontalAlignment = HorizontalAlignment.Left };
            restore.Click += RestoreArchivedCaptureButton_Click;
            line.Children.Add(restore);
            ArchivedCapturesPanel.Children.Add(line);
        }

        if (archivedCaptures.Length == 0)
        {
            ArchivedCapturesPanel.Children.Add(new TextBlock { Text = "没有已归档收件箱条目。", Opacity = 0.68 });
        }
    }

    private void RenderInboxDetail(CaptureEntry? capture)
    {
        InboxDetailPanel.Children.Clear();
        if (capture is null)
        {
            InboxDetailPanel.Children.Add(new TextBlock { Text = "选择一条已捕捉的想法进行查看。", FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            InboxDetailPanel.Children.Add(new TextBlock { Text = "底部栏随时可以捕捉想法。", Foreground = new SolidColorBrush(Colors.DimGray) });
            return;
        }

        InboxDetailPanel.Children.Add(new TextBlock { Text = "已捕捉的想法", FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        InboxDetailPanel.Children.Add(new TextBlock { Text = $"捕捉时间：{capture.CapturedAt.LocalDateTime:g}", Foreground = new SolidColorBrush(Colors.DimGray) });
        InboxDetailPanel.Children.Add(new TextBlock { Text = "原始记录", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 0) });
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
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 10, 0, 0) };
        var convert = new Button { Content = "转换为草稿路线", Tag = capture.Id.ToString() };
        convert.Click += ConvertCaptureButton_Click;
        actions.Children.Add(convert);
        var archive = new Button { Content = "归档想法", Tag = capture.Id.ToString() };
        archive.Click += ArchiveCaptureButton_Click;
        actions.Children.Add(archive);
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
        Render();
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
            });
    }

    private void RoutesNavButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_captureContext.CanChangeUnderlyingContext)
        {
            return;
        }

        _planningDestination = PlanningDestination.Routes;
        _responsivePlanning = _responsivePlanning with
        {
            Destination = PlanningDestination.Routes,
            Detail = PlanningDetail.None
        };
        Render();
    }

    private void InboxNavButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_captureContext.CanChangeUnderlyingContext)
        {
            return;
        }

        _planningDestination = PlanningDestination.Inbox;
        _responsivePlanning = _responsivePlanning with
        {
            Destination = PlanningDestination.Inbox,
            Detail = PlanningDetail.None
        };
        Render();
    }

    private void ArchiveNavButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_captureContext.CanChangeUnderlyingContext)
        {
            return;
        }

        _planningDestination = PlanningDestination.Archive;
        _responsivePlanning = _responsivePlanning with
        {
            Destination = PlanningDestination.Archive,
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

    private async void CreateRouteButton_Click(object sender, RoutedEventArgs e)
    {
        var title = RouteTitleInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(title) || _draftSteps.Count == 0)
        {
            ShowStatus("需要路线标题和至少一个已准备的步骤。", InfoBarSeverity.Error);
            return;
        }

        var route = Route.Create(title, _draftSteps.ToArray());
        var savingUpdate = _editingRouteId;
        var convertingCapture = _convertingCaptureId;
        await RunAsync(
            () => savingUpdate is Guid routeId
                ? _session.UpdateRouteAsync(routeId, title, _draftSteps)
                : convertingCapture is Guid captureId
                    ? _session.ConvertCaptureToRouteAsync(captureId, route)
                    : _session.AddRouteAsync(route),
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
        var step = new Step(id, _draftSteps.Count, action, completionStandard, doNotDo, fallback, _editingStepWasCompleted);
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

    private void ClearStepButton_Click(object sender, RoutedEventArgs e) => ClearStepEditor();

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
        ClearStepEditor();
        SaveRouteButton.Content = "保存草稿路线";
    }

    private void ClearStepEditor()
    {
        _editingStepId = null;
        _editingStepIndex = null;
        _editingStepWasCompleted = false;
        RouteActionInput.Text = string.Empty;
        CompletionStandardInput.Text = string.Empty;
        DoNotDoInput.Text = string.Empty;
        FallbackActionInput.Text = string.Empty;
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
        CompactRoutesNavigation.Visibility = ToVisibility(compact);
        CompactInboxNavigation.Visibility = ToVisibility(compact);
        CompactArchiveNavigation.Visibility = ToVisibility(compact);

        RouteListColumn.Width = compact
            ? routeDetail ? new GridLength(0) : new GridLength(1, GridUnitType.Star)
            : new GridLength(280);
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

    private void SetSelected(Button button, bool selected)
    {
        var isModeButton = button == GuideModeButton || button == PlanningModeButton;
        button.Background = isModeButton ? TransparentBrush : selected ? SelectedButtonBrush : TransparentBrush;
        button.Foreground = selected ? WhiteBrush : TextBrush;
        button.BorderBrush = isModeButton ? TransparentBrush : selected ? SelectedButtonBrush : TransparentBrush;
    }
}
