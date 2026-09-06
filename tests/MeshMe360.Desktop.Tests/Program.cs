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
                    foreach(var command in new[]{"Split","Hollow","Make Solid"})
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
                    Console.WriteLine("PASS: actual Split, Hollow and Make Solid controls preview/cancel/commit/undo");
                    Console.WriteLine("Solid controls evidence: "+testRoot);await session.CompleteAsync();return;
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
                    await bridge.OpenFileAsync(scenePath);
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
