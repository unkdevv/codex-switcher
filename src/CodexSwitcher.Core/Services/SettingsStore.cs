using System.Text.Json;
using System.Text.Json.Serialization;
using CodexSwitcher.Core.Abstractions;
using CodexSwitcher.Core.Models;

namespace CodexSwitcher.Core.Services;

/// <summary>Persiste as <see cref="AppSettings"/> (settings.json) com escrita atômica. Sem segredos.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IFileSystem _fs;
    private readonly string _path;

    public SettingsStore(IFileSystem fs, string path)
    {
        _fs = fs ?? throw new ArgumentNullException(nameof(fs));
        _path = path ?? throw new ArgumentNullException(nameof(path));
    }

    public AppSettings Load()
    {
        if (!_fs.FileExists(_path))
            return new AppSettings();
        try
        {
            var json = _fs.ReadAllText(_path);
            return string.IsNullOrWhiteSpace(json)
                ? new AppSettings()
                : JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            _fs.CreateDirectory(dir);
        _fs.WriteAllTextAtomic(_path, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
