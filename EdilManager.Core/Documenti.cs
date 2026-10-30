namespace EdilManager.Core;

public static class Documenti
{
    // Nelle fatture elettroniche gli importi sono sempre positivi: una nota di credito si distingue solo dal tipo.
    public static bool IsNotaCredito(string tipo) => tipo is "TD04" or "TD08";

    // Totale da usare nelle somme: le note di credito riducono il fatturato.
    public static decimal ConSegno(string tipo, decimal totale) => IsNotaCredito(tipo) ? -totale : totale;
}
