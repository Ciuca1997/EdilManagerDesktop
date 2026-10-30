using System.Windows.Controls;
using System.Windows.Input;
using EdilManager.Desktop.ViewModels;

namespace EdilManager.Desktop.Views;

public partial class CantiereDetailView : UserControl
{
    public CantiereDetailView() => InitializeComponent();

    private void OnFatturaDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow { Item: FatturaRow row } && DataContext is CantiereDetailViewModel vm)
            vm.OpenFatturaCommand.Execute(row);
    }

    private void OnFornitoreDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow { Item: FornitoreRiepilogoRow row } && DataContext is CantiereDetailViewModel vm)
            vm.OpenFornitoreCommand.Execute(row);
    }
}
