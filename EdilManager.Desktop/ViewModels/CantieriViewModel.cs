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

    public bool Corrisponde(string testo) =>
        Nome.Contains(testo, StringComparison.OrdinalIgnoreCase) ||
        CUP.Contains(testo, StringComparison.OrdinalIgnoreCase);
}

public partial class CantieriViewModel : ObservableObject, IReloadable
{
    private readonly CantiereService _service;
    private readonly DialogService _dialogs;
    private readonly DetailOpener _opener;
    private List<CantiereRow> _all = [];

    public CantieriViewModel(CantiereService service, DialogService dialogs, DetailOpener opener)
    {
        _service = service;
        _dialogs = dialogs;
        _opener = opener;
        _ = ReloadAsync();
    }

    public ObservableCollection<CantiereRow> Items { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(NoResults))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(NoResults), nameof(HasError))]
    private string? _errorMessage;

    public bool HasError => ErrorMessage is not null;
    public bool IsEmpty => !IsLoading && !HasError && _all.Count == 0;
    public bool NoResults => !IsLoading && !HasError && _all.Count > 0 && Items.Count == 0;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    public async Task ReloadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            _all = (await _service.GetAllAsync()).Select(d => new CantiereRow(d)).ToList();
            ApplyFilter();
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
            OnPropertyChanged(nameof(NoResults));
        }
    }

    private void ApplyFilter()
    {
        var testo = SearchText.Trim();
        Items.Clear();
        foreach (var row in testo.Length == 0 ? _all : _all.Where(r => r.Corrisponde(testo))) Items.Add(row);
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(NoResults));
    }

    [RelayCommand]
    private Task OpenAsync(CantiereRow row) => _opener.OpenCantiereAsync(row.Id);

    [RelayCommand]
    private async Task EditAsync(CantiereRow row)
    {
        var form = new CantiereFormViewModel(_service, _dialogs, row.Id, row.Nome, row.CUP);
        if (await _dialogs.ShowAsync(form) is not true) return;
        await ReloadAsync();
        if (form.Avviso is not null) await _dialogs.MessageAsync("Archivio fatture", form.Avviso);
    }
}
