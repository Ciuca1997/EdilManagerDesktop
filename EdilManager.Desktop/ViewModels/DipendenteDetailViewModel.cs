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
    private readonly DialogService _dialogs;
    private readonly INavigationService _nav;
    private int _loadVersion;

    public DipendenteDetailViewModel(
        DipendenteDetailDto dipendente, DipendenteService dipendenti, PresenzaService presenze,
        DialogService dialogs, INavigationService nav)
    {
        _dipendente = dipendente;
        _dipendenti = dipendenti;
        _presenze = presenze;
        _dialogs = dialogs;
        _nav = nav;
        _periodo = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _ = LoadPresenzeAsync();
    }

    public ObservableCollection<PresenzaRow> Rows { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NomeCompleto))]
    [NotifyPropertyChangedFor(nameof(Iniziale))]
    private DipendenteDetailDto _dipendente;

    public string NomeCompleto => $"{Dipendente.Cognome} {Dipendente.Nome}";
    public string Iniziale => Dipendente.Cognome.Length > 0 ? Dipendente.Cognome[..1].ToUpperInvariant() : "?";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PeriodoText))]
    [NotifyCanExecuteChangedFor(nameof(NextMonthCommand))]
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

    [RelayCommand]
    private void PreviousMonth() => Periodo = Periodo.AddMonths(-1);

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
            if (updated is not null) Dipendente = updated;
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
        }
    }

    [RelayCommand]
    private async Task AddPresenzaAsync()
    {
        var form = new PresenzaFormViewModel(_presenze, _dialogs, Dipendente.Id, Periodo, null);
        if (await _dialogs.ShowAsync(form) is true) await LoadPresenzeAsync();
    }

    [RelayCommand]
    private async Task EditPresenzaAsync(PresenzaRow row)
    {
        var form = new PresenzaFormViewModel(_presenze, _dialogs, Dipendente.Id, Periodo, row.Dto);
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

    private Task ShowErrorAsync(Exception ex)
    {
        AppLog.Error("Operazione dipendente fallita", ex);
        return _dialogs.MessageAsync("Errore", ex.Message);
    }
}
