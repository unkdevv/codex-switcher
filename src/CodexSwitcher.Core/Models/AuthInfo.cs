namespace CodexSwitcher.Core.Models;

/// <summary>
/// Campos lidos do <c>auth.json</c> tratado como blob opaco. Só os estritamente necessários
/// (ver BUSINESS_RULES.md §2.3 e ponto 11). O restante do arquivo é preservado byte a byte.
/// </summary>
public sealed record AuthFileInfo(
    string? AuthMode,
    DateTimeOffset? LastRefresh,
    string? IdToken,
    string? AccountId);

/// <summary>Claims decodificados localmente do id_token (JWT), sem rede. Ver §7 e ponto 12.</summary>
public sealed record AccountClaims(
    string? Sub,
    string? Email,
    DateTimeOffset? ExpiresAt,
    string? PlanType);

/// <summary>Caminhos do Codex relevantes ao app (o slot ativo é externo, gerido pelo Codex).</summary>
public sealed record CodexPaths(
    string CodexHome,
    string ActiveAuthPath,
    string ConfigTomlPath)
{
    public static CodexPaths ForHome(string codexHome) => new(
        codexHome,
        Path.Combine(codexHome, "auth.json"),
        Path.Combine(codexHome, "config.toml"));
}
