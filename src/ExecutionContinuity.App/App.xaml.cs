using Microsoft.UI.Xaml;
using System.Reflection;
using System.Text;

namespace ExecutionContinuity.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception exception)
        {
            RecordStartupFailure(exception);
            throw;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception exception)
        {
            RecordStartupFailure(exception);
            throw;
        }
    }

    private static void RecordStartupFailure(Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ExecutionContinuity");
            Directory.CreateDirectory(directory);
            var details = new StringBuilder(exception.ToString());
            foreach (var property in exception.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.GetIndexParameters().Length == 0 && property.Name is not "StackTrace")
                {
                    details.AppendLine();
                    details.Append(property.Name);
                    details.Append(": ");
                    details.Append(property.GetValue(exception));
                }
            }

            File.WriteAllText(Path.Combine(directory, "startup-error.txt"), details.ToString());
        }
        catch
        {
            // Startup diagnostics must never replace the original exception.
        }
    }
}
