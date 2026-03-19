using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using VpnClient.UI.ViewModels;

namespace VpnClient.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel vm)
        : this()
    {
        DataContext = vm;
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    private async void OnImportConfigClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || StorageProvider is null)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Добавить сервер из файла",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("VPN-конфиги")
                {
                    Patterns = ["*.vpn", "*.conf"]
                }
            ]
        });

        var selectedFile = files.FirstOrDefault();
        var path = selectedFile?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await viewModel.ImportConfigAsync(path);
        }
    }

    private async void OnPrimaryActionClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (viewModel.HasNoProfiles)
        {
            OnImportConfigClick(sender, e);
            return;
        }

        await viewModel.ExecutePrimaryActionAsync();
    }
}
