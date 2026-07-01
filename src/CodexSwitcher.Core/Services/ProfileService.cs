using CodexSwitcher.Core.Abstractions;
using CodexSwitcher.Core.Models;
using CodexSwitcher.Core.Security;

namespace CodexSwitcher.Core.Services;

/// <summary>
/// Fachada de alto nível sobre cofre + metadados + reconciliação. Mantém a lista de perfis em
/// memória e coordena adicionar/renomear/remover/adotar, deduplicando por <c>sub</c>. Os
/// ViewModels dependem só desta classe e do <see cref="SwitchService"/>/<see cref="RefreshService"/>.
/// </summary>
public sealed class ProfileService
{
    private readonly VaultService _vault;
    private readonly ProfileStore _store;
    private readonly ReconciliationService _reconciliation;
    private readonly IFileSystem _fs;
    private readonly CodexPaths _paths;
    private readonly IClock _clock;
    private readonly IAuditLog _audit;

    public List<ProfileMetadata> Profiles { get; private set; } = [];

    public ProfileService(
        VaultService vault, ProfileStore store, ReconciliationService reconciliation,
        IFileSystem fs, CodexPaths paths, IClock clock, IAuditLog audit)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _reconciliation = reconciliation ?? throw new ArgumentNullException(nameof(reconciliation));
        _fs = fs ?? throw new ArgumentNullException(nameof(fs));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    /// <summary>Carrega os perfis do disco e reconcilia com o slot ativo.</summary>
    public ReconciliationResult Load()
    {
        Profiles = _store.LoadAll();
        return Reconcile();
    }

    /// <summary>Reconcilia; em caso de drift (Codex renovou externamente) faz write-back no cofre.</summary>
    public ReconciliationResult Reconcile()
    {
        var result = _reconciliation.Reconcile(Profiles);

        if (result.Match == ActiveMatch.SameAccountDrifted && result.ActiveProfileId is { } id)
        {
            var profile = Profiles.FirstOrDefault(p => p.Id == id);
            if (profile is not null && _fs.FileExists(_paths.ActiveAuthPath))
            {
                var activeBytes = _fs.ReadAllBytes(_paths.ActiveAuthPath);
                profile.BlobFingerprint = _vault.SaveBlob(profile.Id, activeBytes);
                var info = AuthJsonReader.TryRead(activeBytes);
                if (info?.LastRefresh is { } lr) profile.LastRefreshedAt = lr;
                if (profile.HealthStatus is HealthStatus.Unknown) profile.HealthStatus = HealthStatus.Valid;
                _store.SaveAll(Profiles);
                _audit.Record("reconcile", "write-back", profile.DisplayName);
            }
        }

        return result;
    }

    /// <summary>Existe uma conta não gerenciada no slot ativo (candidata a adoção)?</summary>
    public bool HasUnmanagedActiveAccount()
    {
        if (!_fs.FileExists(_paths.ActiveAuthPath))
            return false;
        var result = _reconciliation.Reconcile(Profiles);
        return result is { Match: ActiveMatch.None, ActiveFingerprint: not null };
    }

    /// <summary>Adota a conta já logada no .codex como um novo perfil (importar). Ver §6 (extra).</summary>
    public ProfileMetadata? AdoptActiveAccount(string? nickname = null)
    {
        if (!_fs.FileExists(_paths.ActiveAuthPath))
            return null;
        var bytes = _fs.ReadAllBytes(_paths.ActiveAuthPath);
        var profile = AddFromAuthJson(bytes, nickname);
        profile.IsActive = true;
        _store.SaveAll(Profiles);
        _audit.Record("adopt", "ok", profile.DisplayName);
        return profile;
    }

    /// <summary>
    /// Cria (ou atualiza, se já existir o mesmo <c>sub</c>) um perfil a partir de um auth.json.
    /// Deduplica por conta. Persiste. Ver §5.2 e §9 (deduplicação).
    /// </summary>
    public ProfileMetadata AddFromAuthJson(byte[] authJson, string? nickname)
    {
        ArgumentNullException.ThrowIfNull(authJson);
        var (file, claims) = AuthJsonReader.Identify(authJson);

        var existing = !string.IsNullOrEmpty(claims.Sub)
            ? Profiles.FirstOrDefault(p => p.AccountSub == claims.Sub)
            : null;

        if (existing is not null)
        {
            existing.BlobFingerprint = _vault.SaveBlob(existing.Id, authJson);
            existing.HealthStatus = HealthStatus.Valid;
            existing.LastError = null;
            existing.LastRefreshedAt = file?.LastRefresh ?? _clock.UtcNow;
            if (!string.IsNullOrWhiteSpace(nickname)) existing.Nickname = nickname!;
            _store.SaveAll(Profiles);
            _audit.Record("add", "updated-existing", existing.DisplayName);
            return existing;
        }

        var profile = new ProfileMetadata
        {
            Nickname = nickname ?? claims.Email ?? string.Empty,
            AccountEmail = claims.Email,
            AccountSub = claims.Sub,
            AuthMode = file?.AuthMode,
            PlanType = claims.PlanType,
            CreatedAt = _clock.UtcNow,
            LastRefreshedAt = file?.LastRefresh ?? _clock.UtcNow,
            HealthStatus = HealthStatus.Valid,
        };
        profile.BlobFingerprint = _vault.SaveBlob(profile.Id, authJson);
        Profiles.Add(profile);
        _store.SaveAll(Profiles);
        _audit.Record("add", "ok", profile.DisplayName);
        return profile;
    }

    public void Rename(Guid id, string nickname)
    {
        var p = Profiles.FirstOrDefault(x => x.Id == id);
        if (p is null) return;
        p.Nickname = nickname?.Trim() ?? string.Empty;
        _store.SaveAll(Profiles);
        _audit.Record("rename", "ok", p.DisplayName);
    }

    public void Remove(Guid id)
    {
        var p = Profiles.FirstOrDefault(x => x.Id == id);
        if (p is null) return;
        _vault.DeleteBlob(id);
        Profiles.Remove(p);
        _store.SaveAll(Profiles);
        _audit.Record("remove", "ok", p.DisplayName);
    }

    public void MarkNeedsReLogin(Guid id)
    {
        var p = Profiles.FirstOrDefault(x => x.Id == id);
        if (p is null) return;
        p.HealthStatus = HealthStatus.NeedsReLogin;
        p.LastError = ErrorInfo.Create(ErrorCategory.RefreshTokenExpired,
            "Marcado manualmente como precisa re-login.", _clock.UtcNow);
        _store.SaveAll(Profiles);
    }

    public void Save() => _store.SaveAll(Profiles);
}
