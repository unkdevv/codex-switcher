using System.Security.Cryptography;

namespace CodexSwitcher.Core.Security;

/// <summary>
/// Hash SHA-256 do conteúdo decifrado do auth.json, usado para reconciliar o slot ativo sem
/// comparar tokens e para detectar mudança externa. Não é segredo, mas não deve ser logado (§7).
/// </summary>
public static class Fingerprint
{
    public static string Compute(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var hash = SHA256.HashData(content);
        return Convert.ToHexStringLower(hash);
    }
}
