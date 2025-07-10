using Avalonia.Controls;
using VpnClient.UI.ViewModels;

namespace VpnClient.UI;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
