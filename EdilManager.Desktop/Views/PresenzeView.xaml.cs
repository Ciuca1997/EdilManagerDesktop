using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using EdilManager.Desktop.ViewModels;

namespace EdilManager.Desktop.Views;

public partial class PresenzeView : UserControl
{
    private static readonly string[] Iniziali = ["D", "L", "M", "M", "G", "V", "S"];

    private PresenzeViewModel? _vm;

    public PresenzeView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // Colonna di un giorno del mese; Giorno è 1-based.
    private sealed class ColonnaGiorno : DataGridTextColumn
    {
        public int Giorno { get; init; }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.StrutturaCambiata -= OnStrutturaCambiata;
        _vm = DataContext as PresenzeViewModel;
        if (_vm is null) return;
        _vm.StrutturaCambiata += OnStrutturaCambiata;
        CostruisciColonne();
    }

    private void OnStrutturaCambiata(object? sender, EventArgs e) => CostruisciColonne();

    // Il numero di colonne e i giorni della settimana cambiano da un mese all'altro: si ricostruiscono ogni volta.
    private void CostruisciColonne()
    {
        if (_vm is null) return;
        Griglia.Columns.Clear();

        var cellaBase = (Style)Application.Current.FindResource(typeof(DataGridCell));
        var centrato = new Style(typeof(TextBlock));
        centrato.Setters.Add(new Setter(HorizontalAlignmentProperty, HorizontalAlignment.Center));
        centrato.Setters.Add(new Setter(VerticalAlignmentProperty, VerticalAlignment.Center));
        // Intestazioni dei giorni strette e centrate: con il margine predefinito i numeri a due cifre verrebbero tagliati.
        var intestazioneGiorno = new Style(typeof(DataGridColumnHeader),
            (Style)Application.Current.FindResource(typeof(DataGridColumnHeader)));
        intestazioneGiorno.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        intestazioneGiorno.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        var modifica = new Style(typeof(TextBox));
        modifica.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        modifica.Setters.Add(new Setter(VerticalAlignmentProperty, VerticalAlignment.Center));
        modifica.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        modifica.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));

        Griglia.Columns.Add(new DataGridTextColumn
        {
            Header = "Dipendente", Binding = new Binding(nameof(RigaPresenze.Nome)), IsReadOnly = true, Width = 190,
        });

        // I totali stanno subito dopo il nome (e restano fermi quando si scorre tra i giorni).
        var destra = (Style)Application.Current.FindResource("RightCell");
        var destraCella = (Style)Application.Current.FindResource("RightCellContainer");
        var intestazioneDestra = (Style)Application.Current.FindResource("RightHeader");
        Griglia.Columns.Add(new DataGridTextColumn
        {
            Header = "Ord.", Binding = new Binding(nameof(RigaPresenze.TotOrd)) { Mode = BindingMode.OneWay }, IsReadOnly = true,
            Width = 64, HeaderStyle = intestazioneDestra, ElementStyle = destra, CellStyle = destraCella,
        });
        Griglia.Columns.Add(new DataGridTextColumn
        {
            Header = "Straord.", Binding = new Binding(nameof(RigaPresenze.TotStr)) { Mode = BindingMode.OneWay }, IsReadOnly = true,
            Width = 92, HeaderStyle = intestazioneDestra, ElementStyle = destra, CellStyle = destraCella,
        });

        for (var giorno = 1; giorno <= _vm.GiorniNelMese; giorno++)
        {
            var indice = giorno - 1;
            var data = new DateTime(_vm.Anno, _vm.Mese, giorno);
            var weekend = data.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            var cella = new Style(typeof(DataGridCell), cellaBase);
            cella.Setters.Add(new Setter(Control.BackgroundProperty, new Binding($"Giorni[{indice}].Sfondo")));
            cella.Setters.Add(new Setter(ToolTipProperty, new Binding($"Giorni[{indice}].Suggerimento")));
            cella.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
            // Il contenitore della cella si restringe al contenuto: va centrato lui, non solo il testo.
            cella.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));

            Griglia.Columns.Add(new ColonnaGiorno
            {
                Giorno = giorno,
                Header = new TextBlock
                {
                    Text = $"{giorno}\n{Iniziali[(int)data.DayOfWeek]}",
                    TextAlignment = TextAlignment.Center,
                    Foreground = weekend ? Brushes.IndianRed : Brushes.Black,
                },
                Binding = new Binding($"Giorni[{indice}].Testo") { Mode = BindingMode.TwoWay },
                Width = 40,
                HeaderStyle = intestazioneGiorno,
                ElementStyle = centrato,
                EditingElementStyle = modifica,
                CellStyle = cella,
            });
        }
    }

    // Celle selezionate della griglia (solo giorni, non nome e totali).
    private List<CellaGiorno> CelleSelezionate()
    {
        var celle = new List<CellaGiorno>();
        foreach (var info in Griglia.SelectedCells)
            if (info.Column is ColonnaGiorno colonna && info.Item is RigaPresenze riga)
                celle.Add(riga.Giorni[colonna.Giorno - 1]);
        return celle;
    }

    // Un giorno con più cantieri non si modifica "al volo": si apre la finestra con una riga per cantiere.
    private void OnBeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (e.Column is not ColonnaGiorno colonna || e.Row.Item is not RigaPresenze riga) return;
        var cella = riga.Giorni[colonna.Giorno - 1];
        if (cella.NonModificabile)
        {
            // Giorno precedente all'assunzione: nessuna modifica (il suggerimento della cella spiega perché).
            e.Cancel = true;
            return;
        }
        if (cella.Presenze.Count <= 1) return;

        e.Cancel = true;
        Dispatcher.BeginInvoke(async () => await _vm!.ApriGiornoAsync(cella));
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Canc cancella le ore delle celle selezionate (quando non si sta scrivendo dentro una cella).
        if (e.Key != Key.Delete || e.OriginalSource is TextBox || _vm is null) return;
        e.Handled = true;
        await _vm.CancellaOreAsync(CelleSelezionate());
    }

    // Il clic destro su una cella fuori dalla selezione la seleziona; dentro la selezione la mantiene intera.
    private void OnPreviewRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindParent<DataGridCell>(e.OriginalSource as DependencyObject) is not { } cella) return;
        if (cella.IsSelected) return;
        Griglia.SelectedCells.Clear();
        cella.Focus();
        Griglia.CurrentCell = new DataGridCellInfo(cella);
        Griglia.SelectedCells.Add(new DataGridCellInfo(cella));
    }

    private void OnMenuOpened(object sender, RoutedEventArgs e)
    {
        MenuCantiere.Items.Clear();
        if (_vm is null) return;
        foreach (var cantiere in _vm.Cantieri)
        {
            var voce = new MenuItem { Header = cantiere.Nome, Tag = cantiere };
            voce.Click += OnAssegnaCantiere;
            MenuCantiere.Items.Add(voce);
        }
    }

    private async void OnAssegnaCantiere(object sender, RoutedEventArgs e)
    {
        if (_vm is null || sender is not MenuItem { Tag: CantiereOption cantiere }) return;
        await _vm.AssegnaCantiereAsync(CelleSelezionate(), cantiere);
    }

    private async void OnDettaglioGiorno(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        if (CelleSelezionate().FirstOrDefault() is { } cella) await _vm.ApriGiornoAsync(cella);
    }

    private async void OnCancellaOre(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        await _vm.CancellaOreAsync(CelleSelezionate());
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null and not T)
            child = child is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(child) : LogicalTreeHelper.GetParent(child);
        return child as T;
    }
}
