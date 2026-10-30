using System.Windows.Controls;
using System.Windows.Input;
using EdilManager.Desktop.ViewModels;

namespace EdilManager.Desktop.Views;

public partial class FornitoreDetailView : UserControl
{
    public FornitoreDetailView() => InitializeComponent();

    private void OnFatturaDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow { Item: FatturaRow row } && DataContext is FornitoreDetailViewModel vm)
            vm.OpenFatturaCommand.Execute(row);
    }
}
