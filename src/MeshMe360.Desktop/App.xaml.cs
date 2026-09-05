// SPDX-License-Identifier: AGPL-3.0-only
using System.Windows;

namespace MeshMe360.Desktop;

public partial class App : Application
{
    private MainWindow? _window;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _window = new MainWindow();
        MainWindow = _window;
        _window.Show();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        // Final lifecycle step after normal window close and WebView disposal.
        // Forced process termination never reaches this path.
        try
        {
            if (_window is { ClosedNormally: true, Session: not null })
                Task.Run(() => _window.Session.CompleteAsync()).GetAwaiter().GetResult();
        }
        catch (Exception error) { System.Diagnostics.Debug.WriteLine(error); }
        finally { _window?.Session?.Dispose(); }
        base.OnExit(e);
    }
}
