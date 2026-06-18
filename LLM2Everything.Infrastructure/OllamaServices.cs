using System.Net.Http.Json;
using System.Text.Json;
using LLM2Everything.Core;

namespace LLM2Everything.Infrastructure;

public sealed class OllamaClient : IOllamaClient
{
    private readonly HttpClient _http = new();

    public async Task<IReadOnlyList<string>> GetModelsAsync(CancellationToken cancellationToken)
    {
        var response = await _http.GetFromJsonAsync<OllamaTagsResponse>("http://localhost:11434/api/tags", cancellationToken);
        return response?.Models.Select(m => m.Name).ToList() ?? [];
    }

    public async Task<string> GenerateAsync(OllamaGenerateRequest request, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));
        var url = request.Url.TrimEnd('/') + "/api/generate";
        var payload = new
        {
            model = request.Model,
            prompt = request.Prompt,
            stream = false,
            options = new { temperature = 0, seed = 20260618 },
            format = SearchIntentSchema.JsonSchema
        };
        var response = await _http.PostAsJsonAsync(url, payload, cts.Token);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cts.Token));
        return doc.RootElement.GetProperty("response").GetString() ?? "";
    }

    private sealed class OllamaTagsResponse
    {
        public List<OllamaModel> Models { get; set; } = [];
    }

    private sealed class OllamaModel
    {
        public string Name { get; set; } = "";
    }
}

public static class SearchIntentSchema
{
    public static object JsonSchema => new
    {
        type = "object",
        properties = new Dictionary<string, object>
        {
            ["decision"] = new { type = "string", @enum = new[] { "Searchable", "Ambiguous", "NotSearchable" } },
            ["includeTerms"] = new { type = "array", items = new { type = "string" } },
            ["excludeTerms"] = new { type = "array", items = new { type = "string" } },
            ["targetFolders"] = new { type = "array", items = new { type = "string" } },
            ["fileTypes"] = new { type = "array", items = new { type = "string" } },
            ["extensions"] = new { type = "array", items = new { type = "string" } },
            ["entryKind"] = new { type = "string", @enum = new[] { "Any", "FileOnly", "FolderOnly" } },
            ["minSizeBytes"] = new { type = new[] { "integer", "null" } },
            ["maxSizeBytes"] = new { type = new[] { "integer", "null" } },
            ["userReason"] = new { type = "string" },
            ["confidence"] = new { type = "number" }
        },
        required = new[] { "decision", "includeTerms", "excludeTerms", "targetFolders", "fileTypes", "extensions", "entryKind", "userReason", "confidence" }
    };
}
