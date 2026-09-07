// SPDX-License-Identifier: AGPL-3.0-only
// Defines and validates the native/web message envelope.
using System.Text.Json;

namespace MeshMe360.Core;

public sealed record BridgeError(string Code, string Message);
public sealed record BridgeMessage(string Type, string RequestId, JsonElement Payload,
    bool? Success = null, BridgeError? Error = null)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = false };
    public static BridgeMessage Create(string type, string requestId, object? payload = null,
        bool? success = null, BridgeError? error = null) =>
        new(type, requestId, JsonSerializer.SerializeToElement(payload, Json), success, error);
    public static BridgeMessage Parse(string json)
    {
        if (json.Length > 65536) throw new InvalidDataException("Control message too large.");
        var message = JsonSerializer.Deserialize<BridgeMessage>(json, Json) ?? throw new InvalidDataException("Empty message.");
        if (string.IsNullOrWhiteSpace(message.Type) || message.Type.Length > 80 ||
            !Guid.TryParse(message.RequestId, out _) || message.Payload.ValueKind == JsonValueKind.Undefined)
            throw new InvalidDataException("Invalid message envelope.");
        return message;
    }
    public string ToJson() => JsonSerializer.Serialize(this, Json);
}

public interface IHostBridge
{
    Task HandleAsync(BridgeMessage message);
    Task OpenFileAsync(string path);
}

public static class HostOrigins
{
    public const string App = "https://app.meshme360.local";
    public const string Bridge = "https://bridge.meshme360.local";
    public static bool IsApp(string source) => Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.Host == "app.meshme360.local" && uri.IsDefaultPort;
}
