using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EdilManager.Desktop.Services;

namespace EdilManager.Desktop.ViewModels;

// Sceglie il cantiere da dare a una o più fatture. Risultato: la CantiereOption scelta (Id nullo = togliere il cantiere); null = annullato.
public partial class AssegnaCantiereViewModel(
    DialogService dialogs, int numeroFatture, IReadOnlyList<CantiereOption> cantieri) : ObservableObject
{
    public string Titolo => numeroFatture == 1 ? "Assegna un cantiere alla fattura" : $"Assegna un cantiere a {numeroFatture} fatture";
    public IReadOnlyList<CantiereOption> Cantieri => cantieri;
    public bool NessunCantiere => cantieri.Count <= 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private CantiereOption? _cantiere;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm() => dialogs.Close(Cantiere);

    private bool CanConfirm() => Cantiere is not null;

    [RelayCommand]
    private void Cancel() => dialogs.Close(null);
}
