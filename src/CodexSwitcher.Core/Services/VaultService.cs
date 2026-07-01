using CodexSwitcher.Core.Abstractions;
using CodexSwitcher.Core.Security;

namespace CodexSwitcher.Core.Services;

/// <summary>
/// Cofre cifrado — <b>fonte da verdade</b> das credenciais. Cada auth.json é cifrado (via
/// <see cref="ISecretProtector"/>) e gravado atomicamente (via <see cref="IFileSystem"/>).
/// O auth.json é preservado byte a byte (blob opaco). Ver BUSINESS_RULES.md §2, §7, pontos 3 e 11.
/// </summary>
public sealed class VaultService
{
    private readonly ISecretProtector _protector;
    private readonly IFileSystem _fs;
    private readonly string _vaultDir;

    public VaultService(ISecretProtector protector, IFileSystem fs, string vaultDir)
    {
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _fs = fs ?? throw new ArgumentNullException(nameof(fs));
        _vaultDir = vaultDir ?? throw new ArgumentNullException(nameof(vaultDir));
    }

    /// <summary>Caminho do blob cifrado de um perfil.</summary>
    public string BlobPath(Guid profileId) => Path.Combine(_vaultDir, profileId.ToString("N") + ".bin");

    public bool Exists(Guid profileId) => _fs.FileExists(BlobPath(profileId));

    /// <summary>
    /// Cifra e grava o auth.json de um perfil no cofre (atômico). Retorna o fingerprint do
    /// conteúdo em claro (para reconciliação). Gravação imediata é obrigatória após todo refresh
    /// para não guardar refresh token rotacionado antigo (ponto 3).
    /// </summary>
    public string SaveBlob(Guid profileId, byte[] authJson)
    {
        ArgumentNullException.ThrowIfNull(authJson);
        var fingerprint = Fingerprint.Compute(authJson);
        SaveEncryptedFile(BlobPath(profileId), authJson);
        return fingerprint;
    }

    /// <summary>Carrega e decifra o auth.json de um perfil. Lança <see cref="SecretDecryptionException"/> se não decifrar.</summary>
    public byte[] LoadBlob(Guid profileId) => LoadEncryptedFile(BlobPath(profileId));

    public void DeleteBlob(Guid profileId)
    {
        var path = BlobPath(profileId);
        if (_fs.FileExists(path))
            _fs.Delete(path);
    }

    /// <summary>Grava bytes cifrados atomicamente em um caminho arbitrário (ex.: backups do slot ativo).</summary>
    public void SaveEncryptedFile(string path, byte[] plaintext)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            _fs.CreateDirectory(dir);
        var cipher = _protector.Protect(plaintext);
        _fs.WriteAllBytesAtomic(path, cipher);
    }

    /// <summary>Lê e decifra bytes de um caminho arbitrário cifrado.</summary>
    public byte[] LoadEncryptedFile(string path)
    {
        var cipher = _fs.ReadAllBytes(path);
        return _protector.Unprotect(cipher);
    }
}
