using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLM2Everything.Core;
using LLM2Everything.Infrastructure;
using Microsoft.Win32;

namespace LLM2Everything.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly IFileTypeRepository _fileTypeRepository;
    private readonly IEverythingInstallationDetector _detector;
    private readonly IOllamaClient _ollamaClient;
    private readonly AppLogger _logger;

    [ObservableProperty] private string statusText = "";
    public AppSettings Settings { get; }
    public ObservableCollection<FileTypeDefinitionRow> FileTypes { get; } = [];
    public ObservableCollection<string> Models { get; } = [];
    public IReadOnlyList<int> ResultLimitOptions { get; } = [100, 500, 1000, 5000];

    public SettingsViewModel(AppSettings settings, ISettingsRepository settingsRepository, IFileTypeRepository fileTypeRepository, IEverythingInstallationDetector detector, IOllamaClient ollamaClient, AppLogger logger)
    {
        Settings = settings;
        _settingsRepository = settingsRepository;
        _fileTypeRepository = fileTypeRepository;
        _detector = detector;
        _ollamaClient = ollamaClient;
        _logger = logger;
    }

    public async Task LoadAsync()
    {
        FileTypes.Clear();
        foreach (var ft in await _fileTypeRepository.LoadAsync()) FileTypes.Add(FileTypeDefinitionRow.FromDefinition(ft));
    }

    [RelayCommand] private async Task SaveAsync()
    {
        await _settingsRepository.SaveAsync(Settings);
        await _fileTypeRepository.SaveAsync(FileTypes.Select(f => f.ToDefinition()).ToList());
        StatusText = "設定を保存しました。";
    }

    [RelayCommand] private void AutoDetect()
    {
        Settings.EsExePath = _detector.FindEsExe() ?? Settings.EsExePath;
        Settings.EverythingPath = _detector.FindEverythingExe() ?? Settings.EverythingPath;
        var es = string.IsNullOrWhiteSpace(Settings.EsExePath) ? "es.exe: 未検出" : $"es.exe: {Settings.EsExePath}";
        var everything = string.IsNullOrWhiteSpace(Settings.EverythingPath) ? "Everything.exe: 未検出" : $"Everything.exe: {Settings.EverythingPath}";
        StatusText = $"自動検出を実行しました。{es} / {everything}";
        OnPropertyChanged(nameof(Settings));
    }

    [RelayCommand] private void BrowseEsExe()
    {
        var path = BrowseExecutable("es.exe を選択", "es.exe|es.exe|実行ファイル|*.exe|すべてのファイル|*.*");
        if (path is null) return;
        Settings.EsExePath = path;
        OnPropertyChanged(nameof(Settings));
        StatusText = "es.exe パスを設定しました。";
    }

    [RelayCommand] private void BrowseEverythingExe()
    {
        var path = BrowseExecutable("Everything.exe を選択", "Everything.exe|Everything.exe|実行ファイル|*.exe|すべてのファイル|*.*");
        if (path is null) return;
        Settings.EverythingPath = path;
        OnPropertyChanged(nameof(Settings));
        StatusText = "Everything.exe パスを設定しました。";
    }

    [RelayCommand] private async Task LoadModelsAsync()
    {
        try
        {
            Models.Clear();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Settings.OllamaTimeoutSeconds));
            foreach (var model in await _ollamaClient.GetModelsAsync(Settings.OllamaUrl, cts.Token)) Models.Add(model);
            StatusText = $"{Models.Count}件のモデルを取得しました。";
        }
        catch (Exception ex)
        {
            StatusText = "Ollama接続に失敗しました: " + ex.Message;
            await _logger.ErrorAsync("Ollamaモデル取得失敗", ex);
        }
    }

    [RelayCommand] private async Task TestEverythingAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Settings.EsExePath) || !File.Exists(Settings.EsExePath))
            {
                StatusText = "es.exe が見つかりません。参照または自動検出で設定してください。";
                return;
            }
            var service = new EsExeSearchService(() => Settings.EsExePath);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var version = await service.GetVersionAsync(cts.Token);
            StatusText = string.IsNullOrWhiteSpace(version) ? "es.exe を確認しました。" : $"es.exe を確認しました: {version}";
        }
        catch (Exception ex)
        {
            StatusText = "es.exe 確認に失敗しました: " + ex.Message;
            await _logger.ErrorAsync("es.exe確認失敗", ex);
        }
    }

    [RelayCommand] private async Task TestOllamaAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Settings.OllamaTimeoutSeconds));
            var models = await _ollamaClient.GetModelsAsync(Settings.OllamaUrl, cts.Token);
            StatusText = $"Ollama に接続できました。モデル数: {models.Count}";
        }
        catch (Exception ex)
        {
            StatusText = "Ollama接続に失敗しました: " + ex.Message;
            await _logger.ErrorAsync("Ollama接続テスト失敗", ex);
        }
    }

    [RelayCommand] private void OpenLogFolder() =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_logger.LogFolder) { UseShellExecute = true });

    private static string? BrowseExecutable(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}

public sealed partial class FileTypeDefinitionRow : ObservableObject
{
    [ObservableProperty] private string name = "";
    [ObservableProperty] private string extensionsText = "";

    public static FileTypeDefinitionRow FromDefinition(FileTypeDefinition definition) => new()
    {
        Name = definition.Name,
        ExtensionsText = string.Join(", ", definition.Extensions)
    };

    public FileTypeDefinition ToDefinition() => new()
    {
        Name = Name.Trim(),
        Extensions = ExtensionsText.Split([',', ';', ' ', '　'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(DefaultFileTypes.NormalizeExtension)
            .Where(e => e.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
    };
}
