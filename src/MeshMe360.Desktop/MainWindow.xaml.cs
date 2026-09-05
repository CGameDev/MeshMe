// SPDX-License-Identifier: AGPL-3.0-only
using System.Windows;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace MeshMe360.Desktop;

public partial class MainWindow : Window
{
    private const string AppOrigin = "https://app.meshme360.local/";
    private readonly TaskCompletionSource _engineReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly StartupLog _log = new();
    private bool _closed;
    private bool _fatal;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += InitializeAsync;
        Closed += (_, _) => { _closed = true; _engineReady.TrySetCanceled(); Web.Dispose(); };
    }

    private async void InitializeAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            await _log.WriteAsync("host.startup", new { version = typeof(MainWindow).Assembly.GetName().Version?.ToString(), framework = Environment.Version.ToString() });
            var webRoot = Path.Combine(AppContext.BaseDirectory, "EngineWeb");
            if (!File.Exists(Path.Combine(webRoot, "index.html")))
                throw new FileNotFoundException("Packaged engine files are missing. Rebuild Mesh Me360.");
            var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeshMe360", "WebView2", "UserData");
            var fixedRuntime = Path.Combine(AppContext.BaseDirectory, "WebView2FixedRuntime");
            // Development can use the installed runtime. M8 will require the pinned
            // fixed runtime in release artifacts; no online bootstrapper runs here.
            var environment = await CoreWebView2Environment.CreateAsync(Directory.Exists(fixedRuntime) ? fixedRuntime : null, userData);
            if (_closed) return;
            await Web.EnsureCoreWebView2Async(environment);
            if (_closed) return;
            var core = Web.CoreWebView2;
            core.SetVirtualHostNameToFolderMapping("app.meshme360.local", webRoot, CoreWebView2HostResourceAccessKind.DenyCors);
            core.NavigationStarting += (_, args) =>
            {
                if (!IsAppUri(args.Uri)) args.Cancel = true;
            };
            core.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                // Explicitly clicked documentation/attribution links use the OS browser.
                if (args.IsUserInitiated && Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                {
                    try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
                    catch (Exception error) { _ = _log.WriteAsync("host.linkError", new { error.Message }); }
                }
            };
            core.WebMessageReceived += OnWebMessage;
            core.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess) FailStartup(new InvalidOperationException($"Engine navigation failed: {args.WebErrorStatus}"));
            };
            core.ProcessFailed += (_, args) => FailStartup(new InvalidOperationException($"WebView2 process failed: {args.ProcessFailedKind}"));
            await _log.WriteAsync("host.webview2", new { environment.BrowserVersionString, fixedRuntime = Directory.Exists(fixedRuntime) });
            core.Navigate(AppOrigin + "index.html");
            await _engineReady.Task.WaitAsync(TimeSpan.FromSeconds(60));
            if (!_closed && !_fatal) StartupPanel.Visibility = Visibility.Collapsed;
        }
        catch (Exception error)
        {
            if (!_closed) FailStartup(error);
        }
    }

    private static bool IsAppUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && uri.Host == "app.meshme360.local" && uri.IsDefaultPort;

    private async void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            if (!IsAppUri(args.Source)) return;
            var json = args.WebMessageAsJson;
            if (json.Length > 32768) throw new InvalidDataException("Startup message exceeds the diagnostic limit.");
            using var message = JsonDocument.Parse(json);
            var root = message.RootElement;
            var type = root.GetProperty("type").GetString();
            if (type is not ("web.ready" or "engine.error")) return;
            var payload = root.GetProperty("payload");
            await _log.WriteAsync(type, new { requestId = root.GetProperty("requestId").GetString(), payload });
            if (_closed) return;
            if (type == "web.ready") _engineReady.TrySetResult();
            else if (payload.TryGetProperty("fatal", out var fatal) && fatal.ValueKind == JsonValueKind.True)
                FailStartup(new InvalidOperationException(payload.GetProperty("message").GetString()));
        }
        catch (Exception error)
        {
            await _log.WriteAsync("host.messageError", new { error.Message });
        }
    }

    private void FailStartup(Exception error)
    {
        if (_closed || _fatal) return;
        _fatal = true;
        _engineReady.TrySetException(error);
        _ = _log.WriteAsync("host.fatal", new { error = error.ToString() });
        Web.Visibility = Visibility.Collapsed;
        StartupPanel.Visibility = Visibility.Visible;
        StartupTitle.Text = "Mesh Me360 could not start";
        StartupMessage.Text = "The local engine or WebView2 runtime could not initialize. Review the details and log folder below.";
        ErrorDetails.Text = error + Environment.NewLine + "Logs: " + _log.DirectoryPath;
        ErrorDetails.Visibility = ErrorActions.Visibility = Visibility.Visible;
    }

    private void CopyDetails_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(ErrorDetails.Text); }
        catch (Exception error) { StartupMessage.Text = "Could not copy details: " + error.Message; }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try { Directory.CreateDirectory(_log.DirectoryPath); Process.Start(new ProcessStartInfo(_log.DirectoryPath) { UseShellExecute = true }); }
        catch (Exception error) { StartupMessage.Text = "Could not open the log folder: " + error.Message; }
    }
}
