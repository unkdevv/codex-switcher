using System.Text.RegularExpressions;
using CodexSwitcher.Core.Abstractions;

namespace CodexSwitcher.Infra.Codex;

/// <summary>
/// Lê/garante <c>cli_auth_credentials_store = "file"</c> no config.toml, preservando o resto do
/// arquivo. Só mexe na chave de nível-raiz (antes do primeiro cabeçalho de tabela). Idempotente:
/// não reescreve se já estiver "file". Ver BUSINESS_RULES.md ponto 1 e §4.3 passo 8.
/// </summary>
public sealed partial class ConfigTomlStore : ICodexConfigStore
{
    private const string Key = "cli_auth_credentials_store";
    private readonly IFileSystem _fs;

    public ConfigTomlStore(IFileSystem fs) => _fs = fs ?? throw new ArgumentNullException(nameof(fs));

    [GeneratedRegex(@"^\s*cli_auth_credentials_store\s*=\s*""(?<val>[^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex KeyLineRegex();

    [GeneratedRegex(@"^\s*\[")]
    private static partial Regex TableHeaderRegex();

    public CredentialsStoreKind ReadCredentialsStore(string configTomlPath)
    {
        if (!_fs.FileExists(configTomlPath))
            return CredentialsStoreKind.Unset;

        var lines = _fs.ReadAllText(configTomlPath).Split('\n');
        var firstTable = FirstTableIndex(lines);

        for (var i = 0; i < firstTable; i++)
        {
            var m = KeyLineRegex().Match(lines[i]);
            if (m.Success)
            {
                return m.Groups["val"].Value.ToLowerInvariant() switch
                {
                    "file" => CredentialsStoreKind.File,
                    "keyring" => CredentialsStoreKind.Keyring,
                    "auto" => CredentialsStoreKind.Auto,
                    _ => CredentialsStoreKind.Unset,
                };
            }
        }

        return CredentialsStoreKind.Unset;
    }

    public bool EnsureFileStore(string configTomlPath)
    {
        var raw = _fs.FileExists(configTomlPath) ? _fs.ReadAllText(configTomlPath) : string.Empty;
        var newline = raw.Contains("\r\n") ? "\r\n" : "\n";
        var lines = raw.Length == 0 ? [] : new List<string>(raw.Split('\n'));

        var firstTable = FirstTableIndex(lines);

        for (var i = 0; i < firstTable; i++)
        {
            var m = KeyLineRegex().Match(lines[i]);
            if (m.Success)
            {
                if (m.Groups["val"].Value.Equals("file", StringComparison.OrdinalIgnoreCase))
                    return false; // já está correto — não reescreve.

                lines[i] = KeyLineRegex().Replace(lines[i], $"{Key} = \"file\"");
                _fs.WriteAllTextAtomic(configTomlPath, string.Join(newline, lines));
                return true;
            }
        }

        // Chave ausente: inserir como chave de nível-raiz, antes do primeiro cabeçalho de tabela.
        var insertAt = firstTable;
        lines.Insert(insertAt, $"{Key} = \"file\"");
        _fs.WriteAllTextAtomic(configTomlPath, string.Join(newline, lines));
        return true;
    }

    private static int FirstTableIndex(IReadOnlyList<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (TableHeaderRegex().IsMatch(lines[i]))
                return i;
        }
        return lines.Count;
    }
}
