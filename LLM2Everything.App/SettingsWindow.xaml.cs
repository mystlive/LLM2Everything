using System.Windows;
using LLM2Everything.App.ViewModels;

namespace LLM2Everything.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is SettingsViewModel vm)
                await vm.LoadAsync();
        };
    }
}
