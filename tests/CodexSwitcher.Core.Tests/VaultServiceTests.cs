using CodexSwitcher.Core.Security;
using CodexSwitcher.Core.Services;
using CodexSwitcher.Core.Tests.TestSupport;
using CodexSwitcher.Infra.Io;
using CodexSwitcher.Infra.Security;

namespace CodexSwitcher.Core.Tests;

/// <summary>
/// Prova, com DPAPI e disco reais, que o cofre armazena e recupera o auth.json de várias contas
/// sem corromper nem perder nada — o coração do requisito "não perder o login das múltiplas contas".
/// </summary>
public sealed class VaultServiceTests
{
    private static VaultService NewVault(TempDir dir) =>
        new(new DpapiSecretProtector(), new PhysicalFileSystem(), dir.Combine("vault"));

    [Fact]
    public void SaveThenLoad_ReturnsIdenticalBytes()
    {
        using var dir = new TempDir();
        var vault = NewVault(dir);
        var id = Guid.NewGuid();
        var original = Sample.AuthJson();

        vault.SaveBlob(id, original);
        var loaded = vault.LoadBlob(id);

        Assert.Equal(original, loaded);
    }

    [Fact]
    public void OnDisk_IsEncrypted_NotPlaintext()
    {
        using var dir = new TempDir();
        var vault = NewVault(dir);
        var id = Guid.NewGuid();
        var original = Sample.AuthJson(refreshToken: "rt-super-secret-marker");

        vault.SaveBlob(id, original);

        var rawOnDisk = File.ReadAllBytes(vault.BlobPath(id));
        var asText = System.Text.Encoding.UTF8.GetString(rawOnDisk);
        Assert.DoesNotContain("rt-super-secret-marker", asText);
        Assert.DoesNotContain("refresh_token", asText);
        Assert.NotEqual(original, rawOnDisk);
    }

    [Fact]
    public void PreservesUnknownFields_ByteForByte()
    {
        using var dir = new TempDir();
        var vault = NewVault(dir);
        var id = Guid.NewGuid();
        // Contém "future_unknown_field" que o app nunca parseia (blob opaco, ponto 11).
        var original = Sample.AuthJson();

        vault.SaveBlob(id, original);
        var loaded = vault.LoadBlob(id);

        Assert.Equal(original, loaded);
        Assert.Contains("future_unknown_field", System.Text.Encoding.UTF8.GetString(loaded));
    }

    [Fact]
    public void MultipleAccounts_StoredIndependently_AllRecoverable()
    {
        using var dir = new TempDir();
        var vault = NewVault(dir);

        var accounts = Enumerable.Range(0, 5)
            .Select(i => (Id: Guid.NewGuid(), Bytes: Sample.AuthJson(accountId: $"acct_{i}", refreshToken: $"rt-{i}")))
            .ToList();

        foreach (var (accId, bytes) in accounts)
            vault.SaveBlob(accId, bytes);

        foreach (var (accId, bytes) in accounts)
            Assert.Equal(bytes, vault.LoadBlob(accId));
    }

    [Fact]
    public void Fingerprint_IsStable_AndMatchesContent()
    {
        using var dir = new TempDir();
        var vault = NewVault(dir);
        var id = Guid.NewGuid();
        var bytes = Sample.AuthJson();

        var fp1 = vault.SaveBlob(id, bytes);
        var fp2 = vault.SaveBlob(id, bytes);

        Assert.Equal(fp1, fp2);
        Assert.Equal(Fingerprint.Compute(bytes), fp1);
        Assert.Equal(64, fp1.Length); // SHA-256 hex
    }

    [Fact]
    public void Overwrite_UpdatesContent_AndFingerprint()
    {
        using var dir = new TempDir();
        var vault = NewVault(dir);
        var id = Guid.NewGuid();

        var v1 = Sample.AuthJson(lastRefresh: "2026-06-20T00:00:00Z");
        var v2 = Sample.AuthJson(lastRefresh: "2026-06-28T00:00:00Z");

        var fp1 = vault.SaveBlob(id, v1);
        var fp2 = vault.SaveBlob(id, v2);

        Assert.NotEqual(fp1, fp2);
        Assert.Equal(v2, vault.LoadBlob(id));
    }

    [Fact]
    public void DeleteBlob_RemovesFile()
    {
        using var dir = new TempDir();
        var vault = NewVault(dir);
        var id = Guid.NewGuid();
        vault.SaveBlob(id, Sample.AuthJson());
        Assert.True(vault.Exists(id));

        vault.DeleteBlob(id);

        Assert.False(vault.Exists(id));
    }
}
