using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Core.DTOs;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.ViewModels;

public sealed partial class GiornoRigaViewModel : ObservableObject
{
    public GiornoRigaViewModel(CantiereOption cantiere, string ordinarie, string straordinarie)
    {
        _cantiere = cantiere;
        _oreOrdinarie = ordinarie;
        _oreStraordinarie = straordinarie;
    }

    [ObservableProperty]
    private CantiereOption? _cantiere;

    [ObservableProperty]
    private string _oreOrdinarie;

    [ObservableProperty]
    private string _oreStraordinarie;
}

// Le presenze di un dipendente in un giorno, una riga per cantiere: permette di dividere le ore tra più cantieri.
public partial class PresenzaGiornoViewModel : ObservableObject
{
    private readonly PresenzaService _service;
    private readonly DialogService _dialogs;
    private readonly int _dipendenteId;
    private readonly DateOnly _data;
    private readonly CantiereOption _cantierePredefinito;

    public PresenzaGiornoViewModel(
        PresenzaService service, DialogService dialogs, int dipendenteId, string dipendente, DateOnly data,
        IReadOnlyList<CantiereOption> cantieri, IReadOnlyList<PresenzaDto> esistenti, CantiereOption cantierePredefinito)
    {
        _service = service;
        _dialogs = dialogs;
        _dipendenteId = dipendenteId;
        _data = data;
        _cantierePredefinito = cantierePredefinito;
        Cantieri = cantieri;
        Sottotitolo = $"{dipendente} · {data.ToString("dddd d MMMM yyyy", Formats.Italian)}";

        foreach (var p in esistenti)
            Righe.Add(new GiornoRigaViewModel(
                cantieri.FirstOrDefault(c => c.Id == p.CantiereId) ?? CantiereOption.Nessuno,
                Ore(p.OreOrdinarie), Ore(p.OreStatordinarie)));
        if (Righe.Count == 0) Righe.Add(NuovaRiga());
    }

    public string Sottotitolo { get; }
    public IReadOnlyList<CantiereOption> Cantieri { get; }
    public ObservableCollection<GiornoRigaViewModel> Righe { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [RelayCommand]
    private void AggiungiRiga()
    {
        // Propone un cantiere non ancora usato in questo giorno, se ce n'è uno.
        var usati = Righe.Select(r => r.Cantiere?.Id).ToHashSet();
        var libero = Cantieri.FirstOrDefault(c => !usati.Contains(c.Id)) ?? CantiereOption.Nessuno;
        Righe.Add(new GiornoRigaViewModel(libero, string.Empty, string.Empty));
    }

    [RelayCommand]
    private void RimuoviRiga(GiornoRigaViewModel riga) => Righe.Remove(riga);

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        var righe = new List<PresenzaGiornoRigaDto>();
        foreach (var r in Righe)
        {
            if (!PresenzeViewModel.TryOre(r.OreOrdinarie, out var ord) || !PresenzeViewModel.TryOre(r.OreStraordinarie, out var str))
            {
                ErrorMessage = "Le ore devono essere un numero maggiore o uguale a zero (es. 4 oppure 7,5).";
                return;
            }
            righe.Add(new PresenzaGiornoRigaDto { CantiereId = r.Cantiere?.Id, OreOrdinarie = ord, OreStraordinarie = str });
        }

        try
        {
            await _service.SalvaGiornoAsync(_dipendenteId, _data, righe);
            _dialogs.Close(true);
        }
        catch (Exception ex)
        {
            AppLog.Error("Salvataggio giorno fallito", ex);
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel() => _dialogs.Close(false);

    private GiornoRigaViewModel NuovaRiga() => new(_cantierePredefinito, string.Empty, string.Empty);

    private static string Ore(decimal ore) => ore == 0 ? string.Empty : ore.ToString("0.##", Formats.Italian);
}
