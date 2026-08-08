using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace ExecutionContinuity.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        StartupDiagnostics.Trace("Program.Main entered");
        try
        {
            StartupDiagnostics.Trace("Program.Main before InitializeComWrappers");
            WinRT.ComWrappersSupport.InitializeComWrappers();
            StartupDiagnostics.Trace("Program.Main after InitializeComWrappers");
            Application.Start(_ =>
            {
                StartupDiagnostics.Trace("Program.Main Application.Start callback entered");
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                StartupDiagnostics.Trace("Program.Main before App constructor");
                new App();
                StartupDiagnostics.Trace("Program.Main after App constructor");
            });
            StartupDiagnostics.Trace("Program.Main Application.Start returned");
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Record(exception);
            throw;
        }
    }
}
