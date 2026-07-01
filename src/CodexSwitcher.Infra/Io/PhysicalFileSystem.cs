using System.Text;
using CodexSwitcher.Core.Abstractions;

namespace CodexSwitcher.Infra.Io;

/// <summary>
/// Sistema de arquivos real com escrita atômica (temp no mesmo diretório + move-replace).
/// A gravação temporária é forçada a disco (flush) antes do move, para nunca corromper a
/// credencial ativa em falha no meio. Ver BUSINESS_RULES.md §2.1 e ponto 10.
/// </summary>
public sealed class PhysicalFileSystem : IFileSystem
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

    public string ReadAllText(string path) => File.ReadAllText(path, Utf8NoBom);

    public void WriteAllBytesAtomic(string path, byte[] contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // Arquivo temporário no MESMO diretório (mesmo volume) para que o move seja atômico.
        var tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var fs = new FileStream(
                tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 4096, FileOptions.WriteThrough))
            {
                fs.Write(contents, 0, contents.Length);
                fs.Flush(flushToDisk: true);
            }

            // Move-replace atômico no mesmo volume (MoveFileEx/REPLACE_EXISTING no Windows).
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch (IOException) { /* lixo temporário; ignorar */ }
            }
        }
    }

    public void WriteAllTextAtomic(string path, string contents) =>
        WriteAllBytesAtomic(path, Utf8NoBom.GetBytes(contents ?? string.Empty));

    public void Copy(string sourcePath, string destPath, bool overwrite)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(destPath));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.Copy(sourcePath, destPath, overwrite);
    }

    public void Move(string sourcePath, string destPath, bool overwrite)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(destPath));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.Move(sourcePath, destPath, overwrite);
    }

    public void Delete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    public IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern) =>
        Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, searchPattern).ToList()
            : [];
}
