using System.Windows;
using System.Windows.Controls;
using EdilManager.Desktop.ViewModels;

namespace EdilManager.Desktop.Views;

public partial class DipendentiView : UserControl
{
    public DipendentiView() => InitializeComponent();

    private async void OnRetryClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is DipendentiViewModel vm) await vm.ReloadAsync();
    }
}
