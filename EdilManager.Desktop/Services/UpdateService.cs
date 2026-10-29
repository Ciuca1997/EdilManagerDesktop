using EdilManager.Desktop.ViewModels;
using Velopack;
using Velopack.Sources;

namespace EdilManager.Desktop.Services;

public sealed class UpdateService(DialogService dialogs)
{
    // Repository pubblico che contiene solo le Release (nessun sorgente).
    private const string RepoUrl = "https://github.com/Ciuca1997/EdilManager-releases";

    public async Task CheckAsync(bool interactive)
    {
        try
        {
            var manager = new UpdateManager(new GithubSource(RepoUrl, null, false));

            if (!manager.IsInstalled)
            {
                if (interactive)
                    await dialogs.MessageAsync("Aggiornamenti", "Il controllo aggiornamenti funziona solo nell'app installata.");
                return;
            }

            var update = await manager.CheckForUpdatesAsync();
            if (update is null)
            {
                if (interactive)
                    await dialogs.MessageAsync("Aggiornamenti", $"Stai già usando l'ultima versione ({AppInfo.Version}).");
                return;
            }

            await dialogs.ShowAsync(new UpdateViewModel(dialogs, manager, update));
        }
        catch (Exception ex)
        {
            AppLog.Error("Controllo aggiornamenti fallito", ex);
            if (interactive)
                await dialogs.MessageAsync("Aggiornamenti", $"Impossibile controllare gli aggiornamenti.\n{ex.Message}");
        }
    }
}
