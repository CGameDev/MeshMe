// SPDX-License-Identifier: AGPL-3.0-only
namespace MeshMe360.Core;

public static class GraphicsSettings
{
    public static bool IsSupported(string mode) => mode is "Automatic" or "SoftwareCompatibility";
    public static string BrowserArguments(string mode) => mode == "SoftwareCompatibility" ? "--disable-gpu" : "";
    public static bool IsGraphicsInitializationFailure(string message) =>
        message.Contains("Error creating WebGL context", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("WebGL is not supported", StringComparison.OrdinalIgnoreCase) ||
        message == "WebView2 process failed: GpuProcessExited";
}
