using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.ComponentModel;
using LLM2Everything.App.ViewModels;
using LLM2Everything.Core;
using LLM2Everything.Infrastructure;

namespace LLM2Everything.App;

public partial class MainWindow : Window
{
    private string? _sortProperty;
    private ListSortDirection _sortDirection = ListSortDirection.Ascending;

    public MainWindow()
    {
        InitializeComponent();
        var store = new JsonAtomicFileStore();
        var vm = new MainViewModel(
            new SettingsRepository(store),
            new HistoryRepository(store),
            new CacheRepository(store),
            new FileTypeRepository(store),
            new EverythingInstallationDetector(),
            new EverythingProcessService(),
            new OllamaClient());
        DataContext = vm;
        Loaded += async (_, _) => await vm.InitializeAsync();
    }

    private void Results_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is System.Windows.Controls.ListView { SelectedItem: SearchResultItem item })
            vm.OpenItemCommand.Execute(item);
    }

    private void History_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.RerunSelectedHistoryCommand.CanExecute(null))
            vm.RerunSelectedHistoryCommand.Execute(null);
    }

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader { Column: { Header: string header } } || DataContext is not MainViewModel vm)
            return;
        var property = header switch
        {
            "ファイル名" => nameof(SearchResultItem.FileName),
            "フルパス" => nameof(SearchResultItem.FullPath),
            "拡張子" => nameof(SearchResultItem.Extension),
            "サイズ" => nameof(SearchResultItem.SizeBytes),
            "更新日時" => nameof(SearchResultItem.ModifiedAt),
            _ => null
        };
        if (property is null) return;
        _sortDirection = _sortProperty == property && _sortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        _sortProperty = property;
        var view = CollectionViewSource.GetDefaultView(vm.Results);
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(property, _sortDirection));
    }
}
