using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Core.DTOs;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.ViewModels;

public partial class PresenzaFormViewModel(
    PresenzaService service, DialogService dialogs, int dipendenteId, DateTime annoMese, PresenzaDto? existing)
    : ObservableObject
{
    public string Title { get; } = existing is null ? "Nuova presenza" : "Modifica presenza";
    public string SaveText { get; } = existing is null ? "Aggiungi" : "Salva";
    public DateTime DisplayStart { get; } = new(annoMese.Year, annoMese.Month, 1);
    public DateTime DisplayEnd { get; } = new DateTime(annoMese.Year, annoMese.Month, 1).AddMonths(1).AddDays(-1);

    [ObservableProperty]
    private DateTime? _data = existing?.Data.ToDateTime(TimeOnly.MinValue);

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
            CantiereId = existing?.CantiereId,
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

    private static bool TryParseOre(string text, out decimal value) =>
        decimal.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value >= 0;
}
