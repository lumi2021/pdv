namespace Pdv.Data;

public static class DatabasePaths
{
    public const string AppFolderName = "PdvLanchonete";
    public const string DatabaseFileName = "pdv.db";

    /// <summary>
    /// Pasta de dados do app, gravável pelo usuário em qualquer sistema:
    /// Windows: %LocalAppData%\PdvLanchonete, Linux: ~/.local/share/PdvLanchonete,
    /// macOS: ~/Library/Application Support/PdvLanchonete.
    /// </summary>
    public static string DataFolder() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName);

    public static string DefaultDatabasePath() =>
        Path.Combine(DataFolder(), DatabaseFileName);

    public static string BackupFolder() =>
        Path.Combine(DataFolder(), "backups");
}
