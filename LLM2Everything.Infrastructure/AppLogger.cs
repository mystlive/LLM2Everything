using System.Text.Json;
using LLM2Everything.Core;

namespace LLM2Everything.Infrastructure;

public sealed class AppLogger
{
    private readonly string _logFolder = Path.Combine(ProductInfo.LocalAppDataFolder, "logs");

    public AppLogger()
    {
        Directory.CreateDirectory(_logFolder);
        Cleanup();
    }

    public Task InfoAsync(string message, object? data = null) => WriteAsync("normal", message, data);
    public Task ErrorAsync(string message, Exception ex, object? data = null) => WriteAsync("error", message, new { exception = ex.ToString(), data });
    public Task LlmAsync(object data) => WriteAsync("llm", "LLM入出力", data);

    private async Task WriteAsync(string kind, string message, object? data)
    {
        var path = Path.Combine(_logFolder, $"{kind}-{DateTime.Now:yyyyMMdd}.jsonl");
        var line = JsonSerializer.Serialize(new
        {
            at = DateTimeOffset.Now,
            app = ProductInfo.LogAppName,
            kind,
            message,
            data
        });
        await File.AppendAllTextAsync(path, line + Environment.NewLine);
    }

    public string LogFolder => _logFolder;

    private void Cleanup()
    {
        foreach (var file in Directory.EnumerateFiles(_logFolder, "*.jsonl"))
        {
            var name = Path.GetFileName(file);
            var days = name.StartsWith("normal-") ? 7 : 30;
            if (File.GetLastWriteTime(file) < DateTime.Now.AddDays(-days))
                File.Delete(file);
        }
    }
}
