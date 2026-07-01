using CodexSwitcher.Core.Models;

namespace CodexSwitcher.Infra;

/// <summary>
/// Resolve os caminhos do app (%LOCALAPPDATA%\CodexSwitcher\...) e do Codex (CODEX_HOME ou
/// %USERPROFILE%\.codex). Usa Local (não Roaming), pois DPAPI CurrentUser não é portável (§2.1/§7).
/// </summary>
public sealed class AppPaths
{
    public string Root { get; }
    public string VaultDir => Path.Combine(Root, "vault");
    public string BackupsDir => Path.Combine(Root, "backups");
    public string ProfilesPath => Path.Combine(Root, "profiles.json");
    public string SettingsPath => Path.Combine(Root, "settings.json");
    public string AuditLogPath => Path.Combine(Root, "audit.log");

    // Pasta de trabalho isolada para login/refresh (CODEX_HOME efêmero). NÃO usar %TEMP%: o codex
    // recusa criar binários auxiliares sob o diretório temporário do sistema. Ver §5/§6.
    public string TempRoot => Path.Combine(Root, "work");
    public CodexPaths Codex { get; }

    public AppPaths(string? root = null, string? codexHome = null)
    {
        // Override opcional da pasta de dados (CODEXSWITCHER_HOME) para portabilidade/testes.
        Root = root
            ?? Environment.GetEnvironmentVariable("CODEXSWITCHER_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexSwitcher");

        var home = codexHome
            ?? Environment.GetEnvironmentVariable("CODEX_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        Codex = CodexPaths.ForHome(home);
    }

    /// <summary>Cria as pastas necessárias. Não toca o .codex real.</summary>
    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(VaultDir);
        Directory.CreateDirectory(BackupsDir);
        Directory.CreateDirectory(TempRoot);
    }
}
