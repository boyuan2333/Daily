using ExecutionContinuity.Domain;
using ExecutionContinuity.Persistence;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ExecutionContinuity.App;

public sealed partial class MainWindow : Window
{
    private readonly ExecutionSession _session;
    private bool _planningMode;
    private bool _enteringBlock;
    private Guid? _editingRouteId;
    private Guid? _editingStepId;
    private int? _editingStepIndex;
    private bool _editingStepWasCompleted;
    private Guid? _convertingCaptureId;
    private readonly List<Step> _draftSteps = new();

    public MainWindow()
    {
        InitializeComponent();
        _session = new ExecutionSession(new SqliteStateStore(DatabasePath()));
        Activated += MainWindow_Activated;
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= MainWindow_Activated;
        await LoadAndRenderAsync();
    }

    private static string DatabasePath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ExecutionContinuity");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "execution-continuity.db");
    }

    private async Task LoadAndRenderAsync()
    {
        await RunAsync(async () => await _session.LoadAsync(), "State restored.");
    }

    private void Render(string? confirmation = null)
    {
        var presentation = GuidePresentation.From(_session.State);
        GuidePanel.Visibility = ToVisibility(!_planningMode && presentation.Screen != GuideScreen.NoActiveRoute);
        NoActivePanel.Visibility = ToVisibility(!_planningMode && presentation.Screen == GuideScreen.NoActiveRoute);
        PlanningPanel.Visibility = ToVisibility(_planningMode);
        PlanningButton.Visibility = ToVisibility(!_planningMode && presentation.Screen == GuideScreen.NoActiveRoute);

        if (confirmation is not null)
        {
            ShowStatus(confirmation, InfoBarSeverity.Success);
        }

        if (_planningMode)
        {
            RenderPlanning();
            return;
        }

        if (presentation.Screen == GuideScreen.NoActiveRoute)
        {
            return;
        }

        GuideHeading.Text = presentation.Screen switch
        {
            GuideScreen.Fallback => "Prepared fallback",
            GuideScreen.Blocked => "Paused at this point",
            _ => "Current action"
        };
        GuideActionText.Text = presentation.Action ?? string.Empty;
        GuidancePanel.Visibility = ToVisibility(presentation.CompletionStandard is not null);
        CompletionText.Text = presentation.CompletionStandard is null ? string.Empty : $"Complete when: {presentation.CompletionStandard}";
        BoundaryText.Text = presentation.DoNotDo is null ? string.Empty : $"Do not do: {presentation.DoNotDo}";
        NormalControls.Visibility = ToVisibility(presentation.Screen == GuideScreen.CurrentAction && !_enteringBlock);
        BlockEntryPanel.Visibility = ToVisibility(_enteringBlock);
        FallbackControls.Visibility = ToVisibility(presentation.CanCompleteFallback);
        BlockedControls.Visibility = ToVisibility(presentation.Screen == GuideScreen.Blocked);
        PausePanel.Visibility = ToVisibility(presentation.CanPause && !_enteringBlock && presentation.Screen != GuideScreen.Blocked);
        CapturePanel.Visibility = ToVisibility(presentation.CanCapture && !_enteringBlock);
    }

    private void RenderPlanning()
    {
        RenderDraftSteps();
        RouteListPanel.Children.Clear();
        foreach (var route in _session.State.Routes.OrderBy(route => route.Title))
        {
            var line = new StackPanel { Spacing = 4 };
            line.Children.Add(new TextBlock { Text = route.Title, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] });
            line.Children.Add(new TextBlock { Text = $"{route.Lifecycle}: {route.CurrentStep()?.Action ?? "No unfinished action"}", TextWrapping = TextWrapping.Wrap, Opacity = 0.72 });
            if (route.Lifecycle is RouteLifecycle.Draft or RouteLifecycle.Paused && route.CurrentStep() is not null)
            {
                var activate = new Button { Content = "Make active", Tag = route.Id.ToString(), HorizontalAlignment = HorizontalAlignment.Left };
                activate.Click += ActivateRouteButton_Click;
                line.Children.Add(activate);
            }

            if (route.Lifecycle != RouteLifecycle.Archived)
            {
                var edit = new Button { Content = "Edit route", Tag = route.Id.ToString(), HorizontalAlignment = HorizontalAlignment.Left };
                edit.Click += EditRouteButton_Click;
                line.Children.Add(edit);
            }

            if (route.Id != _session.State.Execution.ActiveRouteId && route.Lifecycle != RouteLifecycle.Archived)
            {
                var archive = new Button { Content = "Archive route", Tag = route.Id.ToString(), HorizontalAlignment = HorizontalAlignment.Left };
                archive.Click += ArchiveRouteButton_Click;
                line.Children.Add(archive);
            }

            RouteListPanel.Children.Add(line);
        }

        if (_session.State.Routes.Count == 0)
        {
            RouteListPanel.Children.Add(new TextBlock { Text = "No routes yet.", Opacity = 0.68 });
        }

        InboxPanel.Children.Clear();
        foreach (var capture in _session.State.Captures.OrderByDescending(capture => capture.CapturedAt))
        {
            var line = new StackPanel { Spacing = 4 };
            line.Children.Add(new TextBlock
            {
                Text = $"{capture.CapturedAt.LocalDateTime:g}  {capture.RawText}{(capture.IsArchived ? " (archived)" : string.Empty)}",
                TextWrapping = TextWrapping.Wrap
            });
            if (!capture.IsArchived)
            {
                var convert = new Button { Content = "Convert to draft route", Tag = capture.Id.ToString(), HorizontalAlignment = HorizontalAlignment.Left };
                convert.Click += ConvertCaptureButton_Click;
                line.Children.Add(convert);
                var archive = new Button { Content = "Archive capture", Tag = capture.Id.ToString(), HorizontalAlignment = HorizontalAlignment.Left };
                archive.Click += ArchiveCaptureButton_Click;
                line.Children.Add(archive);
            }

            InboxPanel.Children.Add(line);
        }

        if (_session.State.Captures.Count == 0)
        {
            InboxPanel.Children.Add(new TextBlock { Text = "No captured ideas yet.", Opacity = 0.68 });
        }
    }

    private void RenderDraftSteps()
    {
        DraftStepsPanel.Children.Clear();
        for (var index = 0; index < _draftSteps.Count; index++)
        {
            var step = _draftSteps[index];
            var line = new StackPanel { Spacing = 3 };
            line.Children.Add(new TextBlock { Text = $"{index + 1}. {step.Action}", TextWrapping = TextWrapping.Wrap });
            line.Children.Add(new TextBlock { Text = $"Complete when: {step.CompletionStandard}", Opacity = 0.72, TextWrapping = TextWrapping.Wrap });
            var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var edit = new Button { Content = "Edit step", Tag = step.Id.ToString() };
            edit.Click += EditDraftStepButton_Click;
            controls.Children.Add(edit);
            var remove = new Button { Content = "Remove step", Tag = step.Id.ToString() };
            remove.Click += RemoveDraftStepButton_Click;
            controls.Children.Add(remove);
            line.Children.Add(controls);
            DraftStepsPanel.Children.Add(line);
        }
    }

    private async void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        var text = CaptureInput.Text;
        await RunAsync(() => _session.CaptureAsync(text), "Capture saved.", () => CaptureInput.Text = string.Empty);
    }

    private async void NoActiveCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        var text = NoActiveCaptureInput.Text;
        await RunAsync(() => _session.CaptureAsync(text), "Capture saved.", () => NoActiveCaptureInput.Text = string.Empty);
    }

    private async void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        var note = PauseNoteInput.Text;
        await RunAsync(() => _session.PauseAsync(note), "Return point saved.", () => PauseNoteInput.Text = string.Empty);
    }

    private async void CompleteCurrentButton_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => _session.CompleteCurrentStepAsync(), "Action completed.");

    private void StuckButton_Click(object sender, RoutedEventArgs e)
    {
        var presentation = GuidePresentation.From(_session.State);
        if (presentation.CanStartFallback)
        {
            _ = RunAsync(() => _session.StartFallbackAsync(), "Showing the prepared fallback.");
            return;
        }

        _enteringBlock = true;
        Render();
    }

    private async void SaveBlockButton_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(
            () => _session.RecordBlockAndPauseAsync(BlockInput.Text),
            "Return point saved.",
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
        await RunAsync(() => _session.CompleteFallbackAsync(), "Returned to the original action.");

    private async void ReturnFromBlockedButton_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => _session.ReturnFromBlockedAsync(), "Returned to the current action.");

    private async void PauseFromBlockedButton_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => _session.PauseAsync(), "Return point saved.");

    private void PlanningButton_Click(object sender, RoutedEventArgs e)
    {
        _planningMode = true;
        _enteringBlock = false;
        Render();
    }

    private void GuideButton_Click(object sender, RoutedEventArgs e)
    {
        _planningMode = false;
        Render();
    }

    private async void CreateRouteButton_Click(object sender, RoutedEventArgs e)
    {
        var title = RouteTitleInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(title) || _draftSteps.Count == 0)
        {
            ShowStatus("A route title and at least one prepared step are required.", InfoBarSeverity.Error);
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
            savingUpdate is not null ? "Route updated." : convertingCapture is not null ? "Capture converted to a draft route." : "Draft route saved.",
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
            ShowStatus("Action, completion standard, and boundary are required for each step.", InfoBarSeverity.Error);
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
            ShowStatus("Saved route steps are retained in this MVP; resolving a deletion path is an explicit planning action.", InfoBarSeverity.Warning);
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

        var route = _session.State.Route(routeId);
        _editingRouteId = routeId;
        _convertingCaptureId = null;
        RouteTitleInput.Text = route.Title;
        _draftSteps.Clear();
        _draftSteps.AddRange(route.Steps);
        ClearStepEditor();
        SaveRouteButton.Content = "Save route changes";
        RenderPlanning();
    }

    private async void ArchiveRouteButton_Click(object sender, RoutedEventArgs e)
    {
        if (TryReadTag(sender, out var routeId))
        {
            await RunAsync(() => _session.ArchiveRouteAsync(routeId), "Route archived.");
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
        SaveRouteButton.Content = "Convert capture to draft route";
        ShowStatus("Complete the prepared step, add it, then save the converted route.", InfoBarSeverity.Informational);
        RenderPlanning();
    }

    private async void ArchiveCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        if (TryReadTag(sender, out var captureId))
        {
            await RunAsync(() => _session.ArchiveCaptureAsync(captureId), "Capture archived.");
        }
    }

    private void ClearRouteEditor()
    {
        _editingRouteId = null;
        _convertingCaptureId = null;
        _draftSteps.Clear();
        RouteTitleInput.Text = string.Empty;
        ClearStepEditor();
        SaveRouteButton.Content = "Save draft route";
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
            ShowStatus("The selected route is unavailable.", InfoBarSeverity.Error);
            return;
        }

        await RunAsync(() => _session.ActivateRouteAsync(routeId), "Active route saved.");
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
            ShowStatus($"Nothing changed. {exception.Message}", InfoBarSeverity.Error);
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
    }

    private static Visibility ToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
}
