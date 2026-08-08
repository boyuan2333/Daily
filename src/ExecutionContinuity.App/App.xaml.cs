using Microsoft.UI.Xaml;

namespace ExecutionContinuity.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        StartupDiagnostics.Trace("App constructor entered");
        try
        {
            StartupDiagnostics.Trace("App constructor before InitializeComponent");
            InitializeComponent();
            StartupDiagnostics.Trace("App constructor after InitializeComponent");
            UnhandledException += App_UnhandledException;
            StartupDiagnostics.Trace("App constructor completed");
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Record(exception);
            throw;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        StartupDiagnostics.Trace("OnLaunched entered");
        try
        {
            StartupDiagnostics.Trace("OnLaunched before MainWindow constructor");
            _window = new MainWindow();
            StartupDiagnostics.Trace("OnLaunched after MainWindow constructor");
            StartupDiagnostics.Trace("OnLaunched before Activate");
            _window.Activate();
            StartupDiagnostics.Trace("OnLaunched after Activate");
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Record(exception);
            throw;
        }
    }

    private static void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        StartupDiagnostics.Record(e.Exception);
    }
}
