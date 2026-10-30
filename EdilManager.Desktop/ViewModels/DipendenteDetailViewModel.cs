using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Core.DTOs;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.ViewModels;

public sealed class PresenzaRow(PresenzaDto dto)
{
    public PresenzaDto Dto { get; } = dto;
    public string Data { get; } = dto.Data.ToString("dd/MM/yyyy");
    public string OreOrdinarie { get; } = dto.OreOrdinarie.ToString("0.0", Formats.Italian);
    public string OreStraordinarie { get; } = dto.OreStatordinarie > 0 ? dto.OreStatordinarie.ToString("0.0", Formats.Italian) : "-";
    public bool HasStraordinari { get; } = dto.OreStatordinarie > 0;
    public string Cantiere { get; } = dto.Cantiere ?? "-";
}

public partial class DipendenteDetailViewModel : ObservableObject
{
    private static readonly string[] Mesi =
    [
        "", "Gennaio", "Febbraio", "Marzo", "Aprile", "Maggio", "Giugno",
        "Luglio", "Agosto", "Settembre", "Ottobre", "Novembre", "Dicembre",
    ];

    private readonly DipendenteService _dipendenti;
    private readonly PresenzaService _presenze;
    private readonly CantiereService _cantieri;
    private readonly DialogService _dialogs;
    private readonly INavigationService _nav;
    private int _loadVersion;

    public DipendenteDetailViewModel(
        DipendenteDetailDto dipendente, DipendenteService dipendenti, PresenzaService presenze,
        CantiereService cantieri, DialogService dialogs, INavigationService nav)
    {
        _dipendente = dipendente;
        _dipendenti = dipendenti;
        _presenze = presenze;
        _cantieri = cantieri;
        _dialogs = dialogs;
        _nav = nav;
        _periodo = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _ = LoadPresenzeAsync();
    }

    public ObservableCollection<PresenzaRow> Rows { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NomeCompleto))]
    [NotifyPropertyChangedFor(nameof(Iniziale))]
    [NotifyCanExecuteChangedFor(nameof(PreviousMonthCommand))]
    private DipendenteDetailDto _dipendente;

    public string NomeCompleto => $"{Dipendente.Cognome} {Dipendente.Nome}";
    public string Iniziale => Dipendente.Cognome.Length > 0 ? Dipendente.Cognome[..1].ToUpperInvariant() : "?";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PeriodoText))]
    [NotifyCanExecuteChangedFor(nameof(NextMonthCommand), nameof(PreviousMonthCommand))]
    private DateTime _periodo;

    public string PeriodoText => $"{Mesi[Periodo.Month]} {Periodo.Year}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    private string _totaleOrdinarie = "0,0";

    [ObservableProperty]
    private string _totaleStraordinarie = "0,0";

    [ObservableProperty]
    private bool _hasStraordinari;

    public bool HasError => ErrorMessage is not null;
    public bool IsEmpty => !IsLoading && !HasError && Rows.Count == 0;

    partial void OnPeriodoChanged(DateTime value) => _ = LoadPresenzeAsync();

    private async Task LoadPresenzeAsync()
    {
        var version = ++_loadVersion;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var list = await _presenze.GetByMeseAsync(Dipendente.Id, Periodo.Year, Periodo.Month);
            if (version != _loadVersion) return;

            Rows.Clear();
            foreach (var dto in list) Rows.Add(new PresenzaRow(dto));

            var ord = list.Sum(p => p.OreOrdinarie);
            var str = list.Sum(p => p.OreStatordinarie);
            TotaleOrdinarie = ord.ToString("0.0", Formats.Italian);
            TotaleStraordinarie = str.ToString("0.0", Formats.Italian);
            HasStraordinari = str > 0;
        }
        catch (Exception ex)
        {
            if (version != _loadVersion) return;
            AppLog.Error("Caricamento presenze fallito", ex);
            ErrorMessage = ex.Message;
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsLoading = false;
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    [RelayCommand]
    private void GoBack() => _nav.Pop();

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void PreviousMonth() => Periodo = Periodo.AddMonths(-1);

    // Non si va indietro oltre il mese di assunzione: prima non ci sono presenze registrabili.
    private bool CanGoPrevious() => Periodo > new DateTime(Dipendente.DataAssunzione.Year, Dipendente.DataAssunzione.Month, 1);

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextMonth() => Periodo = Periodo.AddMonths(1);

    private bool CanGoNext() => Periodo < new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

    [RelayCommand]
    private async Task EditDipendenteAsync()
    {
        var form = new DipendenteFormViewModel(_dipendenti, _dialogs, Dipendente);
        if (await _dialogs.ShowAsync(form) is not true) return;

        try
        {
            var updated = await _dipendenti.GetByIdAsync(Dipendente.Id);
            if (updated is not null)
            {
                Dipendente = updated;
                // Se la nuova assunzione cade dopo il mese che si sta guardando, si passa al mese di assunzione.
                var inizio = new DateTime(updated.DataAssunzione.Year, updated.DataAssunzione.Month, 1);
                if (Periodo < inizio && inizio <= new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)) Periodo = inizio;
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
        }
    }

    [RelayCommand]
    private async Task EsportaAsync()
    {
        var form = EsportaViewModel.PerDipendente(_dialogs, _presenze, Dipendente.Id, NomeCompleto, new MeseOption(Periodo.Year, Periodo.Month), Dipendente.DataAssunzione);
        if (await _dialogs.ShowAsync(form) is string percorso) await _dialogs.MostraEsportazioneAsync(percorso);
    }

    [RelayCommand]
    private async Task AddPresenzaAsync()
    {
        // Mese interamente precedente all'assunzione: non c'è nessun giorno in cui registrare una presenza.
        if (DateOnly.FromDateTime(Periodo.AddMonths(1).AddDays(-1)) < Dipendente.DataAssunzione)
        {
            await _dialogs.MessageAsync("Presenza non registrabile",
                $"Il dipendente è stato assunto il {Dipendente.DataAssunzione:dd/MM/yyyy}: in {PeriodoText} non ci sono giorni in cui registrare presenze.");
            return;
        }

        var form = new PresenzaFormViewModel(_presenze, _dialogs, Dipendente.Id, Periodo, null, await CaricaCantieriAsync(), Dipendente.DataAssunzione);
        if (await _dialogs.ShowAsync(form) is true) await LoadPresenzeAsync();
    }

    [RelayCommand]
    private async Task EditPresenzaAsync(PresenzaRow row)
    {
        var form = new PresenzaFormViewModel(_presenze, _dialogs, Dipendente.Id, Periodo, row.Dto, await CaricaCantieriAsync(), Dipendente.DataAssunzione);
        if (await _dialogs.ShowAsync(form) is true) await LoadPresenzeAsync();
    }

    [RelayCommand]
    private async Task DeletePresenzaAsync(PresenzaRow row)
    {
        if (!await _dialogs.ConfirmAsync("Elimina presenza", $"Eliminare la presenza del {row.Data}?", "Elimina", destructive: true))
            return;

        try
        {
            await _presenze.DeleteAsync(row.Dto.Id);
            await LoadPresenzeAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
        }
    }

    private async Task<IReadOnlyList<CantiereOption>> CaricaCantieriAsync()
    {
        try
        {
            return CantiereOption.Elenco(await _cantieri.GetAllAsync());
        }
        catch (Exception ex)
        {
            // Senza elenco si può comunque registrare la presenza (senza cantiere).
            AppLog.Error("Caricamento cantieri per la presenza fallito", ex);
            return [CantiereOption.Nessuno];
        }
    }

    private Task ShowErrorAsync(Exception ex)
    {
        AppLog.Error("Operazione dipendente fallita", ex);
        return _dialogs.MessageAsync("Errore", ex.Message);
    }
}
