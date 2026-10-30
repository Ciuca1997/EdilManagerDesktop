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

public partial class CantiereDetailViewModel : ObservableObject
{
    private readonly CantiereService _service;
    private readonly DialogService _dialogs;
    private readonly INavigationService _nav;
    private readonly DetailOpener _opener;
    private readonly int _id;

    public CantiereDetailViewModel(
        CantiereDetailDto cantiere, CantiereService service, DialogService dialogs, INavigationService nav, DetailOpener opener)
    {
        _service = service;
        _dialogs = dialogs;
        _nav = nav;
        _opener = opener;
        _id = cantiere.Id;

        Cup = cantiere.CUP;
        Nome = cantiere.Nome;

        Fatture = cantiere.Fatture.Select(f => new FatturaRow(f)).ToList();
        Fornitori = cantiere.Fornitori.Select(f => new FornitoreRiepilogoRow(f)).ToList();
        FattureText = Fatture.Count switch { 0 => "Nessuna fattura", 1 => "1 fattura", var n => $"{n} fatture" };
        Totale = cantiere.TotaleFatture.ToString("N2", Formats.Italian) + " €";
    }

    public string Cup { get; }

    [ObservableProperty]
    private string _nome;

    public string FattureText { get; }
    public string Totale { get; }
    public IReadOnlyList<FatturaRow> Fatture { get; }
    public IReadOnlyList<FornitoreRiepilogoRow> Fornitori { get; }
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
    private Task OpenFatturaAsync(FatturaRow row) => _opener.OpenFatturaAsync(row.Id);

    [RelayCommand]
    private Task OpenFornitoreAsync(FornitoreRiepilogoRow row) => _opener.OpenFornitoreAsync(row.FornitoreId);
}
