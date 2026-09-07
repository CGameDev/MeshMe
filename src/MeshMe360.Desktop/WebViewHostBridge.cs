// SPDX-License-Identifier: AGPL-3.0-only
// Routes validated web requests to native file, window and settings services.
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using MeshMe360.Core;

namespace MeshMe360.Desktop;

internal sealed class WebViewHostBridge : IHostBridge, IDisposable
{
    private readonly CoreWebView2 _web;
    private readonly Window _owner;
    private readonly StartupLog _log;
    private readonly SettingsService _settings;
    private readonly RecentFilesService _recent;
    private readonly SessionService _session;
    private readonly FileTokenStore _files = new();
    private readonly Dictionary<string, (string Filename, string RequestId, DateTimeOffset Expiry)> _downloads = [];
    private readonly Dictionary<string, string> _activations = [];
    private bool _disposed;
    private string? _activeFileRequestId;
    private string? _lastEngineError;
    private bool _ready, _allowClose;
    private string? _closeToken;
    public Func<string, string>? ConfirmReplacementOverride { get; init; } // integration tests only
    public event Action? Ready;
    public event Action<string>? FatalError;
    public Func<SaveOperation, string, string?>? SaveDialogOverride { get; init; } // integration tests only
    public Action<string>? CopyDiagnosticsOverride { get; init; } // integration tests only
    public Action<string>? OpenLogFolderOverride { get; init; } // integration tests only

    public WebViewHostBridge(CoreWebView2 web, Window owner, StartupLog log, AppDataPaths paths, SessionService session)
    {
        _web = web; _owner = owner; _log = log; _session = session;
        _settings = new(paths); _recent = new(paths);
        _web.WebMessageReceived += OnMessage;
        _web.AddWebResourceRequestedFilter(HostOrigins.Bridge + "/*", CoreWebView2WebResourceContext.All);
        _web.WebResourceRequested += OnResource;
        _web.DownloadStarting += OnDownload;
        _owner.Closing += OnClosing;
        _web.PermissionRequested += OnPermission;
        SystemEvents.UserPreferenceChanged += OnOsChanged;
    }

    private async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (!HostOrigins.IsApp(args.Source)) return;
        try { await HandleAsync(BridgeMessage.Parse(args.WebMessageAsJson)); }
        catch (Exception error) { await _log.WriteAsync("bridge.invalidMessage", new { error.Message }); }
    }
    private void Send(BridgeMessage message)
    {
        if (!_disposed) _web.PostWebMessageAsJson(message.ToJson());
    }
    private void Reply(BridgeMessage request, object? payload = null, BridgeError? error = null) =>
        Send(BridgeMessage.Create(request.Type + ".result", request.RequestId, payload, error is null, error));

    public async Task HandleAsync(BridgeMessage message)
    {
        await _log.WriteAsync("bridge.request", new { message.Type, message.RequestId });
        try
        {
            object? result = null;
            switch (message.Type)
            {
                case "web.ready":
                    _ready = true;
                    var initial = await _settings.GetAsync();
                    ApplyTheme(initial);
                    Send(BridgeMessage.Create("host.ready", message.RequestId, new
                    {
                        settings = initial, osTheme = OsTheme(), osVersion = Environment.OSVersion.Version.ToString(),
                        update = UpdateStatus.NotConfigured, previousSessionUnclean = _session.Previous is { Clean: false, Id.Length: > 0 }
                    }));
                    Ready?.Invoke();
                    break;
                case "settings.get": result = await _settings.GetAsync(); break;
                case "settings.set":
                    var changed = await _settings.PatchAsync(message.Payload);
                    ApplyTheme(changed); result = changed; break;
                case "recent.list": result = await _recent.ListAsync(); break;
                case "recent.clear": result = await _recent.ClearAsync(); break;
                case "recent.remove": result = await _recent.RemoveAsync(Text(message.Payload, "path")); break;
                case "recent.open": await OpenFileAsync(await _recent.ResolveAsync(Text(message.Payload, "path"))); break;
                case "recent.add":
                    // Only host-approved, successfully loaded activations can be added.
                    var activation = Text(message.Payload, "activationId");
                    if (!_activations.Remove(activation, out var approved)) throw new UnauthorizedAccessException("Unknown file activation.");
                    result = await _recent.AddAsync(approved); break;
                case "document.stateChanged":
                    if (message.Payload.TryGetProperty("downloadUrl", out _))
                    {
                        var url = Text(message.Payload, "downloadUrl");
                        var filename = Text(message.Payload, "filename");
                        _ = SaveOperation.FromFilename(filename);
                        if (!url.StartsWith("blob:" + HostOrigins.App + "/", StringComparison.Ordinal) ||
                            filename != Path.GetFileName(filename) || filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                            throw new InvalidDataException("Invalid download metadata.");
                        foreach (var expired in _downloads.Where(d => d.Value.Expiry <= DateTimeOffset.UtcNow).Select(d => d.Key).ToArray()) _downloads.Remove(expired);
                        if (_downloads.Count >= 4) throw new InvalidOperationException("Too many pending downloads.");
                        _downloads.Add(url, (filename, message.RequestId, DateTimeOffset.UtcNow.AddMinutes(5)));
                        result = new {requestId=message.RequestId};
                    }
                    else if (message.Payload.TryGetProperty("state", out var state) && state.GetString() == "ReadyClean"
                        && message.Payload.TryGetProperty("activationId", out var completedActivation)
                        && completedActivation.ValueKind == JsonValueKind.String
                        && completedActivation.GetString() == _activeFileRequestId)
                    {
                        _files.RevokeAll(); // Loaded Blob/File is now owned by the engine.
                    }
                    break;
                case "document.titleChanged": _owner.Title = "Mesh Me360 — " + Text(message.Payload, "title"); break;
                case "document.fileRead":
                    if (Text(message.Payload, "activationId") == _activeFileRequestId) _files.RevokeAll();
                    break;
                case "document.dirtyChanged": break; // Authoritative revision state stays in the web document.
                case "document.confirmReplacement":
                    result = ConfirmReplacementOverride?.Invoke(Text(message.Payload, "name")) ?? ConfirmReplacement(Text(message.Payload, "name"));
                    break;
                case "window.close":
                    _ = _owner.Dispatcher.BeginInvoke(new Action(() => _owner.Close())); break;
                case "window.closeResponse":
                    if (_closeToken is not null && Text(message.Payload, "token") == _closeToken)
                    {
                        _closeToken = null;
                        if (message.Payload.GetProperty("ready").ValueKind == JsonValueKind.True)
                        {
                            _allowClose = true;
                            _ = _owner.Dispatcher.BeginInvoke(new Action(() => _owner.Close()));
                        }
                    }
                    break;
                case "update.status": result = UpdateStatus.NotConfigured; break;
                case "engine.log":
                case "engine.error":
                    if(message.Type == "engine.error") _lastEngineError = Text(message.Payload, "message")[..Math.Min(Text(message.Payload, "message").Length, 2000)];
                    await _log.WriteAsync(message.Type, new { message.RequestId, message = Text(message.Payload, "message") });
                    if (message.Type == "engine.error" && message.Payload.TryGetProperty("fatal", out var fatal) && fatal.ValueKind == JsonValueKind.True)
                        FatalError?.Invoke(Text(message.Payload, "message"));
                    break;
                case "diagnostics.get":
                    result = new {
                        version = typeof(WebViewHostBridge).Assembly.GetName().Version?.ToString(),
                        build = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(WebViewHostBridge).Assembly)?.InformationalVersion,
                        windows = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                        dotnet = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                        webView2 = _web.Environment.BrowserVersionString,
                        lastEngineError = _lastEngineError, logFolder = _log.DirectoryPath
                    }; break;
                case "diagnostics.copy":
                    var diagnosticText = Text(message.Payload, "text");
                    if(CopyDiagnosticsOverride is not null) CopyDiagnosticsOverride(diagnosticText); else Clipboard.SetText(diagnosticText);
                    break;
                case "diagnostics.openLogFolder":
                    Directory.CreateDirectory(_log.DirectoryPath);
                    if(OpenLogFolderOverride is not null) OpenLogFolderOverride(_log.DirectoryPath);
                    else System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_log.DirectoryPath) { UseShellExecute = true });
                    break;
                case "diagnostics.webInfo": break;
                default: Reply(message, error: new("UnsupportedMessage", "This operation is not available in the current milestone.")); return;
            }
            Reply(message, result);
            await _log.WriteAsync("bridge.result", new { message.Type, message.RequestId, success = true });
        }
        catch (Exception error)
        {
            Reply(message, error: new("OperationFailed", error.Message));
            await _log.WriteAsync("bridge.result", new { message.Type, message.RequestId, success = false, error.Message });
            if (message.Type == "web.ready") FatalError?.Invoke(error.Message);
        }
    }
    private static string Text(JsonElement payload, string name)
    {
        var value = payload.GetProperty(name).GetString();
        if (value is null || value.Length > 16384) throw new InvalidDataException("Invalid text field: " + name);
        return value;
    }

    public Task OpenFileAsync(string path)
    {
        _files.RevokeAll(); _activations.Clear();
        var requestId = Guid.NewGuid().ToString();
        _activeFileRequestId = requestId;
        var grant = _files.Issue(path, requestId);
        _activations.Add(requestId, grant.Path);
        Send(BridgeMessage.Create("host.startupFile", requestId, new
        { token = grant.Token, filename = grant.Filename, contentType = "application/octet-stream" }));
        return _log.WriteAsync("bridge.fileIssued", new { requestId, filename = grant.Filename });
    }
    private async void OnResource(object? sender, CoreWebView2WebResourceRequestedEventArgs args)
    {
        using var deferral = args.GetDeferral();
        try
        {
            var origin = args.Request.Headers.Contains("Origin") ? args.Request.Headers.GetHeader("Origin") : "";
            var method = args.Request.Method;
            var uri = args.Request.Uri;
            var result = await Task.Run(() => _files.Consume(method, uri, origin));
            if (_disposed) { result.Stream.Dispose(); return; }
            args.Response = _web.Environment.CreateWebResourceResponse(result.Stream, 200, "OK",
                "Content-Type: application/octet-stream\r\nCache-Control: no-store\r\nAccess-Control-Allow-Origin: " + HostOrigins.App +
                "\r\nVary: Origin\r\nX-Content-Type-Options: nosniff\r\n");
            await _log.WriteAsync("bridge.fileConsumed", new { requestId = result.Grant.RequestId });
        }
        catch (Exception error)
        {
            if (!_disposed)
                args.Response = _web.Environment.CreateWebResourceResponse(new MemoryStream(Encoding.UTF8.GetBytes("File bridge request rejected.")),
                    403, "Forbidden", "Content-Type: text/plain\r\nCache-Control: no-store\r\nAccess-Control-Allow-Origin: " + HostOrigins.App + "\r\n");
            await _log.WriteAsync("bridge.fileRejected", new { error.Message });
        }
    }

    private async void OnDownload(object? sender, CoreWebView2DownloadStartingEventArgs args)
    {
        using var deferral = args.GetDeferral();
        args.Handled = true;
        string? approvedRequest = null;
        try
        {
            if (!_downloads.Remove(args.DownloadOperation.Uri, out var grant) || grant.Expiry <= DateTimeOffset.UtcNow)
                throw new UnauthorizedAccessException("Unapproved download.");
            approvedRequest = grant.RequestId;
            var operation = SaveOperation.FromFilename(grant.Filename);
            string? selected;
            if (SaveDialogOverride is not null) selected = SaveDialogOverride(operation, grant.Filename);
            else
            {
                var dialog = new SaveFileDialog { Title = operation.Label, FileName = grant.Filename, DefaultExt = operation.Extension,
                    Filter = operation.Filter, AddExtension = true, OverwritePrompt = true, CheckPathExists = true };
                selected = dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
            }
            if (selected is null) { args.Cancel = true; Send(BridgeMessage.Create("document.stateChanged", grant.RequestId, new {downloadCompleted=false})); await _log.WriteAsync("bridge.downloadCancelled", new { requestId = grant.RequestId }); return; }
            // WebView2 may refuse an existing destination. Download beside it,
            // then replace only after all bytes arrive; failure keeps the old file.
            var temporary = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(selected))!, ".meshme-" + Guid.NewGuid().ToString("N") + ".download");
            args.ResultFilePath = temporary;
            var download = args.DownloadOperation;
            EventHandler<object>? changed = null;
            changed = async (_, _) =>
            {
                if (download.State == CoreWebView2DownloadState.InProgress) return;
                download.StateChanged -= changed;
                var completed = false;
                try
                {
                    if (download.State == CoreWebView2DownloadState.Completed) { File.Move(temporary, selected, true); completed = true; }
                    if (completed && operation.Extension is ".bumpmesh" or ".mm360") await _recent.AddAsync(selected);
                    await _log.WriteAsync("bridge.downloadResult", new { requestId = grant.RequestId, completed, reason = download.InterruptReason.ToString() });
                }
                catch (Exception error) { await _log.WriteAsync("bridge.downloadResultError", new { requestId = grant.RequestId, error=error.ToString() }); }
                finally
                {
                    Send(BridgeMessage.Create("document.stateChanged", grant.RequestId, new {downloadCompleted=completed,filename=grant.Filename,savedFilename=Path.GetFileName(selected)}));
                    try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { /* Retain failed staging data without touching the destination. */ }
                }
            };
            download.StateChanged += changed;
            await _log.WriteAsync("bridge.downloadStarting", new { requestId = grant.RequestId, operation = operation.Label });
        }
        catch (Exception error) { args.Cancel = true; if(approvedRequest is not null) Send(BridgeMessage.Create("document.stateChanged", approvedRequest, new {downloadCompleted=false})); await _log.WriteAsync("bridge.downloadRejected", new { error.Message }); }
    }
    public void DisableCloseGuard() => _ready = false; // Failed renderer cannot answer a close request; session remains unclean.
    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs args)
    {
        if (_disposed) return;
        if (!_ready || _allowClose) { Dispose(); return; }
        args.Cancel = true;
        if (_closeToken is not null) return;
        var token = _closeToken = Guid.NewGuid().ToString();
        Send(BridgeMessage.Create("host.closeRequested", token, new {}));
        await Task.Delay(TimeSpan.FromMinutes(5));
        if (_closeToken == token) { _closeToken = null; await _log.WriteAsync("host.closeTimedOut", new {token}); }
    }
    private string ConfirmReplacement(string name)
    {
        var dialog = new Window {Owner=_owner,Title="Mesh Me360 — Unsaved changes",SizeToContent=SizeToContent.WidthAndHeight,WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false};
        var panel=new System.Windows.Controls.StackPanel {Margin=new Thickness(20),MaxWidth=480};
        panel.Children.Add(new System.Windows.Controls.TextBlock {Text="Save changes to "+name+" before continuing?",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,20)});
        var buttons=new System.Windows.Controls.StackPanel {Orientation=System.Windows.Controls.Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
        string choice="cancel";
        foreach(var (label,value) in new[]{("Save","save"),("Don’t Save","discard"),("Cancel","cancel")})
        {
            var button=new System.Windows.Controls.Button {Content=label,MinWidth=85,Padding=new Thickness(12,6,12,6),Margin=new Thickness(4),IsDefault=value=="save",IsCancel=value=="cancel"};
            button.Click+=(_,_)=>{choice=value;dialog.Close();};buttons.Children.Add(button);
        }
        panel.Children.Add(buttons);dialog.Content=panel;dialog.ShowDialog();return choice;
    }
    private void OnPermission(object? sender, CoreWebView2PermissionRequestedEventArgs args)
    {
        if (args.PermissionKind != CoreWebView2PermissionKind.MultipleAutomaticDownloads) return;
        // Each actual download must still consume an exact approved Blob URL and
        // pass the native destination dialog. Never persist a blanket permission.
        args.SavesInProfile = false;
        args.State = HostOrigins.IsApp(args.Uri) && _downloads.Values.Any(g => g.Expiry > DateTimeOffset.UtcNow)
            ? CoreWebView2PermissionState.Allow : CoreWebView2PermissionState.Deny;
        args.Handled = true;
    }
    private static string OsTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0 ? "Dark" : "Light";
    }
    private void ApplyTheme(AppSettings settings)
    {
        var dark = (settings.Theme == "System" ? OsTheme() : settings.Theme) == "Dark";
        _owner.Background = dark ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.White;
        _web.Profile.PreferredColorScheme = settings.Theme == "System" ? CoreWebView2PreferredColorScheme.Auto :
            dark ? CoreWebView2PreferredColorScheme.Dark : CoreWebView2PreferredColorScheme.Light;
    }
    private void OnOsChanged(object sender, UserPreferenceChangedEventArgs args) => _owner.Dispatcher.InvokeAsync(async () =>
    {
        try { if (_disposed) return; ApplyTheme(await _settings.GetAsync()); Send(BridgeMessage.Create("theme.osChanged", Guid.NewGuid().ToString(), new { theme = OsTheme() })); }
        catch (Exception error) { await _log.WriteAsync("host.themeError", new { error.Message }); }
    });
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _files.Dispose(); _downloads.Clear(); _activations.Clear();
        _owner.Closing -= OnClosing;
        SystemEvents.UserPreferenceChanged -= OnOsChanged;
        _web.WebMessageReceived -= OnMessage; _web.WebResourceRequested -= OnResource; _web.DownloadStarting -= OnDownload;
        _web.PermissionRequested -= OnPermission;
    }
}
