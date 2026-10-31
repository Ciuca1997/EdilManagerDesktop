using System.IO;
using EdilManager.Desktop.ViewModels;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;

namespace EdilManager.Desktop.Services;

public class DialogService
{
    public const string Identifier = "RootDialog";

    // Se c'è già un dialogo aperto (es. doppio clic ripetuto) DialogHost rifiuta il secondo: lo si ignora e si restituisce null,
    // che per chi chiama equivale a "annullato", invece di far comparire un errore imprevisto.
    public async Task<object?> ShowAsync(object viewModel)
    {
        try
        {
            return await DialogHost.Show(viewModel, Identifier);
        }
        catch (InvalidOperationException ex)
        {
            AppLog.Trace($"DIALOGO ignorato perché ce n'è già uno aperto: {viewModel.GetType().Name} ({ex.Message})");
            return null;
        }
    }

    public void Close(object? result = null) => DialogHost.Close(Identifier, result);

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText = "Conferma", bool destructive = false)
    {
        var vm = new ConfirmViewModel(this, title, message, confirmText, "Annulla", destructive);
        return await ShowAsync(vm) is true;
    }

    public Task MessageAsync(string title, string message) =>
        ShowAsync(new ConfirmViewModel(this, title, message, "OK", null, false));

    public string[] PickFiles(string filter, string title)
    {
        var dialog = new OpenFileDialog { Filter = filter, Title = title, Multiselect = true, CheckFileExists = true };
        return dialog.ShowDialog() == true ? dialog.FileNames : [];
    }

    public string? PickFile(string filter, string title, string? cartellaIniziale = null)
    {
        var dialog = new OpenFileDialog { Filter = filter, Title = title, Multiselect = false, CheckFileExists = true };
        if (Directory.Exists(cartellaIniziale)) dialog.InitialDirectory = cartellaIniziale;
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    // Dopo un'esportazione: dice dove è stato salvato il file e propone di aprirlo.
    public async Task MostraEsportazioneAsync(string percorso)
    {
        if (!await ConfirmAsync("Esportazione completata", $"File salvato in:\n{percorso}", "Apri file")) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(percorso) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Error($"Apertura del file esportato fallita: {percorso}", ex);
            await MessageAsync("Errore", $"Non riesco ad aprire il file: {ex.Message}");
        }
    }

    // cartellaIniziale: se esiste, il dialogo si apre lì (altrimenti Windows ricorda l'ultima cartella usata).
    public string? PickSaveFile(string filter, string title, string fileName, string? cartellaIniziale = null)
    {
        var dialog = new SaveFileDialog { Filter = filter, Title = title, FileName = fileName, OverwritePrompt = true };
        if (Directory.Exists(cartellaIniziale)) dialog.InitialDirectory = cartellaIniziale;
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder(string title, string? cartellaIniziale = null)
    {
        var dialog = new OpenFolderDialog { Title = title };
        if (Directory.Exists(cartellaIniziale)) dialog.InitialDirectory = cartellaIniziale;
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
