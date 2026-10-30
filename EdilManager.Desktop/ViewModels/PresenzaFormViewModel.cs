using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Core.DTOs;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.ViewModels;

public partial class PresenzaFormViewModel(
    PresenzaService service, DialogService dialogs, int dipendenteId, DateTime annoMese, PresenzaDto? existing,
    IReadOnlyList<CantiereOption> cantieri, DateOnly dataAssunzione)
    : ObservableObject
{
    public string Title { get; } = existing is null ? "Nuova presenza" : "Modifica presenza";
    public string SaveText { get; } = existing is null ? "Aggiungi" : "Salva";
    public IReadOnlyList<CantiereOption> Cantieri { get; } = cantieri;
    // Il calendario non offre i giorni precedenti all'assunzione (se il mese finisce prima, resta l'ultimo giorno e il servizio rifiuta il salvataggio).
    public DateTime DisplayStart { get; } = InizioConsentito(new DateTime(annoMese.Year, annoMese.Month, 1), dataAssunzione);
    public DateTime DisplayEnd { get; } = new DateTime(annoMese.Year, annoMese.Month, 1).AddMonths(1).AddDays(-1);

    [ObservableProperty]
    private DateTime? _data = existing?.Data.ToDateTime(TimeOnly.MinValue);

    // Un cantiere non più in elenco (es. eliminato) ricade su "Nessun cantiere".
    [ObservableProperty]
    private CantiereOption? _cantiere =
        cantieri.FirstOrDefault(c => c.Id == existing?.CantiereId) ?? CantiereOption.Nessuno;

    [ObservableProperty]
    private string _oreOrdinarie = existing is null ? "8,0" : existing.OreOrdinarie.ToString("0.0", Formats.Italian);

    [ObservableProperty]
    private string _oreStraordinarie = existing is null ? "0,0" : existing.OreStatordinarie.ToString("0.0", Formats.Italian);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (Data is null)
        {
            ErrorMessage = "Selezionare la data.";
            return;
        }
        if (!TryParseOre(OreOrdinarie, out var ordinarie) || !TryParseOre(OreStraordinarie, out var straordinarie))
        {
            ErrorMessage = "Le ore devono essere un numero maggiore o uguale a zero.";
            return;
        }

        var dto = new PresenzaUpsertDto
        {
            DipendenteId = dipendenteId,
            CantiereId = Cantiere?.Id,
            Data = DateOnly.FromDateTime(Data.Value),
            OreOrdinarie = ordinarie,
            OreStatordinarie = straordinarie,
        };

        try
        {
            if (existing is null) await service.CreateAsync(dto);
            else await service.UpdateAsync(existing.Id, dto);
            dialogs.Close(true);
        }
        catch (Exception ex)
        {
            AppLog.Error("Salvataggio presenza fallito", ex);
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel() => dialogs.Close(false);

    // Primo giorno selezionabile: l'assunzione se cade nel mese, altrimenti inizio mese (o fine mese se l'assunzione è successiva).
    private static DateTime InizioConsentito(DateTime inizioMese, DateOnly assunzione)
    {
        var fineMese = inizioMese.AddMonths(1).AddDays(-1);
        var da = assunzione.ToDateTime(TimeOnly.MinValue);
        return da <= inizioMese ? inizioMese : da > fineMese ? fineMese : da;
    }

    private static bool TryParseOre(string text, out decimal value) =>
        decimal.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value >= 0;
}
