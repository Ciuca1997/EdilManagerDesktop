using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Core.DTOs;
using EdilManager.Desktop.Services;

namespace EdilManager.Desktop.ViewModels;

public partial class FornitoreDetailViewModel : ObservableObject
{
    private readonly INavigationService _nav;
    private readonly DetailOpener _opener;

    public FornitoreDetailViewModel(FornitoreDetailDto fornitore, INavigationService nav, DetailOpener opener)
    {
        _nav = nav;
        _opener = opener;

        Titolo = fornitore.Denominazione;
        PartitaIva = fornitore.PartitaIva;
        CodiceFiscale = fornitore.CodiceFiscale ?? string.Empty;
        Indirizzo = FatturaDetailViewModel.FormattaIndirizzo(fornitore);
        Email = fornitore.Email ?? string.Empty;

        Fatture = fornitore.Fatture.Select(f => new FatturaRow(f)).ToList();
        FattureText = Fatture.Count switch { 0 => "Nessuna fattura", 1 => "1 fattura", var n => $"{n} fatture" };
        Totale = fornitore.TotaleFatture.ToString("N2", Formats.Italian) + " €";
    }

    public string Titolo { get; }
    public string PartitaIva { get; }
    public string CodiceFiscale { get; }
    public string Indirizzo { get; }
    public string Email { get; }

    public string FattureText { get; }
    public string Totale { get; }
    public IReadOnlyList<FatturaRow> Fatture { get; }
    public bool HasFatture => Fatture.Count > 0;
    public bool NoFatture => Fatture.Count == 0;

    [RelayCommand]
    private void GoBack() => _nav.Pop();

    [RelayCommand]
    private Task OpenFatturaAsync(FatturaRow row) => _opener.OpenFatturaAsync(row.Id);
}
