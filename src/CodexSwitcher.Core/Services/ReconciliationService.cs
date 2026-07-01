using CodexSwitcher.Core.Abstractions;
using CodexSwitcher.Core.Models;
using CodexSwitcher.Core.Security;

namespace CodexSwitcher.Core.Services;

/// <summary>
/// Detecta qual perfil ocupa o slot ativo comparando fingerprint e <c>sub</c> do auth.json real
/// com os perfis conhecidos. Não decifra o cofre — só lê o slot ativo em claro. Ver §4.6.
/// </summary>
public sealed class ReconciliationService
{
    private readonly IFileSystem _fs;
    private readonly CodexPaths _paths;

    public ReconciliationService(IFileSystem fs, CodexPaths paths)
    {
        _fs = fs ?? throw new ArgumentNullException(nameof(fs));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    /// <summary>
    /// Atualiza <see cref="ProfileMetadata.IsActive"/> de cada perfil conforme o slot ativo e
    /// devolve o resultado (inclui <see cref="ActiveMatch.SameAccountDrifted"/> quando o Codex
    /// renovou externamente e é preciso write-back).
    /// </summary>
    public ReconciliationResult Reconcile(IReadOnlyList<ProfileMetadata> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        foreach (var p in profiles)
            p.IsActive = false;

        if (!_fs.FileExists(_paths.ActiveAuthPath))
            return ReconciliationResult.NoActive();

        byte[] activeBytes;
        try
        {
            activeBytes = _fs.ReadAllBytes(_paths.ActiveAuthPath);
        }
        catch (IOException)
        {
            return ReconciliationResult.NoActive();
        }

        var fingerprint = Fingerprint.Compute(activeBytes);

        var exact = profiles.FirstOrDefault(p => p.BlobFingerprint == fingerprint);
        if (exact is not null)
        {
            exact.IsActive = true;
            return new ReconciliationResult(ActiveMatch.Exact, exact.Id, exact.AccountSub, fingerprint);
        }

        var (_, claims) = AuthJsonReader.Identify(activeBytes);
        if (!string.IsNullOrEmpty(claims.Sub))
        {
            var sameAccount = profiles.FirstOrDefault(p => p.AccountSub == claims.Sub);
            if (sameAccount is not null)
            {
                sameAccount.IsActive = true;
                return new ReconciliationResult(ActiveMatch.SameAccountDrifted, sameAccount.Id, claims.Sub, fingerprint);
            }
        }

        // Conta não gerenciada no slot ativo (candidata a adoção).
        return new ReconciliationResult(ActiveMatch.None, null, claims.Sub, fingerprint);
    }
}
