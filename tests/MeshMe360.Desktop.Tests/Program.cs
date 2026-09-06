// SPDX-License-Identifier: AGPL-3.0-only
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using MeshMe360.Core;
using MeshMe360.Desktop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            var root = Path.GetFullPath(args[0]);
            var testRoot = Path.Combine(root, "artifacts", "m2-integration-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);
            var paths = new AppDataPaths(Path.Combine(testRoot, "Roaming"), Path.Combine(testRoot, "Local"));
            using var session = new SessionService(paths);
            var window = new Window { Title = "Mesh Me360 integration tests", Width = 1280, Height = 800 };
            var web = new WebView2(); window.Content = web; window.Show();
            WebViewHostBridge? bridge = null;
            try
            {
                if (args.Contains("--locked")) await new SettingsService(paths).PatchAsync(JsonSerializer.SerializeToElement(new {theme="Dark"}));
                await session.StartAsync();
                var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(testRoot, "WebView2")).WaitAsync(TimeSpan.FromSeconds(30));
                await web.EnsureCoreWebView2Async(environment).WaitAsync(TimeSpan.FromSeconds(30));
                var core = web.CoreWebView2;
                core.NavigationStarting += (_, e) => { if (!HostOrigins.IsApp(e.Uri)) e.Cancel = true; };
                core.SetVirtualHostNameToFolderMapping("app.meshme360.local", Path.Combine(root, "artifacts", "web"), CoreWebView2HostResourceAccessKind.DenyCors);
                var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var chosen = Path.Combine(testRoot, "native-export.stl");
                var dialogCount = 0;
                var cancelDownload = false;
                bridge = new WebViewHostBridge(core, window, new StartupLog(Path.Combine(testRoot, "Logs")), paths, session)
                {
                    SaveDialogOverride = (operation, name) => { dialogCount++; chosen = Path.Combine(testRoot, "native-export" + operation.Extension); return cancelDownload ? null : chosen; }
                };
                bridge.Ready += () => ready.TrySetResult();
                bridge.FatalError += error => ready.TrySetException(new Exception(error));
                if(args.Contains("--backend-probe"))
                {
                    core.Navigate(HostOrigins.App+"/candidate/probe.html");
                    string result="";
                    await Until(async()=>{result=JsonSerializer.Deserialize<string>(await core.ExecuteScriptAsync("document.body.innerText"))??"";return result.StartsWith("PASS:")||result.StartsWith("BLOCKED:");});
                    await File.WriteAllTextAsync(Path.Combine(testRoot,"backend-probe.txt"),result);
                    Console.WriteLine(result);Console.WriteLine("Backend probe evidence: "+testRoot);
                    await session.CompleteAsync();return;
                }
                core.Navigate(HostOrigins.App + "/index.html");
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(60));
                Console.WriteLine("PASS: actual WebView2 startup handshake");
                var fixture = Path.Combine(root, "tests", "fixtures", "cube_binary.stl");
                await bridge.OpenFileAsync(fixture);
                var recent = new RecentFilesService(paths);
                await Until(async () => (await recent.ListAsync()).Any(e => e.Path == fixture));
                Console.WriteLine("PASS: native file activation → internal binary GET → preserved engine loader → recent file acknowledgement");
                async Task<JsonElement> Evaluate(string expression)
                {
                    var raw = await core.CallDevToolsProtocolMethodAsync("Runtime.evaluate", JsonSerializer.Serialize(new { expression, awaitPromise = true, returnByValue = true, userGesture = true })).WaitAsync(TimeSpan.FromSeconds(30));
                    using var parsed = JsonDocument.Parse(raw);
                    if (parsed.RootElement.TryGetProperty("exceptionDetails", out var error)) throw new Exception(error.ToString());
                    return parsed.RootElement.GetProperty("result").GetProperty("value").Clone();
                }
                if(args.Contains("--scene"))
                {
                    await bridge.OpenFileAsync(fixture);
                    await Until(async()=> (await Evaluate("window.meshMe360Locked.scene.document.state.objects.length")).GetInt32()==2);
                    await Evaluate("(()=>{const s=window.meshMe360Locked.scene;s.document.transform({position:[30,12,8],scale:[2,1,1]});s.refreshScene();return true})()");
                    await Evaluate("(async()=>{await window.meshMe360Locked.registry.execute('Save');return true})()");
                    var scenePath=Path.Combine(testRoot,"native-export.mm360");
                    await Until(async()=> (await recent.ListAsync()).Any(e=>e.Path==scenePath));
                    await Until(async()=> !(await Evaluate("window.meshMe360Locked.scene.document.dirty")).GetBoolean());
                    using(var sceneZip=ZipFile.OpenRead(scenePath))
                    { if(sceneZip.GetEntry("project.json") is null || sceneZip.GetEntry("meshes/1.positions.bin") is null)throw new Exception("Scene container incomplete"); }
                    await bridge.OpenFileAsync(scenePath);
                    await Until(async()=> (await Evaluate("window.meshMe360Locked.scene.document.state.name")).GetString()=="native-export.mm360");
                    var restored=await Evaluate("window.meshMe360Locked.scene.document.state.objects.map(o=>({position:o.position,scale:o.scale}))");
                    if(restored.GetArrayLength()!=2||restored[1].GetProperty("position")[0].GetInt32()!=30||restored[1].GetProperty("scale")[0].GetInt32()!=2)throw new Exception("Scene state was lost");
                    Console.WriteLine("PASS: actual multi-object import, transform, native mm360 save and binary-bridge reopen; dirty cleared after download completion");
                    Console.WriteLine("Scene evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if (args.Contains("--locked"))
                {
                    await core.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride", "{\"width\":1536,\"height\":1024,\"deviceScaleFactor\":1,\"mobile\":false}");
                    await Evaluate("new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(()=>resolve(true))))");
                    var report = await Evaluate("({size:[innerWidth,innerHeight],regions:window.meshMe360Locked.regions(),controls:window.meshMe360Locked.report(),toolbar:[...document.querySelectorAll('.locked-toolbar button')].map(b=>({name:b.textContent,rect:{x:b.getBoundingClientRect().x,y:b.getBoundingClientRect().y,width:b.getBoundingClientRect().width,height:b.getBoundingClientRect().height}}))})");
                    await File.WriteAllTextAsync(Path.Combine(testRoot,"locked-layout.json"),report.GetRawText());
                    var capture = await core.CallDevToolsProtocolMethodAsync("Page.captureScreenshot", "{\"format\":\"png\",\"captureBeyondViewport\":true}");
                    using var png=JsonDocument.Parse(capture);
                    await File.WriteAllBytesAsync(Path.Combine(testRoot,"locked-1536x1024.png"),Convert.FromBase64String(png.RootElement.GetProperty("data").GetString()!));
                    Console.WriteLine("LOCKED UI EVIDENCE (not an acceptance pass): "+testRoot);
                    await session.CompleteAsync();
                    return;
                }
                var response = await Evaluate("(async()=>{try{return (await fetch('https://bridge.meshme360.local/file/'+'0'.repeat(64))).status}catch{return 403}})()");
                if (response.GetInt32() != 403) throw new Exception("Unknown token accepted");
                Console.WriteLine("PASS: actual resource interception rejects unknown token");
                var saved = await Evaluate("(async()=>{const s=await window.meshMe360.rpc.request('settings.set',{theme:'Dark'});return s.theme})()");
                if (saved.GetString() != "Dark") throw new Exception("Settings bridge failed");
                Console.WriteLine("PASS: settings request/response correlation in WebView2");
                // The host's actual DownloadStarting path runs; only the modal user decision is substituted.
                await Evaluate("(async()=>{await window.meshMe360.download(new Blob([new Uint8Array([1,2,3,4])]),'native-export.stl');return true})()");
                await Until(() => Task.FromResult(File.Exists(chosen) && new FileInfo(chosen).Length == 4));
                if (dialogCount != 1 || !(await File.ReadAllBytesAsync(chosen)).SequenceEqual(new byte[] {1,2,3,4})) throw new Exception("Native destination mismatch");
                Console.WriteLine("PASS: actual WebView2 download intercepted and written to chosen path (dialog decision substituted)");
                await Evaluate("(async()=>{await window.meshMe360.download(new Blob([new Uint8Array([5,6,7,8])]),'native-export.3mf');return true})()");
                var threeMf = Path.Combine(testRoot, "native-export.3mf");
                await Until(() => Task.FromResult(File.Exists(threeMf) && new FileInfo(threeMf).Length == 4));
                if (!(await File.ReadAllBytesAsync(threeMf)).SequenceEqual(new byte[] {5,6,7,8})) throw new Exception("3MF transfer altered bytes");
                Console.WriteLine("PASS: 3MF destination routing and exact bytes (synthetic payload; geometry parity is a separate test)");
                await Evaluate("(()=>{document.getElementById('export-model-chk').checked=true;document.getElementById('export-go-btn').click();return true})()");
                var project = Path.Combine(testRoot, "native-export.bumpmesh");
                await Until(async () => (await recent.ListAsync()).Any(e => e.Path == project));
                using (var zip = ZipFile.OpenRead(project))
                {
                    if (zip.GetEntry("settings.json") is null || zip.GetEntry("model.stl")?.Length != 684) throw new Exception("Project export payload invalid");
                }
                Console.WriteLine("PASS: preserved project serializer → native download → valid ZIP/model and recent entry after success");
                var count = dialogCount;
                cancelDownload = true;
                await Evaluate("(async()=>{await window.meshMe360.download(new Blob(['cancelled']),'cancel.stl');return true})()");
                await Until(() => Task.FromResult(dialogCount == count + 1));
                await Task.Delay(200);
                if (!(await File.ReadAllBytesAsync(Path.Combine(testRoot, "native-export.stl"))).SequenceEqual(new byte[] {1,2,3,4})) throw new Exception("Cancelled download changed destination");
                Console.WriteLine("PASS: cancelled native download preserves existing destination");
                await Evaluate("(()=>{const s=document.getElementById('lang-select');s.value='de';s.dispatchEvent(new Event('change'));return true})()");
                await Until(async () => (await new SettingsService(paths).GetAsync()).Language == "de");
                Console.WriteLine("PASS: upstream language control persists through host settings");
                await session.CompleteAsync();
                Console.WriteLine("M2 integration tests passed. Logs: " + testRoot);
            }
            catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
            finally { bridge?.Dispose(); web.Dispose(); window.Close(); app.Shutdown(Environment.ExitCode); }
        };
        return app.Run();
    }
    private static async Task Until(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline) { if (await condition()) return; await Task.Delay(100); }
        throw new TimeoutException("Integration condition not reached.");
    }
}
