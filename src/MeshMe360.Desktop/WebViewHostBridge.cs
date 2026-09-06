// SPDX-License-Identifier: AGPL-3.0-only
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
    public event Action? Ready;
    public event Action<string>? FatalError;
    public Func<SaveOperation, string, string?>? SaveDialogOverride { get; init; } // integration tests only

    public WebViewHostBridge(CoreWebView2 web, Window owner, StartupLog log, AppDataPaths paths, SessionService session)
    {
        _web = web; _owner = owner; _log = log; _session = session;
        _settings = new(paths); _recent = new(paths);
        _web.WebMessageReceived += OnMessage;
        _web.AddWebResourceRequestedFilter(HostOrigins.Bridge + "/*", CoreWebView2WebResourceContext.All);
        _web.WebResourceRequested += OnResource;
        _web.DownloadStarting += OnDownload;
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
                case "document.dirtyChanged": break; // M4 owns safe-close/dirty-document policy.
                case "update.status": result = UpdateStatus.NotConfigured; break;
                case "engine.log":
                case "engine.error":
                    await _log.WriteAsync(message.Type, new { message.RequestId, message = Text(message.Payload, "message") });
                    if (message.Type == "engine.error" && message.Payload.TryGetProperty("fatal", out var fatal) && fatal.ValueKind == JsonValueKind.True)
                        FatalError?.Invoke(Text(message.Payload, "message"));
                    break;
                case "diagnostics.webInfo": break; // M10 diagnostics UI remains gated.
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
        try
        {
            if (!_downloads.Remove(args.DownloadOperation.Uri, out var grant) || grant.Expiry <= DateTimeOffset.UtcNow)
                throw new UnauthorizedAccessException("Unapproved download.");
            var operation = SaveOperation.FromFilename(grant.Filename);
            string? selected;
            if (SaveDialogOverride is not null) selected = SaveDialogOverride(operation, grant.Filename);
            else
            {
                var dialog = new SaveFileDialog { Title = operation.Label, FileName = grant.Filename, DefaultExt = operation.Extension,
                    Filter = operation.Filter, AddExtension = true, OverwritePrompt = true, CheckPathExists = true };
                selected = dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
            }
            if (selected is null) { args.Cancel = true; await _log.WriteAsync("bridge.downloadCancelled", new { requestId = grant.RequestId }); return; }
            args.ResultFilePath = selected;
            var download = args.DownloadOperation;
            EventHandler<object>? changed = null;
            changed = async (_, _) =>
            {
                if (download.State == CoreWebView2DownloadState.InProgress) return;
                download.StateChanged -= changed;
                try
                {
                    var completed = download.State == CoreWebView2DownloadState.Completed;
                    if (completed && operation.Extension is ".bumpmesh" or ".mm360") await _recent.AddAsync(selected);
                    Send(BridgeMessage.Create("document.stateChanged", grant.RequestId, new {downloadCompleted=completed,filename=grant.Filename,savedFilename=Path.GetFileName(selected)}));
                    await _log.WriteAsync("bridge.downloadResult", new { requestId = grant.RequestId, completed, reason = download.InterruptReason.ToString() });
                }
                catch (Exception error) { await _log.WriteAsync("bridge.downloadResultError", new { requestId = grant.RequestId, error=error.ToString() }); }
            };
            download.StateChanged += changed;
            await _log.WriteAsync("bridge.downloadStarting", new { requestId = grant.RequestId, operation = operation.Label });
        }
        catch (Exception error) { args.Cancel = true; await _log.WriteAsync("bridge.downloadRejected", new { error.Message }); }
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
        _disposed = true; _files.Dispose(); _downloads.Clear(); _activations.Clear();
        SystemEvents.UserPreferenceChanged -= OnOsChanged;
        _web.WebMessageReceived -= OnMessage; _web.WebResourceRequested -= OnResource; _web.DownloadStarting -= OnDownload;
        _web.PermissionRequested -= OnPermission;
    }
}
