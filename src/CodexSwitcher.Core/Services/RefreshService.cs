using CodexSwitcher.Core.Abstractions;
using CodexSwitcher.Core.Models;
using CodexSwitcher.Core.Security;

namespace CodexSwitcher.Core.Services;

/// <summary>
/// Renova o token de um perfil <b>sem novo login</b>, de forma isolada: decifra o auth.json numa
/// pasta temporária, roda <c>codex exec</c> com <c>CODEX_HOME</c> apontando só para ela (nunca o
/// slot real), e grava o resultado de volta no cofre. Ver BUSINESS_RULES.md §6 e pontos 3, 7.
/// </summary>
public sealed class RefreshService
{
    // Marcadores (best-effort) de que o refresh token morreu e é preciso re-login. Ver §6.3.
    private static readonly string[] ReLoginMarkers =
    [
        "not logged in", "please run codex login", "unauthorized", "401",
        "refresh token", "expired", "re-authenticate", "reauthenticate",
        "authentication", "invalid_grant", "login required",
    ];

    private readonly VaultService _vault;
    private readonly IFileSystem _fs;
    private readonly ICodexCli _codex;
    private readonly IClock _clock;
    private readonly IAuditLog _audit;
    private readonly string _tempRoot;

    public RefreshService(
        VaultService vault, IFileSystem fs, ICodexCli codex, IClock clock, IAuditLog audit, string tempRoot)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _fs = fs ?? throw new ArgumentNullException(nameof(fs));
        _codex = codex ?? throw new ArgumentNullException(nameof(codex));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _tempRoot = tempRoot ?? throw new ArgumentNullException(nameof(tempRoot));
    }

    /// <summary>Um perfil está elegível a refresh (due) considerando a idade do último refresh?</summary>
    public bool IsDue(ProfileMetadata profile, double dueDays)
    {
        if (profile.HealthStatus == HealthStatus.NeedsReLogin)
            return false; // refresh não resolve; requer re-login.
        if (profile.LastRefreshedAt is not { } last)
            return true;
        return (_clock.UtcNow - last).TotalDays >= dueDays;
    }

    /// <summary>Renova um perfil. NUNCA toca o slot ativo real. Atualiza os metadados do perfil.</summary>
    public async Task<RefreshResult> RefreshAsync(
        ProfileMetadata profile, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!_codex.IsAvailable)
        {
            return SetError(profile, ErrorCategory.CodexNotFound,
                "Binário do Codex não encontrado; não é possível renovar.");
        }

        byte[] current;
        try
        {
            current = _vault.LoadBlob(profile.Id);
        }
        catch (SecretDecryptionException)
        {
            profile.HealthStatus = HealthStatus.Unknown;
            profile.LastError = ErrorInfo.Create(ErrorCategory.DecryptionFailed,
                "Perfil não pôde ser decifrado neste usuário/máquina.", _clock.UtcNow);
            return new RefreshResult(RefreshOutcome.TransientError, "Perfil não decifrável.", profile.LastError);
        }

        var codexHome = Path.Combine(_tempRoot, $"codex-refresh-{profile.Id:N}");
        try
        {
            _fs.CreateDirectory(codexHome);
            // Escreve o auth.json atual e força file-store no run isolado.
            _fs.WriteAllBytesAtomic(Path.Combine(codexHome, "auth.json"), current);
            _fs.WriteAllTextAtomic(Path.Combine(codexHome, "config.toml"),
                "cli_auth_credentials_store = \"file\"\n");

            profile.HealthStatus = HealthStatus.Refreshing;

            var result = await _codex.ExecAsync("Reply with OK", codexHome, timeout, cancellationToken)
                .ConfigureAwait(false);

            var refreshedPath = Path.Combine(codexHome, "auth.json");
            if (result.Success && _fs.FileExists(refreshedPath))
            {
                var refreshed = _fs.ReadAllBytes(refreshedPath);
                profile.BlobFingerprint = _vault.SaveBlob(profile.Id, refreshed); // grava de volta IMEDIATAMENTE (ponto 3)

                var info = AuthJsonReader.TryRead(refreshed);
                profile.LastRefreshedAt = info?.LastRefresh ?? _clock.UtcNow;
                profile.HealthStatus = HealthStatus.Valid;
                profile.LastError = null;
                _audit.Record("refresh", "ok", profile.DisplayName);
                return new RefreshResult(RefreshOutcome.Success, $"{profile.DisplayName} renovado.");
            }

            // Falha: classificar entre re-login necessário x transiente.
            if (LooksLikeReLogin(result))
            {
                profile.HealthStatus = HealthStatus.NeedsReLogin;
                profile.LastError = ErrorInfo.Create(ErrorCategory.RefreshTokenExpired,
                    "Sessão expirada; é necessário fazer login novamente.", _clock.UtcNow);
                _audit.Record("refresh", "needs-relogin", profile.DisplayName);
                return new RefreshResult(RefreshOutcome.NeedsReLogin,
                    $"{profile.DisplayName} precisa de novo login.", profile.LastError);
            }

            return SetError(profile, ErrorCategory.Network,
                "Falha temporária ao renovar (rede/timeout). Será tentado novamente.");
        }
        catch (OperationCanceledException)
        {
            return SetError(profile, ErrorCategory.Timeout, "Renovação cancelada/tempo esgotado.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return SetError(profile, ErrorCategory.PermissionDenied, "Falha de arquivo ao renovar.");
        }
        finally
        {
            TryDeleteTemp(codexHome);
        }
    }

    private static bool LooksLikeReLogin(CodexCliResult result)
    {
        var text = (result.StandardError + " " + result.StandardOutput).ToLowerInvariant();
        return ReLoginMarkers.Any(text.Contains);
    }

    private RefreshResult SetError(ProfileMetadata profile, ErrorCategory category, string message)
    {
        profile.HealthStatus = HealthStatus.Error;
        profile.LastError = ErrorInfo.Create(category, message, _clock.UtcNow);
        _audit.Record("refresh", "error", $"{profile.DisplayName}: {category}");
        return new RefreshResult(RefreshOutcome.TransientError, message, profile.LastError);
    }

    private void TryDeleteTemp(string dir)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
                return;
            }
            catch (IOException) { Thread.Sleep(50); }
            catch (UnauthorizedAccessException) { Thread.Sleep(50); }
        }
    }
}
