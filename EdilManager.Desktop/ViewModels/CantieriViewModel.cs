using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Core.DTOs;
using EdilManager.Desktop.Services;
using EdilManager.Infrastructure.Services;

namespace EdilManager.Desktop.ViewModels;

public sealed class CantiereRow(CantiereListDto dto)
{
    public int Id { get; } = dto.Id;
    public string Nome { get; } = dto.Nome;
    public string CUP { get; } = dto.CUP;
    public string FattureText { get; } = dto.FattureCount switch
    {
        0 => "Nessuna fattura",
        1 => "1 fattura",
        var n => $"{n} fatture",
    };
}

public partial class CantieriViewModel : ObservableObject, IReloadable
{
    private readonly CantiereService _service;
    private readonly DialogService _dialogs;

    public CantieriViewModel(CantiereService service, DialogService dialogs)
    {
        _service = service;
        _dialogs = dialogs;
        _ = ReloadAsync();
    }

    public ObservableCollection<CantiereRow> Items { get; } = [];

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
            foreach (var dto in list) Items.Add(new CantiereRow(dto));
        }
        catch (Exception ex)
        {
            AppLog.Error("Caricamento cantieri fallito", ex);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    [RelayCommand]
    private async Task EditAsync(CantiereRow row)
    {
        var form = new CantiereFormViewModel(_service, _dialogs, row.Id, row.Nome, row.CUP);
        if (await _dialogs.ShowAsync(form) is true) await ReloadAsync();
    }
}
