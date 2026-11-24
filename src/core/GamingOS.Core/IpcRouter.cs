using System.Text.Json;

namespace GamingOS.Core;

/// <summary>
/// Routes JSON-RPC requests from the UI to registered handlers (docs/PLAN.md 1.4).
/// Request: {"id": 1, "method": "library.list", "params": {...}}.
/// Response: {"id": 1, "result": ...} or {"id": 1, "error": {"code": "...", "message": "..."}}.
/// </summary>
public sealed class IpcRouter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<string, Func<JsonElement?, Task<object?>>> _handlers = [];

    public void Register(string method, Func<JsonElement?, Task<object?>> handler) => _handlers.Add(method, handler);

    /// <returns>The response JSON, or null when the request is so malformed that it has no id to answer.</returns>
    public async Task<string?> HandleAsync(string requestJson)
    {
        int id;
        string? method;
        JsonElement? parameters;
        try
        {
            using var document = JsonDocument.Parse(requestJson);
            var root = document.RootElement;
            id = root.GetProperty("id").GetInt32();
            method = root.GetProperty("method").GetString();
            parameters = root.TryGetProperty("params", out var p) ? p.Clone() : null;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return null;
        }

        if (method is null || !_handlers.TryGetValue(method, out var handler))
            return Error(id, "unknown_method", $"No handler for '{method}'.");

        try
        {
            return JsonSerializer.Serialize(new { id, result = await handler(parameters) }, Json);
        }
        catch (Exception e)
        {
            // A failing handler must not take the host down; the UI decides what to show.
            return Error(id, "internal_error", e.Message);
        }
    }

    private static string Error(int id, string code, string message) =>
        JsonSerializer.Serialize(new { id, error = new { code, message } }, Json);
}
