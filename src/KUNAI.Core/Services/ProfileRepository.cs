using System.Text.Json;
using System.Text.Json.Serialization;
using KUNAI.Core.Abstractions;
using KUNAI.Core.Domain;

namespace KUNAI.Core.Services;

/// <summary>プロファイルを 1 ファイル 1 プロファイルの JSON として保存する。</summary>
public sealed class ProfileRepository : IProfileRepository
{
    private const string Extension = ".json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ProfileRepositoryOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ProfileRepository(ProfileRepositoryOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_options.ProfilesDirectory);

            var hasProfiles = Directory.EnumerateFiles(_options.ProfilesDirectory, "*" + Extension).Any();
            if (hasProfiles
                || string.IsNullOrEmpty(_options.DefaultsDirectory)
                || !Directory.Exists(_options.DefaultsDirectory))
            {
                return;
            }

            foreach (var source in Directory.EnumerateFiles(_options.DefaultsDirectory, "*" + Extension))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = Path.Combine(_options.ProfilesDirectory, Path.GetFileName(source));
                File.Copy(source, target, overwrite: false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<string>> GetProfileNamesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(_options.ProfilesDirectory))
            {
                return Array.Empty<string>();
            }

            return Directory.EnumerateFiles(_options.ProfilesDirectory, "*" + Extension)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Profile> LoadAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeName(name);
        var path = PathFor(normalized);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Profile '{normalized}' was not found.", path);
            }

            Profile? profile;
            try
            {
                await using var stream = File.OpenRead(path);
                profile = await JsonSerializer.DeserializeAsync<Profile>(stream, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Profile '{normalized}' is not valid JSON: {ex.Message}", ex);
            }

            if (profile is null)
            {
                throw new InvalidDataException($"Profile '{normalized}' is empty.");
            }

            return Normalize(profile, normalized);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(Profile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var normalized = NormalizeName(profile.Name);
        var path = PathFor(normalized);
        var temp = path + ".tmp";

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_options.ProfilesDirectory);

            // 一時ファイルに書いてから置き換え、書き込み途中の中断で既存ファイルが壊れないようにする。
            var json = JsonSerializer.Serialize(profile, JsonOptions);
            await File.WriteAllTextAsync(temp, json, cancellationToken).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }

            _gate.Release();
        }
    }

    private string PathFor(string normalizedName) =>
        Path.Combine(_options.ProfilesDirectory, normalizedName + Extension);

    /// <summary>拡張子の有無を吸収し、パス区切りなどファイル名として不正な名前を拒否する。</summary>
    private static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Profile name must not be empty.", nameof(name));
        }

        var trimmed = name.Trim();
        if (trimmed.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^Extension.Length];
        }

        if (trimmed.Length == 0
            || trimmed is "." or ".."
            || trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || trimmed.Contains('\\'))
        {
            throw new ArgumentException($"Invalid profile name: '{name}'.", nameof(name));
        }

        return trimmed;
    }

    /// <summary>名前を補完し、Layer0〜3 が必ず存在する状態にする。</summary>
    private static Profile Normalize(Profile profile, string name)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            profile.Name = name;
        }

        profile.Layers ??= new List<LayerProfile>();
        for (var i = 0; i < Profile.LayerCount; i++)
        {
            profile.GetOrAddLayer(i);
        }

        foreach (var layer in profile.Layers)
        {
            layer.Mappings ??= new List<ButtonMapping>();
            foreach (var mapping in layer.Mappings)
            {
                mapping.Action ??= string.Empty;
            }
        }

        return profile;
    }
}
