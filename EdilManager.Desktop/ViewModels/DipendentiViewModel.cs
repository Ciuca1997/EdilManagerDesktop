using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Core.DTOs;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EdilManager.Desktop.ViewModels;

public sealed class DipendenteRow(DipendenteListDto dto)
{
    public int Id { get; } = dto.Id;
    public string NomeCompleto { get; } = $"{dto.Cognome} {dto.Nome}";
    public string Iniziale { get; } = dto.Cognome.Length > 0 ? dto.Cognome[..1].ToUpperInvariant() : "?";
    public string Sottotitolo { get; } =
        string.Join(" · ", new[] { dto.Mansione, dto.Livello }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

public partial class DipendentiViewModel : ObservableObject, IReloadable
{
    private readonly DipendenteService _service;
    private readonly DialogService _dialogs;
    private readonly INavigationService _nav;
    private readonly IServiceProvider _services;

    public DipendentiViewModel(DipendenteService service, DialogService dialogs, INavigationService nav, IServiceProvider services)
    {
        _service = service;
        _dialogs = dialogs;
        _nav = nav;
        _services = services;
        _ = ReloadAsync();
    }

    public ObservableCollection<DipendenteRow> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => ErrorMessage is not null;
    public bool IsEmpty => !IsLoading && !HasError && Items.Count == 0;

    public async Task ReloadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var list = await _service.GetAllAsync();
            Items.Clear();
            foreach (var dto in list) Items.Add(new DipendenteRow(dto));
        }
        catch (Exception ex)
        {
            AppLog.Error("Caricamento dipendenti fallito", ex);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    [RelayCommand]
    private async Task NewAsync()
    {
        var form = new DipendenteFormViewModel(_service, _dialogs, null);
        if (await _dialogs.ShowAsync(form) is true) await ReloadAsync();
    }

    [RelayCommand]
    private async Task OpenAsync(DipendenteRow row)
    {
        try
        {
            var detail = await _service.GetByIdAsync(row.Id);
            if (detail is null)
            {
                await ReloadAsync();
                return;
            }
            _nav.Push(ActivatorUtilities.CreateInstance<DipendenteDetailViewModel>(_services, detail));
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
        }
    }

    [RelayCommand]
    private async Task EditAsync(DipendenteRow row)
    {
        try
        {
            var detail = await _service.GetByIdAsync(row.Id);
            if (detail is null)
            {
                await ReloadAsync();
                return;
            }
            var form = new DipendenteFormViewModel(_service, _dialogs, detail);
            if (await _dialogs.ShowAsync(form) is true) await ReloadAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(DipendenteRow row)
    {
        if (!await _dialogs.ConfirmAsync("Elimina dipendente", $"Eliminare {row.NomeCompleto}?", "Elimina", destructive: true))
            return;

        try
        {
            await _service.DeleteAsync(row.Id);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
        }
    }

    private Task ShowErrorAsync(Exception ex)
    {
        AppLog.Error("Operazione dipendenti fallita", ex);
        return _dialogs.MessageAsync("Errore", ex.Message);
    }
}
