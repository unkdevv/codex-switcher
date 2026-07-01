using System.Text.Json;
using System.Text.Json.Serialization;
using CodexSwitcher.Core.Abstractions;
using CodexSwitcher.Core.Models;

namespace CodexSwitcher.Core.Services;

/// <summary>
/// Persiste os metadados dos perfis (profiles.json), separados do blob cifrado. Escrita atômica.
/// Sem segredos. Ver BUSINESS_RULES.md §2.1/§2.2 e §9 (separação metadados × blob).
/// </summary>
public sealed class ProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IFileSystem _fs;
    private readonly string _profilesPath;

    public ProfileStore(IFileSystem fs, string profilesPath)
    {
        _fs = fs ?? throw new ArgumentNullException(nameof(fs));
        _profilesPath = profilesPath ?? throw new ArgumentNullException(nameof(profilesPath));
    }

    /// <summary>Carrega todos os perfis. Arquivo ausente ou corrompido → lista vazia (não quebra o app).</summary>
    public List<ProfileMetadata> LoadAll()
    {
        if (!_fs.FileExists(_profilesPath))
            return [];

        try
        {
            var json = _fs.ReadAllText(_profilesPath);
            if (string.IsNullOrWhiteSpace(json))
                return [];
            var list = JsonSerializer.Deserialize<List<ProfileMetadata>>(json, JsonOptions);
            return list ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Grava todos os perfis atomicamente.</summary>
    public void SaveAll(IEnumerable<ProfileMetadata> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        var dir = Path.GetDirectoryName(_profilesPath);
        if (!string.IsNullOrEmpty(dir))
            _fs.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(profiles.ToList(), JsonOptions);
        _fs.WriteAllTextAtomic(_profilesPath, json);
    }
}
