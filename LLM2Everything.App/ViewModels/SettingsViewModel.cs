using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLM2Everything.Core;
using LLM2Everything.Infrastructure;

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
    public ObservableCollection<FileTypeDefinition> FileTypes { get; } = [];
    public ObservableCollection<string> Models { get; } = [];

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
        foreach (var ft in await _fileTypeRepository.LoadAsync()) FileTypes.Add(ft);
    }

    [RelayCommand] private async Task SaveAsync()
    {
        await _settingsRepository.SaveAsync(Settings);
        await _fileTypeRepository.SaveAsync(FileTypes);
        StatusText = "設定を保存しました。";
    }

    [RelayCommand] private void AutoDetect()
    {
        Settings.EsExePath = _detector.FindEsExe() ?? Settings.EsExePath;
        Settings.EverythingPath = _detector.FindEverythingExe() ?? Settings.EverythingPath;
        StatusText = "自動検出を実行しました。";
        OnPropertyChanged(nameof(Settings));
    }

    [RelayCommand] private async Task LoadModelsAsync()
    {
        try
        {
            Models.Clear();
            foreach (var model in await _ollamaClient.GetModelsAsync(CancellationToken.None)) Models.Add(model);
            StatusText = $"{Models.Count}件のモデルを取得しました。";
        }
        catch (Exception ex)
        {
            StatusText = "Ollama接続に失敗しました: " + ex.Message;
            await _logger.ErrorAsync("Ollamaモデル取得失敗", ex);
        }
    }

    [RelayCommand] private void OpenLogFolder() =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_logger.LogFolder) { UseShellExecute = true });
}
