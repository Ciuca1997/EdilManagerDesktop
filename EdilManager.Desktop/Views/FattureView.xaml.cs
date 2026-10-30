using System.Windows.Controls;
using System.Windows.Input;
using EdilManager.Desktop.ViewModels;

namespace EdilManager.Desktop.Views;

public partial class FattureView : UserControl
{
    public FattureView() => InitializeComponent();

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow { Item: FatturaRow row } && DataContext is FattureViewModel vm)
            vm.OpenCommand.Execute(row);
    }
}
