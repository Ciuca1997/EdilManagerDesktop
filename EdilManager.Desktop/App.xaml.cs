using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using EdilManager.Desktop.Services;
using EdilManager.Desktop.ViewModels;
using EdilManager.Infrastructure.Persistence;
using EdilManager.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EdilManager.Desktop;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var it = Formats.Italian;
        CultureInfo.DefaultThreadCurrentCulture = it;
        CultureInfo.DefaultThreadCurrentUICulture = it;
        Thread.CurrentThread.CurrentCulture = it;
        Thread.CurrentThread.CurrentUICulture = it;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(it.IetfLanguageTag)));

        DispatcherUnhandledException += OnUnhandledException;

        try
        {
            _services = BuildServices();
            PrepareDatabase(_services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());
        }
        catch (Exception ex)
        {
            AppLog.Error("Avvio fallito", ex);
            MessageBox.Show($"Impossibile avviare EdilManager.\n\n{ex.Message}\n\nDettagli in: {AppPaths.ErrorLogFile}",
                "EdilManager", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        AppLog.Trace($"Avvio EdilManager {AppInfo.Version}");
        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private static ServiceProvider BuildServices()
    {
        var sc = new ServiceCollection();

        sc.AddDbContextFactory<ApplicationDbContext>(o => o.UseSqlite($"Data Source={AppPaths.DatabasePath}"));
        sc.AddSingleton<DipendenteService>();
        sc.AddSingleton<PresenzaService>();
        sc.AddSingleton(new ImpostazioniService(Path.Combine(AppPaths.DataDirectory, "settings.json")));
        sc.AddSingleton<ArchivioFatture>();
        sc.AddSingleton<CantiereService>();
        sc.AddSingleton<FornitoreService>();
        sc.AddSingleton<AziendaService>();
        sc.AddSingleton<FatturaService>();
        sc.AddSingleton<DashboardService>();

        sc.AddSingleton<DialogService>();
        sc.AddSingleton<DetailOpener>();
        sc.AddSingleton<UpdateService>();
        sc.AddSingleton<MainViewModel>();
        sc.AddSingleton<INavigationService>(sp => sp.GetRequiredService<MainViewModel>());
        sc.AddTransient<DashboardViewModel>();
        sc.AddTransient<DipendentiViewModel>();
        sc.AddTransient<FattureViewModel>();
        sc.AddTransient<PresenzeViewModel>();
        sc.AddTransient<FornitoriViewModel>();
        sc.AddTransient<ImpostazioniViewModel>();
        sc.AddTransient<CantieriViewModel>();
        sc.AddSingleton<MainWindow>();

        return sc.BuildServiceProvider();
    }

    private static void PrepareDatabase(IDbContextFactory<ApplicationDbContext> factory)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);

        using var db = factory.CreateDbContext();
        if (File.Exists(AppPaths.DatabasePath) && db.Database.GetPendingMigrations().Any())
            BackupDatabase();

        db.Database.Migrate();
    }

    // Prima di cambiare lo schema salva una copia del database e conserva le ultime 5.
    private static void BackupDatabase()
    {
        Directory.CreateDirectory(AppPaths.BackupDirectory);
        var target = Path.Combine(AppPaths.BackupDirectory, $"edilmanager-{DateTime.Now:yyyyMMdd-HHmmss}.db");
        File.Copy(AppPaths.DatabasePath, target, overwrite: false);

        foreach (var old in new DirectoryInfo(AppPaths.BackupDirectory).GetFiles("edilmanager-*.db")
                     .OrderByDescending(f => f.Name).Skip(5))
            old.Delete();
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error("Errore non gestito", e.Exception);
        MessageBox.Show($"Si è verificato un errore imprevisto.\n\n{e.Exception.Message}",
            "EdilManager", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
