using System.Text.Json;
using LLM2Everything.Core;

namespace LLM2Everything.Infrastructure;

public sealed class JsonAtomicFileStore
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public async Task<T?> LoadAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            return default;
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, _options, cancellationToken);
        }
        catch (JsonException)
        {
            var backup = path + ".bak";
            if (!File.Exists(backup))
                throw;
            await using var stream = File.OpenRead(backup);
            return await JsonSerializer.DeserializeAsync<T>(stream, _options, cancellationToken);
        }
    }

    public async Task SaveAsync<T>(string path, T value, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        var bak = path + ".bak";
        await using (var stream = File.Create(tmp))
            await JsonSerializer.SerializeAsync(stream, value, _options, cancellationToken);

        if (File.Exists(path))
            File.Copy(path, bak, overwrite: true);
        File.Move(tmp, path, overwrite: true);
    }
}

public abstract class JsonRepositoryBase<T>
{
    private readonly JsonAtomicFileStore _store;
    private readonly string _path;
    private readonly Func<T> _factory;

    protected JsonRepositoryBase(JsonAtomicFileStore store, string fileName, Func<T> factory)
    {
        _store = store;
        _path = Path.Combine(ProductInfo.LocalAppDataFolder, fileName);
        _factory = factory;
    }

    protected async Task<T> LoadValueAsync(CancellationToken cancellationToken = default) =>
        await _store.LoadAsync<T>(_path, cancellationToken) ?? _factory();

    protected Task SaveValueAsync(T value, CancellationToken cancellationToken = default) =>
        _store.SaveAsync(_path, value, cancellationToken);
}

public sealed class SettingsRepository : JsonRepositoryBase<AppSettings>, ISettingsRepository
{
    public SettingsRepository(JsonAtomicFileStore store) : base(store, "settings.json", () => new AppSettings()) { }
    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => LoadValueAsync(cancellationToken);
    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) => SaveValueAsync(settings, cancellationToken);
}

public sealed class HistoryRepository : JsonRepositoryBase<List<HistoryEntry>>, IHistoryRepository
{
    public HistoryRepository(JsonAtomicFileStore store) : base(store, "history.json", () => []) { }
    public async Task<IReadOnlyList<HistoryEntry>> LoadAsync(CancellationToken cancellationToken = default) => await LoadValueAsync(cancellationToken);
    public Task SaveAsync(IReadOnlyList<HistoryEntry> history, CancellationToken cancellationToken = default) =>
        SaveValueAsync(history.Take(20).ToList(), cancellationToken);
}

public sealed class CacheRepository : JsonRepositoryBase<List<CacheEntry>>, ICacheRepository
{
    public CacheRepository(JsonAtomicFileStore store) : base(store, "cache.json", () => []) { }
    public async Task<IReadOnlyList<CacheEntry>> LoadAsync(CancellationToken cancellationToken = default) => await LoadValueAsync(cancellationToken);
    public Task SaveAsync(IReadOnlyList<CacheEntry> entries, CancellationToken cancellationToken = default) =>
        SaveValueAsync(entries.Take(100).ToList(), cancellationToken);
}

public sealed class FileTypeRepository : JsonRepositoryBase<List<FileTypeDefinition>>, IFileTypeRepository
{
    public FileTypeRepository(JsonAtomicFileStore store) : base(store, "filetypes.json", DefaultFileTypes.Create) { }
    public async Task<IReadOnlyList<FileTypeDefinition>> LoadAsync(CancellationToken cancellationToken = default) => await LoadValueAsync(cancellationToken);
    public Task SaveAsync(IReadOnlyList<FileTypeDefinition> fileTypes, CancellationToken cancellationToken = default) =>
        SaveValueAsync(fileTypes.ToList(), cancellationToken);
}
