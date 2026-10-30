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
    public static string LogDirectory => Path.Combine(DataDirectory, "logs");
    public static string TraceLogFile => Path.Combine(LogDirectory, "trace.log");
    public static string ErrorLogFile => Path.Combine(LogDirectory, "error.log");
}

public static class AppInfo
{
    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0] ?? "dev";
}

public static class AppLog
{
    private const long MaxSize = 5 * 1024 * 1024;
    private static readonly object Gate = new();

    // Cronologia delle operazioni (avvio, import file per file...).
    public static void Trace(string message) => Write(AppPaths.TraceLogFile, "INFO ", message);

    // Errori con il dettaglio dell'eccezione in error.log; una riga di sintesi finisce anche in trace.log, per avere la cronologia completa.
    public static void Error(string message, Exception? ex = null)
    {
        Write(AppPaths.ErrorLogFile, "ERROR", ex is null ? message : $"{message}{Environment.NewLine}{ex}");
        Write(AppPaths.TraceLogFile, "ERROR", ex is null ? message : $"{message}: {ex.Message}");
    }

    private static void Write(string file, string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.LogDirectory);
                // Oltre 5 MB il file corrente diventa .old (l'eventuale .old precedente si perde).
                if (File.Exists(file) && new FileInfo(file).Length > MaxSize)
                    File.Move(file, file + ".old", overwrite: true);
                File.AppendAllText(file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
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
