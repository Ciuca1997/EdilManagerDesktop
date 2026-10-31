using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EdilManager.Core.Entities;
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

    // Il clic destro non seleziona la riga: senza questo il menu agirebbe sulla selezione precedente, magari altre fatture.
    private void OnGrigliaRightClick(object sender, MouseButtonEventArgs e)
    {
        var elemento = e.OriginalSource as DependencyObject;
        while (elemento is not null and not DataGridRow)
            elemento = elemento is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(elemento)
                : LogicalTreeHelper.GetParent(elemento);
        if (elemento is not DataGridRow { Item: FatturaRow } riga) return;
        if (riga.IsSelected) return;

        Griglia.SelectedItems.Clear();
        riga.IsSelected = true;
    }

    private async void OnAssegnaCantiere(object sender, RoutedEventArgs e)
    {
        if (DataContext is not FattureViewModel vm) return;
        var selezione = Griglia.SelectedItems.OfType<FatturaRow>().ToList();
        if (selezione.Count > 0) await vm.AssegnaCantiereAsync(selezione);
    }

    // Menu del clic destro: agisce su tutte le righe selezionate (Tag = nome dello stato).
    private async void OnImpostaStato(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string nome } || !Enum.TryParse<StatoPagamento>(nome, out var stato)) return;
        if (DataContext is not FattureViewModel vm) return;

        var selezione = Griglia.SelectedItems.OfType<FatturaRow>().ToList();
        if (selezione.Count > 0) await vm.ImpostaStatoAsync(selezione, stato);
    }
}
