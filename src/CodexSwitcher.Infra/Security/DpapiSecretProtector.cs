using System.Runtime.Versioning;
using System.Security.Cryptography;
using CodexSwitcher.Core.Abstractions;

namespace CodexSwitcher.Infra.Security;

/// <summary>
/// Cifra em repouso com DPAPI, escopo <see cref="DataProtectionScope.CurrentUser"/> (opcionalmente
/// com entropy adicional). Só decifra no mesmo usuário Windows da mesma máquina — não portável,
/// por design. Ver BUSINESS_RULES.md §7 e ponto 9.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiSecretProtector : ISecretProtector
{
    private readonly byte[]? _entropy;

    /// <param name="optionalEntropy">Entropy adicional por-instalação (opcional).</param>
    public DpapiSecretProtector(byte[]? optionalEntropy = null) => _entropy = optionalEntropy;

    public byte[] Protect(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return ProtectedData.Protect(plaintext, _entropy, DataProtectionScope.CurrentUser);
    }

    public byte[] Unprotect(byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        try
        {
            return ProtectedData.Unprotect(ciphertext, _entropy, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException ex)
        {
            // Mensagem sem qualquer dado sensível.
            throw new SecretDecryptionException(
                "Não foi possível decifrar este perfil neste usuário/máquina do Windows.", ex);
        }
    }
}
