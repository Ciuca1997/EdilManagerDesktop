using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Core.DTOs;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.ViewModels;

public sealed class FornitoreRiepilogoRow(FornitoreRiepilogoDto dto)
{
    public int FornitoreId { get; } = dto.FornitoreId;
    public string Denominazione { get; } = dto.Denominazione;
    public string Fatture { get; } = dto.FattureCount.ToString();
    public string Totale { get; } = dto.Totale.ToString("N2", Formats.Italian) + " €";
}

public sealed class ManodoperaRow(ManodoperaDto dto)
{
    public int DipendenteId { get; } = dto.DipendenteId;
    public string Dipendente { get; } = dto.Dipendente;
    public string Giorni { get; } = dto.Giorni.ToString();
    public string OreOrdinarie { get; } = dto.OreOrdinarie.ToString("0.0", Formats.Italian);
    public string OreStraordinarie { get; } = dto.OreStraordinarie > 0 ? dto.OreStraordinarie.ToString("0.0", Formats.Italian) : "-";
}

public partial class CantiereDetailViewModel : ObservableObject
{
    private readonly CantiereService _service;
    private readonly DialogService _dialogs;
    private readonly INavigationService _nav;
    private readonly DetailOpener _opener;
    private readonly int _id;

    public CantiereDetailViewModel(
        CantiereDetailDto cantiere, CantiereService service, DialogService dialogs, INavigationService nav, DetailOpener opener,
        ImpostazioniService impostazioni)
    {
        _service = service;
        _dialogs = dialogs;
        _nav = nav;
        _opener = opener;
        _id = cantiere.Id;

        Cup = cantiere.Interno ? "senza CUP" : cantiere.CUP;
        Interno = cantiere.Interno;
        Nome = cantiere.Nome;

        var giorni = impostazioni.Corrente.GiorniScadenzaPredefiniti;
        var oggi = DateOnly.FromDateTime(DateTime.Today);
        Fatture = cantiere.Fatture.Select(f => new FatturaRow(f, giorni, oggi)).ToList();
        Fornitori = cantiere.Fornitori.Select(f => new FornitoreRiepilogoRow(f)).ToList();
        Manodopera = cantiere.Manodopera.Select(m => new ManodoperaRow(m)).ToList();
        var ordinarie = cantiere.Manodopera.Sum(m => m.OreOrdinarie);
        var straordinarie = cantiere.Manodopera.Sum(m => m.OreStraordinarie);
        OreText = ordinarie + straordinarie == 0
            ? "Nessuna ora registrata"
            : $"{ordinarie.ToString("N1", Formats.Italian)} ord." + (straordinarie > 0 ? $" + {straordinarie.ToString("N1", Formats.Italian)} straord." : string.Empty);
        FattureText = Fatture.Count switch { 0 => "Nessuna fattura", 1 => "1 fattura", var n => $"{n} fatture" };
        Totale = cantiere.TotaleFatture.ToString("N2", Formats.Italian) + " €";
    }

    [ObservableProperty]
    private string _cup;

    [ObservableProperty]
    private bool _interno;

    [ObservableProperty]
    private string _nome;

    public string FattureText { get; }
    public string Totale { get; }
    public IReadOnlyList<FatturaRow> Fatture { get; }
    public IReadOnlyList<FornitoreRiepilogoRow> Fornitori { get; }
    public IReadOnlyList<ManodoperaRow> Manodopera { get; }
    public string OreText { get; }
    public bool HasManodopera => Manodopera.Count > 0;
    public bool HasFatture => Fatture.Count > 0;
    public bool NoFatture => Fatture.Count == 0;

    [RelayCommand]
    private void GoBack() => _nav.Pop();

    [RelayCommand]
    private async Task RenameAsync()
    {
        var form = new CantiereFormViewModel(_service, _dialogs, _id, Nome, Cup);
        if (await _dialogs.ShowAsync(form) is not true) return;

        try
        {
            if (await _service.GetByIdAsync(_id) is { } aggiornato) Nome = aggiornato.Nome;
        }
        catch (Exception ex)
        {
            AppLog.Error("Ricarica cantiere dopo la rinomina fallita", ex);
        }

        if (form.Avviso is not null) await _dialogs.MessageAsync("Archivio fatture", form.Avviso);
    }

    [RelayCommand]
    private async Task ImpostaCupAsync()
    {
        if (await _dialogs.ShowAsync(new ImpostaCupViewModel(_service, _dialogs, _id, Nome)) is not true) return;
        if (await _service.GetByIdAsync(_id) is { } aggiornato)
        {
            Cup = aggiornato.CUP;
            Interno = aggiornato.Interno;
        }
    }

    [RelayCommand]
    private Task OpenFatturaAsync(FatturaRow row) => _opener.OpenFatturaAsync(row.Id);

    [RelayCommand]
    private Task OpenFornitoreAsync(FornitoreRiepilogoRow row) => _opener.OpenFornitoreAsync(row.FornitoreId);

    [RelayCommand]
    private Task OpenDipendenteAsync(ManodoperaRow row) => _opener.OpenDipendenteAsync(row.DipendenteId);
}
