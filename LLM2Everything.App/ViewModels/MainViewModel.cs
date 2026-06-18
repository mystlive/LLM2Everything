using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLM2Everything.Core;
using LLM2Everything.Infrastructure;

namespace LLM2Everything.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly IHistoryRepository _historyRepository;
    private readonly ICacheRepository _cacheRepository;
    private readonly IFileTypeRepository _fileTypeRepository;
    private readonly IEverythingInstallationDetector _detector;
    private readonly IEverythingProcessService _processService;
    private readonly IOllamaClient _ollamaClient;
    private readonly EverythingQueryBuilder _queryBuilder = new();
    private readonly SearchIntentValidator _validator = new();
    private readonly AppLogger _logger = new();
    private CancellationTokenSource? _cts;
    private AppSettings _settings = new();
    private List<FileTypeDefinition> _fileTypes = [];
    private IEverythingSearchService? _searchService;

    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string folderText = "";
    [ObservableProperty] private string extensionText = "";
    [ObservableProperty] private string statusText = "起動中";
    [ObservableProperty] private string detailText = "";
    [ObservableProperty] private string editableEverythingQuery = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isDetailVisible;
    [ObservableProperty] private FileTypeModeOption selectedFileTypeModeOption;
    [ObservableProperty] private FileTypeDefinition? selectedFileType;
    [ObservableProperty] private string elapsedText = "";
    [ObservableProperty] private string warningText = "";

    public ObservableCollection<SearchResultItem> Results { get; } = [];
    public ObservableCollection<HistoryEntry> History { get; } = [];
    public ObservableCollection<FileTypeDefinition> FileTypes { get; } = [];
    public bool IsFileTypeSelectionEnabled => !IsBusy && SelectedFileTypeModeOption.Value == FileTypeMode.FileType;
    public bool IsExtensionSelectionEnabled => !IsBusy && SelectedFileTypeModeOption.Value == FileTypeMode.Extension;
    public IReadOnlyList<FileTypeModeOption> FileTypeModeOptions { get; } =
    [
        new("指定なし", FileTypeMode.None),
        new("ファイルタイプ", FileTypeMode.FileType),
        new("拡張子指定", FileTypeMode.Extension)
    ];

    public MainViewModel(ISettingsRepository settingsRepository, IHistoryRepository historyRepository, ICacheRepository cacheRepository, IFileTypeRepository fileTypeRepository, IEverythingInstallationDetector detector, IEverythingProcessService processService, IOllamaClient ollamaClient)
    {
        selectedFileTypeModeOption = FileTypeModeOptions[0];
        _settingsRepository = settingsRepository;
        _historyRepository = historyRepository;
        _cacheRepository = cacheRepository;
        _fileTypeRepository = fileTypeRepository;
        _detector = detector;
        _processService = processService;
        _ollamaClient = ollamaClient;
    }

    public async Task InitializeAsync()
    {
        _settings = await _settingsRepository.LoadAsync();
        if (string.IsNullOrWhiteSpace(_settings.EsExePath)) _settings.EsExePath = _detector.FindEsExe() ?? "";
        if (string.IsNullOrWhiteSpace(_settings.EverythingPath)) _settings.EverythingPath = _detector.FindEverythingExe() ?? "";
        _searchService = new EsExeSearchService(() => _settings.EsExePath);
        _fileTypes = (await _fileTypeRepository.LoadAsync()).ToList();
        if (_fileTypes.Count == 0) _fileTypes = DefaultFileTypes.Create();
        FileTypes.Clear();
        foreach (var ft in _fileTypes) FileTypes.Add(ft);
        foreach (var h in await _historyRepository.LoadAsync()) History.Add(h);
        WarningText = string.IsNullOrWhiteSpace(_settings.EsExePath) ? "es.exe が未設定です。設定画面で指定してください。" : "";
        StatusText = "待機中";
        _ = CheckOllamaAvailabilityAsync();
    }

    private async Task CheckOllamaAvailabilityAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await _ollamaClient.GetModelsAsync(cts.Token);
        }
        catch
        {
            WarningText = AppendWarning(WarningText, "Ollama に接続できません。高速ルールで確定できない検索は警告になります。");
        }
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task SearchAsync()
    {
        _cts = new CancellationTokenSource();
        await RunSearchPipelineAsync(_cts.Token);
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task SearchEditedQueryAsync()
    {
        _cts = new CancellationTokenSource();
        await ExecuteEverythingAsync(EditableEverythingQuery, ParseMethod.Manual, _cts.Token);
    }

    [RelayCommand] private void Cancel() => _cts?.Cancel();
    [RelayCommand] private void ToggleDetail() => IsDetailVisible = !IsDetailVisible;

    partial void OnSelectedFileTypeModeOptionChanged(FileTypeModeOption value)
    {
        OnPropertyChanged(nameof(IsFileTypeSelectionEnabled));
        OnPropertyChanged(nameof(IsExtensionSelectionEnabled));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsFileTypeSelectionEnabled));
        OnPropertyChanged(nameof(IsExtensionSelectionEnabled));
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private void OpenSettings()
    {
        var vm = new SettingsViewModel(_settings, _settingsRepository, _fileTypeRepository, _detector, _ollamaClient, _logger);
        var window = new SettingsWindow { DataContext = vm, Owner = Application.Current.MainWindow };
        window.ShowDialog();
        _settings = vm.Settings;
        _searchService = new EsExeSearchService(() => _settings.EsExePath);
    }

    [RelayCommand] private void OpenItem(SearchResultItem? item)
    {
        if (item is not null) Process.Start(new ProcessStartInfo(item.FullPath) { UseShellExecute = true });
    }

    [RelayCommand] private void OpenParent(SearchResultItem? item)
    {
        if (item is not null) Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.FullPath}\"") { UseShellExecute = true });
    }

    [RelayCommand] private void CopyPath(SearchResultItem? item)
    {
        if (item is not null) Clipboard.SetText(item.FullPath);
    }

    [RelayCommand] private async Task RerunHistoryAsync(HistoryEntry? entry)
    {
        if (entry is null) return;
        SearchText = entry.Input;
        EditableEverythingQuery = entry.EverythingQuery;
        _cts = new CancellationTokenSource();
        await ExecuteEverythingAsync(entry.EverythingQuery, ParseMethod.Cache, _cts.Token);
    }

    private bool CanOperate() => !IsBusy;

    private async Task RunSearchPipelineAsync(CancellationToken token)
    {
        try
        {
            SetBusy("検索条件を解析中", TimeSpan.FromSeconds(_settings.OllamaTimeoutSeconds));
            var input = CreateInput();
            var cache = await _cacheRepository.LoadAsync(token);
            var key = CacheKey(input);
            var cached = cache.FirstOrDefault(c => c.Key == key && (c.JstDateKey is null || c.JstDateKey == JstDateKey()));
            SearchParseResult result;
            if (cached is not null)
            {
                result = cached.Result;
                result.Method = ParseMethod.Cache;
            }
            else
            {
                var fast = new FastRuleSearchIntentParser(_queryBuilder, _validator);
                result = await fast.ParseAsync(input, token);
                if (result.Decision == SearchDecision.Ambiguous)
                {
                    SetBusy("LLMで検索条件を変換中", TimeSpan.FromSeconds(_settings.OllamaTimeoutSeconds));
                    result = await new OllamaSearchIntentParser(_ollamaClient, _queryBuilder, _validator, _settings).ParseAsync(input, token);
                }
                if (result.Decision == SearchDecision.Searchable)
                {
                    await _cacheRepository.SaveAsync(cache.Where(c => c.Key != key).Prepend(new CacheEntry
                    {
                        Key = key,
                        Result = result,
                        CreatedAt = DateTimeOffset.Now,
                        JstDateKey = result.Intent.ContainsRelativeDate ? JstDateKey() : null
                    }).Take(100).ToList(), token);
                }
            }
            DetailText = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            EditableEverythingQuery = result.EverythingQuery;
            if (result.Decision != SearchDecision.Searchable)
            {
                StatusText = string.IsNullOrWhiteSpace(result.Intent.UserReason) ? "ファイル検索条件として解釈できませんでした。" : result.Intent.UserReason;
                return;
            }
            await ExecuteEverythingAsync(result.EverythingQuery, result.Method, token);
        }
        catch (OperationCanceledException) { StatusText = "キャンセルしました。"; }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            await _logger.ErrorAsync("検索処理でエラー", ex);
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsFileTypeSelectionEnabled));
            OnPropertyChanged(nameof(IsExtensionSelectionEnabled));
            SearchCommand.NotifyCanExecuteChanged();
            SearchEditedQueryCommand.NotifyCanExecuteChanged();
            OpenSettingsCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task ExecuteEverythingAsync(string query, ParseMethod method, CancellationToken token)
    {
        if (_searchService is null) return;
        if (!_processService.IsEverythingRunning())
        {
            if (string.IsNullOrWhiteSpace(_settings.EverythingPath))
            {
                StatusText = "Everything が起動していません。設定画面で Everything.exe を指定してください。";
                return;
            }
            var answer = MessageBox.Show("Everything が起動していません。起動しますか？", ProductInfo.WindowTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                StatusText = "Everything の起動が承認されなかったため検索を中止しました。";
                return;
            }
            await _processService.StartEverythingAsync(_settings.EverythingPath, token);
        }
        SetBusy("es.exeで検索中", TimeSpan.FromSeconds(_settings.EverythingTimeoutSeconds));
        var response = await _searchService.SearchAsync(new EsSearchRequest { Query = query, Limit = _settings.ResultLimit, Timeout = TimeSpan.FromSeconds(_settings.EverythingTimeoutSeconds) }, token);
        Results.Clear();
        foreach (var item in response.Results) Results.Add(item);
        StatusText = response.LimitReached ? $"{response.Results.Count}件以上の結果があります。表示件数を制限しています。" : $"{response.Results.Count}件見つかりました。";
        var history = History.Prepend(new HistoryEntry { Input = SearchText, SelectedFolders = SplitFolders().ToList(), FileTypes = SelectedFileType is null ? [] : [SelectedFileType.Name], Extensions = SplitExtensions().ToList(), EverythingQuery = query, ExecutedAt = DateTimeOffset.Now, ResultCount = response.Results.Count, Method = method }).Take(20).ToList();
        History.Clear();
        foreach (var entry in history) History.Add(entry);
        await _historyRepository.SaveAsync(history, token);
    }

    private SearchInput CreateInput() => new()
    {
        Text = SearchText,
        SelectedFolders = SplitFolders().ToList(),
        FileTypeMode = SelectedFileTypeModeOption.Value,
        SelectedFileTypes = SelectedFileType is null ? [] : [SelectedFileType.Name],
        SelectedExtensions = SplitExtensions().ToList(),
        FileTypeDefinitions = _fileTypes,
        Now = DateTimeOffset.Now,
        TimeZoneId = _settings.TimeZoneId,
        ModelName = _settings.OllamaModel
    };

    private IEnumerable<string> SplitFolders() => FolderText.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    private IEnumerable<string> SplitExtensions() => ExtensionText.Split([',', ';', ' ', '　'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(DefaultFileTypes.NormalizeExtension).Where(e => e.Length > 0);
    private void SetBusy(string status, TimeSpan timeout) { IsBusy = true; StatusText = status; ElapsedText = $"0秒経過 / 残り{(int)timeout.TotalSeconds}秒"; }
    private static string JstDateKey() => DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(9)).ToString("yyyyMMdd");
    private static string CacheKey(SearchInput input) => JsonSerializer.Serialize(new { text = input.Text.Trim().ToLowerInvariant(), input.SelectedFolders, input.FileTypeMode, input.SelectedFileTypes, input.SelectedExtensions, input.TimeZoneId, ProductInfo.PromptVersion, input.ModelName });
    private static string AppendWarning(string current, string message) =>
        string.IsNullOrWhiteSpace(current) ? message : current.Contains(message, StringComparison.Ordinal) ? current : $"{current} {message}";
}

public sealed record FileTypeModeOption(string Label, FileTypeMode Value);
