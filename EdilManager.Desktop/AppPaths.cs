using System.Globalization;
using System.IO;
using System.Reflection;

namespace EdilManager.Desktop;

public static class AppPaths
{
    // Fuori dalla cartella di installazione di Velopack (%LocalAppData%\EdilManager), che viene sostituita a ogni aggiornamento.
    public static string DataDirectory { get; } =
        Environment.GetEnvironmentVariable("EDILMANAGER_DATA_DIR") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EdilManagerData");

    public static string DatabasePath => Path.Combine(DataDirectory, "edilmanager.db");
    public static string BackupDirectory => Path.Combine(DataDirectory, "backups");
    public static string LogFile => Path.Combine(DataDirectory, "app.log");
}

public static class AppInfo
{
    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0] ?? "dev";
}

public static class AppLog
{
    public static void Error(string message, Exception ex)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            File.AppendAllText(AppPaths.LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // il log non deve mai causare un secondo errore
        }
    }
}

public static class Formats
{
    public static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");
}
