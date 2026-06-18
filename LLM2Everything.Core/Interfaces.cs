namespace LLM2Everything.Core;

public interface ISearchIntentParser
{
    Task<SearchParseResult> ParseAsync(SearchInput input, CancellationToken cancellationToken);
}

public sealed class SearchInput
{
    public string Text { get; init; } = "";
    public IReadOnlyList<string> SelectedFolders { get; init; } = [];
    public FileTypeMode FileTypeMode { get; init; } = FileTypeMode.None;
    public IReadOnlyList<string> SelectedFileTypes { get; init; } = [];
    public IReadOnlyList<string> SelectedExtensions { get; init; } = [];
    public IReadOnlyList<FileTypeDefinition> FileTypeDefinitions { get; init; } = [];
    public DateTimeOffset Now { get; init; } = DateTimeOffset.Now;
    public string TimeZoneId { get; init; } = "Asia/Tokyo";
    public string ModelName { get; init; } = "";
}

public interface IEverythingSearchService
{
    Task<EsSearchResponse> SearchAsync(EsSearchRequest request, CancellationToken cancellationToken);
    Task<string> GetVersionAsync(CancellationToken cancellationToken);
    Task<bool> TestAsync(CancellationToken cancellationToken);
}

public interface IEverythingProcessService
{
    bool IsEverythingRunning();
    Task<bool> StartEverythingAsync(string path, CancellationToken cancellationToken);
}

public interface IEverythingInstallationDetector
{
    string? FindEsExe();
    string? FindEverythingExe();
}

public interface IOllamaClient
{
    Task<IReadOnlyList<string>> GetModelsAsync(CancellationToken cancellationToken);
    Task<string> GenerateAsync(OllamaGenerateRequest request, CancellationToken cancellationToken);
}

public sealed class OllamaGenerateRequest
{
    public string Url { get; init; } = "http://localhost:11434";
    public string Model { get; init; } = "";
    public string Prompt { get; init; } = "";
    public int TimeoutSeconds { get; init; } = 60;
}

public interface ISettingsRepository
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public interface IHistoryRepository
{
    Task<IReadOnlyList<HistoryEntry>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IReadOnlyList<HistoryEntry> history, CancellationToken cancellationToken = default);
}

public interface ICacheRepository
{
    Task<IReadOnlyList<CacheEntry>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IReadOnlyList<CacheEntry> entries, CancellationToken cancellationToken = default);
}

public interface IFileTypeRepository
{
    Task<IReadOnlyList<FileTypeDefinition>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IReadOnlyList<FileTypeDefinition> fileTypes, CancellationToken cancellationToken = default);
}
