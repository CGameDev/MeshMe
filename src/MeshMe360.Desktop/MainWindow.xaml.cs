// SPDX-License-Identifier: AGPL-3.0-only
// Owns native startup, WebView lifetime and visible startup failures.
using System.Windows;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using MeshMe360.Core;
using MeshMe360.Core.Licensing;
using MeshMe360.Desktop.Licensing;

namespace MeshMe360.Desktop;

public partial class MainWindow : Window
{
    private const string AppOrigin = "https://app.meshme360.local/";
    private readonly TaskCompletionSource _engineReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly StartupLog _log = new();
    private bool _closed;
    private bool _fatal;
    private WebViewHostBridge? _bridge;
    internal string ActiveRenderingMode { get; private set; } = "Automatic";
    private readonly IHardwareFingerprintService _hardware = new WindowsHardwareFingerprint();
    private ILicenseService? _licensing;
    private bool _licenseDialogOpen;
    private readonly System.Windows.Threading.DispatcherTimer _licenseTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    internal SessionService? Session { get; private set; }
    internal bool ClosedNormally { get; private set; }
    internal bool RestartRequested { get; private set; }

    /// <summary>Initialize native controls and register cleanup before starting the renderer.</summary>
    public MainWindow()
    {
        InitializeComponent();
        Loaded += InitializeAsync;
        Closed += (_, _) => { _licenseTimer.Stop(); _closed = true; _engineReady.TrySetCanceled(); _bridge?.Dispose(); Web.Dispose(); ClosedNormally = !_fatal; };
    }

    /// <summary>Load local runtime assets and wait for the engine handshake before accepting startup files.</summary>
    private async void InitializeAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            var paths = AppDataPaths.ForCurrentUser();
            var configurationPath = Path.Combine(AppContext.BaseDirectory, "activation.json");
            ActivationConfiguration? configuration = File.Exists(configurationPath) ? JsonSerializer.Deserialize<ActivationConfiguration>(await File.ReadAllTextAsync(configurationPath), BridgeMessage.Json) : null;
            IActivationApiClient api = configuration is null ? new UnconfiguredActivation() : new ActivationApiClient(new Uri(configuration.Endpoint));
            _licensing = new LicenseService(_hardware, api, new EntitlementTokenVerifier(configuration?.PublicKeys ?? new()), new DpapiLicenseCache(Path.Combine(paths.Local, "Licensing")));
            if (!await EnsureEntitlementAsync()) return;
            _licenseTimer.Tick += async (_, _) => { if (!_licenseDialogOpen) await EnsureEntitlementAsync(); };
            _licenseTimer.Start();
            ActiveRenderingMode = (await new SettingsService(paths).GetAsync()).RenderingMode;
            await _log.WriteAsync("host.startup", new { version = typeof(MainWindow).Assembly.GetName().Version?.ToString(), framework = Environment.Version.ToString() });
            Session = new SessionService(AppDataPaths.ForCurrentUser());
            await Session.StartAsync();
            var webRoot = Path.Combine(AppContext.BaseDirectory, "EngineWeb");
            if (!File.Exists(Path.Combine(webRoot, "index.html")))
                throw new FileNotFoundException("Packaged engine files are missing. Rebuild Mesh Me360.");
            var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeshMe360", "WebView2", "UserData");
            var fixedRuntime = Path.Combine(AppContext.BaseDirectory, "WebView2FixedRuntime");
            // Development can use the installed runtime. M8 will require the pinned
            // fixed runtime in release artifacts; no online bootstrapper runs here.
            var environment = await CoreWebView2Environment.CreateAsync(Directory.Exists(fixedRuntime) ? fixedRuntime : null, userData,
                new CoreWebView2EnvironmentOptions(GraphicsSettings.BrowserArguments(ActiveRenderingMode)));
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
            _bridge = new WebViewHostBridge(core, this, _log, AppDataPaths.ForCurrentUser(), Session);
            _bridge.Ready += () => _engineReady.TrySetResult();
            _bridge.FatalError += message => FailStartup(new InvalidOperationException(message));
            core.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess) FailStartup(new InvalidOperationException($"Engine navigation failed: {args.WebErrorStatus}"));
            };
            core.ProcessFailed += (_, args) => FailStartup(new InvalidOperationException($"WebView2 process failed: {args.ProcessFailedKind}"));
            await _log.WriteAsync("host.webview2", new { environment.BrowserVersionString, fixedRuntime = Directory.Exists(fixedRuntime) });
            core.Navigate(AppOrigin + "index.html");
            await _engineReady.Task.WaitAsync(TimeSpan.FromSeconds(60));
            if (!_closed && !_fatal)
            {
                // WebView2 has its own native surface; a WPF overlay cannot hide its early frames.
                Web.Visibility = Visibility.Visible;
                StartupPanel.Visibility = Visibility.Collapsed;
            }
            if (!_closed && !_fatal)
            {
                try
                {
                    var startup = HostFiles.ParseArguments(Environment.GetCommandLineArgs().Skip(1).ToArray());
                    if (startup.Warning is not null) MessageBox.Show(this, startup.Warning, "Mesh Me360");
                    if (startup.Path is not null) await _bridge.OpenFileAsync(startup.Path);
                }
                catch (Exception error)
                {
                    await _log.WriteAsync("host.startupFileError", new { error.Message });
                    MessageBox.Show(this, error.Message, "Mesh Me360 — Could not open file", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception error)
        {
            if (!_closed) FailStartup(error);
        }
    }

    /// <summary>Restrict in-app navigation to the configured application origin.</summary>
    private static bool IsAppUri(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && uri.Host == "app.meshme360.local" && uri.IsDefaultPort;

    /// <summary>Expose a recoverable error surface and prevent a failed session from appearing clean.</summary>
    private void FailStartup(Exception error)
    {
        if (_closed || _fatal) return;
        _fatal = true;
        _bridge?.DisableCloseGuard();
        _engineReady.TrySetException(error);
        _ = _log.WriteAsync("host.fatal", new { error = error.ToString() });
        Web.Visibility = Visibility.Collapsed;
        StartupPanel.Visibility = Visibility.Visible;
        StartupTitle.Text = "Mesh Me360 could not start";
        StartupMessage.Text = "The local engine or WebView2 runtime could not initialize. Review the details and log folder below.";
        ErrorDetails.Text = error + Environment.NewLine + "Logs: " + _log.DirectoryPath;
        ErrorDetails.Visibility = ErrorActions.Visibility = Visibility.Visible;
        if (ActiveRenderingMode != "SoftwareCompatibility" && GraphicsSettings.IsGraphicsInitializationFailure(error.Message))
        {
            var recovery = new System.Windows.Controls.Button { Content = "Restart in Compatibility Mode", Margin = new Thickness(8) };
            recovery.Click += async (_, _) =>
            {
                if (MessageBox.Show(this, "Software Compatibility Mode can be slower. Save this preference and restart? You can restore Automatic in Preferences.", "Graphics recovery", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                await new SettingsService(AppDataPaths.ForCurrentUser()).PatchAsync(JsonSerializer.SerializeToElement(new { renderingMode = "SoftwareCompatibility" }));
                RestartRequested = true; Close();
            };
            ErrorActions.Children.Add(recovery);
        }
    }

    internal async Task ShowLicenseAsync() => await EnsureEntitlementAsync(true);
    private async Task<bool> EnsureEntitlementAsync(bool show = false)
    {
        if (_licensing is null || _licenseDialogOpen || _closed) return false;
        _licenseDialogOpen = true;
        try
        {
            LicenseState state;
            try { state = await _licensing.RefreshAsync(); }
            catch { state = new(false, "License validation could not complete. Retry or contact support. Your projects are preserved."); }
            if (state.CanEdit && !show) return true;
            if (!state.CanEdit) { Web.IsEnabled = false; Web.Visibility = Visibility.Hidden; }
            var dialog = new LicenseWindow(this, _licensing, _hardware, state, _bridge is null ? null : async () =>
                { await Web.ExecuteScriptAsync("window.meshMe360Locked?.registry.execute('Save')"); });
            dialog.ShowDialog();
            if (dialog.Entitled) { Web.IsEnabled = true; if (_bridge is not null && !_fatal) Web.Visibility = Visibility.Visible; return true; }
            Web.IsEnabled = false;
            Web.Visibility = Visibility.Hidden;
            // Keep the normal unsaved-work close guard active; no project/recovery data is deleted.
            Close(); return false;
        }
        finally { _licenseDialogOpen = false; }
    }

    /// <summary>Copy the displayed startup failure for troubleshooting.</summary>
    private void CopyDetails_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(ErrorDetails.Text); }
        catch (Exception error) { StartupMessage.Text = "Could not copy details: " + error.Message; }
    }

    /// <summary>Open the current application log folder through the Windows shell.</summary>
    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try { Directory.CreateDirectory(_log.DirectoryPath); Process.Start(new ProcessStartInfo(_log.DirectoryPath) { UseShellExecute = true }); }
        catch (Exception error) { StartupMessage.Text = "Could not open the log folder: " + error.Message; }
    }
}
