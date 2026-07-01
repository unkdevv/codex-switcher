namespace CodexSwitcher.Core.Abstractions;

/// <summary>Como o Codex guarda credenciais, conforme <c>cli_auth_credentials_store</c>.</summary>
public enum CredentialsStoreKind
{
    /// <summary>Não definido no config.toml (default do Codex = auto).</summary>
    Unset = 0,
    File = 1,
    Keyring = 2,
    Auto = 3,
}

/// <summary>
/// Lê/força a configuração <c>cli_auth_credentials_store = "file"</c> no config.toml,
/// preservando o resto do arquivo. Ver BUSINESS_RULES.md ponto 1 e §4.3 passo 8.
/// </summary>
public interface ICodexConfigStore
{
    /// <summary>Valor atual da chave (Unset quando ausente).</summary>
    CredentialsStoreKind ReadCredentialsStore(string configTomlPath);

    /// <summary>
    /// Garante <c>cli_auth_credentials_store = "file"</c> de forma idempotente, preservando o
    /// restante do TOML. Retorna true se alterou o arquivo.
    /// </summary>
    bool EnsureFileStore(string configTomlPath);
}
