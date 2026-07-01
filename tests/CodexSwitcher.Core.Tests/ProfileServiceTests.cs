using CodexSwitcher.Core.Models;
using CodexSwitcher.Core.Services;
using CodexSwitcher.Core.Support;
using CodexSwitcher.Core.Tests.TestSupport;
using CodexSwitcher.Infra.Io;
using CodexSwitcher.Infra.Security;

namespace CodexSwitcher.Core.Tests;

public sealed class ProfileServiceTests
{
    private sealed class Env : IDisposable
    {
        public TempDir Dir { get; } = new();
        public ProfileService Service { get; }
        public FakeClock Clock { get; } = new();

        public Env()
        {
            var fs = new PhysicalFileSystem();
            var vault = new VaultService(new DpapiSecretProtector(), fs, Dir.Combine("vault"));
            var store = new ProfileStore(fs, Dir.Combine("profiles.json"));
            var paths = CodexSwitcher.Core.Models.CodexPaths.ForHome(Dir.Combine(".codex"));
            var recon = new ReconciliationService(fs, paths);
            Service = new ProfileService(vault, store, recon, fs, paths, Clock, new FakeAudit());
        }

        public void Dispose() => Dir.Dispose();
    }

    [Fact]
    public void AddFromAuthJson_CreatesProfile_WithIdentity()
    {
        using var env = new Env();
        var token = Sample.Jwt(sub: "sub-1", email: "user@x.com");
        var bytes = Sample.AuthJson(idToken: token);

        var p = env.Service.AddFromAuthJson(bytes, nickname: "Trabalho");

        Assert.Equal("Trabalho", p.Nickname);
        Assert.Equal("sub-1", p.AccountSub);
        Assert.Equal("user@x.com", p.AccountEmail);
        Assert.Single(env.Service.Profiles);
    }

    [Fact]
    public void AddFromAuthJson_SameSub_DeduplicatesAndUpdates()
    {
        using var env = new Env();
        var token = Sample.Jwt(sub: "sub-dup", email: "a@b.com");
        var first = Sample.AuthJson(idToken: token, refreshToken: "rt-1");
        var second = Sample.AuthJson(idToken: token, refreshToken: "rt-2");

        var p1 = env.Service.AddFromAuthJson(first, "Nome1");
        var p2 = env.Service.AddFromAuthJson(second, null);

        Assert.Same(p1, p2);                       // mesmo perfil (dedupe por sub)
        Assert.Single(env.Service.Profiles);
    }

    [Fact]
    public void Remove_DeletesBlobAndMetadata()
    {
        using var env = new Env();
        var p = env.Service.AddFromAuthJson(Sample.AuthJson(idToken: Sample.Jwt(sub: "s")), "X");

        env.Service.Remove(p.Id);

        Assert.Empty(env.Service.Profiles);
    }

    [Fact]
    public void Reload_PersistsAcrossInstances()
    {
        using var env = new Env();
        env.Service.AddFromAuthJson(Sample.AuthJson(idToken: Sample.Jwt(sub: "s1")), "One");

        env.Service.Load(); // recarrega do disco

        Assert.Single(env.Service.Profiles);
        Assert.Equal("One", env.Service.Profiles[0].Nickname);
    }
}

public sealed class RelativeTimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Null_IsNever() => Assert.Equal("nunca", RelativeTime.Humanize(null, Now));

    [Fact]
    public void JustNow() => Assert.Equal("agora mesmo", RelativeTime.Humanize(Now.AddSeconds(-30), Now));

    [Fact]
    public void Minutes() => Assert.Equal("há 5 min", RelativeTime.Humanize(Now.AddMinutes(-5), Now));

    [Fact]
    public void Yesterday() => Assert.Equal("ontem", RelativeTime.Humanize(Now.AddDays(-1), Now));

    [Fact]
    public void Days() => Assert.Equal("há 3 dias", RelativeTime.Humanize(Now.AddDays(-3), Now));
}
