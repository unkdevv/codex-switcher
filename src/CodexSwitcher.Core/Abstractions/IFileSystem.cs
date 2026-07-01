namespace CodexSwitcher.Core.Abstractions;

/// <summary>
/// Abstração de sistema de arquivos com <b>escrita atômica</b>, para permitir testar as
/// transações de switch/refresh sem tocar o disco real. Ver BUSINESS_RULES.md §2.1 e ponto 10.
/// </summary>
public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);

    byte[] ReadAllBytes(string path);
    string ReadAllText(string path);

    /// <summary>Escrita atômica: grava em arquivo temporário no mesmo diretório e faz move-replace.</summary>
    void WriteAllBytesAtomic(string path, byte[] contents);

    /// <summary>Escrita atômica de texto (UTF-8 sem BOM).</summary>
    void WriteAllTextAtomic(string path, string contents);

    void Copy(string sourcePath, string destPath, bool overwrite);
    void Move(string sourcePath, string destPath, bool overwrite);
    void Delete(string path);

    IReadOnlyList<string> EnumerateFiles(string directory, string searchPattern);
}
