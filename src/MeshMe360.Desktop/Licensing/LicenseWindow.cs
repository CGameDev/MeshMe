// SPDX-License-Identifier: AGPL-3.0-only
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MeshMe360.Core.Licensing;

namespace MeshMe360.Desktop.Licensing;

internal sealed class LicenseWindow : Window
{
    public bool Entitled { get; private set; }
    public LicenseWindow(Window owner, ILicenseService service, IHardwareFingerprintService hardware, LicenseState state, Func<Task>? save = null)
    {
        Owner = owner; Title = "Mesh Me360 — Trial and License"; Width = 540; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        MaxHeight = Math.Max(400, SystemParameters.WorkArea.Height - 40);
        var panel = new StackPanel { Margin = new Thickness(24) }; Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        void Text(string text, double size = 14) => panel.Children.Add(new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        Text("Mesh Me360", 24); Text("Create. Texture. Print.\nDone by Computer Universal Technology Systems");
        var status = new TextBlock { Text = state.Message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 12) }; panel.Children.Add(status);
        Text("Activation sends hashed device identifiers, app version and service/security metadata. No models, projects or textures are uploaded.");
        var key = new TextBox { MaxLength = 64, Margin = new Thickness(0, 0, 0, 12), ToolTip = "XXXXX-XXXXX-XXXXX-XXXXX-XXXXX" };
        System.Windows.Automation.AutomationProperties.SetName(key, "Activation key"); panel.Children.Add(key);
        var actions = new StackPanel(); panel.Children.Add(actions);
        void Button(string title, Func<Task> action)
        {
            var button = new Button { Content = title, Margin = new Thickness(0, 3, 0, 3), Padding = new Thickness(8) }; actions.Children.Add(button);
            button.Click += async (_, _) => { actions.IsEnabled = false; try { await action(); } catch { status.Text = "The operation could not complete. Retry or contact support. Your projects are preserved."; } finally { actions.IsEnabled = true; } };
        }
        void Accept(LicenseState result) { status.Text = result.Message; if (result.CanEdit) { Entitled = true; DialogResult = true; } }
        Button("Start 7-Day Trial", async () => Accept(await service.StartTrialAsync()));
        Button("Activate License", async () => { var entered = key.Text; key.Clear(); Accept(await service.ActivateAsync(entered)); });
        Button("Retry Online Validation", async () => Accept(await service.RefreshAsync(true)));
        if (state.Entitlement?.Type == "license") Button("Deactivate This PC", async () =>
        {
            if (MessageBox.Show(this, "Deactivate this PC? Online reactivation will be required to edit again.", Title, MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            await service.DeactivateAsync(); status.Text = "This PC is deactivated. The server seat is now available."; Entitled = false;
        });
        if (save is not null) Button("Save Current Project", save);
        Button("Copy Device ID for Support", async () => Clipboard.SetText((await hardware.GetAsync()).Combined[..16]));
        Button("Source Code / License", async () =>
        {
            var legal = new Window { Owner = this, Title = "Mesh Me360 — GNU AGPLv3", Width = 700, Height = 560, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var layout = new DockPanel();
            var source = new Button { Content = "Open corresponding source code", Padding = new Thickness(8) };
            source.Click += (_, _) => Process.Start(new ProcessStartInfo("https://github.com/CGameDev/MeshMe") { UseShellExecute = true });
            DockPanel.SetDock(source, Dock.Bottom); layout.Children.Add(source);
            layout.Children.Add(new TextBox { Text = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "LICENSE")), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            legal.Content = layout; legal.ShowDialog();
        });
        Text("Official build/service entitlement does not remove your AGPL source, modification or redistribution rights.");
        if (state.CanEdit) Button("Return to Model", async () => Accept(await service.RefreshAsync()));
        Button("Exit", () => { Close(); return Task.CompletedTask; });
    }
}
