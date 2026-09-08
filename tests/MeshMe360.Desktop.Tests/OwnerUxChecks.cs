// SPDX-License-Identifier: AGPL-3.0-only
using System.IO;
using System.Text.Json;
using MeshMe360.Core.Licensing;
using MeshMe360.Desktop.Licensing;

internal static class OwnerUxChecks
{
    internal static async Task Run(Func<string,Task<JsonElement>> evaluate,string testRoot)
    {
        async Task Check(string expression,string name){if(!(await evaluate(expression)).GetBoolean())throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);}
        await Check("!document.querySelector('.locked-advanced').open","Texture Advanced collapsed initially");
        await Check("(async()=>{const r=meshMe360Locked.registry;await r.execute('Command Search');const s=document.querySelector('input[aria-label=\"Search commands, projects, presets and help\"]');s.value='mode:Wireframe';s.dispatchEvent(new Event('input'));s.dispatchEvent(new KeyboardEvent('keydown',{key:'Enter',bubbles:true}));await new Promise(r=>setTimeout(r,50));return document.querySelector('[data-command=\"mode:Wireframe\"]').classList.contains('active')})()","Command search executes shared view command");
        await Check("(async()=>{window.dispatchEvent(new KeyboardEvent('keydown',{key:'k',ctrlKey:true,bubbles:true}));await new Promise(r=>setTimeout(r,100));const s=document.querySelector('input[aria-label=\"Search commands, projects, presets and help\"]');if(!s)return false;s.dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowDown',bubbles:true}));const selected=!!s.getAttribute('aria-activedescendant');s.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}));return selected&&document.querySelector('.locked-dialog').hidden})()","Ctrl+K opens search; arrows select and Escape dismisses");
        await Check("(()=>{meshMe360Locked.registry.execute('Hollow');document.querySelector('.locked-dialog button').dispatchEvent(new KeyboardEvent('keydown',{key:'F1',bubbles:true}));return document.querySelector('.locked-dialog h2').textContent==='Help: Hollow'})()","F1 opens bundled help for active tool");
        await Check("(async()=>{document.querySelector('.locked-dialog .close').click();document.querySelector('.issue').click();await new Promise(r=>setTimeout(r,50));return document.querySelector('[data-diagnostics-status]')!==null})()","Clickable Model Health routes to real diagnostics");
        await Check("(()=>{document.querySelector('.locked-dialog .close').click();meshMe360Locked.registry.execute('Make Solid');const input=document.querySelector('[aria-label=\"Cell size (mm)\"]'),quality=document.querySelector('[aria-label=Quality]'),base=Number(input.value);quality.value='High';quality.dispatchEvent(new Event('change'));if(Number(input.value)!==base/2)return false;input.value=base*.8;input.dispatchEvent(new Event('input'));return quality.value==='Custom'})()","Quality preset maps to actual cell size; explicit edits select Custom");
        await Check("(()=>{document.querySelector('.locked-dialog .close').click();meshMe360Locked.registry.execute('Repair');return !document.querySelector('.locked-dialog details').open&&!!document.querySelector('[aria-label=\"Maximum hole diameter (mm)\"]')})()","Repair Advanced retains safe tolerance controls");
        await Check("(()=>{document.querySelector('.locked-dialog .close').click();const row=document.querySelector('[data-object-id]');row.dispatchEvent(new MouseEvent('contextmenu',{bubbles:true,clientX:180,clientY:280}));return [...document.querySelectorAll('[role=menuitem]')].some(b=>b.textContent==='Rename Selected')})()","Project right-click shows applicable context menu");
        await Check("(()=>{document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}));return !document.querySelector('[aria-label=\"Context actions\"]')})()","Context menu Escape dismisses without scene edits");
        await Check("(async()=>{await meshMe360Locked.registry.execute('Preferences');const p=document.querySelector('.locked-dialog');return p.textContent.includes('Performance')&&p.textContent.includes('Graphics')&&p.querySelector('[aria-label=\"Rendering mode\"]').value==='Automatic'})()","Graphics controls reside in Preferences with Automatic default");
        await Check("(()=>{document.querySelector('.locked-dialog .close').click();const transfer=new DataTransfer();transfer.items.add(new File(['invalid'], 'picture.png',{type:'image/png'}));document.querySelector('.locked-viewport').dispatchEvent(new DragEvent('drop',{bubbles:true,dataTransfer:transfer}));return document.querySelector('.locked-dialog').textContent.includes('Use as Texture')&&document.querySelector('.locked-dialog').textContent.includes('Use as Displacement Source')})()","Image drop asks intent before loading source");
        var pc=await new WindowsHardwareFingerprint().GetAsync();pc.Validate();
        Console.WriteLine("PASS: native WMI/registry hardware collection yields validated hashed components");
        var cache=new DpapiLicenseCache(Path.Combine(testRoot,"Licensing"));
        var value=new LicenseCache(new SignedEntitlement("test-payload","test-signature","test-key"),1,2,false);
        await cache.WriteAsync(value);var restored=await cache.ReadAsync();
        if(restored!=value||System.Text.Encoding.UTF8.GetString(await File.ReadAllBytesAsync(Path.Combine(testRoot,"Licensing","entitlement.dat"))).Contains("test-payload"))throw new Exception("DPAPI cache failed");
        Console.WriteLine("PASS: Windows DPAPI cache round-trip protects locally stored entitlement");
        var fake=new NativeFakeLicense();
        var owner=System.Windows.Application.Current.Windows.Cast<System.Windows.Window>().First(w=>w.IsVisible);
        var dialog=new LicenseWindow(owner,fake,new WindowsHardwareFingerprint(),new(false,"Choose an entitlement."));
        if(fake.Trials!=0)throw new Exception("Native entitlement surface silently started a trial");
        _ = dialog.Dispatcher.BeginInvoke(new Action(()=>
        {
            var panel=(System.Windows.Controls.StackPanel)((System.Windows.Controls.ScrollViewer)dialog.Content).Content;
            var trial=panel.Children.OfType<System.Windows.Controls.StackPanel>().SelectMany(p=>p.Children.OfType<System.Windows.Controls.Button>()).Single(b=>(string)b.Content=="Start 7-Day Trial");
            trial.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        }));
        dialog.ShowDialog();
        if(!dialog.Entitled||fake.Trials!=1)throw new Exception("Native explicit trial action failed");
        Console.WriteLine("PASS: actual native first-run surface starts a trial only after its explicit button action (fake service)");
    }
    private sealed class NativeFakeLicense : ILicenseService
    {
        public int Trials;
        public Task<LicenseState> StartTrialAsync(){Trials++;return Task.FromResult(new LicenseState(true,"Trial active"));}
        public Task<LicenseState> ActivateAsync(string key)=>Task.FromResult(new LicenseState(false,"Unused in this fixture"));
        public Task<LicenseState> RefreshAsync(bool force=false)=>Task.FromResult(new LicenseState(false,"Not activated"));
        public Task DeactivateAsync()=>Task.CompletedTask;
    }
}
