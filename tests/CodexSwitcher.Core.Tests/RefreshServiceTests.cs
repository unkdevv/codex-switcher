using CodexSwitcher.Core.Abstractions;
using CodexSwitcher.Core.Models;
using CodexSwitcher.Core.Services;
using CodexSwitcher.Core.Tests.TestSupport;
using CodexSwitcher.Infra.Io;
using CodexSwitcher.Infra.Security;

namespace CodexSwitcher.Core.Tests;

/// <summary>
/// Verifica que o refresh é isolado (usa CODEX_HOME temporário, nunca o slot real), grava de volta
/// no cofre imediatamente e classifica corretamente re-login × falha transiente. Ver §6.
/// </summary>
public sealed class RefreshServiceTests
{
    private sealed class Env : IDisposable
    {
        public TempDir Dir { get; } = new();
        public PhysicalFileSystem Fs { get; } = new();
        public VaultService Vault { get; }
        public FakeCodexCli Codex { get; } = new();
        public FakeClock Clock { get; } = new();
        public FakeAudit Audit { get; } = new();
        public RefreshService Service { get; }

        public Env()
        {
            Vault = new VaultService(new DpapiSecretProtector(), Fs, Dir.Combine("vault"));
            Service = new RefreshService(Vault, Fs, Codex, Clock, Audit, Dir.Combine("temp"));
        }

        public ProfileMetadata AddProfile(byte[] bytes, DateTimeOffset? lastRefresh)
        {
            var p = new ProfileMetadata { Nickname = "P", CreatedAt = Clock.UtcNow, LastRefreshedAt = lastRefresh };
            p.BlobFingerprint = Vault.SaveBlob(p.Id, bytes);
            return p;
        }

        public void Dispose() => Dir.Dispose();
    }

    [Fact]
    public async Task Refresh_Success_WritesRenewedBlobBack_AndMarksValid()
    {
        using var env = new Env();
        var old = Sample.AuthJson(refreshToken: "rt-OLD", lastRefresh: "2026-06-20T00:00:00Z");
        var renewed = Sample.AuthJson(refreshToken: "rt-NEW", lastRefresh: "2026-06-30T00:00:00Z");
        var p = env.AddProfile(old, new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero));
        env.Codex.RefreshedAuthToWrite = renewed;

        var result = await env.Service.RefreshAsync(p, TimeSpan.FromSeconds(5));

        Assert.Equal(RefreshOutcome.Success, result.Outcome);
        Assert.Equal(renewed, env.Vault.LoadBlob(p.Id)); // cofre atualizado com o token novo
        Assert.Equal(HealthStatus.Valid, p.HealthStatus);
        Assert.Equal(new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero), p.LastRefreshedAt);
    }

    [Fact]
    public async Task Refresh_UsesIsolatedCodexHome_UnderTempRoot_NotRealSlot()
    {
        using var env = new Env();
        var p = env.AddProfile(Sample.AuthJson(), null);
        env.Codex.RefreshedAuthToWrite = Sample.AuthJson();

        await env.Service.RefreshAsync(p, TimeSpan.FromSeconds(5));

        Assert.NotNull(env.Codex.LastCodexHome);
        Assert.Contains("codex-refresh-", env.Codex.LastCodexHome!);
        Assert.StartsWith(env.Dir.Combine("temp"), env.Codex.LastCodexHome!);
    }

    [Fact]
    public async Task Refresh_TempDir_IsCleanedUp()
    {
        using var env = new Env();
        var p = env.AddProfile(Sample.AuthJson(), null);
        env.Codex.RefreshedAuthToWrite = Sample.AuthJson();

        await env.Service.RefreshAsync(p, TimeSpan.FromSeconds(5));

        Assert.False(Directory.Exists(env.Codex.LastCodexHome!));
    }

    [Fact]
    public async Task Refresh_DeadRefreshToken_MarksNeedsReLogin()
    {
        using var env = new Env();
        var p = env.AddProfile(Sample.AuthJson(), null);
        env.Codex.RefreshedAuthToWrite = null; // codex não reescreve
        env.Codex.ExecResult = new CodexCliResult(1, "", "Error: refresh token expired, please run codex login");

        var result = await env.Service.RefreshAsync(p, TimeSpan.FromSeconds(5));

        Assert.Equal(RefreshOutcome.NeedsReLogin, result.Outcome);
        Assert.Equal(HealthStatus.NeedsReLogin, p.HealthStatus);
        Assert.Equal(ErrorCategory.RefreshTokenExpired, p.LastError!.Category);
    }

    [Fact]
    public async Task Refresh_TransientFailure_MarksError_NotReLogin()
    {
        using var env = new Env();
        var p = env.AddProfile(Sample.AuthJson(), null);
        env.Codex.RefreshedAuthToWrite = null;
        env.Codex.ExecResult = new CodexCliResult(1, "", "network unreachable");

        var result = await env.Service.RefreshAsync(p, TimeSpan.FromSeconds(5));

        Assert.Equal(RefreshOutcome.TransientError, result.Outcome);
        Assert.Equal(HealthStatus.Error, p.HealthStatus);
    }

    [Fact]
    public async Task Refresh_CodexNotAvailable_ReturnsError()
    {
        using var env = new Env();
        var p = env.AddProfile(Sample.AuthJson(), null);
        env.Codex.IsAvailable = false;

        var result = await env.Service.RefreshAsync(p, TimeSpan.FromSeconds(5));

        Assert.Equal(RefreshOutcome.TransientError, result.Outcome);
        Assert.Equal(ErrorCategory.CodexNotFound, p.LastError!.Category);
    }

    [Fact]
    public void IsDue_RespectsThreshold_AndNeedsReLogin()
    {
        using var env = new Env();
        var never = env.AddProfile(Sample.AuthJson(), null);
        Assert.True(env.Service.IsDue(never, 5));

        var fresh = env.AddProfile(Sample.AuthJson(), env.Clock.UtcNow.AddDays(-1));
        Assert.False(env.Service.IsDue(fresh, 5));

        var stale = env.AddProfile(Sample.AuthJson(), env.Clock.UtcNow.AddDays(-6));
        Assert.True(env.Service.IsDue(stale, 5));

        var dead = env.AddProfile(Sample.AuthJson(), env.Clock.UtcNow.AddDays(-30));
        dead.HealthStatus = HealthStatus.NeedsReLogin;
        Assert.False(env.Service.IsDue(dead, 5)); // refresh não resolve
    }
}
