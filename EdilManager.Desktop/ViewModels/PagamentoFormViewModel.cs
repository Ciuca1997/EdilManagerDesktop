using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Desktop.Services;

namespace EdilManager.Desktop.ViewModels;

// Chiede la data in cui una o più fatture sono state pagate (predefinita: oggi).
public partial class PagamentoFormViewModel(DialogService dialogs, int numeroFatture) : ObservableObject
{
    public string Titolo => numeroFatture == 1 ? "Segna la fattura come pagata" : $"Segna {numeroFatture} fatture come pagate";

    public DateTime MaxDate { get; } = DateTime.Today;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private DateTime? _dataPagamento = DateTime.Today;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm() => dialogs.Close(DateOnly.FromDateTime(DataPagamento!.Value));

    private bool CanConfirm() => DataPagamento is { } d && d.Date <= DateTime.Today;

    [RelayCommand]
    private void Cancel() => dialogs.Close(null);
}
