using System.Windows;
using EdilManager.Desktop.ViewModels;

namespace EdilManager.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.OnWindowLoadedAsync();
    }
}
