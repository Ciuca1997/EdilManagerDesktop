using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Core.DTOs;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.ViewModels;

public partial class DipendenteFormViewModel(
    DipendenteService service, DialogService dialogs, DipendenteDetailDto? existing) : ObservableObject
{
    public string Title { get; } = existing is null ? "Nuovo dipendente" : "Modifica dipendente";
    public string SaveText { get; } = existing is null ? "Crea" : "Salva";
    public DateTime MinDate { get; } = new(1940, 1, 1);
    public DateTime MaxDate { get; } = DateTime.Today;

    [ObservableProperty] private string _cognome = existing?.Cognome ?? string.Empty;
    [ObservableProperty] private string _nome = existing?.Nome ?? string.Empty;
    [ObservableProperty] private string _codiceFiscale = existing?.CodiceFiscale ?? string.Empty;
    [ObservableProperty] private DateTime? _dataNascita = existing?.DataNascita.ToDateTime(TimeOnly.MinValue);
    [ObservableProperty] private DateTime? _dataAssunzione = existing?.DataAssunzione.ToDateTime(TimeOnly.MinValue);
    [ObservableProperty] private string _mansione = existing?.Mansione ?? string.Empty;
    [ObservableProperty] private string _livello = existing?.Livello ?? string.Empty;
    [ObservableProperty] private string _telefono = existing?.Telefono ?? string.Empty;
    [ObservableProperty] private string _email = existing?.Email ?? string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Cognome) || string.IsNullOrWhiteSpace(Nome) ||
            string.IsNullOrWhiteSpace(CodiceFiscale) || DataNascita is null || DataAssunzione is null)
        {
            ErrorMessage = "Compilare tutti i campi obbligatori: cognome, nome, codice fiscale, data di nascita e data di assunzione.";
            return;
        }

        var dto = new DipendenteUpsertDto
        {
            Cognome = Cognome.Trim(),
            Nome = Nome.Trim(),
            CodiceFiscale = CodiceFiscale.Trim().ToUpperInvariant(),
            DataNascita = DateOnly.FromDateTime(DataNascita.Value),
            DataAssunzione = DateOnly.FromDateTime(DataAssunzione.Value),
            Mansione = NullIfEmpty(Mansione),
            Livello = NullIfEmpty(Livello),
            Telefono = NullIfEmpty(Telefono),
            Email = NullIfEmpty(Email),
        };

        try
        {
            if (existing is null) await service.CreateAsync(dto);
            else await service.UpdateAsync(existing.Id, dto);
            dialogs.Close(true);
        }
        catch (Exception ex)
        {
            AppLog.Error("Salvataggio dipendente fallito", ex);
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel() => dialogs.Close(false);

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
