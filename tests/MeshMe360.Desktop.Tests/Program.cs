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
            var web = new WebView2(); if(args.Contains("--startup")||args.Contains("--hidden-startup"))web.Visibility=Visibility.Hidden; window.Content = web; window.Show();
            WebViewHostBridge? bridge = null;
            try
            {
                if (args.Contains("--locked")) await new SettingsService(paths).PatchAsync(JsonSerializer.SerializeToElement(new {theme="Dark"}));
                await session.StartAsync();
                var packagedRuntime=Path.Combine(AppContext.BaseDirectory,"WebView2FixedRuntime");
                var environment = await CoreWebView2Environment.CreateAsync(args.Contains("--packaged") ? packagedRuntime : null, Path.Combine(testRoot, "WebView2"), new CoreWebView2EnvironmentOptions(GraphicsSettings.BrowserArguments(args.Contains("--compat") ? "SoftwareCompatibility" : "Automatic"))).WaitAsync(TimeSpan.FromSeconds(30));
                await web.EnsureCoreWebView2Async(environment).WaitAsync(TimeSpan.FromSeconds(30));
                if(args.Contains("--startup"))web.Visibility=Visibility.Visible;
                var core = web.CoreWebView2;
                core.GetDevToolsProtocolEventReceiver("Runtime.exceptionThrown").DevToolsProtocolEventReceived += (_,e)=>Console.Error.WriteLine("Web runtime error: "+e.ParameterObjectAsJson);
                await core.CallDevToolsProtocolMethodAsync("Runtime.enable","{}");
                core.NavigationStarting += (_, e) => { if (!HostOrigins.IsApp(e.Uri)) e.Cancel = true; };
                core.SetVirtualHostNameToFolderMapping("app.meshme360.local", args.Contains("--packaged") ? Path.Combine(AppContext.BaseDirectory,"EngineWeb") : Path.Combine(root, "artifacts", "web"), CoreWebView2HostResourceAccessKind.DenyCors);
                var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var chosen = Path.Combine(testRoot, "native-export.stl");
                var dialogCount = 0;
                var cancelDownload = false;
                var replacementChoice="cancel";var replacementPrompts=0;
                string? copiedDiagnostics=null,openedLogFolder=null,openedGuide=null;
                bridge = new WebViewHostBridge(core, window, new StartupLog(Path.Combine(testRoot, "Logs")), paths, session)
                {
                    SaveDialogOverride = (operation, name) => { dialogCount++; chosen = Path.Combine(testRoot, "native-export" + operation.Extension); return cancelDownload ? null : chosen; },
                    CopyDiagnosticsOverride = value=>copiedDiagnostics=value,
                    OpenLogFolderOverride = value=>openedLogFolder=value,
                    OpenUserGuideOverride = value=>openedGuide=value,
                    ConfirmReplacementOverride = name=>{replacementPrompts++;return replacementChoice;}
                };
                window.Closed += (_,_)=>bridge.Dispose();
                bridge.Ready += () => ready.TrySetResult();
                bridge.FatalError += error => ready.TrySetException(new Exception(error));
                if(args.Contains("--backend-probe"))
                {
                    core.Navigate(HostOrigins.App+"/candidate/probe.html");
                    string result="";
                    await Until(async()=>{result=JsonSerializer.Deserialize<string>(await core.ExecuteScriptAsync("document.body.innerText"))??"";return result.StartsWith("PASS:")||result.StartsWith("BLOCKED:");});
                    await File.WriteAllTextAsync(Path.Combine(testRoot,"backend-probe.txt"),result);
                    Console.WriteLine(result);Console.WriteLine("Backend probe evidence: "+testRoot);
                    if(!result.StartsWith("PASS:")) throw new Exception("Backend probe failed: "+result);
                    await session.CompleteAsync();return;
                }
                if(args.Contains("--startup"))
                {
                    core.AddWebResourceRequestedFilter(HostOrigins.App+"/js/main.js",CoreWebView2WebResourceContext.All);
                    core.WebResourceRequested += async (_,e)=>{if(e.Request.Uri==HostOrigins.App+"/js/main.js"){using var delay=e.GetDeferral();await Task.Delay(900);}};
                    await core.AddScriptToExecuteOnDocumentCreatedAsync("window.startupFrames={waiting:0,leaks:0};function observeStartup(){const b=document.body,h=document.querySelector('body>header');if(b&&!document.documentElement.hasAttribute('data-meshme-ready')){startupFrames.waiting++;if(getComputedStyle(b).visibility!=='hidden')startupFrames.leaks++;}if(h&&getComputedStyle(h).display!=='none'&&getComputedStyle(h).visibility!=='hidden')startupFrames.leaks++;requestAnimationFrame(observeStartup);}requestAnimationFrame(observeStartup);");
                }
                core.Navigate(HostOrigins.App + "/index.html");
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(60));
                if(args.Contains("--hidden-startup"))web.Visibility=Visibility.Visible;
                Console.WriteLine("PASS: actual WebView2 startup handshake");
                var fixture = Path.Combine(root, "tests", "fixtures", "cube_binary.stl");
                await bridge.OpenFileAsync(fixture);
                var recent = new RecentFilesService(paths);
                await Until(async () => (await recent.ListAsync()).Any(e => e.Path == fixture));
                Console.WriteLine("PASS: native file activation → internal binary GET → preserved engine loader → recent file acknowledgement");
                async Task<JsonElement> Evaluate(string expression, int timeoutSeconds = 30)
                {
                    var raw = await core.CallDevToolsProtocolMethodAsync("Runtime.evaluate", JsonSerializer.Serialize(new { expression, awaitPromise = true, returnByValue = true, userGesture = true })).WaitAsync(TimeSpan.FromSeconds(timeoutSeconds));
                    using var parsed = JsonDocument.Parse(raw);
                    if (parsed.RootElement.TryGetProperty("exceptionDetails", out var error)) throw new Exception(error.ToString());
                    return parsed.RootElement.GetProperty("result").GetProperty("value").Clone();
                }
                async Task ReopenNativeProject(string projectPath)
                {
                    var reopenRevision=(await Evaluate("window.meshMe360Locked.scene.document.revision")).GetInt32();
                    await bridge.OpenFileAsync(projectPath);
                    await Until(async()=> (await Evaluate($"(()=>{{const d=window.meshMe360Locked.scene.document;return d.revision!=={reopenRevision}&&!d.dirty&&d.state.name==={JsonSerializer.Serialize(Path.GetFileName(projectPath))}}})()")).GetBoolean());
                }
                if(args.Contains("--owner")){await OwnerUxChecks.Run(expression=>Evaluate(expression),testRoot);await session.CompleteAsync();return;}
                if(args.Contains("--stress"))
                {
                    var reports=new List<JsonElement>();
                    foreach(var segments in new[]{90,288,448})
                    {
                        var triangles=12*segments*segments;
                        Console.WriteLine($"Stress: creating and importing {triangles:N0} triangles");
                        await Evaluate($$"""
                        (async()=>{const T=await import('./vendor/three-0.170.0/build/three.module.js'),{exportSTL}=await import('./js/exporter.js');const indexed=new T.BoxGeometry(20,20,20,{{segments}},{{segments}},{{segments}}),g=indexed.toNonIndexed();const bytes=exportSTL(g,null);indexed.dispose();g.dispose();await window.meshMe360.download(new Blob([bytes]),'stress.stl');return true})()
                        """,180);
                        await Until(()=>Task.FromResult(File.Exists(chosen)&&new FileInfo(chosen).Length==84L+50L*triangles));
                        await Evaluate("(async()=>{const a=window.meshMe360Locked;a.scene.document.markSaved();await a.registry.execute('New Project');return true})()");
                        var timer=System.Diagnostics.Stopwatch.StartNew();
                        await bridge.OpenFileAsync(chosen);
                        await Until(async()=>(await Evaluate($"window.meshMe360Locked.scene.document.state.objects.some(o=>o.positions.length/9==={triangles})",180)).GetBoolean());
                        var loadMs=timer.Elapsed.TotalMilliseconds;
                        Console.WriteLine($"Stress: imported in {loadMs:F0} ms; exercising worker pipeline");
                        var report=await Evaluate($$"""
                        (async()=>{
                          const a=window.meshMe360Locked,e=await import('./js/main.js'),{startDisplacement}=await import('./app/displacement-client.js'),T=await import('./vendor/three-0.170.0/build/three.module.js'),{exportSTL}=await import('./js/exporter.js');
                          const d=a.scene.document,revision=d.revision,source=d.state.objects.at(-1).positions;
                          const input=e.desktopDisplacementInput();input.mode='export';input.settings={...input.settings,refineLength:1,amplitude:.1,textureHeight:.1,maxTriangles:Math.floor({{triangles}}*.8),regularizeEnabled:false,smoothBottom:false};
                          input.imageData={data:new Uint8ClampedArray([255,255,255,255])};input.imgWidth=1;input.imgHeight=1;
                          const viewer=await import('./js/viewer.js'),camera=viewer.getCamera(),startPosition=camera.position.clone(),frameBefore=viewer.getRenderer().info.render.frame;
                          let beats=0,last=performance.now(),maxGap=0,peakHeap=0;const heartbeat=setInterval(()=>{const now=performance.now();maxGap=Math.max(maxGap,now-last);last=now;beats++;peakHeap=Math.max(peakHeap,performance.memory?.usedJSHeapSize??0);camera.position.applyAxisAngle(new T.Vector3(0,0,1),.002);viewer.getControls().update();viewer.requestRender()},16);
                          const events=[],started=performance.now();let output;
                          try{output=await startDisplacement(input,p=>events.push({stage:p.stage,p:p.p,ms:performance.now()-started})).promise}finally{clearInterval(heartbeat);camera.position.copy(startPosition);viewer.getControls().update();viewer.requestRender()}
                          const pipelineMs=performance.now()-started;
                          if(!output.positions.every(Number.isFinite)||!output.positions.length||output.positions.length%9)throw Error('Invalid stress output');
                          if(source!==d.state.objects.at(-1).positions||revision!==d.revision)throw Error('Worker changed authored geometry');
                          const g=new T.BufferGeometry();g.setAttribute('position',new T.BufferAttribute(output.positions,3));g.setAttribute('normal',new T.BufferAttribute(output.normals,3));g.computeBoundingBox();
                          const exportStart=performance.now(),bytes=exportSTL(g,null),exportMs=performance.now()-exportStart,bounds={min:g.boundingBox.min.toArray(),max:g.boundingBox.max.toArray()};g.dispose();
                          if(bytes.byteLength!==84+50*output.positions.length/9||beats===0||!events.some(p=>p.stage==='decimate'))throw Error('Stress export/worker responsiveness/decimation failed');
                          const renderedFrames=viewer.getRenderer().info.render.frame-frameBefore,baselineStart=performance.now();
                          const baseline=await new Promise((resolve,reject)=>{const w=new Worker('./js/exportWorker.js',{type:'module'});w.onmessage=({data})=>{if(data.type==='ready')w.postMessage({cmd:'run',input});if(data.type==='done'){w.terminate();resolve(data.result)}if(data.type==='error'){w.terminate();reject(Error(data.message))} };w.onerror=error=>{w.terminate();reject(Error(error.message))} });
                          const baselineMs=performance.now()-baselineStart;
                          if(renderedFrames<1||baseline.positions.length!==output.positions.length||!baseline.positions.every((v,i)=>v===output.positions[i])||!baseline.normals.every((v,i)=>v===output.normals[i]))throw Error('Stress rendering or pinned-worker parity failed');
                          return{triangles:{{triangles}},loadMs:{{loadMs.ToString(System.Globalization.CultureInfo.InvariantCulture)}},pipelineMs,baselineMs,parity:true,exportMs,exportBytes:bytes.byteLength,resultTriangles:output.positions.length/9,bounds,heartbeatCount:beats,maxHeartbeatGapMs:maxGap,renderedFrames,events,peakObservedMainHeapBytes:peakHeap,heapBytes:performance.memory?.usedJSHeapSize??null};
                        })()
                        """,600);
                        reports.Add(report);
                        await File.WriteAllTextAsync(Path.Combine(testRoot,"stress-results.json"),JsonSerializer.Serialize(reports));
                        Console.WriteLine($"PASS: {triangles:N0} input triangles; finite worker output, unchanged source, responsive event loop and binary export");
                    }
                    Console.WriteLine("Stress evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--recovery-compat"))
                {
                    var recoveryCheck=await Evaluate("(async()=>{const a=window.meshMe360Locked,r=await import('./app/recovery-snapshot.js');a.scene.capture();const d=a.scene.document,before=JSON.stringify(d.state),saved=d.savedRevision,bytes=r.createRecoverySnapshot(d),originalSaveUntouched=d.savedRevision===saved;d.duplicate();r.restoreRecoverySnapshot(d,bytes);a.scene.refreshScene({activate:true});const e=await import('./js/main.js'),live=e.desktopSceneCapture();const match=JSON.stringify(d.state)===before&&d.dirty&&!!live.texture&&live.geometry.attributes.position.count===36;live.geometry.dispose();return{match,bytes:bytes.length,originalSaveUntouched}})()");
                    if(!recoveryCheck.GetProperty("match").GetBoolean()||!recoveryCheck.GetProperty("originalSaveUntouched").GetBoolean())throw new Exception("Recovered scene did not restore the renderer/texture and dirty state");
                    await File.WriteAllTextAsync(Path.Combine(testRoot,"recovery-format-check.json"),recoveryCheck.GetRawText());
                    Console.WriteLine("PASS: current unsaved mm360 snapshot restores real renderer/source state and remains dirty; M6 crash scheduling/retention is not exercised");Console.WriteLine("Recovery compatibility evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--startup"))
                {
                    var result=await Evaluate("({waiting:startupFrames.waiting,leaks:startupFrames.leaks,ready:document.documentElement.getAttribute('data-meshme-ready'),visible:getComputedStyle(document.body).visibility,title:document.title,model:window.meshMe360Locked.scene.document.selected.length})");
                    if(result.GetProperty("waiting").GetInt32()<2||result.GetProperty("leaks").GetInt32()!=0||result.GetProperty("ready").GetString()!="true"||result.GetProperty("visible").GetString()!="visible"||result.GetProperty("model").GetInt32()!=1)throw new Exception("Startup flash regression: "+result);
                    await File.WriteAllTextAsync(Path.Combine(testRoot,"startup-frames.json"),result.GetRawText());
                    Console.WriteLine("PASS: delayed engine startup never paints legacy UI; completed shell and model are visible. "+result);await session.CompleteAsync();return;
                }
                if(args.Contains("--guide"))
                {
                    await Evaluate("(async()=>{await window.meshMe360Locked.registry.execute('User Guide');return true})()");
                    var expected=Path.Combine(AppContext.BaseDirectory,"Documentation","MeshMe360-User-Guide.pdf");
                    if(openedGuide!=expected || !File.ReadAllText(expected).StartsWith("%PDF-"))throw new Exception("User Guide did not route to the packaged PDF");
                    await bridge.HandleAsync(BridgeMessage.Create("documentation.openUserGuide",Guid.NewGuid().ToString(),new {path="C:/Windows/System32/calc.exe"}));
                    if(openedGuide!=expected)throw new Exception("Web payload changed guide path");
                    Console.WriteLine("PASS: Help User Guide opens the shipped PDF; arbitrary payload path ignored");await session.CompleteAsync();return;
                }
                if(args.Contains("--about"))
                {
                    foreach(var theme in new[]{"Dark","Light"}){
                        await Evaluate($"(async()=>{{await window.meshMe360.updateSettings({{theme:'{theme}'}});await window.meshMe360Locked.registry.execute('About Mesh Me360');return true}})()");
                        var about=await Evaluate("(()=>{const d=document.querySelector('.about-dialog'),r=d.getBoundingClientRect(),b=d.querySelector('.about-banner').getBoundingClientRect(),f=d.querySelector('.about-footer').getBoundingClientRect();return{ok:!d.hidden&&r.width<=686&&r.height<=510&&b.height===220&&f.bottom<=r.bottom&&d.textContent.includes('Computer Universal Technology Systems')&&d.textContent.includes('Stefan Hermann')&&d.textContent.includes('Version 0.1.'),width:r.width,height:r.height}})()");
                        if(!about.GetProperty("ok").GetBoolean())throw new Exception("About layout/content failed: "+about);
                        var raw=await core.CallDevToolsProtocolMethodAsync("Page.captureScreenshot","{\"format\":\"png\"}");using var screen=JsonDocument.Parse(raw);await File.WriteAllBytesAsync(Path.Combine(testRoot,$"about-{theme}.png"),Convert.FromBase64String(screen.RootElement.GetProperty("data").GetString()!));
                        await Evaluate("(()=>{document.querySelector('.about-footer button').click();return true})()");
                        if(!(await Evaluate("document.querySelector('.locked-dialog').hidden&&window.meshMe360Locked.scene.document.selected.length===1")).GetBoolean())throw new Exception("About Close changed selection or failed");
                    }
                    Console.WriteLine("PASS: compact About banner, live version, required credits, both themes and Close preserve scene state");Console.WriteLine("About evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--dropdown"))
                {
                    foreach(var theme in new[]{"Dark","Light"}){
                        await Evaluate($"(async()=>{{await window.meshMe360.updateSettings({{theme:'{theme}'}});return true}})()");
                        foreach(var name in new[]{"File","Edit","Mesh","Texture","Modify","View","Tools","Help","Export"}){
                            await Evaluate($"(()=>{{window.meshMe360Locked.registry.execute('menu:{name}');return true}})()");
                            var state=await Evaluate("(()=>{const d=document.querySelector('.command-menu'),r=d.getBoundingClientRect(),buttons=[...d.querySelectorAll('[role=menuitem]')];return{ok:!d.hidden&&d.getAttribute('role')==='menu'&&getComputedStyle(d.querySelector('.close')).display==='none'&&r.width<320&&r.height<innerHeight&&r.right<=innerWidth&&r.bottom<=innerHeight&&buttons.length>0&&buttons.every((b,i)=>!i||b.getBoundingClientRect().top>=buttons[i-1].getBoundingClientRect().bottom),width:r.width,height:r.height}})()");
                            if(!state.GetProperty("ok").GetBoolean())throw new Exception("Dropdown layout failed: "+name+state);
                            if(name is "File" or "View"){var raw=await core.CallDevToolsProtocolMethodAsync("Page.captureScreenshot","{\"format\":\"png\"}");using var screen=JsonDocument.Parse(raw);await File.WriteAllBytesAsync(Path.Combine(testRoot,$"menu-{theme}-{name}.png"),Convert.FromBase64String(screen.RootElement.GetProperty("data").GetString()!));}
                        }
                    }
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('menu:Edit');return true})()");
                    await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyDown\",\"key\":\"ArrowDown\",\"code\":\"ArrowDown\",\"windowsVirtualKeyCode\":40}");
                    if(!(await Evaluate("document.activeElement.getAttribute('role')==='menuitem'&&!document.activeElement.disabled")).GetBoolean())throw new Exception("Menu keyboard focus failed");
                    await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyDown\",\"key\":\"Escape\",\"code\":\"Escape\",\"windowsVirtualKeyCode\":27}");
                    if(!(await Evaluate("document.querySelector('.locked-dialog').hidden&&document.activeElement.dataset.command==='menu:Edit'&&window.meshMe360Locked.scene.document.selected.length===1")).GetBoolean())throw new Exception("Menu Escape did not restore trigger focus and preserve selection");
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('menu:Edit');[...document.querySelectorAll('[role=menuitem]')].find(b=>b.textContent==='Duplicate Selected').click();return true})()");
                    if(!(await Evaluate("document.querySelector('.locked-dialog').hidden&&window.meshMe360Locked.scene.document.state.objects.length===2")).GetBoolean())throw new Exception("Dropdown command did not execute or dismiss");
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('menu:File');[...document.querySelectorAll('[role=menuitem]')].find(b=>b.textContent==='Project Settings').click();return true})()");
                    if(!(await Evaluate("!document.querySelector('.locked-dialog').hidden&&!document.querySelector('.locked-dialog').classList.contains('command-menu')&&!!document.querySelector('[aria-label=\"Project display units\"]')")).GetBoolean())throw new Exception("Menu command did not open its real settings dialog");
                    Console.WriteLine("PASS: all compact dropdown layouts in both themes, keyboard/Escape focus, real command execution and tool-dialog handoff");Console.WriteLine("Menu evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--display"))
                {
                    var displayReports=new List<JsonElement>();
                    foreach(var theme in new[]{"Dark","Light"}){
                        await Evaluate($"(async()=>{{await window.meshMe360.updateSettings({{theme:'{theme}'}});return true}})()");
                        foreach(var (width,height) in new[]{(1280,720),(1366,768),(1536,1024),(1600,900),(1920,1080)})foreach(var scale in new[]{1.0,1.25,1.5,1.75,2.0}){
                            await core.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride",JsonSerializer.Serialize(new{width,height,deviceScaleFactor=scale,mobile=false}));
                            var layout=await Evaluate("(async()=>{await new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)));const selectors=['.locked-toolbar button','.locked-quick button','.locked-tabs button','.locked-menu .menus button','.export-group button'];const failures=[];for(const selector of selectors)for(const b of document.querySelectorAll(selector)){const r=b.getBoundingClientRect();if(!r.width||r.x<0||r.y<0||r.right>innerWidth+1||r.bottom>innerHeight+1)failures.push(b.textContent+' outside client');const span=b.querySelector('span')??b,range=document.createRange();range.selectNodeContents(span);const text=range.getBoundingClientRect();if(text.width>r.width+1)failures.push(b.textContent+' clipped label');}return{width:innerWidth,height:innerHeight,scale:devicePixelRatio,theme:document.documentElement.dataset.theme,failures,viewport:document.querySelector('.locked-viewport').getBoundingClientRect().toJSON()}})()");
                            displayReports.Add(layout);if(layout.GetProperty("failures").GetArrayLength()>0)throw new Exception("Display gate failed: "+layout);
                            if(scale==1.0||width==1536&&scale==2.0){var raw=await core.CallDevToolsProtocolMethodAsync("Page.captureScreenshot","{\"format\":\"png\",\"fromSurface\":true}");using var screen=JsonDocument.Parse(raw);await File.WriteAllBytesAsync(Path.Combine(testRoot,$"display-{theme}-{width}x{height}-{scale}.png"),Convert.FromBase64String(screen.RootElement.GetProperty("data").GetString()!));}
                        }
                    }
                    await File.WriteAllTextAsync(Path.Combine(testRoot,"display-layout.json"),JsonSerializer.Serialize(displayReports));
                    Console.WriteLine("PASS: five required client sizes, both themes and 100/125/150/175/200% emulated pixel density without clipped toolbar/menu/quick-action labels");Console.WriteLine("Display evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--legacy"))
                {
                    var legacyPath=Path.Combine(testRoot,"legacy-fixture.bumpmesh");
                    using(var legacyZip=ZipFile.Open(legacyPath,ZipArchiveMode.Create)){
                        legacyZip.CreateEntryFromFile(fixture,"model.stl");
                        using var settingsWriter=new StreamWriter(legacyZip.CreateEntry("settings.json").Open());
                        await settingsWriter.WriteAsync("{\"version\":1,\"textureHeight\":1.25,\"scaleU\":3,\"scaleV\":4,\"activeMapName\":\"Bricks\"}");
                    }
                    var legacyPrior=(await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString();
                    await bridge.OpenFileAsync(legacyPath);
                    await Until(async()=> (await Evaluate("!document.querySelector('.locked-dialog').hidden&&document.querySelector('.locked-dialog h2').textContent==='Open legacy project'")).GetBoolean());
                    await Evaluate("(()=>{document.querySelector('.locked-dialog .close').click();return true})()");
                    await Until(async()=>!(await Evaluate("window.meshMe360Locked.registry.busy")).GetBoolean());
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=legacyPrior)throw new Exception("Cancelled legacy import changed scene");
                    await bridge.OpenFileAsync(legacyPath);
                    await Until(async()=> (await Evaluate("!document.querySelector('.locked-dialog').hidden&&document.querySelector('.locked-dialog h2').textContent==='Open legacy project'")).GetBoolean());
                    await Evaluate("(()=>{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Settings only').click();return true})()");
                    await Until(async()=>!(await Evaluate("window.meshMe360Locked.registry.busy")).GetBoolean());
                    if(!(await Evaluate("(()=>{const d=window.meshMe360Locked.scene.document;return d.selected[0].engineSettings.textureHeight===1.25&&d.state.objects.length===1&&d.selected[0].sourceFormat==='stl'})()")).GetBoolean())throw new Exception("Legacy settings-only failed");
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Undo');return true})()");
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=legacyPrior)throw new Exception("Legacy settings Undo mismatch");
                    replacementChoice="discard";await bridge.OpenFileAsync(legacyPath);
                    await Until(async()=> (await Evaluate("!document.querySelector('.locked-dialog').hidden&&document.querySelector('.locked-dialog h2').textContent==='Open legacy project'")).GetBoolean());
                    await Evaluate("(()=>{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Load model and settings').click();return true})()");
                    await Until(async()=> (await Evaluate("!window.meshMe360Locked.registry.busy&&window.meshMe360Locked.scene.document.state.name==='legacy-fixture.mm360'")).GetBoolean());
                    if(!(await Evaluate("(()=>{const d=window.meshMe360Locked.scene.document;return d.state.name==='legacy-fixture.mm360'&&d.dirty&&d.state.objects.length===1&&d.selected[0].sourceFormat==='bumpmesh'&&d.selected[0].engineSettings.textureHeight===1.25&&!!d.selected[0].textureId})()")).GetBoolean())throw new Exception("Legacy full conversion failed");
                    await Evaluate("(async()=>{await window.meshMe360Locked.registry.execute('Save');return true})()");await ReopenNativeProject(chosen);
                    if(!(await Evaluate("window.meshMe360Locked.scene.document.selected[0].engineSettings.textureHeight===1.25&&!window.meshMe360Locked.scene.document.dirty")).GetBoolean())throw new Exception("Converted legacy project reopen failed");
                    Console.WriteLine("PASS: native legacy chooser cancellation, settings-only transaction/Undo, full model/settings conversion and mm360 native round trip");Console.WriteLine("Legacy evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--advanced"))
                {
                    await Evaluate("(()=>{const a=window.meshMe360Locked;a.scene.document.markSaved();a.registry.execute('legacy');const input=document.getElementById('texture-smoothing-val');input.scrollIntoView();input.focus();input.select();return true})()");
                    await core.CallDevToolsProtocolMethodAsync("Input.insertText","{\"text\":\"3\"}");
                    await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyDown\",\"key\":\"Tab\",\"code\":\"Tab\",\"windowsVirtualKeyCode\":9}");
                    await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyUp\",\"key\":\"Tab\",\"code\":\"Tab\",\"windowsVirtualKeyCode\":9}");
                    await Until(async()=> (await Evaluate("window.meshMe360Locked.scene.document.selected[0].engineSettings.textureSmoothing===3&&window.meshMe360Locked.scene.document.dirty")).GetBoolean());
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Undo');return true})()");
                    if(!(await Evaluate("document.getElementById('texture-smoothing-val').value==='0'&&!window.meshMe360Locked.scene.document.dirty")).GetBoolean())throw new Exception("Advanced setting Undo mismatch");
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Redo');document.getElementById('excl-mode-exclude').click();return true})()");
                    var selectedId=(await Evaluate("window.meshMe360Locked.scene.document.selected[0].id")).GetString();
                    var point=await Evaluate("(()=>{const r=document.querySelector('.locked-viewport canvas').getBoundingClientRect();return{x:r.x+r.width*.5,y:r.y+r.height*.5}})()");
                    foreach(var type in new[]{"mouseMoved","mousePressed","mouseReleased"})await core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new{type,x=point.GetProperty("x").GetDouble(),y=point.GetProperty("y").GetDouble(),button=type=="mouseMoved"?"none":"left",clickCount=1}));
                    await Until(async()=> (await Evaluate("(window.meshMe360Locked.scene.document.selected[0].engineMask?.excluded.length??0)>0")).GetBoolean());
                    var painted=(await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.selected[0].engineMask)")).GetString();
                    if((await Evaluate("window.meshMe360Locked.scene.document.selected[0].id")).GetString()!=selectedId)throw new Exception("Painting changed object selection");
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Undo');return true})()");
                    if(!(await Evaluate("(async()=>{const e=await import('./js/main.js'),s=e.desktopSceneCapture();s.geometry.dispose();return !s.mask?.excluded.length})()")).GetBoolean())throw new Exception("Mask Undo retained painted engine faces");
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Redo');return true})()");
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.selected[0].engineMask)")).GetString()!=painted)throw new Exception("Mask Redo mismatch");
                    await Evaluate("(async()=>{document.querySelector('.locked-dialog .close').click();await window.meshMe360Locked.registry.execute('Save');return true})()");await ReopenNativeProject(chosen);
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.selected[0].engineMask)")).GetString()!=painted||!(await Evaluate("window.meshMe360Locked.scene.document.selected[0].engineSettings.textureSmoothing===3&&!window.meshMe360Locked.scene.document.dirty")).GetBoolean())throw new Exception("Advanced settings/mask native round trip mismatch");
                    var orientationBefore=(await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString();
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('legacy');document.getElementById('rotate-btn').click();document.getElementById('rotate-x').value='30';document.getElementById('rotate-apply-btn').click();return true})()");
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()==orientationBefore)throw new Exception("Legacy orientation did not commit scene geometry");
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Undo');return true})()");
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=orientationBefore)throw new Exception("Legacy orientation Undo mismatch");
                    var bakePoint=await Evaluate("(()=>{if(document.getElementById('advanced-section').classList.contains('collapsed'))document.getElementById('advanced-toggle').click();const b=document.getElementById('bake-mask-chk');b.scrollIntoView();const r=b.getBoundingClientRect();return{x:r.x+r.width/2,y:r.y+r.height/2}})()");
                    foreach(var type in new[]{"mousePressed","mouseReleased"})await core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new{type,x=bakePoint.GetProperty("x").GetDouble(),y=bakePoint.GetProperty("y").GetDouble(),button="left",clickCount=1}));
                    if((await Evaluate("window.meshMe360Locked.scene.document.selected[0].engineSettings.desktopBakeMask")).GetBoolean())throw new Exception("Native baked-mask option was not recorded");
                    await Evaluate("(async()=>{document.querySelector('.locked-dialog .close').click();await window.meshMe360Locked.registry.execute('Save');return true})()");await ReopenNativeProject(chosen);
                    if((await Evaluate("document.getElementById('bake-mask-chk').checked")).GetBoolean())throw new Exception("Baked-mask option project restore failed");
                    Console.WriteLine("PASS: advanced texture setting native keyboard Undo/Redo, viewport bucket painting without reselection, mask Undo/Redo and native settings/mask round trip");Console.WriteLine("Advanced evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--lifecycle"))
                {
                    var original=(await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString();
                    await Evaluate("(async()=>{await window.meshMe360Locked.registry.execute('New Project');return true})()");
                    if(replacementPrompts!=1||(await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=original)throw new Exception("Cancelled New lost document");
                    replacementChoice="save";cancelDownload=true;
                    if((await Evaluate("(async()=>{return await window.meshMe360Locked.registry.execute('Save')})()")).GetBoolean())throw new Exception("Cancelled save reported success");
                    if(!(await Evaluate("window.meshMe360Locked.scene.document.dirty&&!window.meshMe360Locked.registry.busy")).GetBoolean())throw new Exception("Cancelled save cleared dirty or left busy");
                    await Evaluate("(async()=>{await window.meshMe360Locked.registry.execute('New Project');return true})()");
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=original)throw new Exception("Cancelled Save-and-New lost document");
                    cancelDownload=false;
                    await Evaluate("(async()=>{await window.meshMe360Locked.registry.execute('New Project');return true})()");
                    if(!File.Exists(chosen)||!(await Evaluate("window.meshMe360Locked.scene.document.state.objects.length===0&&!window.meshMe360Locked.scene.document.dirty")).GetBoolean())throw new Exception("Save-and-New failed");
                    await ReopenNativeProject(chosen);
                    await Evaluate("(()=>{const a=window.meshMe360Locked;a.scene.document.transform({position:[12,0,0]});a.scene.refreshScene();return true})()");
                    var edited=(await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString();
                    replacementChoice="cancel";
                    await bridge.OpenFileAsync(chosen);
                    await Until(()=>Task.FromResult(replacementPrompts==4));
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=edited)throw new Exception("Cancelled Open changed document");
                    replacementChoice="discard";
                    await Evaluate("(async()=>{await window.meshMe360Locked.registry.execute('New Project');return true})()");
                    if(!(await Evaluate("window.meshMe360Locked.scene.document.state.objects.length===0&&!window.meshMe360Locked.scene.document.dirty")).GetBoolean())throw new Exception("Don't Save did not create clean empty project");
                    await ReopenNativeProject(chosen);
                    await Evaluate("(()=>{const a=window.meshMe360Locked;a.scene.document.transform({position:[12,0,0]});a.scene.refreshScene();return true})()");
                    replacementChoice="cancel";
                    var beforeClose=replacementPrompts;
                    await Evaluate("(async()=>{await window.meshMe360Locked.registry.execute('Exit');return true})()");
                    await Until(()=>Task.FromResult(replacementPrompts>beforeClose));
                    await Task.Delay(100);
                    if(!window.IsVisible)throw new Exception("Cancelled Exit closed window");
                    replacementChoice="save";cancelDownload=true;beforeClose=replacementPrompts;window.Close();
                    await Until(()=>Task.FromResult(replacementPrompts>beforeClose));
                    await Until(async()=>!(await Evaluate("window.meshMe360Locked.registry.busy")).GetBoolean());
                    if(!window.IsVisible||!(await Evaluate("window.meshMe360Locked.scene.document.dirty")).GetBoolean())throw new Exception("Cancelled native close save lost document");
                    cancelDownload=false;window.Close();
                    await Until(()=>Task.FromResult(!window.IsVisible));
                    using(var closedProject=ZipFile.OpenRead(chosen))if(closedProject.GetEntry("project.json") is null)throw new Exception("Exit did not write a valid project");
                    Console.WriteLine("PASS: native Save/Don't Save/Cancel routing, cancelled save acknowledgement, Save-and-New, cancelled Open/Exit and window-close save completion");Console.WriteLine("Lifecycle evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--mesh-processing"))
                {
                    foreach(var command in new[]{"Subdivide/Refine","Simplify/Decimate","Recalculate Normals"})
                    {
                        if(command=="Recalculate Normals")await Evaluate("(()=>{const a=window.meshMe360Locked;a.scene.document.transact('Fixture normals',()=>a.scene.document.selected[0].normals.fill(0));a.scene.refreshScene({activate:true});return true})()");
                        var meshBefore=(await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString();
                        await Evaluate($"(()=>{{window.meshMe360Locked.registry.execute({JsonSerializer.Serialize(command)});const allow=document.querySelector('[aria-label=\"Allow refinement growth\"]');if(allow)allow.checked=true;document.querySelector('[aria-label=\"Allow relief refinement\"]')?.click();[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Preview').click();return true}})()");
                        await Until(async()=> (await Evaluate("document.querySelector('[data-mesh-status]').dataset.meshStatus")).GetString() is "ready" or "error");
                        if((await Evaluate("document.querySelector('[data-mesh-status]').dataset.meshStatus")).GetString()!="ready")throw new Exception((await Evaluate("document.querySelector('.locked-dialog').innerText")).GetString());
                        if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=meshBefore)throw new Exception(command+" preview mutated document");
                        await Evaluate("(()=>{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Commit').click();return true})()");
                        if(!(await Evaluate("(async()=>{const a=window.meshMe360Locked,r=await(await import('./app/print-client.js')).startPrintAnalysis([a.scene.document.selected[0]]).promise;return r[0].solid&&r[0].open===0&&r[0].nonManifold===0&&r[0].degenerate===0&&a.scene.document.selected[0].normals.some(n=>Math.abs(n)>.5)})()")).GetBoolean())throw new Exception(command+" failed closed/normal qualification");
                        await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Undo');return true})()");
                        if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=meshBefore)throw new Exception(command+" Undo mismatch");
                        await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Redo');return true})()");
                    }
                    var meshNormals=(await Evaluate("JSON.stringify([...window.meshMe360Locked.scene.document.selected[0].normals])")).GetString();
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Save');return true})()");await Until(()=>Task.FromResult(File.Exists(chosen)&&chosen.EndsWith(".mm360")));await Until(async()=>!(await Evaluate("window.meshMe360Locked.scene.document.dirty")).GetBoolean());await ReopenNativeProject(chosen);
                    if((await Evaluate("JSON.stringify([...window.meshMe360Locked.scene.document.selected[0].normals])")).GetString()!=meshNormals)throw new Exception("Mesh normals project round trip failed");
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Subdivide/Refine');document.querySelector('[aria-label=\"Allow refinement growth\"]').checked=true;document.querySelector('[aria-label=\"Allow relief refinement\"]')?.click();[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Preview').click();document.querySelector('.locked-dialog .close').click();return true})()");
                    if((await Evaluate("window.meshMe360Locked.registry.busy")).GetBoolean())throw new Exception("Mesh processing cancel left commands busy");
                    Console.WriteLine("PASS: actual mesh Refine/Simplify/Normals preview, commit, closed result diagnostics, Undo/Redo, cancellation and native normal-asset round trip");Console.WriteLine("Mesh processing evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--app-diagnostics"))
                {
                    await Evaluate("(async()=>{await window.meshMe360Locked.registry.execute('Application Diagnostics');return true})()");
                    var diagnosticJson=(await Evaluate("document.querySelector('[data-app-diagnostics]').textContent")).GetString()!;
                    using var diagnosticReport=JsonDocument.Parse(diagnosticJson);
                    foreach(var field in new[]{"version","build","windows","dotnet","webView2","webGLVendor","webGLRenderer","activeModelFormat","cameraProjection","logFolder"})
                        if(string.IsNullOrEmpty(diagnosticReport.RootElement.GetProperty(field).GetString()))throw new Exception("Missing application diagnostics field: "+field);
                    if(diagnosticReport.RootElement.GetProperty("triangles").GetInt32()!=12||diagnosticReport.RootElement.GetProperty("dimensionsMm").EnumerateArray().Any(v=>v.GetDouble()!=20)||diagnosticJson.Contains("positions")||diagnosticJson.Contains("bytes"))throw new Exception("Diagnostics contain incorrect statistics or mesh payload");
                    await Evaluate("(()=>{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Copy Diagnostics').click();return true})()");
                    await Until(()=>Task.FromResult(copiedDiagnostics is not null));
                    if(copiedDiagnostics!=diagnosticJson)throw new Exception("Copy Diagnostics did not send displayed report");
                    await Evaluate("(()=>{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Open Log Folder').click();return true})()");
                    await Until(()=>Task.FromResult(openedLogFolder is not null));
                    if(openedLogFolder!=Path.Combine(testRoot,"Logs"))throw new Exception("Open Log Folder used an arbitrary/non-host path");
                    Console.WriteLine("PASS: actual native/runtime/WebGL application diagnostics, exact model summary and copy/log-folder host routes (clipboard/Explorer decisions substituted)");
                    Console.WriteLine("App diagnostics evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--empty"))
                {
                    var emptyObjectId=(await Evaluate("window.meshMe360Locked.scene.document.selected[0].id")).GetString();
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Project Settings');return true})()");
                    await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyDown\",\"key\":\"Escape\",\"code\":\"Escape\",\"windowsVirtualKeyCode\":27}");
                    await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyUp\",\"key\":\"Escape\",\"code\":\"Escape\",\"windowsVirtualKeyCode\":27}");
                    if((await Evaluate("window.meshMe360Locked.scene.document.selected[0]?.id??''")).GetString()!=emptyObjectId)throw new Exception("Closing a dialog unexpectedly cleared object selection");
                    await Evaluate("(()=>{window.meshMe360Locked.scene.document.markSaved();window.meshMe360Locked.registry.execute('New Project');return true})()");
                    if(!(await Evaluate("(async()=>{const a=window.meshMe360Locked,v=await import('./js/viewer.js');return a.scene.document.state.objects.length===0&&!a.scene.document.dirty&&!v.getCurrentMesh().visible&&!a.registry.state('Comparison').enabled&&!a.registry.state('Fit').enabled&&document.getElementById('amplitude-val').disabled&&document.querySelector('.locked-stats').innerText.includes('0')})()")).GetBoolean())throw new Exception("New Project exposed the engine's retained old mesh or editable controls");
                    await bridge.OpenFileAsync(fixture);
                    await Until(async()=> (await Evaluate("window.meshMe360Locked.scene.document.state.objects.length")).GetInt32()==1);
                    if(!(await Evaluate("(async()=>{const v=await import('./js/viewer.js');return v.getCurrentMesh().visible&&!document.getElementById('amplitude-val').disabled})()")).GetBoolean())throw new Exception("Import after New Project failed to re-enable controls");
                    Console.WriteLine("PASS: Escape closes a dialog without clearing selection; New Project hides stale engine mesh, disables invalid controls and permits fresh import");
                    Console.WriteLine("Empty scene evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--diagnostics"))
                {
                    var diagnosticsBefore=(await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString();
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Mesh Diagnostics');[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Run diagnostics').click();return true})()");
                    await Until(async()=> (await Evaluate("document.querySelector('[data-diagnostics-status]').dataset.diagnosticsStatus")).GetString() is "ready" or "error");
                    if((await Evaluate("document.querySelector('[data-diagnostics-status]').dataset.diagnosticsStatus")).GetString()!="ready")throw new Exception((await Evaluate("document.querySelector('.locked-dialog').innerText")).GetString());
                    if(!(await Evaluate("(()=>{const a=window.meshMe360Locked,r=a.scene.diagnostics.reportFor(a.scene.document.selected[0].id);return r.solid&&r.open===0&&r.nonManifold===0&&r.degenerate===0&&Math.abs(r.wall.minimumSampleMm-20)<.001&&document.querySelector('.locked-status .issue').textContent==='No detected mesh issues'})()")).GetBoolean())throw new Exception("Cube diagnostics or status incorrect");
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=diagnosticsBefore)throw new Exception("Diagnostics changed document geometry/state");
                    await Evaluate("(()=>{document.querySelector('.locked-dialog .close').click();window.meshMe360Locked.registry.execute('tab:Properties');return true})()");
                    if(!(await Evaluate("document.querySelector('.locked-inspector-body').innerText.includes('Closed with positive volume')")).GetBoolean())throw new Exception("Properties did not show current diagnostics");
                    await Evaluate("(()=>{const a=window.meshMe360Locked;a.scene.document.transform({position:[10,0,0]});a.scene.refreshScene();return true})()");
                    if(!(await Evaluate("(()=>{const a=window.meshMe360Locked;return !a.scene.diagnostics.reportFor(a.scene.document.selected[0].id)&&document.querySelector('.locked-status .issue').textContent==='Diagnostics not run'})()")).GetBoolean())throw new Exception("Stale diagnostics survived an edit");
                    await Evaluate("(()=>{const a=window.meshMe360Locked;a.registry.execute('Mesh Diagnostics');[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Run diagnostics').click();document.querySelector('.locked-dialog .close').click();return true})()");
                    if((await Evaluate("window.meshMe360Locked.registry.busy")).GetBoolean())throw new Exception("Diagnostics cancel left app busy");
                    Console.WriteLine("PASS: actual read-only mesh diagnostics, measured cube wall, Properties/status report, edit invalidation and safe cancellation");
                    Console.WriteLine("Diagnostics evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--view"))
                {
                    if(!(await Evaluate("(async()=>{const a=window.meshMe360Locked,v=await import('./js/viewer.js');a.registry.execute('Grid');a.registry.execute('Axes');if(v.getGrid().visible||v.getDesktopAxes().visible)return false;a.scene.refreshScene({activate:true});return !v.getGrid().visible&&!v.getDesktopAxes().visible})()")).GetBoolean())throw new Exception("Guide toggles did not survive scene refresh");
                    if(!(await Evaluate("(async()=>{const a=window.meshMe360Locked,v=await import('./js/viewer.js');a.registry.execute('view:Perspective');if(!v.getCamera().isPerspectiveCamera)return false;a.registry.execute('view:Orthographic');return !!v.getCamera().isOrthographicCamera})()")).GetBoolean())throw new Exception("Projection commands did not switch cameras");
                    if(!(await Evaluate("(()=>{const r=window.meshMe360Locked.registry,v=document.querySelector('.locked-viewport'),width=v.clientWidth,height=v.clientHeight;r.execute('view:Browser panel');if(v.clientWidth<=width)return false;r.execute('view:Browser panel');r.execute('view:Right inspector panel');if(v.clientWidth<=width)return false;r.execute('view:Right inspector panel');r.execute('view:Lower library shelf');if(v.clientHeight<=height)return false;r.execute('view:Lower library shelf');return v.clientWidth===width&&v.clientHeight===height})()")).GetBoolean())throw new Exception("Panel toggles failed to reclaim viewport space or restore layout");
                    await Evaluate("(()=>{const a=window.meshMe360Locked;a.registry.execute('Duplicate Selected');a.scene.document.transform({position:[100,0,0]});a.scene.refreshScene();return true})()");
                    if(!(await Evaluate("(async()=>{const a=window.meshMe360Locked,v=await import('./js/viewer.js'),before=JSON.stringify(a.scene.document.state);a.registry.execute('Fit All');if(Math.abs(v.getControls().target.x-50)>.001)return false;a.registry.execute('Fit Selected');if(Math.abs(v.getControls().target.x-100)>.001)return false;a.registry.execute('Home view');return Math.abs(v.getControls().target.x-50)<.001&&JSON.stringify(a.scene.document.state)===before&&v.getCamera().projectionMatrix.elements.every(Number.isFinite)})()")).GetBoolean())throw new Exception("Fit selection/all did not use visible world bounds or changed document");
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Grid');window.meshMe360Locked.registry.execute('Axes');return true})()");
                    Console.WriteLine("PASS: actual guide/projection commands, panel hide/restore and selected/all world-bounds fitting without document changes");
                    foreach(var face in new[]{"Top","Bottom","Front","Back","Left","Right"}){
                        await Evaluate($"(()=>{{window.meshMe360Locked.registry.execute('view:{face}');return true}})()");
                        if(!(await Evaluate($"(()=>{{const f=document.querySelector('[data-cube-face={face}]');return getComputedStyle(f).display!=='none'&&Number(f.dataset.facing)>.99}})()")).GetBoolean())throw new Exception("Orientation cube does not reflect "+face);
                    }
                    Console.WriteLine("View evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--settings"))
                {
                    await Evaluate("(()=>{window.meshMe360Locked.scene.capture();window.meshMe360Locked.scene.document.markSaved();return true})()");
                    var settingsBefore=await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.selected[0].engineSettings)");
                    var settingsHistory=(await Evaluate("window.meshMe360Locked.scene.document.undoStack.length")).GetInt32();
                    await Evaluate("(()=>{const i=document.getElementById('amplitude-val');i.focus();i.select();return true})()");
                    await core.CallDevToolsProtocolMethodAsync("Input.insertText","{\"text\":\"1.75\"}");
                    await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyDown\",\"key\":\"Tab\",\"code\":\"Tab\",\"windowsVirtualKeyCode\":9}");
                    await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyUp\",\"key\":\"Tab\",\"code\":\"Tab\",\"windowsVirtualKeyCode\":9}");
                    if(!(await Evaluate($"(()=>{{const d=window.meshMe360Locked.scene.document;return d.dirty&&d.selected[0].engineSettings.textureHeight===1.75&&d.undoStack.length==={settingsHistory+1}}})()")).GetBoolean())throw new Exception("Actual Depth edit did not create one dirty transaction");
                    await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyDown\",\"key\":\"z\",\"code\":\"KeyZ\",\"windowsVirtualKeyCode\":90,\"modifiers\":2}");
                    await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyUp\",\"key\":\"z\",\"code\":\"KeyZ\",\"windowsVirtualKeyCode\":90,\"modifiers\":2}");
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.selected[0].engineSettings)")).GetString()!=settingsBefore.GetString()||(await Evaluate("window.meshMe360Locked.scene.document.dirty")).GetBoolean())throw new Exception("Texture settings Undo did not restore clean settings");
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Redo');return true})()");
                    if((await Evaluate("Number(document.getElementById('amplitude-val').value)")).GetDouble()!=1.75)throw new Exception("Depth redo did not update engine controls");
                    await Evaluate("(()=>{const i=document.getElementById('scale-u-val');i.focus();i.select();return true})()");
                    await core.CallDevToolsProtocolMethodAsync("Input.insertText","{\"text\":\"7\"}");
                    await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyDown\",\"key\":\"Tab\",\"code\":\"Tab\",\"windowsVirtualKeyCode\":9}");
                    await core.CallDevToolsProtocolMethodAsync("Input.dispatchKeyEvent","{\"type\":\"keyUp\",\"key\":\"Tab\",\"code\":\"Tab\",\"windowsVirtualKeyCode\":9}");
                    if((await Evaluate("window.meshMe360Locked.scene.document.selected[0].engineSettings.scaleU")).GetDouble()!=7)throw new Exception("Actual Scale edit was not recorded");
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Save');return true})()");
                    await Until(()=>Task.FromResult(File.Exists(chosen)&&chosen.EndsWith(".mm360")));
                    await Until(async()=>!(await Evaluate("window.meshMe360Locked.scene.document.dirty")).GetBoolean());
                    await ReopenNativeProject(chosen);
                    if(!(await Evaluate("(()=>{const d=window.meshMe360Locked.scene.document;return !d.dirty&&d.undoStack.length===0&&d.selected[0].engineSettings.scaleU===7&&d.selected[0].engineSettings.textureHeight===1.75})()")).GetBoolean())throw new Exception("Project restore generated synthetic history or lost texture settings");
                    Console.WriteLine("PASS: actual keyboard Depth/Scale edits create dirty undo transactions, Undo/Redo restores controls, native project restore stays clean");
                    Console.WriteLine("Settings evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--menus"))
                {
                    await Evaluate("(async()=>{await window.meshMe360Locked.registry.execute('Preferences');return true})()");
                    await Evaluate("(()=>{document.querySelector('[aria-label=\"Application theme\"]').value='Dark';[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Apply preferences').click();return true})()");
                    await Until(async()=> (await new SettingsService(paths).GetAsync()).Theme=="Dark");
                    await Until(async()=> (await Evaluate("document.documentElement.dataset.theme==='dark'&&document.querySelector('.locked-dialog').hidden")).GetBoolean());
                    await Evaluate("(async()=>{await window.meshMe360Locked.registry.execute('Preferences');return true})()");
                    if((await Evaluate("document.querySelector('[aria-label=\"Application theme\"]').value")).GetString()!="Dark")throw new Exception("Preferences did not reopen persisted theme");
                    await Evaluate("(()=>{document.querySelector('.locked-dialog .close').click();window.meshMe360Locked.registry.execute('Project Settings');document.querySelector('[aria-label=\"Project display units\"]').value='in';document.querySelector('[aria-label=\"Project export profile\"]').value='bambu-020';[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Apply project settings').click();return true})()");
                    if(!(await Evaluate("(()=>{const d=window.meshMe360Locked.scene.document;return d.state.units==='in'&&d.state.activeProfile==='bambu-020'&&Math.max(...d.selected[0].positions)===10})()")).GetBoolean())throw new Exception("Project settings changed scale or failed to set profile");
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Undo');return true})()");
                    if(!(await Evaluate("(()=>{const d=window.meshMe360Locked.scene.document;return d.state.units==='mm'&&d.state.activeProfile==='generic-stl'})()")).GetBoolean())throw new Exception("Project settings undo failed");
                    await Evaluate("(()=>{document.querySelector('[data-export-profile=\"bambu-020\"]').click();return true})()");
                    if(!(await Evaluate("window.meshMe360Locked.scene.document.state.activeProfile==='bambu-020'&&document.querySelector('[data-export-profile=\"bambu-020\"]').getAttribute('aria-pressed')==='true'")).GetBoolean())throw new Exception("Tree export profile failed");
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Save');return true})()");
                    await Until(()=>Task.FromResult(File.Exists(chosen)&&chosen.EndsWith(".mm360")));
                    await Until(async()=>!(await Evaluate("window.meshMe360Locked.scene.document.dirty")).GetBoolean());
                    await ReopenNativeProject(chosen);
                    if((await Evaluate("window.meshMe360Locked.scene.document.state.activeProfile")).GetString()!="bambu-020")throw new Exception("Profile project round trip failed");
                    await Evaluate("(()=>{const app=window.meshMe360Locked;app.scene.document.transform({position:[12,30,42]});app.scene.refreshScene();app.registry.execute('menu:Modify');[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Place on Bed').click();return true})()");
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.selected[0].position)")).GetString()!="[12,30,10]")throw new Exception("Modify Place on Bed failed");
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Reset Transform');return true})()");
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.selected[0].position)")).GetString()!="[0,0,0]")throw new Exception("Modify Reset Transform failed");
                    await Evaluate("(()=>{const r=window.meshMe360Locked.registry;r.execute('Undo');r.execute('Undo');r.execute('Undo');return true})()");
                    foreach(var helpCommand in new[]{"Documentation","Keyboard Shortcuts","Source Code / License","Third-Party Notices","About Mesh Me360"})
                    {
                        await Evaluate($"(async()=>{{await window.meshMe360Locked.registry.execute({JsonSerializer.Serialize(helpCommand)});return true}})()");
                        if((await Evaluate("document.querySelector('.locked-dialog').innerText.length")).GetInt32()<120)throw new Exception("Empty Help command: "+helpCommand);
                    }
                    var menuObjectCount=(await Evaluate("window.meshMe360Locked.scene.document.state.objects.length")).GetInt32();
                    await Evaluate("(async()=>{await window.meshMe360Locked.registry.execute('Open Recent');return true})()");
                    await Evaluate($"(()=>{{[...document.querySelectorAll('[data-recent-path]')].find(b=>b.dataset.recentPath==={JsonSerializer.Serialize(fixture)}).click();return true}})()");
                    await Until(async()=> (await Evaluate("window.meshMe360Locked.scene.document.state.objects.length")).GetInt32()==menuObjectCount+1);
                    if(!(await Evaluate("(()=>{const rows=[...document.querySelectorAll('.model-name')],d=window.meshMe360Locked.scene.document;rows[1].click();rows[0].dispatchEvent(new MouseEvent('click',{bubbles:true,ctrlKey:true}));if(d.selected[0].id!==d.state.objects[1].id||d.selected.length!==2)return false;rows[1]=document.querySelectorAll('.model-name')[1];rows[1].dispatchEvent(new MouseEvent('click',{bubbles:true,ctrlKey:true}));return d.selected.length===1&&d.selected[0].id===d.state.objects[0].id})()")).GetBoolean())throw new Exception("Native tree Ctrl toggle or operand order failed");
                    await recent.RemoveAsync(fixture);
                    await Evaluate("(async()=>{await window.meshMe360Locked.registry.execute('Open Recent');return true})()");
                    if((await Evaluate($"[...document.querySelectorAll('[data-recent-path]')].some(b=>b.dataset.recentPath==={JsonSerializer.Serialize(fixture)})")).GetBoolean())throw new Exception("Recent list did not refresh host removal");
                    await Evaluate("(()=>{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Clear recent files').click();return true})()");
                    await Until(async()=> (await recent.ListAsync()).Count==0);
                    Console.WriteLine("PASS: Windows preferences persistence, project settings/undo/native reopen, export profile tree, recent native open/clear, Ctrl selection and operand order");
                    Console.WriteLine("Menu evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--relief"))
                {
                    await Evaluate("(async()=>{const e=await import('./js/main.js'),c=document.createElement('canvas');c.width=c.height=8;const ctx=c.getContext('2d');ctx.fillStyle='white';ctx.fillRect(0,0,8,8);const blob=await new Promise(r=>c.toBlob(r));await e.desktopLoadTexture(new File([blob],'white.png',{type:'image/png'}));e.desktopConfigureTexture({textureHeight:1,refineLength:3,maxTriangles:50000,smoothBottom:false,noDownwardZ:false,boundaryFalloff:0,topAngleLimit:0,bottomAngleLimit:0});return true})()");
                    foreach(var command in new[]{"Emboss","Deboss","Apply Texture"})
                    {
                        await Evaluate("(async()=>{(await import('./js/main.js')).desktopConfigureTexture({textureHeight:1,refineLength:3,maxTriangles:50000,smoothBottom:false,noDownwardZ:false,boundaryFalloff:0,topAngleLimit:0,bottomAngleLimit:0});return true})()");
                        if(command=="Emboss"){
                            await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Emboss');[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Preview').click();return true})()");
                            if((await Evaluate("document.querySelector('[data-relief-status]').dataset.reliefStatus")).GetString()!="error")throw new Exception("Relief refinement started without growth consent");
                            await Evaluate("(()=>{document.querySelector('.locked-dialog .close').click();return true})()");
                        }
                        var before=await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)");
                        var oldMax=(await Evaluate("(()=>{const p=window.meshMe360Locked.scene.document.selected[0].positions;let z=-Infinity;for(let i=2;i<p.length;i+=3)z=Math.max(z,p[i]);return z})()")).GetDouble();
                        await Evaluate($"(()=>{{document.querySelector('[data-command=\"{command}\"]').click();document.querySelector('[aria-label=\"Allow relief refinement\"]')?.click();[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Preview').click();return true}})()");
                        try{await Until(async()=> (await Evaluate("document.querySelector('[data-relief-status]')?.dataset.reliefStatus??''")).GetString() is "ready" or "error");}
                        catch{Console.Error.WriteLine(command+" state: "+await Evaluate("({dialog:document.querySelector('.locked-dialog').innerText,status:document.querySelector('[data-relief-status]')?.dataset.reliefStatus})"));throw;}
                        if((await Evaluate("document.querySelector('[data-relief-status]').dataset.reliefStatus")).GetString()!="ready")throw new Exception((await Evaluate("document.querySelector('[data-relief-status]').textContent")).GetString());
                        if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=before.GetString())throw new Exception(command+" preview changed document");
                        await Evaluate("(()=>{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Commit').click();return true})()");
                        var newMax=(await Evaluate("(()=>{const p=window.meshMe360Locked.scene.document.selected[0].positions;let z=-Infinity;for(let i=2;i<p.length;i+=3)z=Math.max(z,p[i]);return z})()")).GetDouble();
                        if(command=="Deboss"?newMax>=oldMax-.05:newMax<=oldMax+.05)throw new Exception(command+" did not change actual surface in expected direction: "+oldMax+" -> "+newMax);
                        await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Undo');return true})()");
                        if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=before.GetString())throw new Exception(command+" undo failed");
                    }
                    Console.WriteLine("PASS: actual Emboss, Deboss and Apply Texture worker preview/commit/undo; originals untouched until commit and signed geometry displacement verified");
                    Console.WriteLine("Relief evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--library"))
                {
                    await Evaluate("(()=>{document.querySelector('[data-command=\"Generate Texture\"]').click();for(const [name,value]of [['Seed',7],['Blur',2]]){const input=document.querySelector('.locked-dialog [aria-label=\"'+name+'\"]');input.value=value;input.dispatchEvent(new Event('input'));}[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Generate Preview').click();return true})()");
                    await Until(async()=> (await Evaluate("!![...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Save to Library'&&!b.disabled)")).GetBoolean());
                    await Evaluate("(()=>{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Save to Library').click();return true})()");
                    await Until(async()=> (await Evaluate("window.meshMe360Locked.library.items.length")).GetInt32()==1);
                    var libraryResult=await Evaluate("(async()=>{const l=window.meshMe360Locked.library;l.store.close();await l.refresh();const item=l.items[0];await window.meshMe360Locked.registry.execute('preset:'+item.id);const e=await import('./js/main.js'),p=await import('./app/procedural.js'),snapshot=e.desktopSceneCapture(),expected=p.generateHeight(item.parameters);const exact=expected.every((v,i)=>snapshot.texture.bytes[i*4]===v&&snapshot.texture.bytes[i*4+1]===v&&snapshot.texture.bytes[i*4+2]===v&&snapshot.texture.bytes[i*4+3]===255);snapshot.geometry.dispose();return{seed:item.parameters.seed,blur:item.parameters.blur,exact}})()");
                    if(libraryResult.GetProperty("seed").GetInt32()!=7||libraryResult.GetProperty("blur").GetInt32()!=2||!libraryResult.GetProperty("exact").GetBoolean())throw new Exception("Generated library texture differs after persistence/apply");
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Texture Library');const input=document.querySelector('.locked-dialog [aria-label=\"Texture name\"]');input.value='Saved noise';[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Rename').click();return true})()");
                    await Until(async()=> (await Evaluate("window.meshMe360Locked.library.items[0].name")).GetString()=="Saved noise");
                    await Evaluate("(()=>{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Favorite').click();return true})()");
                    await Until(async()=> (await Evaluate("window.meshMe360Locked.library.items[0].favorite")).GetBoolean());
                    if(!(await Evaluate("(async()=>{try{await window.meshMe360Locked.library.store.remove('Stone');return false}catch{return true}})()")).GetBoolean())throw new Exception("Built-in texture deletion accepted");
                    Console.WriteLine("PASS: actual procedural generator, blur/seed, PNG save to IndexedDB, store reopen, exact pixel application, rename/favorite and built-in protection");
                    Console.WriteLine("Library evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--source"))
                {
                    if(!(await Evaluate("(async()=>{const e=await import('./js/main.js'),b=document.querySelector('.locked-field #lock-scale');e.desktopConfigureTexture({scaleU:5,scaleV:9,lockScale:false});b.click();const a=e.desktopSceneCapture();a.geometry.dispose();return a.settings.lockScale&&a.settings.scaleU===a.settings.scaleV&&b.getAttribute('aria-pressed')==='true'})()")).GetBoolean())throw new Exception("Relocated texture scale link failed");
                    await Evaluate("(async()=>{const e=await import('./js/main.js');for(const value of [128,64]){const c=document.createElement('canvas');c.width=c.height=8;const ctx=c.getContext('2d');ctx.fillStyle='rgb('+value+','+value+','+value+')';ctx.fillRect(0,0,8,8);const blob=await new Promise(r=>c.toBlob(r));await e.desktopLoadTexture(new File([blob],'height-'+value+'.png',{type:'image/png'}));}e.desktopConfigureTexture({textureSmoothing:1});window.meshMe360Locked.scene.capture();return true})()");
                    async Task SourcePixels(int expected)
                    {
                        await Until(async()=> !(await Evaluate("window.meshMe360Locked.registry.busy")).GetBoolean());
                        var actual=await Evaluate("(async()=>{const e=await import('./js/main.js'),a=e.desktopSceneCapture();a.geometry.dispose();const input=e.desktopDisplacementInput();return{raw:a.texture.bytes[0],pipeline:input.imageData.data[0]}})()");
                        if(actual.GetProperty("raw").GetInt32()!=expected||actual.GetProperty("pipeline").GetInt32()!=expected)throw new Exception("Source pixels or smoothed pipeline cache incorrect: "+actual+" expected "+expected);
                    }
                    foreach(var sourceMode in new[]{("normal",64),("multiply",32),("add",192),("subtract",64),("min",64)})
                    {
                        await Evaluate($"(()=>{{const b=document.querySelector('[aria-label=\"Source blend mode\"]');b.value='{sourceMode.Item1}';b.dispatchEvent(new Event('change'));return true}})()");await SourcePixels(sourceMode.Item2);
                    }
                    await Evaluate("(()=>{const b=document.querySelector('[aria-label=\"Source blend mode\"]');b.value='normal';b.dispatchEvent(new Event('change'));return true})()");await SourcePixels(64);
                    var sourceBefore=await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)");
                    await Evaluate("(()=>{const s=document.querySelector('[aria-label=Strength]');s.value='.5';s.dispatchEvent(new Event('change'));return true})()");await SourcePixels(96);
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Undo');return true})()");await SourcePixels(64);
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=sourceBefore.GetString())throw new Exception("Source influence undo failed");
                    await Evaluate("(()=>{document.querySelector('[aria-label=\"Invert source\"]').click();return true})()");await SourcePixels(191);
                    await Evaluate("(async()=>{await window.meshMe360Locked.registry.execute('Save');return true})()");var sourceProject=Path.Combine(testRoot,"native-export.mm360");await Until(async()=> (await recent.ListAsync()).Any(e=>e.Path==sourceProject));await ReopenNativeProject(sourceProject);await Until(async()=> (await Evaluate("window.meshMe360Locked.scene.document.state.name")).GetString()=="native-export.mm360");await SourcePixels(191);
                    if(!(await Evaluate("document.querySelector('[aria-label=\"Invert source\"]').checked")).GetBoolean())throw new Exception("Source inspector did not restore persisted state");
                    await Evaluate("(()=>{document.querySelector('[aria-label=\"Invert source\"]').click();return true})()");await SourcePixels(64);
                    var clearBefore=(await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString();
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Clear Texture');return true})()");
                    if(!(await Evaluate("(async()=>{const a=window.meshMe360Locked,e=await import('./js/main.js'),c=e.desktopSceneCapture();const ok=!c.texture&&!a.scene.document.selected[0].textureId&&!a.registry.state('Apply Texture').enabled;c.geometry.dispose();return ok})()")).GetBoolean())throw new Exception("Clear Texture retained the engine source or Apply state");
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Undo');return true})()");await SourcePixels(64);
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=clearBefore)throw new Exception("Clear Texture Undo did not restore source and geometry");
                    Console.WriteLine("PASS: actual source strength/invert/five blend modes, pipeline pixels with smoothing, undo and native source/base/settings round trip");
                    var presetCheck=await Evaluate("""
                    (async()=>{const a=window.meshMe360Locked,s=a.scene,d=s.document,selected=()=>[...document.querySelectorAll('.locked-source-grid .active')].map(b=>b.dataset.command).join();await a.registry.execute('preset:Scales');if(selected()!=='preset:Scales')throw Error('Scales highlight missing');await a.registry.execute('preset:Rock');a.registry.execute('Undo');if(selected()!=='preset:Scales')throw Error('Preset Undo highlight stale');a.registry.execute('Redo');if(selected()!=='preset:Rock')throw Error('Preset Redo highlight stale');const first=d.selected[0].id;d.duplicate();s.refreshScene({activate:true});await a.registry.execute('preset:Scales');d.select([first]);s.refreshScene({activate:true});if(selected()!=='preset:Rock')throw Error('Object selection retained another preset');d.select(d.state.objects.map(o=>o.id));s.refreshScene();if(selected())throw Error('Multiple selection shows a single active preset');d.select([first]);s.refreshScene({activate:true});return true})()
                    """);
                    if(!presetCheck.GetBoolean())throw new Exception("Preset selection failed");
                    await Evaluate("(async()=>{await window.meshMe360Locked.registry.execute('Save');return true})()");await ReopenNativeProject(sourceProject);
                    if(!(await Evaluate("document.querySelector('.locked-source-grid .active')?.dataset.command==='preset:Rock'")).GetBoolean())throw new Exception("Reopened project lost preset identity");
                    Console.WriteLine("PASS: preset identity follows Undo, Redo, object/multiple selection and native project reopen");
                    Console.WriteLine("Source evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--print"))
                {
                    await Evaluate("(()=>{const s=window.meshMe360Locked.scene;s.document.transform({position:[30,20,40]});s.refreshScene();s.capture();return true})()");
                    var printBefore=await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)");
                    async Task PrintButton(string label)=>await Evaluate($"(()=>{{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='{label}').click();return true}})()");
                    async Task CheckPrint()
                    {
                        await PrintButton("Run checks");await Until(async()=> (await Evaluate("document.querySelector('[data-print-status]').dataset.printStatus")).GetString() is "ready" or "error");
                        if((await Evaluate("document.querySelector('[data-print-status]').dataset.printStatus")).GetString()!="ready")throw new Exception("Print checks failed: "+await Evaluate("document.querySelector('.locked-dialog').innerText"));
                    }
                    await Evaluate("(()=>{document.querySelector('[data-command=\"Prepare for Print\"]').click();return true})()");await CheckPrint();
                    var printText=(await Evaluate("document.querySelector('[data-print-object]').innerText")).GetString()!;
                    if(!printText.Contains("0 open edges")||!printText.Contains("0 degenerate")||!printText.Contains("20.000 mm")||!printText.Contains("closed with positive volume"))throw new Exception("Cube print diagnostics incorrect: "+printText);
                    await Evaluate("(()=>{const t=document.querySelector('[aria-label=\"Wall warning threshold (mm)\"]');t.value='25';t.dispatchEvent(new Event('input'));return true})()");
                    if(!(await Evaluate("document.querySelector('[data-print-object]').innerText.includes('below your 25 mm threshold')")).GetBoolean())throw new Exception("Wall threshold warning missing");
                    await PrintButton("Cancel");if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=printBefore.GetString())throw new Exception("Print review changed the source");
                    await Evaluate("(()=>{document.querySelector('[data-command=\"Prepare for Print\"]').click();return true})()");await PrintButton("Apply proposed placement");
                    if((await Evaluate("window.meshMe360Locked.scene.document.selected[0].position")).GetRawText()!="[0,0,10]")throw new Exception("Reviewed placement incorrect");
                    await PrintButton("Cancel");await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Undo');return true})()");
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=printBefore.GetString())throw new Exception("Preparation placement undo failed");
                    await Evaluate("(()=>{document.querySelector('[data-command=\"Prepare for Print\"]').click();return true})()");await PrintButton("Apply proposed placement");
                    await CheckPrint();await Evaluate("(()=>{const p=document.querySelector('[aria-label=\"Preparation profile\"]');p.value='bambu-020';p.dispatchEvent(new Event('change'));const u=document.querySelector('[aria-label=\"Preparation display units\"]');u.value='in';u.dispatchEvent(new Event('change'));return true})()");await PrintButton("Apply units and profile");await PrintButton("Continue to export");
                    if((await Evaluate("document.querySelector('[aria-label=\"Export format\"]').value")).GetString()!="3mf")throw new Exception("Preparation profile did not select final 3MF export");
                    await PrintButton("Validate and prepare");await Until(async()=> (await Evaluate("document.querySelector('[data-export-status]').dataset.exportStatus")).GetString() is "ready" or "error");await PrintButton("Choose destination…");
                    var printExportPath=Path.Combine(testRoot,"native-export.3mf");await Until(()=>Task.FromResult(File.Exists(printExportPath)&&new FileInfo(printExportPath).Length>84));
                    using(var printZip=ZipFile.OpenRead(printExportPath)){using var reader=new StreamReader(printZip.GetEntry("3D/3dmodel.model")!.Open());var xml=await reader.ReadToEndAsync();if(!xml.Contains("Bambu Lab (0.20mm)")||!xml.Contains("millimeter"))throw new Exception("Preparation export profile/units metadata missing");}
                    await Evaluate("(async()=>{await window.meshMe360Locked.registry.execute('Save');return true})()");var printProject=Path.Combine(testRoot,"native-export.mm360");await Until(async()=> (await recent.ListAsync()).Any(e=>e.Path==printProject));
                    await ReopenNativeProject(printProject);await Until(async()=> (await Evaluate("window.meshMe360Locked.scene.document.state.name")).GetString()=="native-export.mm360");
                    if(!(await Evaluate("window.meshMe360Locked.scene.document.state.activeProfile==='bambu-020'&&window.meshMe360Locked.scene.document.state.units==='in'")).GetBoolean())throw new Exception("Preparation project preferences not persisted");
                    var printFixtures=await Evaluate("(async()=>{const T=await import('three'),{objectFromGeometry}=await import('./app/scene-document.js'),{startPrintAnalysis}=await import('./app/print-client.js');const open=objectFromGeometry(new T.PlaneGeometry(20,20),'Open sheet'),thin=objectFromGeometry(new T.BoxGeometry(20,20,.4),'Thin box'),reversed=objectFromGeometry(new T.BoxGeometry(20,20,20),'Reversed');for(let i=0;i<reversed.indices.length;i+=3){const t=reversed.indices[i+1];reversed.indices[i+1]=reversed.indices[i+2];reversed.indices[i+2]=t;}return await startPrintAnalysis([open,thin,reversed]).promise})()");
                    if(printFixtures[0].GetProperty("open").GetInt32()!=4||printFixtures[0].GetProperty("solid").GetBoolean()||printFixtures[0].GetProperty("wall").GetProperty("minimumSampleMm").ValueKind!=JsonValueKind.Null||!printFixtures[1].GetProperty("solid").GetBoolean()||Math.Abs(printFixtures[1].GetProperty("wall").GetProperty("minimumSampleMm").GetDouble()-.4)>.001||printFixtures[2].GetProperty("solid").GetBoolean())throw new Exception("Print fixture diagnostics incorrect: "+printFixtures);
                    if(!(await Evaluate("(async()=>{const {startPrintAnalysis}=await import('./app/print-client.js'),job=startPrintAnalysis(window.meshMe360Locked.scene.document.state.objects);job.cancel();try{await job.promise;return false}catch(error){return error.name==='AbortError'}})()")).GetBoolean())throw new Exception("Print worker cancellation failed");
                    Console.WriteLine("PASS: print dimensions/checks without mutation, explicit placement, threshold warning, units/profile persistence, native 3MF, open/reversed/thin fixtures");
                    Console.WriteLine("Print evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--pattern"))
                {
                    await Evaluate("(async()=>{const e=await import('./js/main.js'),c=document.createElement('canvas');c.width=c.height=8;const ctx=c.getContext('2d');ctx.fillStyle='white';ctx.fillRect(0,0,8,8);const blob=await new Promise(r=>c.toBlob(r));await e.desktopLoadTexture(new File([blob],'detail.png',{type:'image/png'}));return true})()");
                    var patternSamples=new HashSet<string>();
                    foreach(var patternType in new[]{"linear","grid","radial"})
                    {
                        await Evaluate("(async()=>{(await import('./js/main.js')).desktopConfigureTexture({textureHeight:1,refineLength:1,maxTriangles:50000,smoothBottom:false,noDownwardZ:false,boundaryFalloff:0,topAngleLimit:0,bottomAngleLimit:0,mappingMode:0});window.meshMe360Locked.scene.capture();return true})()");
                        var patternBefore=await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)");
                        async Task PreviewPattern()
                        {
                            await Evaluate($"(()=>{{document.querySelector('[data-command=Pattern]').click();const select=document.querySelector('[aria-label=\"Pattern type\"]');select.value='{patternType}';select.dispatchEvent(new Event('change'));document.querySelector('[aria-label=\"Allow relief refinement\"]')?.click();[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Preview').click();return true}})()");
                            await Until(async()=> (await Evaluate("document.querySelector('[data-relief-status]').dataset.reliefStatus")).GetString() is "ready" or "error");
                            if((await Evaluate("document.querySelector('[data-relief-status]').dataset.reliefStatus")).GetString()!="ready")throw new Exception("Pattern preview failed: "+await Evaluate("document.querySelector('[data-relief-status]').textContent"));
                        }
                        await PreviewPattern();
                        if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=patternBefore.GetString())throw new Exception("Pattern preview changed original state");
                        await Evaluate("(()=>{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Cancel').click();return true})()");
                        if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=patternBefore.GetString())throw new Exception("Pattern cancellation changed original state");
                        await PreviewPattern();
                        await Evaluate("(()=>{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Commit').click();return true})()");
                        var patternResult=await Evaluate("(async()=>{const s=window.meshMe360Locked.scene.document,o=s.selected[0],p=o.positions;let changed=false;for(let i=2;i<p.length;i+=3)if(p[i]>10.05)changed=true;const digest=await crypto.subtle.digest('SHA-256',p);return{valid:p.every(Number.isFinite)&&p.length%9===0,changed,triangles:p.length/9,pattern:o.modifiers.at(-1).pattern,hash:Array.from(new Uint8Array(digest)).join(',')}})()");
                        if(!patternResult.GetProperty("valid").GetBoolean()||!patternResult.GetProperty("changed").GetBoolean()||patternResult.GetProperty("triangles").GetInt32()<=12||patternResult.GetProperty("pattern").GetProperty("kind").GetString()!=patternType)throw new Exception("Pattern did not commit valid displaced geometry: "+patternResult);
                        patternSamples.Add(patternResult.GetProperty("hash").GetString()!);
                        if(!(await Evaluate("(async()=>{const {serializeProject,deserializeProject}=await import('./app/scene-document.js'),s=window.meshMe360Locked.scene.document.state;return JSON.stringify(deserializeProject(serializeProject(s)).objects[0].modifiers)===JSON.stringify(s.objects[0].modifiers)})()")).GetBoolean())throw new Exception("Pattern metadata round trip failed");
                        await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Undo');return true})()");
                        if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=patternBefore.GetString())throw new Exception("Pattern undo did not restore original geometry and texture");
                    }
                    if(patternSamples.Count!=3)throw new Exception("Pattern types did not create distinct geometry");
                    Console.WriteLine("PASS: actual Linear/Grid/Radial preview/cancel/commit/undo, distinct finite displaced meshes and persisted pattern metadata");
                    Console.WriteLine("Pattern evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--measure"))
                {
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('view:Front');return true})()");
                    var measureOriginal=await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state.objects)");
                    async Task MeasureButton(string label)=>await Evaluate($"(()=>{{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='{label}').click();return true}})()");
                    async Task PickMeasurement(double px,double py,double pz)
                    {
                        var measurePoint=await Evaluate($"(async()=>{{const v=await import('./js/viewer.js'),T=await import('three');const p=new T.Vector3({px},{py},{pz}).project(v.getCamera()),r=v.getRenderer().domElement.getBoundingClientRect();return{{x:r.left+(p.x+1)*r.width/2,y:r.top+(1-p.y)*r.height/2}}}})()");
                        foreach(var eventType in new[]{"mouseMoved","mousePressed","mouseReleased"})
                            await core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new{type=eventType,x=measurePoint.GetProperty("x").GetDouble(),y=measurePoint.GetProperty("y").GetDouble(),button=eventType=="mouseMoved"?"none":"left",buttons=eventType=="mousePressed"?1:0,clickCount=1}));
                    }
                    foreach(var measureKind in new[]{"distance","angle","bounds","thickness"})
                    {
                        await Evaluate($"(()=>{{document.querySelector('[data-command=Measure]').click();const select=document.querySelector('[aria-label=\"Measurement type\"]');select.value='{measureKind}';select.dispatchEvent(new Event('change'));return true}})()");
                        await Until(async()=> (await Evaluate("document.querySelector('[data-measure-status]').dataset.measureStatus")).GetString() is "picking" or "ready" or "error");
                        if(measureKind=="distance"){await PickMeasurement(-5,-10,0);await PickMeasurement(5,-10,0);}
                        if(measureKind=="angle"){await PickMeasurement(-5,-10,0);await PickMeasurement(0,-10,0);await PickMeasurement(0,-10,5);}
                        if(measureKind=="thickness")await PickMeasurement(0,-10,0);
                        if((await Evaluate("document.querySelector('[data-measure-status]').dataset.measureStatus")).GetString()!="ready")throw new Exception("Measurement did not finish: "+await Evaluate("document.querySelector('.locked-dialog').innerText"));
                        await MeasureButton("Save measurement");
                    }
                    var measured=await Evaluate("window.meshMe360Locked.scene.document.state.measurements");
                    if(measured.GetArrayLength()!=4||Math.Abs(measured[0].GetProperty("valueMm").GetDouble()-10)>.1||Math.Abs(measured[1].GetProperty("valueDegrees").GetDouble()-90)>1||measured[2].GetProperty("dimensionsMm").EnumerateArray().Any(v=>Math.Abs(v.GetDouble()-20)>.01)||Math.Abs(measured[3].GetProperty("valueMm").GetDouble()-20)>.1)throw new Exception("Known cube measurements incorrect: "+measured);
                    await Evaluate("(()=>{document.querySelector('[data-command=Measure]').click();return true})()");await MeasureButton("Cancel");
                    await Evaluate("(()=>{document.querySelector('[data-measurement-id]').click();return true})()");await MeasureButton("Rename");
                    await Evaluate("(()=>{document.querySelector('.locked-dialog input').value='Known distance';return true})()");await MeasureButton("Rename");await MeasureButton("Close");
                    await Evaluate("(()=>{document.querySelector('[data-measurement-id]').click();return true})()");await MeasureButton("Delete");await MeasureButton("Close");
                    if((await Evaluate("window.meshMe360Locked.scene.document.state.measurements.length")).GetInt32()!=3)throw new Exception("Measurement delete failed");
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Undo');return true})()");
                    var measurementSaved=await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state.measurements)");
                    if(!measurementSaved.GetString()!.Contains("Known distance"))throw new Exception("Measurement rename/undo failed");
                    await Evaluate("(async()=>{await window.meshMe360Locked.registry.execute('Save');return true})()");
                    var measurementPath=Path.Combine(testRoot,"native-export.mm360");await Until(async()=> (await recent.ListAsync()).Any(e=>e.Path==measurementPath));await ReopenNativeProject(measurementPath);
                    await Until(async()=> (await Evaluate("window.meshMe360Locked.scene.document.state.name")).GetString()=="native-export.mm360");
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state.measurements)")).GetString()!=measurementSaved.GetString()||(await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state.objects)")).GetString()!=measureOriginal.GetString())throw new Exception("Measurement round trip changed saved values or geometry");
                    Console.WriteLine("PASS: actual surface mouse picks for distance/angle/wall, bounds, cancel, tree rename/delete/undo and native project round trip");
                    Console.WriteLine("Measure evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--exports"))
                {
                    await bridge.OpenFileAsync(fixture);
                    await Until(async()=> (await Evaluate("window.meshMe360Locked.scene.document.state.objects.length")).GetInt32()==2);
                    await Evaluate("(()=>{const s=window.meshMe360Locked.scene;s.document.transform({position:[30,0,0]});s.document.setMaterial({color:'#ff4400',metallic:.5,roughness:.4,opacity:.7});s.refreshScene({activate:true});return true})()");
                    var exportOriginal=await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state.objects.map(o=>({p:Array.from(o.positions),position:o.position,rotation:o.rotation,scale:o.scale})))");
                    await Evaluate("(async()=>{const e=await import('./js/main.js'),c=document.createElement('canvas');c.width=c.height=8;const ctx=c.getContext('2d');ctx.fillStyle='white';ctx.fillRect(0,0,8,8);const blob=await new Promise(r=>c.toBlob(r));await e.desktopLoadTexture(new File([blob],'white.png',{type:'image/png'}));e.desktopConfigureTexture({textureHeight:1,refineLength:3,maxTriangles:50000,smoothBottom:false,noDownwardZ:false,boundaryFalloff:0,topAngleLimit:0,bottomAngleLimit:0});return true})()");
                    async Task PrepareExport(string command)
                    {
                        await Evaluate($"(()=>{{[...document.querySelectorAll('[data-command]')].find(b=>b.dataset.command==='{command}').click();const scope=document.querySelector('[aria-label=\"Export scope\"]');scope.value='project';scope.dispatchEvent(new Event('change'));const live=document.querySelector('[aria-label=\"Include current relief preview\"]');live.checked=false;live.dispatchEvent(new Event('change'));[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Validate and prepare').click();return true}})()");
                        await Until(async()=> (await Evaluate("document.querySelector('[data-export-status]')?.dataset.exportStatus??''")).GetString() is "ready" or "error");
                        if((await Evaluate("document.querySelector('[data-export-status]').dataset.exportStatus")).GetString()!="ready")throw new Exception((await Evaluate("document.querySelector('[data-export-status]').textContent")).GetString());
                    }
                    await PrepareExport("STL");
                    await Evaluate("(()=>{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Cancel').click();return true})()");
                    if(dialogCount!=0)throw new Exception("Export cancellation opened a native save dialog");
                    foreach(var formatName in new[]{"STL","3MF","OBJ"})
                    {
                        await PrepareExport(formatName);
                        await Evaluate("(()=>{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Choose destination…').click();return true})()");
                        var extension=formatName=="OBJ"?"zip":formatName.ToLowerInvariant();var path=Path.Combine(testRoot,"native-export."+extension);
                        await Until(()=>Task.FromResult(File.Exists(path)&&new FileInfo(path).Length>84));
                        if(formatName=="STL"){var bytes=await File.ReadAllBytesAsync(path);if(BitConverter.ToUInt32(bytes,80)!=24)throw new Exception("Scene STL triangle count lost");}
                        else {using var zip=ZipFile.OpenRead(path);var entry=zip.GetEntry(formatName=="OBJ"?"model.obj":"3D/3dmodel.model")??throw new Exception("Export package entry missing");using var reader=new StreamReader(entry.Open());var text=await reader.ReadToEndAsync();var exportFaceCount=formatName=="OBJ"?text.Split('\n').Count(l=>l.StartsWith("f ")):System.Text.RegularExpressions.Regex.Matches(text,"<triangle ").Count;if(exportFaceCount!=24)throw new Exception("Scene export triangles lost");if(formatName=="OBJ"&&zip.GetEntry("materials.mtl") is null)throw new Exception("OBJ materials missing");}
                    }
                    await Evaluate("(()=>{document.querySelector('[data-command=Export]').click();return true})()");
                    if((await Evaluate("document.querySelector('[aria-label=\"Export format\"]').value")).GetString()!="obj")throw new Exception("Primary Export did not retain the last chosen format");
                    await Evaluate("(()=>{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Cancel').click();return true})()");
                    var previousStl=await File.ReadAllBytesAsync(Path.Combine(testRoot,"native-export.stl"));var priorDialogs=dialogCount;cancelDownload=true;await PrepareExport("STL");
                    await Evaluate("(()=>{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Choose destination…').click();return true})()");
                    await Until(()=>Task.FromResult(dialogCount>priorDialogs));
                    if(!previousStl.SequenceEqual(await File.ReadAllBytesAsync(Path.Combine(testRoot,"native-export.stl"))))throw new Exception("Native export Cancel changed existing file");
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state.objects.map(o=>({p:Array.from(o.positions),position:o.position,rotation:o.rotation,scale:o.scale})))")).GetString()!=exportOriginal.GetString())throw new Exception("Export changed authored geometry or transforms");
                    using(var objPackage=ZipFile.OpenRead(Path.Combine(testRoot,"native-export.zip"))){if(!objPackage.Entries.Any(e=>e.FullName.StartsWith("source-textures/")&&e.Length>16))throw new Exception("OBJ source image missing");}
                    var liveExport=await Evaluate("(async()=>{const s=window.meshMe360Locked.scene,e=await import('./js/main.js'),{startSceneExport}=await import('./app/export-client.js');s.capture();const o=structuredClone(s.document.selected[0]);o.pipelineInput=structuredClone(e.desktopDisplacementInput());const result=await startSceneExport({objects:[o],format:'stl'}).promise;let z=-Infinity;for(let i=2;i<o.positions.length;i+=3)z=Math.max(z,o.positions[i]);return result.reports[0].bounds.max[2]>z+o.position[2]+.05})()");
                    if(!liveExport.GetBoolean())throw new Exception("Live relief export did not use the real displacement pipeline");
                    Console.WriteLine("PASS: actual STL/3MF/OBJ workflow, worker review, native destinations, triangle/material/source-image packages, live displacement, last format and both cancellation paths");
                    Console.WriteLine("Export evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--transform"))
                {
                    await Evaluate("(()=>{document.querySelector('[data-command=Move]').click();return true})()");
                    await Evaluate("new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(()=>r(true))))");
                    var point=await Evaluate("(async()=>{const v=await import('./js/viewer.js');const T=await import('three');const root=v.getScene().children.find(x=>x.isTransformControlsRoot);if(!root)throw new Error('No transform controls');root.updateMatrixWorld(true);const h=root.controls._gizmo.gizmo.translate.children.find(x=>x.name==='X'&&x.isMesh&&x.visible);h.geometry.computeBoundingBox();const p=h.geometry.boundingBox.getCenter(new T.Vector3()).applyMatrix4(h.matrixWorld).project(v.getCamera()),r=v.getRenderer().domElement.getBoundingClientRect();return{x:r.left+(p.x+1)*r.width/2,y:r.top+(1-p.y)*r.height/2}})()");
                    var x=point.GetProperty("x").GetDouble();var y=point.GetProperty("y").GetDouble();
                    var before=await Evaluate("({position:window.meshMe360Locked.scene.document.selected[0].position,history:window.meshMe360Locked.scene.document.undoStack.length})");
                    foreach(var item in new[]{new{type="mouseMoved",x,y,button="none",buttons=0},new{type="mousePressed",x,y,button="left",buttons=1},new{type="mouseMoved",x=x+40,y,button="left",buttons=1},new{type="mouseReleased",x=x+40,y,button="left",buttons=0}})
                        await core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new{item.type,item.x,item.y,item.button,item.buttons,clickCount=1}));
                    var after=await Evaluate("({position:window.meshMe360Locked.scene.document.selected[0].position,history:window.meshMe360Locked.scene.document.undoStack.length})");
                    if(before.GetProperty("position").GetRawText()==after.GetProperty("position").GetRawText()||after.GetProperty("history").GetInt32()!=before.GetProperty("history").GetInt32()+1)throw new Exception("Actual gizmo drag did not commit exactly one transform: "+after);
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Undo');return true})()");
                    if((await Evaluate("window.meshMe360Locked.scene.document.selected[0].position")).GetRawText()!=before.GetProperty("position").GetRawText())throw new Exception("Gizmo undo failed");
                    Console.WriteLine("PASS: actual mouse drag on Three.js Move gizmo creates one undoable transform");
                    foreach(var selectionSize in new[]{1,2})
                    {
                    if(selectionSize==2)await Evaluate("(()=>{const s=window.meshMe360Locked.scene,d=s.document;d.duplicate();d.transform({position:[30,0,0]});d.select(d.state.objects.map(o=>o.id));s.refreshScene({activate:true});window.meshMe360Locked.registry.execute('Fit All');return true})()");
                    foreach(var tool in new[]{"Rotate","Scale","Move"})
                    {
                        await Evaluate($"(()=>{{window.meshMe360Locked.registry.execute('{tool}');return true}})()");
                        await Evaluate("new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(()=>r(true))))");
                        var handle=await Evaluate("""
                        (async()=>{const v=await import('./js/viewer.js'),T=await import('three'),root=v.getScene().children.find(x=>x.isTransformControlsRoot),c=root.controls,parts=c._gizmo.picker[c.mode].children,axis=c.mode==='rotate'?'Z':'X',r=v.getRenderer().domElement.getBoundingClientRect(),ray=new T.Raycaster();root.updateMatrixWorld(true);for(const mesh of parts.filter(m=>m.name===axis)){const p=mesh.geometry.attributes.position;for(let i=0;i<p.count;i++){mesh.geometry.computeBoundingBox();const at=k=>mesh.geometry.index?mesh.geometry.index.getX(k):k,n=mesh.geometry.index?.count??p.count;if(i+2>=n)continue;const local=c.mode!=='rotate'?mesh.geometry.boundingBox.getCenter(new T.Vector3()):new T.Vector3().fromBufferAttribute(p,at(i)).add(new T.Vector3().fromBufferAttribute(p,at(i+1))).add(new T.Vector3().fromBufferAttribute(p,at(i+2))).divideScalar(3),q=local.applyMatrix4(mesh.matrixWorld).project(v.getCamera());ray.setFromCamera(new T.Vector2(q.x,q.y),v.getCamera());if(ray.intersectObjects(parts,true)[0]?.object.name===axis)return{x:r.left+(q.x+1)*r.width/2,y:r.top+(1-q.y)*r.height/2};}}throw Error('No reachable transform handle')})()
                        """);
                        var initial=(await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.selected.map(o=>({position:o.position,rotation:o.rotation,scale:o.scale})))")).GetString();
                        var history=(await Evaluate("window.meshMe360Locked.scene.document.undoStack.length")).GetInt32();
                        var hx=handle.GetProperty("x").GetDouble();var hy=handle.GetProperty("y").GetDouble();
                        foreach(var item in new[]{new{type="mouseMoved",x=hx,y=hy,button="none",buttons=0},new{type="mousePressed",x=hx,y=hy,button="left",buttons=1},new{type="mouseMoved",x=hx+35,y=hy-25,button="left",buttons=1},new{type="mouseReleased",x=hx+35,y=hy-25,button="left",buttons=0}})
                            await core.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",JsonSerializer.Serialize(new{item.type,item.x,item.y,item.button,item.buttons,clickCount=1}));
                        if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.selected.map(o=>({position:o.position,rotation:o.rotation,scale:o.scale})))")).GetString()==initial||(await Evaluate("window.meshMe360Locked.scene.document.undoStack.length")).GetInt32()!=history+1)throw new Exception(tool+" drag did not commit exactly one transform for selection size "+selectionSize);
                        await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Undo');return true})()");
                        if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.selected.map(o=>({position:o.position,rotation:o.rotation,scale:o.scale})))")).GetString()!=initial)throw new Exception(tool+" drag Undo failed");
                    }
                    }
                    Console.WriteLine("PASS: actual Move/Rotate/Scale handle drags for one and two selected objects create one reversible transform each");
                    Console.WriteLine("Transform evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--solid"))
                {
                    await bridge.OpenFileAsync(fixture);
                    await Until(async()=> (await Evaluate("window.meshMe360Locked.scene.document.state.objects.length")).GetInt32()==2);
                    await Evaluate("(()=>{const s=window.meshMe360Locked.scene;s.document.transform({position:[5,0,0]});s.document.select(s.document.state.objects.map(o=>o.id));s.refreshScene({activate:true});return true})()");
                    var before = await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)");
                    await Evaluate("(()=>{document.querySelector('[data-command=Boolean]').click();document.querySelector('[aria-label=\"Allow relief refinement\"]')?.click();[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Preview').click();return true})()");
                    await Until(async()=> (await Evaluate("document.querySelector('[data-solid-status]')?.dataset.solidStatus??''")).GetString() is "ready" or "error");
                    if((await Evaluate("document.querySelector('[data-solid-status]').dataset.solidStatus")).GetString()!="ready")throw new Exception((await Evaluate("document.querySelector('[data-solid-status]').textContent")).GetString());
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=before.GetString())throw new Exception("Preview mutated originals");
                    await Evaluate("(()=>{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Cancel').click();return true})()");
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=before.GetString())throw new Exception("Cancel mutated originals");
                    await Evaluate("(()=>{document.querySelector('[data-command=Boolean]').click();document.querySelector('[aria-label=\"Allow relief refinement\"]')?.click();[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Preview').click();return true})()");
                    await Until(async()=> (await Evaluate("document.querySelector('[data-solid-status]').dataset.solidStatus")).GetString()=="ready");
                    await Evaluate("(()=>{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Commit').click();return true})()");
                    if((await Evaluate("window.meshMe360Locked.scene.document.state.objects.length")).GetInt32()!=1)throw new Exception("Boolean commit failed");
                    var exportCheck=await Evaluate("(async()=>{const engine=await import('./js/main.js'),{exportSTL}=await import('./js/exporter.js'),{STLLoader}=await import('three/addons/loaders/STLLoader.js');const snapshot=engine.desktopSceneCapture(),g=snapshot.geometry;const bytes=exportSTL(g,null),roundTrip=new STLLoader().parse(bytes);const expected=window.meshMe360Locked.scene.document.selected[0].indices.length/3;const valid=!g.index&&new DataView(bytes).getUint32(80,true)===expected&&roundTrip.attributes.position.count===expected*3;g.dispose();roundTrip.dispose();return valid})()");
                    if(!exportCheck.GetBoolean())throw new Exception("Indexed solid result failed preserved engine/STL export boundary");
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Undo');return true})()");
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=before.GetString())throw new Exception("Boolean undo did not restore scene");
                    var workerTests=await Evaluate("(async()=>{const {startSolidOperation}=await import('./app/solid-client.js');const o=window.meshMe360Locked.scene.document.state.objects[0];const job=startSolidOperation({operation:'validate',objects:[o]});job.cancel();let cancelled=false;try{await job.promise}catch(e){cancelled=e.name==='AbortError'}const result=await startSolidOperation({operation:'validate',objects:[o]}).promise;return{cancelled,status:result.results[0].diagnostics.status}})()");
                    if(!workerTests.GetProperty("cancelled").GetBoolean()||workerTests.GetProperty("status").GetString()!="NoError")throw new Exception("Worker cancel/restart failed");
                    await Evaluate("(()=>{const s=window.meshMe360Locked.scene;s.document.select([s.document.state.objects[0].id]);s.refreshScene({activate:true});return true})()");
                    foreach(var command in new[]{"Split","Hollow","Make Solid","Repair","Repair Mesh"})
                    {
                        var original=await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)");
                        var launch=$"(()=>{{[...document.querySelectorAll('[data-command]')].find(b=>b.dataset.command==='{command}').click();const cell=document.querySelector('[aria-label=\"Cell size (mm)\"]');if(cell){{cell.value='1';cell.dispatchEvent(new Event('input'));}}document.querySelector('[aria-label=\"Allow relief refinement\"]')?.click();[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Preview').click();return true}})()";
                        await Evaluate(launch);
                        await Until(async()=> (await Evaluate("document.querySelector('[data-solid-status]')?.dataset.solidStatus??''")).GetString() is "ready" or "error");
                        if((await Evaluate("document.querySelector('[data-solid-status]').dataset.solidStatus")).GetString()!="ready")throw new Exception((await Evaluate("document.querySelector('[data-solid-status]').textContent")).GetString());
                        if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=original.GetString())throw new Exception(command+" preview mutated originals");
                        await Evaluate("(()=>{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Cancel').click();return true})()");
                        if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=original.GetString())throw new Exception(command+" cancel mutated originals");
                        await Evaluate(launch);
                        await Until(async()=> (await Evaluate("document.querySelector('[data-solid-status]')?.dataset.solidStatus??''")).GetString()=="ready");
                        await Evaluate("(()=>{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Commit').click();return true})()");
                        if((await Evaluate("window.meshMe360Locked.scene.document.state.objects.length")).GetInt32()!=(command=="Split"?3:2))throw new Exception(command+" commit failed");
                        await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Undo');return true})()");
                        if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=original.GetString())throw new Exception(command+" undo failed");
                    }
                    Console.WriteLine("PASS: actual Boolean toolbar preview/cancel/commit/undo, originals unchanged, worker cancellation and restart under application CSP");
                    Console.WriteLine("PASS: actual Split, Hollow, Make Solid, Repair and Repair Mesh controls preview/cancel/commit/undo");
                    Console.WriteLine("Solid controls evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--combine"))
                {
                    await bridge.OpenFileAsync(fixture);
                    await Until(async()=> (await Evaluate("window.meshMe360Locked.scene.document.state.objects.length")).GetInt32()==2);
                    await Evaluate("(()=>{const s=window.meshMe360Locked.scene;s.document.transform({position:[20,0,0]});s.document.setMaterial({color:'#ff4400',metallic:.5,roughness:.4,opacity:1});s.document.select(s.document.state.objects.map(o=>o.id));s.refreshScene({activate:true});return true})()");
                    var original=await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)");
                    const string launch="(()=>{document.querySelector('[data-command=Combine]').click();const weld=document.querySelector('[aria-label=\"Weld exactly coincident vertices\"]');weld.checked=true;weld.dispatchEvent(new Event('input'));document.querySelector('[aria-label=\"Allow relief refinement\"]')?.click();[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Preview').click();return true})()";
                    foreach(var finish in new[]{"Cancel","Commit"})
                    {
                        await Evaluate(launch);
                        await Until(async()=> (await Evaluate("document.querySelector('[data-solid-status]')?.dataset.solidStatus??''")).GetString() is "ready" or "error");
                        if((await Evaluate("document.querySelector('[data-solid-status]').dataset.solidStatus")).GetString()!="ready")throw new Exception((await Evaluate("document.querySelector('[data-solid-status]').textContent")).GetString());
                        if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=original.GetString())throw new Exception("Combine preview mutated source");
                        await Evaluate($"(()=>{{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='{finish}').click();return true}})()");
                    }
                    var valid=await Evaluate("(async()=>{const s=window.meshMe360Locked.scene,o=s.document.selected[0],v=await import('./js/viewer.js'),m=v.getCurrentMesh();return s.document.state.objects.length===1&&o.positions.length===36&&o.indices.length===72&&o.groups.length===2&&o.materials[1].color==='#ff4400'&&m.geometry.groups.length===2&&m.userData.groupMaterials[1].color.getHexString()==='ff4400'})()");
                    if(!valid.GetBoolean())throw new Exception("Combine failed group geometry/material rendering boundary: "+await Evaluate("(async()=>{const s=window.meshMe360Locked.scene,o=s.document.selected[0],v=await import('./js/viewer.js'),m=v.getCurrentMesh();return {objects:s.document.state.objects.length,positions:o.positions.length,indices:o.indices.length,groups:o.groups,materials:o.materials,engineGroups:m.geometry.groups,palette:m.userData.groupMaterials?.map(x=>x.color.getHexString())}})()"));
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Undo');return true})()");
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=original.GetString())throw new Exception("Combine undo failed");
                    await Evaluate("(()=>{window.meshMe360Locked.registry.execute('Redo');return true})()");
                    await Evaluate("(async()=>{await window.meshMe360Locked.registry.execute('Save');return true})()");
                    var combinedSaved=Path.Combine(testRoot,"native-export.mm360");
                    await Until(async()=> (await recent.ListAsync()).Any(e=>e.Path==combinedSaved));
                    await ReopenNativeProject(combinedSaved);
                    await Until(async()=> (await Evaluate("window.meshMe360Locked.scene.document.state.name")).GetString()=="native-export.mm360");
                    if(!(await Evaluate("window.meshMe360Locked.scene.document.selected[0].materials[1].color==='#ff4400'&&window.meshMe360Locked.scene.document.selected[0].groups.length===2")).GetBoolean())throw new Exception("Combined material groups lost on native reopen");
                    Console.WriteLine("PASS: actual Combine preview/cancel/commit/undo/redo, exact welding, rendered material groups and native project reopen");
                    Console.WriteLine("Combine evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--scene"))
                {
                    await Evaluate("(async()=>{await window.meshMe360Locked.registry.execute('preset:Bricks');return true})()");
                    await bridge.OpenFileAsync(fixture);
                    await Until(async()=> (await Evaluate("window.meshMe360Locked.scene.document.state.objects.length")).GetInt32()==2);
                    await Evaluate("(async()=>{await window.meshMe360Locked.registry.execute('preset:Wood');return true})()");
                    await Evaluate("(()=>{const s=window.meshMe360Locked.scene;s.document.transform({position:[30,12,8],scale:[2,1,1]});s.refreshScene();return true})()");
                    await Evaluate("(async()=>{await window.meshMe360Locked.registry.execute('Save');return true})()");
                    var scenePath=Path.Combine(testRoot,"native-export.mm360");
                    await Until(async()=> (await recent.ListAsync()).Any(e=>e.Path==scenePath));
                    await Until(async()=> !(await Evaluate("window.meshMe360Locked.scene.document.dirty")).GetBoolean());
                    using(var sceneZip=ZipFile.OpenRead(scenePath))
                    { if(sceneZip.GetEntry("project.json") is null || sceneZip.GetEntry("meshes/1.positions.bin") is null)throw new Exception("Scene container incomplete"); }
                    await ReopenNativeProject(scenePath);
                    await Until(async()=> (await Evaluate("window.meshMe360Locked.scene.document.state.name")).GetString()=="native-export.mm360");
                    var restored=await Evaluate("window.meshMe360Locked.scene.document.state.objects.map(o=>({position:o.position,scale:o.scale}))");
                    if(restored.GetArrayLength()!=2||restored[1].GetProperty("position")[0].GetInt32()!=30||restored[1].GetProperty("scale")[0].GetInt32()!=2)throw new Exception("Scene state was lost");
                    var textures=await Evaluate("(async()=>{const s=window.meshMe360Locked.scene,e=await import('./js/main.js');const names=[];for(const o of s.document.state.objects){s.document.select([o.id]);s.refreshScene({activate:true});const c=e.desktopSceneCapture();const asset=s.document.state.textures.find(t=>t.id===o.textureId);if(!asset||!c.texture.bytes.every((b,i)=>b===asset.bytes[i]))throw new Error('Texture pixels differ');names.push(c.texture.name);c.geometry.dispose();}return names})()");
                    if(textures[0].GetString()!="Bricks.png"||textures[1].GetString()!="Wood.png")throw new Exception("Per-object textures lost: "+textures);
                    Console.WriteLine("PASS: actual multi-object import, transform, native mm360 save and binary-bridge reopen; dirty cleared after download completion");
                    Console.WriteLine("PASS: distinct embedded Bricks/Wood sampled pixels restore exactly on object selection after native project reopen");
                    Console.WriteLine("Scene evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if (args.Contains("--locked"))
                {
                    await core.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride", "{\"width\":1536,\"height\":1024,\"deviceScaleFactor\":1,\"mobile\":false}");
                    await Evaluate("new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(()=>resolve(true))))");
                    var report = await Evaluate("({size:[innerWidth,innerHeight],regions:window.meshMe360Locked.regions(),controls:window.meshMe360Locked.report(),toolbar:[...document.querySelectorAll('.locked-toolbar button')].map(b=>({name:b.textContent,rect:{x:b.getBoundingClientRect().x,y:b.getBoundingClientRect().y,width:b.getBoundingClientRect().width,height:b.getBoundingClientRect().height}}))})");
                    await File.WriteAllTextAsync(Path.Combine(testRoot,"locked-layout.json"),report.GetRawText());
                    var detailLayout=await Evaluate("Object.fromEntries(['.locked-menu .menus','.locked-menu .document-name','.locked-menu .export-group','.locked-inspector-body','.locked-source-grid','.locked-advanced','.locked-apply'].map(selector=>{const n=document.querySelector(selector),r=n.getBoundingClientRect(),s=getComputedStyle(n);return[selector,{x:r.x,y:r.y,width:r.width,height:r.height,font:s.fontSize,lineHeight:s.lineHeight,clientHeight:n.clientHeight,scrollHeight:n.scrollHeight}]}))");
                    await File.WriteAllTextAsync(Path.Combine(testRoot,"locked-details.json"),detailLayout.GetRawText());
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
