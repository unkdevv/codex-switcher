namespace CodexSwitcher.Core.Tests.TestSupport;

/// <summary>Diretório temporário isolado por teste, removido no dispose. Nunca toca o .codex real.</summary>
public sealed class TempDir : IDisposable
{
    public string Root { get; }

    public TempDir()
    {
        Root = Path.Combine(Path.GetTempPath(), "cxsw-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Combine(params string[] parts) => Path.Combine([Root, .. parts]);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
        catch (IOException) { /* melhor esforço */ }
        catch (UnauthorizedAccessException) { /* melhor esforço */ }
    }
}
