namespace CodexSwitcher.Core.Abstractions;

/// <summary>
/// Cifra/decifra segredos em repouso. Implementação de produção usa DPAPI (escopo CurrentUser),
/// amarrando a decifragem ao mesmo usuário Windows da mesma máquina. Ver BUSINESS_RULES.md §7.
/// </summary>
public interface ISecretProtector
{
    /// <summary>Cifra os bytes em claro. Nunca loga o conteúdo.</summary>
    byte[] Protect(byte[] plaintext);

    /// <summary>
    /// Decifra os bytes. Lança <see cref="SecretDecryptionException"/> quando não é possível
    /// (ex.: blob criado em outra máquina/usuário) — tratado como perfil "Indisponível".
    /// </summary>
    byte[] Unprotect(byte[] ciphertext);
}

/// <summary>Falha de decifragem — normalmente perfil de outro usuário/máquina. Nunca contém tokens.</summary>
public sealed class SecretDecryptionException : Exception
{
    public SecretDecryptionException(string message, Exception? inner = null)
        : base(message, inner) { }
}
