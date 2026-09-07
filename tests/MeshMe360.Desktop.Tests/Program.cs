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
                core.GetDevToolsProtocolEventReceiver("Runtime.exceptionThrown").DevToolsProtocolEventReceived += (_,e)=>Console.Error.WriteLine("Web runtime error: "+e.ParameterObjectAsJson);
                await core.CallDevToolsProtocolMethodAsync("Runtime.enable","{}");
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
                    if(!result.StartsWith("PASS:")) throw new Exception("Backend probe failed: "+result);
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
                async Task ReopenNativeProject(string projectPath)
                {
                    var reopenRevision=(await Evaluate("window.meshMe360Locked.scene.document.revision")).GetInt32();
                    await bridge.OpenFileAsync(projectPath);
                    await Until(async()=> (await Evaluate($"(()=>{{const d=window.meshMe360Locked.scene.document;return d.revision!=={reopenRevision}&&!d.dirty&&d.state.name==={JsonSerializer.Serialize(Path.GetFileName(projectPath))}}})()")).GetBoolean());
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
                        var before=await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)");
                        var oldMax=(await Evaluate("(()=>{const p=window.meshMe360Locked.scene.document.selected[0].positions;let z=-Infinity;for(let i=2;i<p.length;i+=3)z=Math.max(z,p[i]);return z})()")).GetDouble();
                        await Evaluate($"(()=>{{document.querySelector('[data-command=\"{command}\"]').click();[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Preview').click();return true}})()");
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
                    Console.WriteLine("PASS: actual source strength/invert/five blend modes, pipeline pixels with smoothing, undo and native source/base/settings round trip");
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
                            await Evaluate($"(()=>{{document.querySelector('[data-command=Pattern]').click();const select=document.querySelector('[aria-label=\"Pattern type\"]');select.value='{patternType}';select.dispatchEvent(new Event('change'));[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Preview').click();return true}})()");
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
                    Console.WriteLine("Transform evidence: "+testRoot);await session.CompleteAsync();return;
                }
                if(args.Contains("--solid"))
                {
                    await bridge.OpenFileAsync(fixture);
                    await Until(async()=> (await Evaluate("window.meshMe360Locked.scene.document.state.objects.length")).GetInt32()==2);
                    await Evaluate("(()=>{const s=window.meshMe360Locked.scene;s.document.transform({position:[5,0,0]});s.document.select(s.document.state.objects.map(o=>o.id));s.refreshScene({activate:true});return true})()");
                    var before = await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)");
                    await Evaluate("(()=>{document.querySelector('[data-command=Boolean]').click();[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Preview').click();return true})()");
                    await Until(async()=> (await Evaluate("document.querySelector('[data-solid-status]')?.dataset.solidStatus??''")).GetString() is "ready" or "error");
                    if((await Evaluate("document.querySelector('[data-solid-status]').dataset.solidStatus")).GetString()!="ready")throw new Exception((await Evaluate("document.querySelector('[data-solid-status]').textContent")).GetString());
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=before.GetString())throw new Exception("Preview mutated originals");
                    await Evaluate("(()=>{[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Cancel').click();return true})()");
                    if((await Evaluate("JSON.stringify(window.meshMe360Locked.scene.document.state)")).GetString()!=before.GetString())throw new Exception("Cancel mutated originals");
                    await Evaluate("(()=>{document.querySelector('[data-command=Boolean]').click();[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Preview').click();return true})()");
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
                        var launch=$"(()=>{{[...document.querySelectorAll('[data-command]')].find(b=>b.dataset.command==='{command}').click();const cell=document.querySelector('[aria-label=\"Cell size (mm)\"]');if(cell){{cell.value='1';cell.dispatchEvent(new Event('input'));}}[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Preview').click();return true}})()";
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
                    const string launch="(()=>{document.querySelector('[data-command=Combine]').click();const weld=document.querySelector('[aria-label=\"Weld exactly coincident vertices\"]');weld.checked=true;weld.dispatchEvent(new Event('input'));[...document.querySelectorAll('.locked-dialog button')].find(b=>b.textContent==='Preview').click();return true})()";
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
